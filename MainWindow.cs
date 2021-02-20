// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MainWindow.cs" company="URB">
// All Right Reserved
// </copyright>
// <summary>
// Функции измерения
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Komlog
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Text;
    using System.Threading;
    using System.Windows.Threading;
    using DevExpress.Xpf.Charts;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Используемая кодировка файлов 1251
        /// </summary>
        public static readonly Encoding Enc1251 = Encoding.GetEncoding(1251);

        /// <summary>
        /// Txt - фильтр
        /// </summary>
        private const string TxtFilter = "All files (*.*)|*.*|Txt files (*.txt)|*.txt";

        /// <summary>
        /// Контекст синхронизации с UI
        /// </summary>
        private readonly SynchronizationContext synchronizationContext;

        /// <summary>
        /// Измерение данных
        /// </summary>
        private readonly MeasurementClass measurement = new MeasurementClass();

        /// <summary>
        /// Количество каналов измепрений
        /// </summary>
        private int chanelAmount;

        /// <summary>
        /// Буфер данных по каналам
        /// </summary>
        private ChanelPoint[] chanelPoint;

        /// <summary>
        /// Фоновая работа
        /// </summary>
        private BackgroundWorker backgroundVisualisation;

        /// <summary>
        /// The visualisation count.
        /// </summary>
        private int visualisationCount;

        /// <summary>
        /// The support device.
        /// </summary>
        public enum SupportDevice : byte
        {
            /// <summary>
            /// The point r.
            /// </summary>
            PointR = 1,

            /// <summary>
            /// The spb unit.
            /// </summary>
            SpbUnit = 2
        }

        /// <summary>
        /// The init spb unit screen.
        /// </summary>
        private void InitSpbUnitScreen()
        {
            this.ActiveDevice = SupportDevice.SpbUnit;
            this.RAWRadioButton.Visibility = System.Windows.Visibility.Hidden;
            this.AFRadioButton.Visibility = System.Windows.Visibility.Hidden;
            this.NNRadioButton.Visibility = System.Windows.Visibility.Hidden;
            
            this.Chanel1CheckBox1.Visibility = System.Windows.Visibility.Visible;
            this.Chanel2CheckBox1.Visibility = System.Windows.Visibility.Visible;
            this.Chanel3CheckBox1.Visibility = System.Windows.Visibility.Visible;
            this.Chanel4CheckBox1.Visibility = System.Windows.Visibility.Hidden;
            this.Chanel5CheckBox1.Visibility = System.Windows.Visibility.Hidden;
            this.Chanel6CheckBox1.Visibility = System.Windows.Visibility.Hidden;
            
            this.Chanel1CheckBox2.Visibility = System.Windows.Visibility.Visible;
            this.Chanel2CheckBox2.Visibility = System.Windows.Visibility.Visible;
            this.Chanel3CheckBox2.Visibility = System.Windows.Visibility.Visible;
            this.Chanel4CheckBox2.Visibility = System.Windows.Visibility.Hidden;
            this.Chanel5CheckBox2.Visibility = System.Windows.Visibility.Hidden;
            this.Chanel6CheckBox2.Visibility = System.Windows.Visibility.Hidden;

            this.GaugeTextBox1.Visibility = System.Windows.Visibility.Visible;
            this.GaugeTextBox2.Visibility = System.Windows.Visibility.Visible;
            this.GaugeTextBox3.Visibility = System.Windows.Visibility.Visible;
            this.GaugeTextBox4.Visibility = System.Windows.Visibility.Hidden;
            this.GaugeTextBox5.Visibility = System.Windows.Visibility.Hidden;
            this.GaugeTextBox6.Visibility = System.Windows.Visibility.Hidden;
            this.HeatIndicatorControl1.Visibility = System.Windows.Visibility.Visible;
            this.HeatIndicatorControl2.Visibility = System.Windows.Visibility.Visible;
            this.HeatIndicatorControl3.Visibility = System.Windows.Visibility.Visible;
            this.HeatIndicatorControl4.Visibility = System.Windows.Visibility.Hidden;
            this.HeatIndicatorControl5.Visibility = System.Windows.Visibility.Hidden;
            this.HeatIndicatorControl6.Visibility = System.Windows.Visibility.Hidden;

            this.Chanel1CheckBox1.Content = "Питание 1";
            this.Chanel2CheckBox1.Content = "Нагреватель 1";
            this.Chanel3CheckBox1.Content = "Датчик 1";
            this.Chanel4CheckBox1.Content = string.Empty;
            this.Chanel5CheckBox1.Content = string.Empty;
            this.Chanel6CheckBox1.Content = string.Empty;

            this.Chanel1CheckBox2.Content = "Питание 2";
            this.Chanel2CheckBox2.Content = "Нагреватель 2";
            this.Chanel3CheckBox2.Content = "Датчик 2";
            this.Chanel4CheckBox2.Content = string.Empty;
            this.Chanel5CheckBox2.Content = string.Empty;
            this.Chanel6CheckBox2.Content = string.Empty;
        }

        /// <summary>
        /// The init point r screen.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Argument OutOfRangeException
        /// </exception>
        private void InitPointRScreen()
        {
            this.ActiveDevice = SupportDevice.PointR;
            this.RAWRadioButton.Visibility = System.Windows.Visibility.Visible;
            this.AFRadioButton.Visibility = System.Windows.Visibility.Visible;
            this.NNRadioButton.Visibility = System.Windows.Visibility.Visible;
            var mode = MeasurementClass.PointRViewMode.None;
            if (this.RAWRadioButton.IsChecked != null && (bool)this.RAWRadioButton.IsChecked) mode = MeasurementClass.PointRViewMode.Raw;
            if (this.AFRadioButton.IsChecked != null && (bool)this.AFRadioButton.IsChecked) mode = MeasurementClass.PointRViewMode.AmplFasa;
            if (this.NNRadioButton.IsChecked != null && (bool)this.NNRadioButton.IsChecked) mode = MeasurementClass.PointRViewMode.NeuralNet;
            if (this.PlanBRadioButton.IsChecked != null && (bool)this.PlanBRadioButton.IsChecked) mode = MeasurementClass.PointRViewMode.PlanB;

            switch (mode)
            {
                case MeasurementClass.PointRViewMode.None:
                    throw new ArgumentOutOfRangeException();
                case MeasurementClass.PointRViewMode.Raw:
                case MeasurementClass.PointRViewMode.AmplFasa:
                    this.Chanel1CheckBox1.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel2CheckBox1.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel3CheckBox1.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel4CheckBox1.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel5CheckBox1.Visibility = System.Windows.Visibility.Hidden;
                    this.Chanel6CheckBox1.Visibility = System.Windows.Visibility.Hidden;

                    this.Chanel1CheckBox2.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel2CheckBox2.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel3CheckBox2.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel4CheckBox2.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel5CheckBox2.Visibility = System.Windows.Visibility.Hidden;
                    this.Chanel6CheckBox2.Visibility = System.Windows.Visibility.Hidden;

                    this.GaugeTextBox1.Visibility = System.Windows.Visibility.Visible;
                    this.GaugeTextBox2.Visibility = System.Windows.Visibility.Visible;
                    this.GaugeTextBox3.Visibility = System.Windows.Visibility.Visible;
                    this.GaugeTextBox4.Visibility = System.Windows.Visibility.Visible;
                    this.GaugeTextBox5.Visibility = System.Windows.Visibility.Hidden;
                    this.GaugeTextBox6.Visibility = System.Windows.Visibility.Hidden;
                    
                    this.HeatIndicatorControl1.Visibility = System.Windows.Visibility.Visible;
                    this.HeatIndicatorControl2.Visibility = System.Windows.Visibility.Visible;
                    this.HeatIndicatorControl3.Visibility = System.Windows.Visibility.Visible;
                    this.HeatIndicatorControl4.Visibility = System.Windows.Visibility.Visible;
                    this.HeatIndicatorControl5.Visibility = System.Windows.Visibility.Hidden;
                    this.HeatIndicatorControl6.Visibility = System.Windows.Visibility.Hidden;
                    
                    this.Chanel1CheckBox1.Content = "Канал 1 X";
                    this.Chanel2CheckBox1.Content = "Канал 2 Z";
                    this.Chanel3CheckBox1.Content = "Канал 3 Y";
                    this.Chanel4CheckBox1.Content = "Синхронизация";
                    this.Chanel5CheckBox1.Content = string.Empty;
                    this.Chanel6CheckBox1.Content = string.Empty;
                    
                    this.Chanel1CheckBox2.Content = "Канал 1 Alfa";
                    this.Chanel2CheckBox2.Content = "Канал 2 Betta";
                    this.Chanel3CheckBox2.Content = "Канал 3 Gamma";
                    this.Chanel4CheckBox2.Content = "Синхронизация";
                    this.Chanel5CheckBox2.Content = string.Empty;
                    this.Chanel6CheckBox2.Content = string.Empty;

                    break;
                case MeasurementClass.PointRViewMode.NeuralNet:
                case MeasurementClass.PointRViewMode.PlanB:

                    this.Chanel1CheckBox1.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel2CheckBox1.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel3CheckBox1.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel4CheckBox1.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel5CheckBox1.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel6CheckBox1.Visibility = System.Windows.Visibility.Visible;
                    
                    this.Chanel1CheckBox2.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel2CheckBox2.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel3CheckBox2.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel4CheckBox2.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel5CheckBox2.Visibility = System.Windows.Visibility.Visible;
                    this.Chanel6CheckBox2.Visibility = System.Windows.Visibility.Visible;
                    
                    this.GaugeTextBox1.Visibility = System.Windows.Visibility.Visible;
                    this.GaugeTextBox2.Visibility = System.Windows.Visibility.Visible;
                    this.GaugeTextBox3.Visibility = System.Windows.Visibility.Visible;
                    this.GaugeTextBox4.Visibility = System.Windows.Visibility.Visible;
                    this.GaugeTextBox5.Visibility = System.Windows.Visibility.Visible;
                    this.GaugeTextBox6.Visibility = System.Windows.Visibility.Visible;
                    this.HeatIndicatorControl1.Visibility = System.Windows.Visibility.Visible;
                    this.HeatIndicatorControl2.Visibility = System.Windows.Visibility.Visible;
                    this.HeatIndicatorControl3.Visibility = System.Windows.Visibility.Visible;
                    this.HeatIndicatorControl4.Visibility = System.Windows.Visibility.Visible;
                    this.HeatIndicatorControl5.Visibility = System.Windows.Visibility.Visible;
                    this.HeatIndicatorControl6.Visibility = System.Windows.Visibility.Visible;
                    
                    this.Chanel1CheckBox1.Content = "Смещение (изм.)";
                    this.Chanel2CheckBox1.Content = "Высота (изм.)";
                    this.Chanel3CheckBox1.Content = "Дальность (изм.)";
                    this.Chanel4CheckBox1.Content = "Смещение (расч. NN)";
                    this.Chanel5CheckBox1.Content = "Высота (расч. NN)";
                    this.Chanel6CheckBox1.Content = "Дальность (расч. NN)";
                    
                    this.Chanel1CheckBox2.Content = "Alfa (изм.)";
                    this.Chanel2CheckBox2.Content = "Betta (изм.)";
                    this.Chanel3CheckBox2.Content = "Gamma (изм.)";
                    this.Chanel4CheckBox2.Content = "Alfa (расч. NN)";
                    this.Chanel5CheckBox2.Content = "Betta (расч. NN)";
                    this.Chanel6CheckBox2.Content = "Gamma (расч. NN)";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Событие появления нового сообщения
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="messageType">
        /// The message type.
        /// </param>
        private void NewMessage(string message, MeasurementClass.MessageType messageType)
        {
            switch (messageType)
            {
                case MeasurementClass.MessageType.StopMeasurementMessage:
                    this.synchronizationContext.Post(status => this.StopButtonClick(null, null), message);
                    break;
                case MeasurementClass.MessageType.InfoMessage:
                    this.synchronizationContext.Post(status => this.InfoMessageStatusBarItem.Content = status, message);
                    break;
                case MeasurementClass.MessageType.MeasureAmountMessage:
                    this.synchronizationContext.Post(status => this.MeasureAmountStatusBarItem.Content = status, message);
                    break;
                case MeasurementClass.MessageType.ErorrAmountMessage:
                    this.synchronizationContext.Post(status => this.ErrorAmountStatusBarItem.Content = status, message);
                    break;
                case MeasurementClass.MessageType.StateMessage:
                    this.synchronizationContext.Post(status => this.StateStatusBarItem.Content = status, message);
                    break;
                case MeasurementClass.MessageType.DebugMessage:
                    this.synchronizationContext.Post(status => this.DebugMessageListBox.Items.Add(status), message);
                    break;
                case MeasurementClass.MessageType.PositionStatistics:
                    var items = message.Split('|');
                    this.synchronizationContext.Post(
                        status =>
                            {
                                switch (items.Length)
                                {
                                    case 4:
                                        this.GaugeTextBox1.Text = items[0];
                                        this.GaugeTextBox2.Text = items[1];
                                        this.GaugeTextBox3.Text = items[2];
                                        this.GaugeTextBox4.Text = items[3];
                                        break;
                                    case 6:
                                        this.GaugeTextBox1.Text = items[0];
                                        this.GaugeTextBox2.Text = items[1];
                                        this.GaugeTextBox3.Text = items[2];
                                        this.GaugeTextBox4.Text = items[3];
                                        this.GaugeTextBox5.Text = items[4];
                                        this.GaugeTextBox6.Text = items[5];
                                        break;
                                }
                            },
                        message);
                    break;
                case MeasurementClass.MessageType.Log:
                    this.synchronizationContext.Post(status => this.LogListBox.Items.Add(status), message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(messageType), messageType, null);
            }
        }

        /// <summary>
        /// The point r new data.
        /// </summary>
        /// <param name="ampl">
        /// The ampl.
        /// </param>
        /// <param name="delta">
        /// The delta.
        /// </param>
        /// <param name="ordinata">
        /// The ordinata.
        /// </param>
        private void PointRNewData(double[] ampl, double[] delta, ulong ordinata)
        {
            for (var chanel = 0; chanel < this.chanelAmount; chanel++)
            {
                this.chanelPoint[chanel].Value1.Add(ampl[chanel]);
                this.chanelPoint[chanel].Value2.Add(delta[chanel]);
                this.chanelPoint[chanel].Ordinata.Add(ordinata);
            }
        }

        /// <summary>
        /// The point r neural net data.
        /// </summary>
        /// <param name="sensorPosition">
        /// The sensor position.
        /// </param>
        /// <param name="sensorAngle">
        /// The sensor angle.
        /// </param>
        /// <param name="neuralNetPosition">
        /// The neural net position.
        /// </param>
        /// <param name="neuralNetAngle">
        /// The neural net angle.
        /// </param>
        /// <param name="ordinata">
        /// The ordinata.
        /// </param>
        private void PointRNeuralNetData(double[] sensorPosition, double[] sensorAngle, double[] neuralNetPosition, double[] neuralNetAngle, ulong ordinata)
        {
            for (var chanel = 0; chanel < 3; chanel++)
            {
                this.chanelPoint[chanel].Value1.Add(sensorPosition[chanel]);
                this.chanelPoint[chanel + 3].Value1.Add(neuralNetPosition[chanel]);
                this.chanelPoint[chanel].Value2.Add(sensorAngle[chanel]);
                this.chanelPoint[chanel + 3].Value2.Add(neuralNetAngle[chanel]);
                this.chanelPoint[chanel].Ordinata.Add(ordinata);
                this.chanelPoint[chanel + 3].Ordinata.Add(ordinata);
            }
        }

        /// <summary>
        /// The spb unit new data.
        /// </summary>
        /// <param name="value1">
        /// The value 1.
        /// </param>
        /// <param name="value2">
        /// The value 2.
        /// </param>
        /// <param name="ordinata">
        /// The ordinata.
        /// </param>
        private void SpbUnitNewData(double[] value1, double[] value2, ulong ordinata)
        {
            for (var chanel = 0; chanel < 3; chanel++)
            {
                this.chanelPoint[chanel].Value1.Add(value1[chanel]);
                this.chanelPoint[chanel].Value2.Add(value2[chanel]);
                this.chanelPoint[chanel].Ordinata.Add(ordinata);
            }
        }

        /// <summary>
        /// Цикл отображения
        /// </summary>
        private void VisualisationLoop()
        {
            try
            {
                this.TimeStatusBarItem.Content = DateTime.Now.ToString("HH:mm:ss");
                this.StateStatusBarItem.Content = "Состояние: Измерение";
                var lastMeasurementCount = this.chanelPoint[0].Count() - 1; // Последнее измеренное значение
                while (this.visualisationCount < lastMeasurementCount)
                {
                    for (var chanel = 0; chanel < this.chanelAmount; chanel++)
                    {
                         var value1 = this.chanelPoint[chanel].Value1[this.visualisationCount];
                         var value2 = this.chanelPoint[chanel].Value2[this.visualisationCount];
                         var ordinata = this.chanelPoint[chanel].Ordinata[this.visualisationCount];
                         this.KomlogValue1XYDiagram2D.Series[chanel].Points.Add(new SeriesPoint(ordinata, value1));
                         this.KomlogValue2XYDiagram2D.Series[chanel].Points.Add(new SeriesPoint(ordinata, value2));
                    }

                    this.visualisationCount++;
                }
            }
            catch (Exception ex)
            {
                this.LogListBox.Items.Add(ex.Message);
            }
        }

        /// <summary>
        /// Получение номера команды
        /// </summary>
        /// <returns>
        /// Возврат номера команды
        /// </returns>
        private MeasurementClass.CommandCod GetCommandCod()
        {
            if (this.EmptyCommandRadioButton.IsChecked == true) return MeasurementClass.CommandCod.EmptyCommand;
            if (this.GetVersionCommandRadioButton.IsChecked == true) return MeasurementClass.CommandCod.GetVersion;
            if (this.GetSingleDataRadioButton.IsChecked == true) return MeasurementClass.CommandCod.GetSingleData;
            if (this.StartDataStreamRadioButton.IsChecked == true) return MeasurementClass.CommandCod.StartDataStream;
            if (this.StopDataStreamRadioButton.IsChecked == true) return MeasurementClass.CommandCod.StopDataStream;
            if (this.SetKeyRadioButton.IsChecked == true) return MeasurementClass.CommandCod.SetKey;
            if (this.GetDacRadioButton.IsChecked == true) return MeasurementClass.CommandCod.GetDac;
            if (this.SetDacRadioButton.IsChecked == true) return MeasurementClass.CommandCod.SetDac;
            if (this.TestPointRRadioButton.IsChecked == true) return MeasurementClass.CommandCod.TestPointR;
            if (this.GetDumpPointRRadioButton.IsChecked == true) return MeasurementClass.CommandCod.GetDumpPointR;
            if (this.GetDataPointRRadioButton.IsChecked == true) return MeasurementClass.CommandCod.GetDataPointR;
            return 0;
        }

        /// <summary>
        /// Цикл визуализации результатов измерений
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void VisualisationDoWork(object sender, DoWorkEventArgs e)
        {
            while (!this.measurement.StopFlag)
            {
                Thread.Sleep(1000);
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new ThreadStart(this.VisualisationLoop)); // Для синхронизации с UI
            }
        }

        /// <summary>
        /// Завершение работы
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void VisualisationRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                this.StateStatusBarItem.Content = "Состояние: Прервано";
            }
            else if (e.Error != null)
            {
                this.StateStatusBarItem.Content = "Состояние: Получено прерывание";
            }
            else
            {
                this.StateStatusBarItem.Content = "Состояние: Выполнено";
            }
        }

        /// <summary>
        /// The chanel point.
        /// </summary>
        private class ChanelPoint
        {
            /// <summary>
            /// The value 1.
            /// </summary>
            public readonly List<double> Value1;

            /// <summary>
            /// The value 2.
            /// </summary>
            public readonly List<double> Value2;

            /// <summary>
            /// The ordinata.
            /// </summary>
            public readonly List<ulong> Ordinata;

            /// <summary>
            /// Initializes a new instance of the <see cref="ChanelPoint"/> class.
            /// </summary>
            public ChanelPoint()
            {
                this.Value1 = new List<double>();
                this.Value2 = new List<double>();
                this.Ordinata = new List<ulong>();
            }

            /// <summary>
            /// The clear.
            /// </summary>
            public void Clear()
            {
                this.Value1.Clear();
                this.Value2.Clear();
                this.Ordinata.Clear();
            }

            /// <summary>
            /// The count.
            /// </summary>
            /// <returns>
            /// The <see cref="int"/>.
            /// </returns>
            public int Count()
            {
                return this.Ordinata.Count;
            }
        }
    }
}
