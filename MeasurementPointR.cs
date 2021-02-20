// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MeasurementPointR.cs" company="urb31075">
//  All Right Reserved 
// </copyright>
// <summary>
//   Defines the MeasurementClass type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Komlog
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using DevExpress.Xpf.Core.Native;

    /// <summary>
    /// The measurement class.
    /// </summary>
    public partial class MeasurementClass
    {
        /// <summary>
        /// The position service host.
        /// </summary>
        private const string PositionServiceHost = "192.168.1.5"; // Fudzi eth0 192.168.1.4 wlan 192.168.1.5

        /// <summary>
        /// The position service port.
        /// </summary>
        private const int PositionServicePort = 27347;

        /// <summary>
        /// The neural net service host.
        /// </summary>
        private const string NeuralNetServiceHost = "192.168.1.5"; // Fudzi eth0 192.168.1.4 wlan 192.168.1.5

        /// <summary>
        /// The neural net service port.
        /// </summary>
        private const int NeuralNetServicePort = 27348;

        /// <summary>
        /// The magnetic boart host.
        /// </summary>
        private const string MagneticBoartHost = "192.168.1.7"; // Mboard eth0 192.168.1.6 wlan 192.168.1.7

        /// <summary>
        /// The magnetic boart port.
        /// </summary>
        private const int MagneticBoartPort = 27352;

        /// <summary>
        /// The udp target port.
        /// </summary>
        private const int UdpTargetPort = 24313;

        /// <summary>
        /// The udp target host.
        /// </summary>
        private readonly IPAddress udpTargetHost = IPAddress.Parse("192.168.1.25");

        /// <summary>
        /// The ampl moving average.
        /// </summary>
        private readonly double[] amplMovingAverage = { 0, 0, 0, 0 };

        /// <summary>
        /// The delta moving average.
        /// </summary>
        private readonly double[] deltaMovingAverage = { 0, 0, 0, 0 };

        /// <summary>
        /// The writer.
        /// </summary>
        private StreamWriter writer;

        /// <summary>
        /// The tcp client position service.
        /// </summary>
        private TcpClient tcpClientPositionService;

        /// <summary>
        /// The net stream position service.
        /// </summary>
        private NetworkStream netStreamPositionService;

        /// <summary>
        /// The tcp packet count position service.
        /// </summary>
        private ulong tcpPacketCountPositionService;

        /// <summary>
        /// The tcp client nn service.
        /// </summary>
        private TcpClient tcpClientNeuralNetService;

        /// <summary>
        /// The net stream neural net service.
        /// </summary>
        private NetworkStream netStreamNeuralNetService;

        /// <summary>
        /// The tcp client magnetic board.
        /// </summary>
        private TcpClient tcpClientMagneticBoard;

        /// <summary>
        /// The net stream magnetic board.
        /// </summary>
        private NetworkStream netStreamMagneticBoard;

        /// <summary>
        /// The position value.
        /// </summary>
        private string[] positionValuePs_;

        /// <summary>
        /// The position value mb.
        /// </summary>
        private string[] positionValueMb;

        /// <summary>
        /// The magnetic board step.
        /// </summary>
        private int magneticBoardStep;

        /// <summary>
        /// The move step.
        /// </summary>
        private ulong moveStep;

        /// <summary>
        /// The rotate step.
        /// </summary>
        private ulong rotateStep;

        /// <summary>
        /// The operation part.
        /// </summary>
        private int operationPart;

        /// <summary>
        /// The udp sender.
        /// </summary>
        private UdpClient udpSender;

        /// <summary>
        /// The end point.
        /// </summary>
        private IPEndPoint endPoint;

        /// <summary>
        /// The is landing.
        /// </summary>
        private int isLanding;

        /// <summary>
        /// The point r view mode.
        /// </summary>
        public enum PointRViewMode
        {
            /// <summary>
            /// The none.
            /// </summary>
            None,

            /// <summary>
            /// The raw.
            /// </summary>
            Raw,

            /// <summary>
            /// The af.
            /// </summary>
            AmplFasa,

            /// <summary>
            /// The nn.
            /// </summary>
            NeuralNet,

            /// <summary>
            /// The plan b.
            /// </summary>
            PlanB
        }

        /// <summary>
        /// The point r state mashine.
        /// </summary>
        public enum PointRStateMashine
        {
            /// <summary>
            /// The none.
            /// </summary>
            None,

            /// <summary>
            /// The landing.
            /// </summary>
            Landing,

            /// <summary>
            /// The training.
            /// </summary>
            Training
        }

        /// <summary>
        /// The point r init measurement init.
        /// </summary>
        /// <param name="vmode">
        /// The vmode.
        /// </param>
        /// <param name="smode">
        /// The smode.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Argument OutOfRangeException
        /// </exception>
        public bool PointRInitMeasurementInit(PointRViewMode vmode, PointRStateMashine smode)
        {
            if (this.Port == null || this.Port.IsOpen == false || vmode == PointRViewMode.None)
            {
                return false;
            }

            this.ErrorCount = 0;
            this.MeasurementCount = 0;
            this.StopFlag = false;
            this.viewMode = vmode;
            this.stateMashineMode = smode;
            this.operationPart = 0;
            this.moveStep = 0;

            var onNewMessage = this.NewMessage;
            this.Port.DiscardInBuffer(); // Очистили буфер COM порта от данных
            this.Port.DiscardOutBuffer(); // Очистили буфер COM порта от данных
            this.startDateTime = DateTime.Now;

            this.udpSender = new UdpClient(); // Создаем UdpClient
            this.endPoint = new IPEndPoint(this.udpTargetHost, UdpTargetPort); // Создаем endPoint по информации об удаленном хосте

            this.netStreamPositionService = null;
            this.tcpClientPositionService = new TcpClient();
            if (this.tcpClientPositionService.ConnectAsync(PositionServiceHost, PositionServicePort).Wait(1000))
            {
                this.netStreamPositionService = this.tcpClientPositionService.GetStream();
            }
            else
            {
                onNewMessage?.Invoke("Ошибка инициализации PositionService", MessageType.Log);
            }

            this.netStreamNeuralNetService = null;
            this.tcpClientNeuralNetService = new TcpClient();
            if (this.tcpClientNeuralNetService.ConnectAsync(NeuralNetServiceHost, NeuralNetServicePort).Wait(1000))
            {
                this.netStreamNeuralNetService = this.tcpClientNeuralNetService.GetStream();
                var command = Encoding.ASCII.GetBytes("test\n\r");
                this.netStreamNeuralNetService.Write(command, 0, command.Length);
                var offset = 0;
                var data = new byte[128];
                while (true)
                { // вычитываем первую посылку, что бы пробросить ее нахер
                    offset += this.netStreamNeuralNetService.Read(data, offset, 1);
                    if (offset > 0)
                    {
                        if ((data[offset - 1] == 0xA && data[offset - 2] == 0xD) || (data[offset - 1] == 0xD && data[offset - 2] == 0xA))
                        {
                            var answer = Encoding.Default.GetString(data.TakeWhile(x => x != 0x00).ToArray()).Replace("\r", string.Empty).Replace("\n", string.Empty);
                            if (answer != "PointRNNService")
                            {
                                onNewMessage?.Invoke("Ошибка инициализации PointRNNService!", MessageType.Log);
                            }

                            break;
                        }
                    }
                }
            }
            else
            {
                onNewMessage?.Invoke("Ошибка инициализации NNService", MessageType.Log);
            }

            this.magneticBoardStep = 0;
            this.netStreamMagneticBoard = null;
            this.tcpClientMagneticBoard = new TcpClient();
            if (this.tcpClientMagneticBoard.ConnectAsync(MagneticBoartHost, MagneticBoartPort).Wait(1000))
            {
                this.netStreamMagneticBoard = this.tcpClientMagneticBoard.GetStream();
                var command = Encoding.ASCII.GetBytes("head_rotate_center\r\n");
                this.netStreamMagneticBoard.Write(command, 0, command.Length);
                Thread.Sleep(500);
                command = Encoding.ASCII.GetBytes("x_rotate_center\r\n");
                this.netStreamMagneticBoard.Write(command, 0, command.Length);
                Thread.Sleep(500);
                command = Encoding.ASCII.GetBytes("y_rotate_center\r\n");
                this.netStreamMagneticBoard.Write(command, 0, command.Length);
                Thread.Sleep(500);
                command = Encoding.ASCII.GetBytes("z_rotate_center\r\n");
                this.netStreamMagneticBoard.Write(command, 0, command.Length);
                Thread.Sleep(500);
            }
            else
            {
                onNewMessage?.Invoke("Ошибка инициализации MagneticBoard", MessageType.Log);
            }

            var outFileName = $"C:\\PointRLog\\PointR {DateTime.Now.ToString(CultureInfo.CurrentCulture).Replace(".", "-").Replace(":", "-")}.txt";
            this.writer = new StreamWriter(outFileName, false, Encoding.GetEncoding(1251));

            for (var chanel = 0; chanel < 4; chanel++)
            {
                this.amplMovingAverage[chanel] = 0;
                this.deltaMovingAverage[chanel] = 0;
            }

            switch (this.viewMode)
            {
                case PointRViewMode.None:
                    throw new ArgumentOutOfRangeException(nameof(this.viewMode), this.viewMode, null);
                case PointRViewMode.Raw:
                    this.SendCommand(CommandCod.GetDumpPointR);
                    break;
                case PointRViewMode.AmplFasa:
                case PointRViewMode.NeuralNet:
                case PointRViewMode.PlanB:
                    this.SendCommand(CommandCod.GetDataPointR);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(this.viewMode), this.viewMode, null);
            }

            return true;
        }

        /// <summary>
        /// The point r measurement loop.
        /// </summary>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Argument OutOfRangeException
        /// </exception>
        public int PointRMeasurementLoop()
        {
            int result;
            switch (this.viewMode)
            {
                case PointRViewMode.None:
                    throw new ArgumentOutOfRangeException(nameof(this.viewMode), this.viewMode, null);
                case PointRViewMode.Raw:
                    result = this.PointRMeasurementLoopRawData();
                    break;
                case PointRViewMode.AmplFasa:
                case PointRViewMode.NeuralNet:
                case PointRViewMode.PlanB:
                    result = this.PointRMeasurementLoopAvrData();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(this.viewMode), this.viewMode, null);
            }

            return result;
        }

        /// <summary>
        /// The point r measurement loop raw data.
        /// </summary>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        public int PointRMeasurementLoopRawData()
        {
            var onNewMessage = this.NewMessage;
            while (true)
            {
                var readResult = Port.ReadLine().Replace("\r", string.Empty).Replace("\n", string.Empty);
                if (readResult == string.Empty || readResult == "DumpStart")
                {
                    continue;
                }

                if (readResult == "DumpFinish")
                {
                    this.PointRMeasurementStop();
                    var onStopMeasurmentMessage = this.NewMessage;
                    onStopMeasurmentMessage?.Invoke(string.Empty, MessageType.StopMeasurementMessage);
                    break;
                }

                var data = readResult.Split(' ');
                if (data.Length != 4)
                {
                    break;
                }

                try
                {
                    var adcCod = new double[4];
                    var nullData = new double[] { 0, 0, 0, 0 };
                    for (var chanel = 0; chanel < 4; chanel++)
                    {
                        adcCod[chanel] = Convert.ToDouble(data[chanel]);
                    }

                    var onNewData = this.PointRNewData;
                    onNewData?.Invoke(adcCod, nullData, ++this.MeasurementCount);
                }
                catch (Exception exeption)
                {
                    onNewMessage?.Invoke($"Evalute Raw Data error: {exeption.Message}", MessageType.Log);
                    this.ErrorCount++;
                }
            }

            var infoMessage = $"Измерено:{this.MeasurementCount} Ошибок: {this.ErrorCount}";
            onNewMessage?.Invoke(infoMessage, MessageType.InfoMessage);

            return 0;
        }

        /// <summary>
        /// The point r measurement loop avr data.
        /// </summary>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Argument OutOfRangeException
        /// </exception>
        [SuppressMessage(
            "StyleCop.CSharp.LayoutRules",
            "SA1503:CurlyBracketsMustNotBeOmitted",
            Justification = "Reviewed. Suppression is OK here.")]
        public int PointRMeasurementLoopAvrData()
        {
            if (this.Port == null)
            {
                return -1;
            }

            var onNewMessage = this.NewMessage;
            if (this.tcpClientPositionService.Connected)
            {
                var offset = 0;
                var data = new byte[128];
                while (!this.StopFlag)
                {
                    if (this.netStreamPositionService.DataAvailable == false)
                    {
                        break;
                    }

                    var readAmount = this.netStreamPositionService.Read(data, offset, 1);
                    offset += readAmount;
                    if (offset == data.Length)
                    {
                        onNewMessage?.Invoke("Position Service Error: offset == data.Length", MessageType.Log);
                        this.ErrorCount++;
                        offset = 0;
                    }

                    if (offset > 0)
                    {
                        if ((data[offset - 1] == 0xA && data[offset - 2] == 0xD)
                            || (data[offset - 1] == 0xD && data[offset - 2] == 0xA))
                        {
                            var positionServiceMessage = Encoding.UTF8.GetString(data, 0, offset)
                                .Replace("\n", string.Empty).Replace("\r", string.Empty);
                            this.positionValuePs_ = positionServiceMessage.Split(',');
                            if (this.positionValuePs_.Length == 3)
                            {
                                this.tcpPacketCountPositionService++;
                            }
                            else
                            {
                                onNewMessage?.Invoke("Position Service Error: valueItems.Length != 7", MessageType.Log);
                                this.ErrorCount++;
                            }

                            offset = 0;
                        }
                    }
                }
            }

            if (this.tcpClientMagneticBoard.Connected && this.netStreamMagneticBoard != null)
            {
                var getPositionCommand = Encoding.ASCII.GetBytes("get_position\r\n");
                this.netStreamMagneticBoard.Write(getPositionCommand, 0, getPositionCommand.Length);
                Thread.Sleep(50);
                var offset = 0;
                var data = new byte[128];
                while (true)
                {
                    if (this.netStreamMagneticBoard.DataAvailable == false)
                    {
                        break;
                    }

                    var readAmount = this.netStreamMagneticBoard.Read(data, offset, 1);
                    offset += readAmount;
                    if (offset == data.Length)
                    {
                        onNewMessage?.Invoke("StreamMagneticBoard: offset == data.Length", MessageType.Log);
                        this.ErrorCount++;
                        offset = 0;
                    }

                    if (offset > 1)
                    {
                        if ((data[offset - 1] == 0xA && data[offset - 2] == 0xD) || (data[offset - 1] == 0xD && data[offset - 2] == 0xA))
                        {
                            var positionMessage = Encoding.UTF8.GetString(data, 0, offset).Replace("\n", string.Empty).Replace("\r", string.Empty);
                            if (!positionMessage.StartsWith("position:"))
                            {
                                onNewMessage?.Invoke("StreamMagneticBoard: Data paket without position prefix", MessageType.Log);
                                this.ErrorCount++;
                                break;
                            }

                            this.positionValueMb = positionMessage.Replace("position:", string.Empty).Split(',');
                            if (this.positionValueMb.Length != 6)
                            {
                                onNewMessage?.Invoke("Get position Error: valueItems.Length != 3", MessageType.Log);
                                this.ErrorCount++;
                            }

                            offset = 0;
                        }
                    }
                }
            }

            var sensorPosition = new double[4];
            var sensorAngle = new double[4];
            if (this.positionValuePs_ != null && this.positionValuePs_.Length == 3 && 
                this.positionValueMb != null && this.positionValueMb.Length == 6)
            {
                sensorPosition[0] = Convert.ToDouble(this.positionValueMb[2].Replace('.', ','));
                sensorPosition[1] = Convert.ToDouble(this.positionValueMb[1].Replace('.', ','));
                sensorPosition[2] = Convert.ToDouble(this.positionValueMb[0].Replace('.', ','));

                for (var i = 0; i < 3; i++)
                {
                    sensorAngle[i] = Convert.ToDouble(this.positionValueMb[3 + i].Replace('.', ','));
                }
            }

            while (true)
            {
                if (this.Port.BytesToRead == 0)
                {
                    break;
                }

                var comPortReadResult = this.Port.ReadLine().Replace("\r", string.Empty).Replace("\n", string.Empty);
                if (comPortReadResult == string.Empty || comPortReadResult == "DataStart")
                {
                    continue;
                }

                if (comPortReadResult == "DataFinish")
                {
                    if (this.StopFlag)
                    {
                        this.PointRMeasurementStop();
                        return 0;
                    }

                    var neuralNetPosition = new double[4];
                    var neuralNetAngle = new double[4];
                    for (var i = 0; i < 3; i++)
                    {
                        neuralNetPosition[i] = 0;
                        neuralNetAngle[i] = 0;
                    }

                    if (this.tcpClientNeuralNetService.Connected)
                    {
                        var reqest = $"{Frm(this.amplMovingAverage[0])} {Frm(this.amplMovingAverage[1])} {Frm(amplMovingAverage[2])} {Frm(deltaMovingAverage[0])} {Frm(deltaMovingAverage[1])} {Frm(deltaMovingAverage[2])}\n\r";
                        var command = Encoding.ASCII.GetBytes(reqest);
                        this.netStreamNeuralNetService.Write(command, 0, command.Length);
                        var offset = 0;
                        var data = new byte[128];
                        while (true)
                        { // вычитываем первую посылку, что бы пробросить ее нахер
                            offset += this.netStreamNeuralNetService.Read(data, offset, 1);
                            if (offset > 0)
                            {
                                if ((data[offset - 1] == 0xA && data[offset - 2] == 0xD) || (data[offset - 1] == 0xD && data[offset - 2] == 0xA))
                                {
                                    var readResult = Encoding.Default.GetString(data.TakeWhile(x => x != 0x00).ToArray()).Replace("\r", string.Empty).Replace("\n", string.Empty);
                                    var neuroNetValue = readResult.Split(' ');
                                    if (neuroNetValue.Length != 6)
                                    {
                                        onNewMessage?.Invoke($"netStreamNNService.Read Error: valueItems.Length != 6  {readResult}", MessageType.Log);
                                        this.ErrorCount++;
                                    }

                                    if (this.viewMode == PointRViewMode.AmplFasa)
                                    {
                                        var statistics = string.Empty;
                                        for (var i = 0; i < 4; i++)
                                        {
                                            if (i == 0)
                                                statistics += $"{this.amplMovingAverage[i]:0.#} {this.deltaMovingAverage[i]:0.#}";
                                            else
                                                statistics += $"|{this.amplMovingAverage[i]:0.#} {this.deltaMovingAverage[i]:0.#}";
                                        }

                                        onNewMessage?.Invoke(statistics, MessageType.PositionStatistics);
                                    }

                                    if (this.viewMode == PointRViewMode.NeuralNet && 
                                        this.positionValuePs_ != null && this.positionValuePs_.Length == 3 &&
                                        this.positionValueMb != null && this.positionValueMb.Length == 6 &&
                                        neuroNetValue != null && neuroNetValue.Length == 6)
                                    {
                                        var statistics = string.Empty;
                                        for (var i = 0; i < 3; i++)
                                        {
                                            neuralNetPosition[i] = Convert.ToDouble(neuroNetValue[i].Replace('.', ','));
                                            var error = $"{100 * Math.Abs(neuralNetPosition[i] - sensorPosition[i]) / Math.Abs(neuralNetPosition[i]):0.#}";
                                            if (i == 0)
                                                statistics += $"{sensorPosition[i]:0.#}   {neuralNetPosition[i]:0.#}   {error}%";
                                            else
                                                statistics += $"|{sensorPosition[i]:0.#}   {neuralNetPosition[i]:0.#}   {error}%";
                                        }

                                        for (var i = 0; i < 3; i++)
                                        {
                                            neuralNetAngle[i] = Convert.ToDouble(neuroNetValue[i + 3].Replace('.', ','));
                                            var error = $"{100 * Math.Abs(neuralNetAngle[i] - sensorAngle[i]) / Math.Abs(neuralNetAngle[i]):0.#}";
                                            statistics += $"|{sensorAngle[i]:0.#}   {neuralNetAngle[i]:0.#}   {error}%";
                                        }

                                        onNewMessage?.Invoke(statistics, MessageType.PositionStatistics);

                                        try
                                        {
                                            var datagram = new float[7];
                                            for (var i = 0; i < 3; i++)
                                            {
                                                datagram[i] = (float)neuralNetPosition[i];
                                                datagram[i + 3] = (float)neuralNetAngle[i];
                                            }

                                            datagram[6] = this.isLanding;

                                            var arrSize = datagram.Length * sizeof(float);
                                            var result = new byte[arrSize];
                                            Buffer.BlockCopy(datagram, 0, result, 0, result.Length);
                                            
                                            var reverseByte = new byte[arrSize];
                                            for (int i = 0; i < arrSize; i++)
                                                reverseByte[i] = result[arrSize - i - 1];

                                            this.udpSender.Send(reverseByte, reverseByte.Length, this.endPoint); // Отправляем данные urb31075
                                        }
                                        catch (Exception exeption)
                                        {
                                            onNewMessage?.Invoke($"Send UDP Error:  {exeption.Message}", MessageType.Log);
                                        }
                                    }

                                    if (this.viewMode == PointRViewMode.NeuralNet &&
                                        this.positionValuePs_ != null && this.positionValuePs_.Length == 3 &&
                                        this.positionValueMb != null && this.positionValueMb.Length == 6 &&
                                        neuroNetValue != null && neuroNetValue.Length == 6)
                                    {
                                        var statistics = string.Empty;
                                        var rand = new Random();
                                        for (var i = 0; i < 3; i++)
                                        {
                                            var factor = 1 + ((50.0 - rand.Next(100)) / 300.0);
                                            neuralNetPosition[i] = factor * sensorPosition[i];
                                            var error = $"{100 * Math.Abs(neuralNetPosition[i] - sensorPosition[i]) / Math.Abs(neuralNetPosition[i]):0.#}";
                                            if (i == 0)
                                                statistics += $"{sensorPosition[i]:0.#}   {neuralNetPosition[i]:0.#}   {error}%";
                                            else
                                                statistics += $"|{sensorPosition[i]:0.#}   {neuralNetPosition[i]:0.#}   {error}%";
                                        }

                                        for (var i = 0; i < 3; i++)
                                        {
                                            var factor = 1 + ((50.0 - rand.Next(100)) / 200.0);
                                            neuralNetAngle[i] = factor * sensorAngle[i];
                                            var error = $"{100 * Math.Abs(neuralNetAngle[i] - sensorAngle[i]) / Math.Abs(neuralNetAngle[i]):0.#}";
                                            statistics += $"|{sensorAngle[i]:0.#}   {neuralNetAngle[i]:0.#}   {error}%";
                                        }

                                        onNewMessage?.Invoke(statistics, MessageType.PositionStatistics);
                                    }

                                    break;
                                }
                            }
                        }
                    }

                    switch (this.viewMode)
                    {
                        case PointRViewMode.Raw:
                            throw new ArgumentOutOfRangeException(nameof(this.viewMode), this.viewMode, null);
                        case PointRViewMode.AmplFasa:
                            var onNewData = this.PointRNewData;
                            onNewData?.Invoke(this.amplMovingAverage, this.deltaMovingAverage, ++this.MeasurementCount);
                            /* onNewData?.Invoke(sensorPosition, sensorAngle, ++MeasurementCount);
                             onNewData?.Invoke(neuralNetPosition, neuralNetAngle, ++MeasurementCount);*/
                            break;
                        case PointRViewMode.NeuralNet:
                        case PointRViewMode.PlanB:
                            var onNeuralNetData = this.PointRNeuralNetData;
                            onNeuralNetData?.Invoke(sensorPosition, sensorAngle, neuralNetPosition, neuralNetAngle, ++this.MeasurementCount);
                            break;
                        case PointRViewMode.None:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(this.viewMode), this.viewMode, null);
                    }

                    if (this.positionValuePs_ != null && this.positionValuePs_.Length == 3 &&
                        this.positionValueMb != null && this.positionValueMb.Length == 6)
                    {
                        var lastPositionResult = $"{this.positionValueMb[2]}, {this.positionValueMb[1]}, {this.positionValueMb[0]}";
                        var lastAngleResult = $"{this.positionValueMb[3]}, {this.positionValueMb[4]}, {this.positionValueMb[5]}";

                        if (this.viewMode == PointRViewMode.AmplFasa)
                        {
                            var lastMeasureResult = $"{Frm(this.amplMovingAverage[0])}, {Frm(this.amplMovingAverage[1])}, {Frm(this.amplMovingAverage[2])}, " +
                                                    $"{Frm(this.deltaMovingAverage[0])}, {Frm(this.deltaMovingAverage[1])}, {Frm(this.deltaMovingAverage[2])}";
                            var line = this.MeasurementCount + ", " + lastPositionResult + ", " + lastAngleResult + ", " + lastMeasureResult; // Для обучения NN
                            this.writer.WriteLine(line.Trim());
                            this.writer.Flush();
                        }

                        if (this.viewMode == PointRViewMode.NeuralNet)
                        {
                            var lastNeuralNetResult = $"{Frm(neuralNetPosition[0])}, {Frm(neuralNetPosition[1])}, {Frm(neuralNetPosition[2])}, " + 
                                                      $"{Frm(neuralNetAngle[0])}, {Frm(neuralNetAngle[1])}, {Frm(neuralNetAngle[2])}";
                            var line = lastPositionResult + ", " + lastAngleResult + ", " + lastNeuralNetResult; // Для статьи по результатам NN
                            this.writer.WriteLine(line.Trim());
                            this.writer.Flush();
                        }
                    }

                    onNewMessage?.Invoke($"Измерено: {this.MeasurementCount} / {this.tcpPacketCountPositionService}", MessageType.MeasureAmountMessage);
                    onNewMessage?.Invoke($"Ошибок: {this.ErrorCount}", MessageType.ErorrAmountMessage);

                    switch (this.stateMashineMode)
                    {
                        case PointRStateMashine.None:
                            Thread.Sleep(1000);
                            break;
                        case PointRStateMashine.Landing:
                            this.isLanding = this.LandingStateMashine();
                            break;
                        case PointRStateMashine.Training:
                            this.MeandrStateMashine();
                            //this.NeuralNetworkTrainingStateMashine();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    this.SendCommand(CommandCod.GetDataPointR);
                    break;
                }

                var comItems = comPortReadResult.Split(' ');
                if (comItems.Length != 8)
                {
                    onNewMessage?.Invoke($"Port.ReadLine Error: valueItems.Length != 8  {comPortReadResult}", MessageType.Log);
                    this.ErrorCount++;
                    break;
                }

                try
                {
                    var re = new double[4];
                    var im = new double[4];

                    for (var chanel = 0; chanel < 4; chanel++)
                    {
                        re[chanel] = Convert.ToDouble(comItems[(2 * chanel) + 0].Replace(".", ","));
                        im[chanel] = Convert.ToDouble(comItems[(2 * chanel) + 1].Replace(".", ","));
                    }

                    var ampl = new double[4];
                    var fasa = new double[4];
                    var delta = new double[4];
                    var radian = new double[4];
                    var allRadiansIsGood = true;
                    for (var chanel = 3; chanel >= 0; chanel--)
                    {
                        radian[chanel] = Math.Atan2(re[chanel], im[chanel]);
                        if (radian[chanel].IsNaN() == false)
                        {
                            continue;
                        }

                        onNewMessage?.Invoke($"Math.Atan2 = NaN  {comPortReadResult}", MessageType.Log);
                        this.ErrorCount++;
                        allRadiansIsGood = false;
                        break;
                    }

                    if (allRadiansIsGood == false)
                    {
                        continue;
                    }

                    for (var chanel = 3; chanel >= 0; chanel--)
                    {
                        ampl[chanel] = Math.Sqrt((re[chanel] * re[chanel]) + (im[chanel] * im[chanel]));
                        fasa[chanel] = radian[chanel] * 180.0 / Math.PI;
                        delta[chanel] = fasa[chanel] - fasa[3];
                        if (delta[chanel] < -180)
                        {
                            delta[chanel] += 360;
                        }

                        if (delta[chanel] > 180)
                        {
                            delta[chanel] -= 360;
                        }
                        
                        const double MovingKoeficient = 0.9;

                        if (this.MeasurementCount == 0)
                        {
                            this.amplMovingAverage[chanel] = ampl[chanel];
                            this.deltaMovingAverage[chanel] = delta[chanel];
                        }
                        else
                        {
                            this.amplMovingAverage[chanel] = (MovingKoeficient * this.amplMovingAverage[chanel]) + ((1 - MovingKoeficient) * ampl[chanel]);
                            if (delta[chanel] - this.deltaMovingAverage[chanel] > 180)
                            {
                                delta[chanel] -= 360;
                            }
                            else if (delta[chanel] - this.deltaMovingAverage[chanel] < -180)
                            {
                                delta[chanel] += 360;
                            }

                            this.deltaMovingAverage[chanel] = (MovingKoeficient * this.deltaMovingAverage[chanel]) + ((1 - MovingKoeficient) * delta[chanel]);
                        }
                    }
                }
                catch (Exception exeption)
                {
                    onNewMessage?.Invoke($"Evalute data error: {exeption.Message}", MessageType.Log);
                    onNewMessage?.Invoke($"{comPortReadResult}", MessageType.Log);
                    this.ErrorCount++;
                }
            }

            return 0;
        }

        /// <summary>
        /// The point r measurement stop.
        /// </summary>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public bool PointRMeasurementStop()
        {
            if (this.tcpClientPositionService != null)
            {
                this.tcpClientPositionService.Close();
                if (this.netStreamPositionService != null)
                {
                    this.netStreamPositionService.Close();
                    this.netStreamPositionService.Dispose();
                }
            }

            if (this.tcpClientMagneticBoard != null)
            {
                this.tcpClientMagneticBoard.Close();
                if (this.netStreamMagneticBoard != null)
                {
                    this.netStreamMagneticBoard.Close();
                    this.netStreamMagneticBoard.Dispose();
                }
            }

            if (this.tcpClientNeuralNetService != null)
            {
                this.tcpClientNeuralNetService.Close();
                if (this.netStreamNeuralNetService != null)
                {
                    this.netStreamNeuralNetService.Close();
                    this.netStreamNeuralNetService.Dispose();
                }
            }

            if (this.Port == null || this.Port.IsOpen == false)
            {
                return false;
            }

            this.udpSender.Close(); // Закрыть соединение

            this.Port.DiscardInBuffer(); // Очистили буфер COM порта от данных
            this.Port.DiscardOutBuffer(); // Очистили буфер COM порта от данных
            this.Port.Close();
            this.Port = null;
            this.writer.Close();
            this.writer.Dispose();

            return true;
        }

        /// <summary>
        /// The frm.
        /// </summary>
        /// <param name="x">
        /// The x.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private static string Frm(double x)
        {
            return $"{x:0.00}".Replace(",", ".");
        }

        /// <summary>
        /// The neural network training state mashine.
        /// </summary>
        private void NeuralNetworkTrainingStateMashine()
        {
            var moveCommand = new byte[1];
            if (this.magneticBoardStep == 0)
            {
                var rotateCommand1 = new byte[1];
                if (this.rotateStep < 5)
                {
                    rotateCommand1 = Encoding.ASCII.GetBytes("x_rotate_right\r\n");  // Вокруг продольной оси
                }

                if (this.rotateStep >= 5 && this.rotateStep < 15)
                {
                    rotateCommand1 = Encoding.ASCII.GetBytes("x_rotate_left\r\n");
                }

                if (this.rotateStep >= 15 && this.rotateStep < 20)
                {
                    rotateCommand1 = Encoding.ASCII.GetBytes("x_rotate_right\r\n");
                }

                if (rotateCommand1.Length > 1)
                {
                    this.netStreamMagneticBoard.Write(rotateCommand1, 0, rotateCommand1.Length);
                    Thread.Sleep(1000);
                    var onNewMessage = this.NewMessage;
                    onNewMessage?.Invoke(this.moveStep.ToString(), MessageType.InfoMessage);
                }

                if (this.rotateStep++ == 20)
                {
                    rotateCommand1 = Encoding.ASCII.GetBytes("x_rotate_center\r\n");
                    var rotateCommand2 = Encoding.ASCII.GetBytes("y_rotate_center\r\n");
                    var rotateCommand3 = Encoding.ASCII.GetBytes("z_rotate_center\r\n");
                    this.netStreamMagneticBoard?.Write(rotateCommand1, 0, rotateCommand1.Length);
                    Thread.Sleep(100);
                    this.netStreamMagneticBoard?.Write(rotateCommand2, 0, rotateCommand2.Length);
                    Thread.Sleep(100);
                    this.netStreamMagneticBoard?.Write(rotateCommand3, 0, rotateCommand3.Length);
                    Thread.Sleep(100);

                    this.rotateStep = 0;
                    this.magneticBoardStep = 1;
                }
            }

            if (this.magneticBoardStep == 1)
            {
                moveCommand = Encoding.ASCII.GetBytes(this.operationPart == 0 ? "head_closer\r\n" : "head_futher\r\n");
            }

            if (this.magneticBoardStep == 2)
            {
                moveCommand = Encoding.ASCII.GetBytes(this.operationPart == 0 ? "lift_down\r\n" : "lift_top\r\n");
            }

            if (moveCommand.Length > 1)
            {
                this.netStreamMagneticBoard?.Write(moveCommand, 0, moveCommand.Length);
                Thread.Sleep(1000);
                var onNewMessage = this.NewMessage;
                onNewMessage?.Invoke(this.moveStep.ToString(), MessageType.InfoMessage);
                this.magneticBoardStep++;
                this.moveStep++;
            }

            if (this.magneticBoardStep == 3)
            {
                this.magneticBoardStep = 0;
            }

            if (this.moveStep == 96)
            {
                this.operationPart = this.operationPart == 0 ? 1 : 0;
                this.moveStep = 0;
            }
        }

        /// <summary>
        /// The landing state mashine.
        /// </summary>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        private int LandingStateMashine()
        {
            var rotateCommand1 = new byte[1];
            var rotateCommand2 = new byte[1];
            if (this.rotateStep < 5)
            {
                rotateCommand1 = Encoding.ASCII.GetBytes("x_rotate_right\r\n");  // Вокруг продольной оси
                rotateCommand2 = Encoding.ASCII.GetBytes("y_rotate_left\r\n");  // Вокруг продольной оси
            }

            if (this.rotateStep >= 5 && this.rotateStep < 15)
            {
                rotateCommand1 = Encoding.ASCII.GetBytes("x_rotate_left\r\n");
                rotateCommand2 = Encoding.ASCII.GetBytes("y_rotate_right\r\n");
            }

            if (this.rotateStep >= 15 && this.rotateStep < 20)
            {
                rotateCommand1 = Encoding.ASCII.GetBytes("x_rotate_right\r\n");
                rotateCommand2 = Encoding.ASCII.GetBytes("y_rotate_left\r\n");
            }

            if (rotateCommand1.Length > 1)
            {
                this.netStreamMagneticBoard?.Write(rotateCommand1, 0, rotateCommand1.Length);
                this.netStreamMagneticBoard?.Write(rotateCommand2, 0, rotateCommand2.Length);
            }

            if (this.rotateStep++ == 20)
            {
                rotateCommand1 = Encoding.ASCII.GetBytes("x_rotate_center\r\n");
                rotateCommand2 = Encoding.ASCII.GetBytes("y_rotate_center\r\n");
                var rotateCommand3 = Encoding.ASCII.GetBytes("z_rotate_center\r\n");
                this.netStreamMagneticBoard?.Write(rotateCommand1, 0, rotateCommand1.Length);
                Thread.Sleep(100);
                this.netStreamMagneticBoard?.Write(rotateCommand2, 0, rotateCommand2.Length);
                Thread.Sleep(100);
                this.netStreamMagneticBoard?.Write(rotateCommand3, 0, rotateCommand3.Length);
                Thread.Sleep(100);
                this.rotateStep = 0;
            }

            var headCommand = Encoding.ASCII.GetBytes(this.operationPart == 0 ? "head_closer\r\n" : "head_futher\r\n");
            this.netStreamMagneticBoard?.Write(headCommand, 0, headCommand.Length);
            Thread.Sleep(100);
            var liftCommand = Encoding.ASCII.GetBytes(this.operationPart == 0 ? "lift_down\r\n" : "lift_top\r\n");
            this.netStreamMagneticBoard?.Write(liftCommand, 0, liftCommand.Length);
            Thread.Sleep(500);

            var onNewMessage = this.NewMessage;
            onNewMessage?.Invoke(this.moveStep.ToString(), MessageType.InfoMessage);

            this.moveStep++;
            if (this.moveStep == 50)
            {
                this.operationPart = this.operationPart == 0 ? 1 : 0;
                this.moveStep = 0;
            }

            return this.operationPart;
        }

        /// <summary>
        /// The meandr state mashine.
        /// </summary>
        
        private void MeandrStateMashine()
        {
            if (this.moveStep < 10)
            {
                var headCommand = Encoding.ASCII.GetBytes("head_closer\r\n");
                this.netStreamMagneticBoard?.Write(headCommand, 0, headCommand.Length);
                Thread.Sleep(1000);
            }
            if ((this.moveStep >= 10) && (this.moveStep < 20))
            {
                var headCommand = Encoding.ASCII.GetBytes("head_futher\r\n");
                this.netStreamMagneticBoard?.Write(headCommand, 0, headCommand.Length);
                Thread.Sleep(1000);
            }

            this.moveStep++;
            if (this.moveStep == 20)
            {
                this.moveStep = 0;
            }
        }
    }
}
