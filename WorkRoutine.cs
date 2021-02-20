// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WorkRoutine.cs" company="urb31075">
// All Right reserved
// </copyright>
// <summary>
//   Defines the MainWindow type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Komlog
{
    using System;
    using System.ComponentModel;
    using System.IO.Ports;
    using System.Linq;

    /// <summary>
    /// The main window.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Gets or sets the active device.
        /// </summary>
        public SupportDevice ActiveDevice { get; set; }

        /// <summary>
        /// The no selected divece init.
        /// </summary>
        private void NoSelectedDiveceInit()
        {
            this.Chanel1CheckBox1.Visibility = System.Windows.Visibility.Hidden;
            this.Chanel2CheckBox1.Visibility = System.Windows.Visibility.Hidden;
            this.Chanel3CheckBox1.Visibility = System.Windows.Visibility.Hidden;
            this.Chanel4CheckBox1.Visibility = System.Windows.Visibility.Hidden;
            this.Chanel5CheckBox1.Visibility = System.Windows.Visibility.Hidden;
            this.Chanel6CheckBox1.Visibility = System.Windows.Visibility.Hidden;
            this.Chanel1CheckBox2.Visibility = System.Windows.Visibility.Hidden;
            this.Chanel2CheckBox2.Visibility = System.Windows.Visibility.Hidden;
            this.Chanel3CheckBox2.Visibility = System.Windows.Visibility.Hidden;
            this.Chanel4CheckBox2.Visibility = System.Windows.Visibility.Hidden;
            this.Chanel5CheckBox2.Visibility = System.Windows.Visibility.Hidden;
            this.Chanel6CheckBox2.Visibility = System.Windows.Visibility.Hidden;

            this.GaugeTextBox1.Visibility = System.Windows.Visibility.Hidden;
            this.GaugeTextBox2.Visibility = System.Windows.Visibility.Hidden;
            this.GaugeTextBox3.Visibility = System.Windows.Visibility.Hidden;
            this.GaugeTextBox4.Visibility = System.Windows.Visibility.Hidden;
            this.GaugeTextBox5.Visibility = System.Windows.Visibility.Hidden;
            this.GaugeTextBox6.Visibility = System.Windows.Visibility.Hidden;
            this.HeatIndicatorControl1.Visibility = System.Windows.Visibility.Hidden;
            this.HeatIndicatorControl2.Visibility = System.Windows.Visibility.Hidden;
            this.HeatIndicatorControl3.Visibility = System.Windows.Visibility.Hidden;
            this.HeatIndicatorControl4.Visibility = System.Windows.Visibility.Hidden;
            this.HeatIndicatorControl5.Visibility = System.Windows.Visibility.Hidden;
            this.HeatIndicatorControl6.Visibility = System.Windows.Visibility.Hidden;
        }

        /// <summary>
        /// The port data received.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Argument OutOfRangeException
        /// </exception>
        private void PortDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            switch (this.ActiveDevice)
            {
                case SupportDevice.PointR: this.measurement.PointRMeasurementLoop();
                    break;
                case SupportDevice.SpbUnit: this.measurement.SpbUnitMeasurementLoop();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// The start work.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Argument OutOfRangeException
        /// </exception>
        private void StartWork()
        {
            this.experimentDateTime = DateTime.Now;
            this.ClearDataButtonClick(null, null);
            this.FindedDeviceComboBox.IsEnabled = false;
            this.FindDeviceButton.IsEnabled = false;
            this.SetChanelVisibleCheckBoxClick(null, null);

            if (this.measurement.Port == null)
            {
                var selectDevice = this.availableDevices.First(device => FindedDeviceComboBox.Text == $"{device.Port}:{device.Name}");

                this.measurement.PortName = selectDevice.Port;
                this.measurement.Port = new SerialPort(selectDevice.Port, 115200, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 3000,
                    ReadBufferSize = 16 * 1024
                };

                this.measurement.Port.Open();
            }

            this.KomlogStateIndicatorControl.StateIndex = 1;
            this.LogListBox.Items.Add("Start " + DateTime.Now.ToString("HH:mm:ss"));

            bool initResult;
            switch (this.ActiveDevice)
            {
                case SupportDevice.PointR:
                    var vmode = MeasurementClass.PointRViewMode.None;
                    if (this.RAWRadioButton.IsChecked != null && (bool)this.RAWRadioButton.IsChecked) vmode = MeasurementClass.PointRViewMode.Raw;
                    if (this.AFRadioButton.IsChecked != null && (bool)this.AFRadioButton.IsChecked) vmode = MeasurementClass.PointRViewMode.AmplFasa;
                    if (this.NNRadioButton.IsChecked != null && (bool)this.NNRadioButton.IsChecked) vmode = MeasurementClass.PointRViewMode.NeuralNet;
                    if (this.PlanBRadioButton.IsChecked != null && (bool)this.PlanBRadioButton.IsChecked) vmode = MeasurementClass.PointRViewMode.PlanB;

                    switch (vmode)
                    {
                        case MeasurementClass.PointRViewMode.None:
                            this.chanelAmount = 0;
                            break;
                        case MeasurementClass.PointRViewMode.Raw:
                            this.chanelAmount = 4;
                            break;
                        case MeasurementClass.PointRViewMode.AmplFasa:
                            this.chanelAmount = 4;
                            break;
                        case MeasurementClass.PointRViewMode.NeuralNet:
                        case MeasurementClass.PointRViewMode.PlanB:
                            this.chanelAmount = 6;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    this.chanelPoint = new ChanelPoint[this.chanelAmount];
                    for (var chanel = 0; chanel < this.chanelAmount; chanel++)
                    {
                        this.chanelPoint[chanel] = new ChanelPoint();
                        this.KomlogValue1XYDiagram2D.Series[chanel].Points.Clear();
                        this.KomlogValue2XYDiagram2D.Series[chanel].Points.Clear();
                    }

                    var smode = MeasurementClass.PointRStateMashine.None;
                    if (this.TrainingScriptRadioButton.IsChecked != null && (bool)this.TrainingScriptRadioButton.IsChecked)
                    {
                        smode = MeasurementClass.PointRStateMashine.Training;
                    }

                    if (this.LandingScriptRadioButton.IsChecked != null && (bool)this.LandingScriptRadioButton.IsChecked)
                    {
                        smode = MeasurementClass.PointRStateMashine.Landing;
                    }

                    initResult = this.measurement.PointRInitMeasurementInit(vmode, smode);
                    break;
                case SupportDevice.SpbUnit:
                    chanelAmount = 3;
                    chanelPoint = new ChanelPoint[chanelAmount];
                    for (var chanel = 0; chanel < chanelAmount; chanel++)
                    {
                        chanelPoint[chanel] = new ChanelPoint();
                        KomlogValue1XYDiagram2D.Series[chanel].Points.Clear();
                        KomlogValue2XYDiagram2D.Series[chanel].Points.Clear();
                    }

                    initResult = this.measurement.SpbUnitMeasurementInit();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (initResult == false)
            {
                this.StateStatusBarItem.Content = "Ошибка инициализации блока измерений!";
                return;
            }

            visualisationCount = 0;
            this.backgroundVisualisation = new BackgroundWorker();
            this.backgroundVisualisation.DoWork += this.VisualisationDoWork; // Set up the Background Worker Events
            this.backgroundVisualisation.RunWorkerCompleted += this.VisualisationRunWorkerCompleted;
            this.backgroundVisualisation.RunWorkerAsync();

            if (this.ActiveDevice == SupportDevice.PointR)
            {
                this.measurement.Port.DataReceived += this.PortDataReceived;
            }
        }

        /// <summary>
        /// The stop work.
        /// </summary>
        private void StopWork()
        {
            this.LogListBox.Items.Add("Stop " + DateTime.Now.ToString("HH:mm:ss"));
            this.KomlogStateIndicatorControl.StateIndex = 0;
            this.measurement.StopFlag = true;
            this.backgroundVisualisation.Dispose();
        }
    }
}
