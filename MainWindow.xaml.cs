// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="URB">
// All Right Reserved
// </copyright>
// <summary>
//   Interaction logic for MainWindow.xaml
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Komlog
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.IO.Ports;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Windows;
    using System.Windows.Threading;

    using Microsoft.Win32;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// The udp local port.
        /// </summary>
        private const int UdpLocalPort = 24313;

        /// <summary>
        /// The udp remote port.
        /// </summary>
        private const int UdpRemotePort = 24313;

        /// <summary>
        /// The remote ip address.
        /// </summary>
        private readonly IPAddress remoteIpAddress = IPAddress.Parse("127.0.0.1"); // IPAddress.Parse("192.168.1.25");

        /// <summary>
        /// Дата эксперимента
        /// </summary>
        private DateTime experimentDateTime;

        /// <summary>
        /// The tcp client position service.
        /// </summary>
        private TcpClient tcpClientPositionService;

        /// <summary>
        /// The net stream position service.
        /// </summary>
        private NetworkStream netStreamPositionService;

        /// <summary>
        /// The tcp worker position service.
        /// </summary>
        private BackgroundWorker tcpWorkerPositionService;

        /// <summary>
        /// The read tcp.
        /// </summary>
        private bool readTcp = true;

        /// <summary>
        /// The available devices.
        /// </summary>
        private List<DeviceInfo> availableDevices;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            this.InitializeComponent();
            this.synchronizationContext = SynchronizationContext.Current;
            this.measurement.NewMessage += this.NewMessage;
            this.measurement.PointRNewData += this.PointRNewData;
            this.measurement.PointRNeuralNetData += this.PointRNeuralNetData;
            this.measurement.SpbUnitNewData += this.SpbUnitNewData;
        }

        /// <summary>
        /// Делегат для обновления элемента
        /// </summary>
        private delegate void NoArgDelegate();

        /// <summary>
        /// The refresh.
        /// </summary>
        /// <param name="obj">
        /// The obj.
        /// </param>
        public void Refresh(DependencyObject obj)
        {
            obj.Dispatcher.Invoke(DispatcherPriority.Loaded, (NoArgDelegate)delegate { });
        }

        /// <summary>
        /// The start button click.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void StartButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                this.StartButton.IsEnabled = false;
                this.StopButton.IsEnabled = true;
                this.FindDeviceButton.IsEnabled = false;
                this.ClearDataButton.IsEnabled = true;
                this.SaveButton.IsEnabled = false;
                this.FindedDeviceComboBox.IsEnabled = false;
                this.StartWork();
            }
            catch (Exception ex)
            {
                this.LogListBox.Items.Add("*****************************************************************************");
                this.LogListBox.Items.Add($"Ошибка: {DateTime.Now:HH:mm:ss} {ex.Message}");
                this.LogListBox.Items.Add($"{ex.StackTrace}");
            }
        }

        /// <summary> Остановка измерений
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void StopButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                FindDeviceButton.IsEnabled = true;
                SaveButton.IsEnabled = true;
                FindedDeviceComboBox.IsEnabled = true;
                this.StopWork();
            }
            catch (Exception ex)
            {
                LogListBox.Items.Add("*****************************************************************************");
                LogListBox.Items.Add($"Ошибка: {DateTime.Now:HH:mm:ss} {ex.Message}");
                LogListBox.Items.Add($"{ex.StackTrace}");
            }
        }

        /// <summary>  Отчистка данных в серии
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void ClearDataButtonClick(object sender, RoutedEventArgs e)
        {
            this.measurement.MeasurementCount = 0;
            for (var chanel = 0; chanel < chanelAmount; chanel++)
            {
                KomlogValue1XYDiagram2D.Series[chanel].Points.Clear();
                KomlogValue2XYDiagram2D.Series[chanel].Points.Clear();
                this.chanelPoint[chanel].Clear();
            }
        }

        /// <summary>  Сохранение результата
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void SaveButtonClick(object sender, RoutedEventArgs e)
        {
            var outFileName = $"Experiment {this.experimentDateTime.ToShortDateString()} {this.experimentDateTime.ToShortTimeString().Replace(":", "-")}";
            var fileDialog = new SaveFileDialog { Filter = TxtFilter, FilterIndex = 2, RestoreDirectory = true, FileName = outFileName };
            if (fileDialog.ShowDialog() != true)
            {
                return;
            }

            outFileName = fileDialog.FileName;

            var maxPointAmount = 0;
            for (var chanel = 0; chanel < chanelAmount; chanel++)
            {
                if (this.chanelPoint[chanel].Count() > maxPointAmount)
                {
                    maxPointAmount = this.chanelPoint[chanel].Value1.Count;
                }
            }

            var writer = new StreamWriter(outFileName, false, Enc1251);
            for (var point = 1; point < this.chanelPoint[0].Count(); point++)
            {
                var line = $"{point};   {this.chanelPoint[0].Ordinata[point]}";
                for (var chanel = 0; chanel < chanelAmount; chanel++)
                {
                    var value1 = this.chanelPoint[chanel].Value1[point];
                    var value2 = this.chanelPoint[chanel].Value2[point];
                    var value1Str = $"{value1:0.00}".Replace(",", ".");
                    var value2Str = $"{value2:0.00}".Replace(",", ".");
                    line += $";   {value1Str};   {value2Str}";
                }

                writer.WriteLine(line.Trim());
            }

            writer.Close();
            writer.Dispose();
        }

        /// <summary> Выбор видимости отображения каналов
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void SetChanelVisibleCheckBoxClick(object sender, RoutedEventArgs e)
        {
            var isChecked = this.Chanel1CheckBox1.IsChecked;
            if (isChecked != null) this.KomlogValue1XYDiagram2D.Series[0].Visible = (bool)isChecked;

            isChecked = this.Chanel2CheckBox1.IsChecked;
            if (isChecked != null) this.KomlogValue1XYDiagram2D.Series[1].Visible = (bool)isChecked;

            isChecked = this.Chanel3CheckBox1.IsChecked;
            if (isChecked != null) this.KomlogValue1XYDiagram2D.Series[2].Visible = (bool)isChecked;

            isChecked = this.Chanel4CheckBox1.IsChecked;
            if (isChecked != null) this.KomlogValue1XYDiagram2D.Series[3].Visible = (bool)isChecked;

            isChecked = this.Chanel5CheckBox1.IsChecked;
            if (isChecked != null) this.KomlogValue1XYDiagram2D.Series[4].Visible = (bool)isChecked;

            isChecked = this.Chanel6CheckBox1.IsChecked;
            if (isChecked != null) this.KomlogValue1XYDiagram2D.Series[5].Visible = (bool)isChecked;

            isChecked = this.Chanel1CheckBox2.IsChecked;
            if (isChecked != null) this.KomlogValue2XYDiagram2D.Series[0].Visible = (bool)isChecked;

            isChecked = this.Chanel2CheckBox2.IsChecked;
            if (isChecked != null) this.KomlogValue2XYDiagram2D.Series[1].Visible = (bool)isChecked;

            isChecked = this.Chanel3CheckBox2.IsChecked;
            if (isChecked != null) this.KomlogValue2XYDiagram2D.Series[2].Visible = (bool)isChecked;

            isChecked = this.Chanel4CheckBox2.IsChecked;
            if (isChecked != null) this.KomlogValue2XYDiagram2D.Series[3].Visible = (bool)isChecked;

            isChecked = this.Chanel5CheckBox2.IsChecked;
            if (isChecked != null) this.KomlogValue2XYDiagram2D.Series[4].Visible = (bool)isChecked;

            isChecked = this.Chanel6CheckBox2.IsChecked;
            if (isChecked != null) this.KomlogValue2XYDiagram2D.Series[5].Visible = (bool)isChecked;
        }

        /// <summary>  Выбрать все каналы
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void SelectAllChanelButtonClick1(object sender, RoutedEventArgs e)
        {
            Chanel1CheckBox1.IsChecked = true;
            Chanel2CheckBox1.IsChecked = true;
            Chanel3CheckBox1.IsChecked = true;
            Chanel4CheckBox1.IsChecked = true;
            Chanel5CheckBox1.IsChecked = true;
            Chanel6CheckBox1.IsChecked = true;
            this.SetChanelVisibleCheckBoxClick(sender, e);
        }

        /// <summary>
        /// The select all chanel button click 2.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void SelectAllChanelButtonClick2(object sender, RoutedEventArgs e)
        {
            this.Chanel1CheckBox2.IsChecked = true;
            this.Chanel2CheckBox2.IsChecked = true;
            this.Chanel3CheckBox2.IsChecked = true;
            this.Chanel4CheckBox2.IsChecked = true;
            this.Chanel5CheckBox2.IsChecked = true;
            this.Chanel6CheckBox2.IsChecked = true;
            this.SetChanelVisibleCheckBoxClick(sender, e);
        }

        /// <summary>  Сбросить все каналы
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void DeselectAllChanelButtonClick1(object sender, RoutedEventArgs e)
        {
            this.Chanel1CheckBox1.IsChecked = false;
            this.Chanel2CheckBox1.IsChecked = false;
            this.Chanel3CheckBox1.IsChecked = false;
            this.Chanel4CheckBox1.IsChecked = false;
            this.Chanel5CheckBox1.IsChecked = false;
            this.Chanel6CheckBox1.IsChecked = false;
            this.SetChanelVisibleCheckBoxClick(sender, e);
        }

        /// <summary>
        /// The deselect all chanel button click 2.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void DeselectAllChanelButtonClick2(object sender, RoutedEventArgs e)
        {
            this.Chanel1CheckBox2.IsChecked = false;
            this.Chanel2CheckBox2.IsChecked = false;
            this.Chanel3CheckBox2.IsChecked = false;
            this.Chanel4CheckBox2.IsChecked = false;
            this.Chanel5CheckBox2.IsChecked = false;
            this.Chanel6CheckBox2.IsChecked = false;
            this.SetChanelVisibleCheckBoxClick(sender, e);
        }

        /// <summary>  Инвертировать выбор каналов
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void InvertChanelButtonClick1(object sender, RoutedEventArgs e)
        {
            this.Chanel1CheckBox1.IsChecked = !Chanel1CheckBox1.IsChecked;
            this.Chanel2CheckBox1.IsChecked = !Chanel2CheckBox1.IsChecked;
            this.Chanel3CheckBox1.IsChecked = !Chanel3CheckBox1.IsChecked;
            this.Chanel4CheckBox1.IsChecked = !Chanel4CheckBox1.IsChecked;
            this.Chanel5CheckBox1.IsChecked = !Chanel5CheckBox1.IsChecked;
            this.Chanel6CheckBox1.IsChecked = !Chanel6CheckBox1.IsChecked;
            this.SetChanelVisibleCheckBoxClick(sender, e);
        }

        /// <summary>
        /// The invert chanel button click 2.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void InvertChanelButtonClick2(object sender, RoutedEventArgs e)
        {
            this.Chanel1CheckBox2.IsChecked = !Chanel1CheckBox2.IsChecked;
            this.Chanel2CheckBox2.IsChecked = !Chanel2CheckBox2.IsChecked;
            this.Chanel3CheckBox2.IsChecked = !Chanel3CheckBox2.IsChecked;
            this.Chanel4CheckBox2.IsChecked = !Chanel4CheckBox2.IsChecked;
            this.Chanel5CheckBox2.IsChecked = !Chanel5CheckBox2.IsChecked;
            this.Chanel6CheckBox2.IsChecked = !Chanel6CheckBox2.IsChecked;
            this.SetChanelVisibleCheckBoxClick(sender, e);
        }

        /// <summary>  Поиск устройств
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void FindDeviceButtonClick(object sender, RoutedEventArgs e)
        {
            this.FillComPortComboBox();
            var finddevice = new FindDevice();
            this.availableDevices = finddevice.Run(this, InfoMessageStatusBarItem);
            if (this.availableDevices.Count == 0)
            {
                this.InfoMessageStatusBarItem.Content = "Устройства не обнаружены!";
                return;
            }

            this.InfoMessageStatusBarItem.Content = $"Обнаружено устройств: {availableDevices.Count}";

            this.FindedDeviceComboBox.Items.Clear();
            foreach (var deviceLabel in this.availableDevices.Select(device => $"{device.Port}:{device.Name}"))
            {
                this.DebugMessageListBox.Items.Add(deviceLabel);
                this.FindedDeviceComboBox.Items.Add(deviceLabel);
            }

            this.FindedDeviceComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// The to hex str.
        /// </summary>
        /// <param name="packetData">
        /// The packet data.
        /// </param>
        /// <param name="packetLength">
        /// The packet length.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private string ToHexStr(byte[] packetData, int packetLength)
        {
            var line = new byte[1];
            var outHex = string.Empty;
            for (var i = 0; i < packetLength; i++)
            {
                line[0] = packetData[i];
                outHex += BitConverter.ToString(line) + " ";
            }

            return outHex;
        }

        /// <summary>
        /// The finded device combo box selection changed.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void FindedDeviceComboBoxSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.StartButton == null || this.FindedDeviceComboBox == null) return;

            this.StartButton.IsEnabled = this.FindedDeviceComboBox.SelectedIndex >= 0;
            if (this.FindedDeviceComboBox.SelectedValue == null)
            {
                this.NoSelectedDiveceInit();
                return;
            }

            var selectedValue = this.FindedDeviceComboBox.SelectedValue.ToString();
            if (selectedValue.Contains("SpbUnit")) this.InitSpbUnitScreen();
            if (selectedValue.Contains("PointR")) this.InitPointRScreen();
        }

        /// <summary>
        /// The refresh comm command button click.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void RefreshCommCommandButtonClick(object sender, RoutedEventArgs e)
        {
            this.FillComPortComboBox();
        }

        /// <summary>
        /// Послать команду на устройство
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void SendCommandButtonClick(object sender, RoutedEventArgs e)
        {
            var commandCod = this.GetCommandCod();
            var port = this.ComPortComboBox.Text;
            try
            {
                this.measurement.Port = new SerialPort(port, 115200, Parity.None, 8, StopBits.One) { ReadTimeout = 1000 };
                this.measurement.Port.Open();
                this.measurement.Port.DiscardInBuffer();
                this.measurement.Port.DiscardOutBuffer();
                string outHex;
                if (commandCod == MeasurementClass.CommandCod.SetDac)
                {
                    outHex = this.measurement.SendCommand(commandCod, 1, 1.22f);
                    outHex += " ";
                    outHex += this.measurement.SendCommand(commandCod, 2, 1.22f);
                }
                else
                    outHex = this.measurement.SendCommand(commandCod);

                DebugMessageListBox.Items.Add($"Отправка команды на {port}: {outHex}");

                switch (commandCod)
                {
                    case MeasurementClass.CommandCod.TestPointR:
                    {
                        var signatura = this.measurement.Port.ReadLine().Replace("\r", string.Empty).Replace("\n", string.Empty);
                        this.DebugMessageListBox.Items.Add(signatura);
                        break;
                    }

                    case MeasurementClass.CommandCod.GetDataPointR:
                    {
                        var first = this.measurement.Port.ReadLine().Replace("\r", string.Empty).Replace("\n", string.Empty);
                        this.DebugMessageListBox.Items.Add(first);
                        for (var i = 0; i < 11; i++)
                        {
                            var dataLine = this.measurement.Port.ReadLine().Replace("\r", string.Empty).Replace("\n", string.Empty);
                            this.DebugMessageListBox.Items.Add(dataLine);
                        }

                        var last = this.measurement.Port.ReadLine().Replace("\r", string.Empty).Replace("\n", string.Empty);
                        this.DebugMessageListBox.Items.Add(last);
                        break;
                    }

                    case MeasurementClass.CommandCod.GetDumpPointR:
                    {
                        var first = this.measurement.Port.ReadLine().Replace("\r", string.Empty).Replace("\n", string.Empty);
                        this.DebugMessageListBox.Items.Add(first);
                        for (var i = 0; i < 1024; i++)
                        {
                            var dataLine = this.measurement.Port.ReadLine().Replace("\r", string.Empty).Replace("\n", string.Empty);
                            this.DebugMessageListBox.Items.Add(dataLine);
                        }

                        var last = this.measurement.Port.ReadLine().Replace("\r", string.Empty).Replace("\n", string.Empty);
                        this.DebugMessageListBox.Items.Add(last);
                        break;
                    }

                    default:
                    {
                        var data = new byte[1024];
                        var offset = 0;
                        try
                        {
                            while (true)
                            {
                                var readResult = this.measurement.Port.Read(data, offset, 1);
                                offset += readResult;
                                if (offset >= 512) break;
                            }
                        }
                        catch (Exception)
                        {
                            // ignore
                        }

                        DebugMessageListBox.Items.Add("Считано: " + offset + " байт HEX Dump: " + this.ToHexStr(data, offset));
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                this.DebugMessageListBox.Items.Add($"Ошибка: {DateTime.Now:HH:mm:ss} {ex.Message}");
                this.LogListBox.Items.Add("*****************************************************************************");
                this.LogListBox.Items.Add($"Ошибка: {DateTime.Now:HH:mm:ss} {ex.Message}");
                this.LogListBox.Items.Add($"{ex.StackTrace}");
            }
            finally
            {
                this.measurement.Port.Close();
                this.measurement.Port = null;
            }
        }

        /// <summary>
        /// Сохранить в текстовой файл
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void SaveDebugMessageButtonClick(object sender, RoutedEventArgs e)
        {
            var fileDialog = new SaveFileDialog { Filter = TxtFilter, FilterIndex = 2, RestoreDirectory = true };
            if (fileDialog.ShowDialog() != true)
            {
                return;
            }

            var writer = new StreamWriter(fileDialog.FileName, false, Enc1251);
            foreach (var item in DebugMessageListBox.Items)
            {
                writer.WriteLine(item.ToString());
            }

            writer.Close();
            writer.Dispose();
        }

        /// <summary>
        /// Отчистить отладочное окно
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void ClearDebugMessageButtonClick(object sender, RoutedEventArgs e)
        {
            this.DebugMessageListBox.Items.Clear();
        }

        /// <summary>
        /// Сохранить в текстовой файл
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void SaveLogButtonClick(object sender, RoutedEventArgs e)
        {
            var fileDialog = new SaveFileDialog { Filter = TxtFilter, FilterIndex = 2, RestoreDirectory = true };
            if (fileDialog.ShowDialog() != true)
            {
                return;
            }

            var writer = new StreamWriter(fileDialog.FileName, false, Enc1251);
            foreach (var item in LogListBox.Items)
            {
                writer.WriteLine(item.ToString());
            }

            writer.Close();
            writer.Dispose();
        }

        /// <summary>
        /// Отчистить отладочное окно
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void ClearLogButtonClick(object sender, RoutedEventArgs e)
        {
            this.LogListBox.Items.Clear();
        }

        /// <summary>
        /// The fill com port combo box.
        /// </summary>
        private void FillComPortComboBox()
        {
            var portnames = SerialPort.GetPortNames();
            if (portnames.Length == 0)
            {
                this.InfoMessageStatusBarItem.Content = "Не обнаружено COM-портов!";
                return;
            }

            this.ComPortComboBox.Items.Clear();
            foreach (var port in portnames)
            {
                this.ComPortComboBox.Items.Add(port);
            }

            this.ComPortComboBox.SelectedIndex = 0;
            this.FindDeviceButton.IsEnabled = portnames.Length > 0;
            this.FindedDeviceComboBoxSelectionChanged(null, null);
        }

        /// <summary>
        /// Загрузка формы
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            this.FindDeviceButtonClick(null, null);
            this.FillComPortComboBox();
        }

        /// <summary>
        /// The worker do work.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void WorkerDoWork(object sender, DoWorkEventArgs e)
        {
            var psrd = (PositionServiceReadOptions)e.Argument;
            var data = new byte[128];
            var offset = 0;
            ulong tcpPacketCount = 0;

            while (this.readTcp)
            {
                offset += this.netStreamPositionService.Read(data, offset, 1);
                if (offset > 0)
                {
                    if ((data[offset - 1] == 0xA && data[offset - 2] == 0xD) || (data[offset - 1] == 0xD && data[offset - 2] == 0xA))
                    {
                        var message = Encoding.UTF8.GetString(data, 0, offset).Replace("\n", string.Empty).Replace("\r", string.Empty);
                        this.synchronizationContext.Post(status => DebugMessageListBox.Items.Add(status), message);
                        offset = 0;
                        tcpPacketCount++;
                        Thread.Sleep(psrd.Timeout);
                    }
                }

                if (offset == data.Length) offset = 0;
            }
            
            this.netStreamPositionService.Close();
            this.netStreamPositionService.Dispose();
            this.tcpClientPositionService.Close();
            e.Result = tcpPacketCount;
        }

        /// <summary>
        /// The worker progress changed.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void WorkerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
        }

        /// <summary>
        /// The worker run worker completed.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void WorkerRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show("Recive tcp pakets: " + e.Result);
        }

        /// <summary>
        /// The open tcp command button click.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void OpenTcpCommandButtonClick(object sender, RoutedEventArgs e)
        {
            this.readTcp = true;
            try
            {
                this.tcpClientPositionService = new TcpClient();
                this.tcpClientPositionService.Connect("127.0.0.1", 31253);
                this.netStreamPositionService = this.tcpClientPositionService.GetStream();
                this.tcpWorkerPositionService = new BackgroundWorker();
                this.tcpWorkerPositionService.DoWork += this.WorkerDoWork;
                this.tcpWorkerPositionService.ProgressChanged += this.WorkerProgressChanged;
                this.tcpWorkerPositionService.RunWorkerCompleted += this.WorkerRunWorkerCompleted;
                PositionServiceReadOptions psrd;
                psrd.Timeout = 100;
                this.tcpWorkerPositionService.RunWorkerAsync(psrd);
                this.CloseTCPCommandButton.IsEnabled = false;
                this.CloseTCPCommandButton.IsEnabled = true;
            }
            catch (Exception exception)
            {
                this.DebugMessageListBox.Items.Add(exception.Message);
            }
        }

        /// <summary>
        /// The close tcp command button click.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void CloseTcpCommandButtonClick(object sender, RoutedEventArgs e)
        {
            this.readTcp = false;
            this.CloseTCPCommandButton.IsEnabled = true;
            this.CloseTCPCommandButton.IsEnabled = false;
        }

        /// <summary>
        /// The point r view mode radio button click.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void PointRViewModeRadioButtonClick(object sender, RoutedEventArgs e)
        {
            this.InitPointRScreen();
        }

        /// <summary>
        /// The receiver.
        /// </summary>
        private void Receiver()
        {
            try
            {
                var receivingUdpClient = new UdpClient(UdpLocalPort); // Создаем UdpClient для чтения входящих данных
                IPEndPoint remoteIpEndPoint = null;
                while (true)
                {
                    var receiveBytes = receivingUdpClient.Receive(ref remoteIpEndPoint); // Ожидание дейтаграммы
                    
                    var result = new double[receiveBytes.Length / sizeof(double)];
                    Buffer.BlockCopy(receiveBytes, 0, result, 0, receiveBytes.Length);
                    for (var i = 0; i < result.Length; i++)
                    { 
                        this.NewMessage($" {i} --> {result[i]}", MeasurementClass.MessageType.Log);
                    }
                }
            }
            catch (Exception ex)
            {
                this.NewMessage("Возникло исключение: " + ex + "\n  " + ex.Message, MeasurementClass.MessageType.Log);
            }
        }

        /// <summary>
        /// The udp server button click.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void UdpServerButtonClick(object sender, RoutedEventArgs e)
        {
            var listener = new Thread(this.Receiver);
            listener.Start();
        }

        /// <summary>
        /// The udp send button click.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void UdpSendButtonClick(object sender, RoutedEventArgs e)
        {
            var udpSender = new UdpClient(); // Создаем UdpClient
            var endPoint = new IPEndPoint(this.remoteIpAddress, UdpRemotePort); // Создаем endPoint по информации об удаленном хосте

            try
            {
                var datagram = new double[6];
                for (var i = 0; i < 6; i++) datagram[i] = i + 0.2;
                var result = new byte[datagram.Length * sizeof(double)];
                Buffer.BlockCopy(datagram, 0, result, 0, result.Length);
                udpSender.Send(result, result.Length, endPoint); // Отправляем данные
            }
            catch (Exception ex)
            {
                this.LogListBox.Items.Add("Возникло исключение: " + ex + "\n  " + ex.Message);
            }
            finally
            {
                udpSender.Close(); // Закрыть соединение
            }
        }

        /// <summary>
        /// The position service read options.
        /// </summary>
        private struct PositionServiceReadOptions
        {
            /// <summary>
            /// The timeout.
            /// </summary>
            public int Timeout;
        }
    }
}