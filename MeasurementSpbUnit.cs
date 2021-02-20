using System.ComponentModel;

namespace Komlog
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using DevExpress.Xpf.Core.Native;

    public partial class MeasurementClass
    {
        private float deltaVoltage = 0.1f;
        private float voltage1;
        private float voltage2;

        private BackgroundWorker spbWorker;

        public bool SpbUnitMeasurementInit()
        {
            if (this.Port == null || this.Port.IsOpen == false) return false;
            ErrorCount = 0;
            MeasurementCount = 0;
            StopFlag = false;
            this.Port.DiscardInBuffer(); // Очистили буфер COM порта от данных
            this.Port.DiscardOutBuffer(); // Очистили буфер COM порта от данных
            this.startDateTime = DateTime.Now;

            voltage1 = 1.5f;
            voltage2 = 1.5f;
            SendCommand(CommandCod.SetDac, 1, voltage1);
            GetAnswer(14);
            SendCommand(CommandCod.SetDac, 2, voltage2);
            GetAnswer(14);
            SendCommand(CommandCod.StartDataStream);
            var answer = GetAnswer(6);
            if (answer != "1D 05 83 05 0C CB")
            {
                var onNewLogMessage = NewMessage;
                onNewLogMessage?.Invoke("Unexpected answer: " + answer, MessageType.Log);
                ErrorCount++;
                return false;
            }

            this.spbWorker = new BackgroundWorker();
            this.spbWorker.DoWork += this.DoWork; // Set up the Background Worker Events
            this.spbWorker.RunWorkerCompleted += this.RunWorkerCompleted;
            this.spbWorker.RunWorkerAsync();

            return true;
        }

        private void DoWork(object sender, DoWorkEventArgs e)
        {
            while (!StopFlag) SpbUnitMeasurementLoop();
        }

        private void RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var onNewLogMessage = NewMessage;
            onNewLogMessage?.Invoke("SpbUnit Complette", MessageType.StateMessage);
        }

        private float Alfa = 0.0027f;
        private float RH0 = 10.1f;
        private float RH20 = 18.6f;

        public int SpbUnitMeasurementLoop()
        {
            var data = new byte[512];
            var offset = 0;
            bool header_is_recive = false;
            int paket_length = 0;
            int manager_id = 0;
            int attribute = 0;
            bool paket_is_recive = false;
            bool is_data_paket = false;
            bool is_voltage_paket = false;
            string paket_in_hex = "xxx";
            var onNewMessage = this.NewMessage;
            try
            {
                while (true)
                {
                    if (Port.BytesToRead > 4000)
                    {
                        onNewMessage?.Invoke(Port.BytesToRead.ToString(), MessageType.InfoMessage);
                    }

                    var readResult = Port.Read(data, offset, 1);
                    if (offset == 0 && data[0] == 0x1D) header_is_recive = true;
                    if (offset == 1 && header_is_recive) paket_length = data[1] + 1;
                    if (offset == 2 && header_is_recive) manager_id = data[2];
                    if (offset == 3 && header_is_recive) attribute = data[3];
                    if (offset == 4 && header_is_recive && data[4] == 0x07) is_data_paket = true;
                    if (offset == 4 && header_is_recive && data[4] == 0x0A) is_voltage_paket = true;
                    if (offset == paket_length - 1 && header_is_recive)
                    {
                        paket_in_hex = ToHexStr(data, paket_length);
                        if (is_data_paket || is_voltage_paket) paket_is_recive = true;
                        break;
                    }

                    offset += readResult;
                    if (offset == 64) break;
                    //var finish = DateTime.Now;
                    //var span = finish - start;
                    //if (span.Milliseconds > 500) break; //Отключать на время отладки
                }

                if (StopFlag) SpbUnitMeasurementStop();

                if (paket_is_recive)
                {
                    if (is_data_paket)
                    {
                        var checkSum = CalCheckSum(data, 32);
                        if (data[32] != checkSum)
                        {
                            ErrorCount++;
                            var onNewLogMessage = NewMessage;
                            onNewLogMessage?.Invoke(paket_in_hex + "  error CRC!", MessageType.Log);
                        }
                        else
                        {
                            var Ut0 = System.BitConverter.ToSingle(data, 5) / 1000f; //Общее напряжениен агрева
                            var UH = System.BitConverter.ToSingle(data, 9) / 1000f; //Напряжение на нагревателе
                            var sensor1 = System.BitConverter.ToSingle(data, 13);
                            var power2 = System.BitConverter.ToSingle(data, 17);
                            var heater2 = System.BitConverter.ToSingle(data, 21);
                            var sensor2 = System.BitConverter.ToSingle(data, 25);
                            var key = data[29];
                            var measureNumber = 256 * (uint)data[31] + (uint)data[30];

                            float RH = RH0 * UH / (Ut0 - UH);
                            float T = 20f + (RH - RH20) / (RH20 * Alfa);

                            var value1 = new double[3];
                            value1[0] = RH;
                            value1[1] = T;
                            value1[2] = 0;

                            var value2 = new double[3];
                            value2[0] = Ut0; //power2;
                            value2[1] = UH; //heater2
                            value2[2] = 0; //sensor2;

                            var onNewData = this.SpbUnitNewData;
                            onNewData?.Invoke(value1, value2, MeasurementCount++);
                            MeasurementCount++;
                            if (MeasurementCount % 100 == 0)
                            {
                                if (voltage1 > 3)
                                    deltaVoltage = -1 * deltaVoltage;
                                if (voltage1 < 1.3f)
                                    deltaVoltage = -1 * deltaVoltage;
                                voltage1 += deltaVoltage;
                                voltage2 += deltaVoltage;
                                //var debugMessage = $"v1:{voltage1} v2: {voltage2}";
                                //onNewMessage?.Invoke(debugMessage, MessageType.InfoMessage);
                            }

                            if (MeasurementCount % 10 == 0)
                            {
                                onNewMessage?.Invoke(Port.BytesToRead.ToString(), MessageType.InfoMessage);
                            }

                            //  if (MeasurementCount % 1 == 0)
                            {
                                SendCommand(CommandCod.SetDac, 1, voltage1);
                                SendCommand(CommandCod.SetDac, 2, voltage2);
                            }
                        }
                    }
                    if (is_voltage_paket)
                    {
                        var checkSum = CalCheckSum(data, 13);
                        if (data[13] != checkSum)
                        {
                            ErrorCount++;
                            var onNewLogMessage = NewMessage;
                            onNewLogMessage?.Invoke(paket_in_hex + "  error CRC!", MessageType.Log);
                        }
                        else
                        {
                            float heater1 = System.BitConverter.ToSingle(data, 5);
                            float heater2 = System.BitConverter.ToSingle(data, 9);
                        }
                    }
                }
                else
                {
                    ErrorCount++;
                    onNewMessage?.Invoke("Переполнение буффера", MessageType.Log);
                }

                onNewMessage?.Invoke($"Измерено:{MeasurementCount}", MessageType.MeasureAmountMessage);
                onNewMessage?.Invoke($"Ошибок: {ErrorCount}", MessageType.ErorrAmountMessage);
            }
            catch (Exception ex)
            {
                onNewMessage?.Invoke(ex.Message, MessageType.StateMessage);
            }
            return 0;
        }
        public bool SpbUnitMeasurementStop()
        {
            if (this.Port == null || this.Port.IsOpen == false) return false;
            //this.Port.DiscardInBuffer(); // Очистили буфер COM порта от данных
            //this.Port.DiscardOutBuffer(); // Очистили буфер COM порта от данных
            this.startDateTime = DateTime.Now;
            SendCommand(CommandCod.StopDataStream);
            var data = new byte[64];
            var offset = 0;
            while (true)
            {
                var readResult = Port.Read(data, offset, 1);
                offset += readResult;
                if (offset == 6)
                    break;
            }
            var result = ToHexStr(data, 6);
            if (result != "1D 05 83 06 0C 9E")
            {
                var onNewLogMessage = NewMessage;
                onNewLogMessage?.Invoke("Unexpected answer: " + result, MessageType.Log);
                ErrorCount++;
                return false;
            }

            Port.Close();
            Port = null;
            return true;
        }
    }
}