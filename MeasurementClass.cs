// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MeasurementClass.cs" company="TC">
// All Right Reserved
// </copyright>
// <summary>
//   Класс измерений
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Komlog
{
    using System;
    using System.Collections.Generic;
    using System.IO.Ports;

    /// <summary>
    /// Класс измерений
    /// </summary>
    public partial class MeasurementClass
    {
        /// <summary>
        /// The start date time.
        /// </summary>
        private DateTime startDateTime;

        /// <summary>
        /// The view mode.
        /// </summary>
        private PointRViewMode viewMode;

        /// <summary>
        /// The state mashine mode.
        /// </summary>
        private PointRStateMashine stateMashineMode;

        /// <summary>
        /// Gets or sets a value indicating whether stop flag.
        /// </summary>
        public bool StopFlag { get; set; }

        /// <summary>
        /// Делегат изменения состояния процесса
        /// </summary>
        /// <param name="processValue">
        /// The process value.
        /// </param>
        public delegate void ChangeProcessValueHandler(string processValue);

        /// <summary>
        /// Делегат появления нового сообщения
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="messageType">
        /// The message type.
        /// </param>
        public delegate void NewMessageHandler(string message, MessageType messageType);

        public delegate void PointRNewDataHandler(double[] ampl, double[] delta, ulong ordinata);

        public delegate void PointRNeuralNetDataHandler(double[] sensorPosition, double[] sensorAngle, 
                                                        double[] neuralNetPosition, double[] neuralNetAngle, ulong ordinata);

        public delegate void SpbUnitNewDataHandler(double[] value1, double[] value2, ulong ordinata);

        /// <summary>
        /// Событие появлкния нового сообщения
        /// </summary>
        public event NewMessageHandler NewMessage;

        public event PointRNewDataHandler PointRNewData;
        public event PointRNeuralNetDataHandler PointRNeuralNetData;
        public event SpbUnitNewDataHandler SpbUnitNewData;

        /// <summary>
        /// Типы сообщений
        /// </summary>
        public enum MessageType
        {
            InfoMessage = 0,
            MeasureAmountMessage = 1,
            ErorrAmountMessage = 2,
            StateMessage = 3,
            DebugMessage = 4,
            PositionStatistics = 5,
            Log = 6,
            StopMeasurementMessage = 7
        }

        /// <summary>
        /// Gets or sets Port.
        /// </summary>
        public SerialPort Port { get; set; }

        /// <summary>
        /// Gets or sets the port name.
        /// </summary>
        public string PortName { get; set; }

        /// <summary>
        /// Gets or sets MeasurementCount.
        /// </summary>
        public ulong MeasurementCount { get; set; }
        public ulong ErrorCount { get; set; }

        /// <summary>
        /// Коды команд
        /// </summary>
        public enum CommandCod : byte
        {
            Nop = 0,
            EmptyCommand = 1,
            GetVersion = 2,
            GetSingleData = 3,
            StartDataStream = 4,
            StopDataStream = 5,
            SetKey = 6,
            GetDac = 7,
            SetDac = 8,
            TestPointR = 9,
            GetDumpPointR = 10,
            GetDataPointR = 11
        }

        private static byte CalCheckSum(IReadOnlyList<byte> packetData, int packetLength)
        {
            byte messCrc = 0;
            const byte b80 = 0x80;
            const byte b18 = 0x18;

            for (var i = 0; i < packetLength; i++)
            {
                var inch = packetData[i];
                for (var b = 0; b < 8; b++)
                {
                    if (((inch ^ messCrc) & 0x01) > 0)
                    {
                        messCrc = (byte)(b80 | ((messCrc ^ b18) >> 1));
                    }
                    else
                    {
                        messCrc = (byte)(messCrc >> 1);
                    }

                    inch >>= 1;
                }

            }
            return messCrc;
        }

        private static string ToHexStr(IReadOnlyList<byte> packetData, int packetLength)
        {
            var line = new byte[1];
            var outHex = "";
            for (var i = 0; i < packetLength; i++)
            {
                line[0] = packetData[i];
                outHex += BitConverter.ToString(line) + " ";
            }

            return outHex.Trim();
        }

        private int xxx = 0;
        public string SendCommand(CommandCod commandCod, byte chanel = 0, float voltage = 0)
        {
            var data = new byte[64];
            byte commandLength = 0;

            var commandStr = string.Empty;
            switch (commandCod)
            {
                case CommandCod.EmptyCommand:
                    commandLength = 5;
                    data[0] = 0x1D;
                    data[1] = commandLength;
                    data[2] = 0x80;
                    data[3] = 0x00;
                    data[4] = 0x00;  // MESSAGE_00
                    data[5] = CalCheckSum(data, commandLength);
                    break;
                case CommandCod.GetVersion:
                    commandLength = 5;
                    data[0] = 0x1D;
                    data[1] = commandLength;
                    data[2] = 0x83;
                    data[3] = 0x00;
                    data[4] = 0x03; // DEVICE_GET_VERSION
                    data[5] = CalCheckSum(data, commandLength);
                    break;
                case CommandCod.GetSingleData:
                    commandLength = 5;
                    data[0] = 0x1D;
                    data[1] = commandLength;
                    data[2] = 0x83;
                    data[3] = 0x00;
                    data[4] = 0x07; // DATA_MEASUR
                    data[5] = CalCheckSum(data, commandLength);
                    break;
                case CommandCod.StartDataStream:
                    commandLength = 5;
                    data[0] = 0x1D;
                    data[1] = commandLength;
                    data[2] = 0x83;
                    data[3] = 0x00;
                    data[4] = 0x05; // START_MEASUR
                    data[5] = CalCheckSum(data, commandLength);
                    break;
                case CommandCod.StopDataStream:
                    commandLength = 5;
                    data[0] = 0x1D;
                    data[1] = commandLength;
                    data[2] = 0x83;
                    data[3] = 0x00;
                    data[4] = 0x06; // STOP_MEASUR
                    data[5] = CalCheckSum(data, commandLength);
                    break;

                case CommandCod.SetKey:
                    commandLength = 5;
                    data[0] = 0x1D;
                    data[1] = commandLength;
                    data[2] = 0x83;
                    data[3] = 0x0F; // 7-4бит. – состояния ключа. 0 – выкл. 1 – вкл. 3 - 0бит. – номер ключа. 1, 2, 3.
                    data[4] = 0x09; // ON_OFF_KEY
                    data[5] = CalCheckSum(data, commandLength);
                    break;

                case CommandCod.GetDac:
                    commandLength = 5;
                    data[0] = 0x1D;
                    data[1] = commandLength;
                    data[2] = 0x83;
                    data[3] = 0;
                    data[4] = 0x0A; // GET_VOLT_CH1_2.
                    data[5] = CalCheckSum(data, commandLength);
                    break;

                case CommandCod.SetDac:
                    commandLength = 9;
                    data[0] = 0x1D;
                    data[1] = commandLength;
                    data[2] = 0x83;
                    data[3] = chanel; //0x01/0x02 – атрибут сообщения.  Номер установочного канала.
                    data[4] = 0x15; // SET_VOLT_CH1_2
                    var yyy = BitConverter.GetBytes(voltage);
                    data[5] = yyy[0]; //4байта - float fRpxVol   напряж. питания нагревателя. Кан.x  0, 1.22B. – 3.999B.
                    data[6] = yyy[1];
                    data[7] = yyy[2];
                    data[8] = yyy[3];
                    data[9] = CalCheckSum(data, commandLength);
                    break;

                case CommandCod.Nop:
                    break;
                case CommandCod.TestPointR:
                    commandStr = "T";
                    break;
                case CommandCod.GetDumpPointR:
                    commandStr = "D";
                    break;
                case CommandCod.GetDataPointR:
                    commandStr = "S";
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(commandCod), commandCod, null);
            }

            if (this.Port == null) return string.Empty;

            var returnValue = string.Empty;
            try
            {
                if (commandStr == string.Empty)
                {
                    returnValue = ToHexStr(data, commandLength + 1);
                    this.Port.Write(data, 0, commandLength + 1);
                }
                else
                {
                    returnValue = commandStr;
                    this.Port.WriteLine(commandStr);
                }
            }
            catch (Exception)
            {
                //LogListBox.Items.Add("*****************************************************************************");
                //LogListBox.Items.Add($"Ошибка: {DateTime.Now:HH:mm:ss} {ex.Message}");
                //LogListBox.Items.Add($"{ex.StackTrace}");
            }

            return returnValue;
        }

        public string GetAnswer(int length)
        {
            var data = new byte[64];
            var offset = 0;
            while (true)
            {
                var readResult = Port.Read(data, offset, 1);
                offset += readResult;
                if (offset == length)
                    break;
            }
            var result = ToHexStr(data, 6);
            return result;
        }
    }
}