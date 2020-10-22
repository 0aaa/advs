using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using VerificationAirVelocitySensor.Model;
using VerificationAirVelocitySensor.ViewModel.BaseVm;
using VerificationAirVelocitySensor.ViewModel.DvsVm;
using VerificationAirVelocitySensor.ViewModel.Services;
using YamlDotNet.Serialization;

namespace VerificationAirVelocitySensor.ViewModel
{
    public class MainWindowVm : BaseVm.BaseVm
    {
        #region RealyCommand

        #region Частотомер ЧЗ-85/6

        #region Set Gate Time

        public RelayCommand SetGateTime1SCommand =>
            new RelayCommand(() => SetGateTime(GateTime.S1), SetGateTime1SValidation);

        public RelayCommand SetGateTime4SCommand =>
            new RelayCommand(() => SetGateTime(GateTime.S4), SetGateTime4SValidation);

        public RelayCommand SetGateTime7SCommand =>
            new RelayCommand(() => SetGateTime(GateTime.S7), SetGateTime7SValidation);

        public RelayCommand SetGateTime10SCommand =>
            new RelayCommand(() => SetGateTime(GateTime.S10), SetGateTime10SValidation);

        public RelayCommand SetGateTime100SCommand =>
            new RelayCommand(() => SetGateTime(GateTime.S100), SetGateTime100SValidation);

        #endregion

        public RelayCommand ReadValueOnFrequencyCounterCommand =>
            new RelayCommand(ReadValueOnFrequencyCounter, FrequencyCounterDevice.Instance.IsOpen);

        public RelayCommand ResetCommand => new RelayCommand(FrequencyCounterDevice.Instance.RstCommand,
            FrequencyCounterDevice.Instance.IsOpen);

        public RelayCommand SetFrequencyChannel1Command =>
            new RelayCommand(() => SetFrequencyChannel(FrequencyChannel.Channel1), SetFrequencyChannel1Validation);

        public RelayCommand SetFrequencyChannel2Command =>
            new RelayCommand(() => SetFrequencyChannel(FrequencyChannel.Channel2), SetFrequencyChannel2Validation);

        public RelayCommand OnFilterChannel1Command =>
            new RelayCommand(() => OnOffFilter(1, true), OnFilterChannel1Validation);

        public RelayCommand OffFilterChannel1Command =>
            new RelayCommand(() => OnOffFilter(1, false), OffFilterChannel1Validation);

        public RelayCommand OnFilterChannel2Command =>
            new RelayCommand(() => OnOffFilter(2, true), OnFilterChannel2Validation);

        public RelayCommand OffFilterChannel2Command =>
            new RelayCommand(() => OnOffFilter(2, false), OffFilterChannel2Validation);

        public RelayCommand OpenPortFrequencyCounterCommand =>
            new RelayCommand(() => FrequencyCounterDevice.Instance.OpenPort(ComPortFrequencyCounter));

        public RelayCommand ClosePortFrequencyCounterCommand =>
            new RelayCommand(() => FrequencyCounterDevice.Instance.ClosePort(), FrequencyCounterDevice.Instance.IsOpen);

        #endregion

        #region Анемометр / Частотный двигатель

        public RelayCommand OpenPortFrequencyMotorCommand =>
            new RelayCommand(() => FrequencyMotorDevice.Instance.OpenPort(ComPortFrequencyMotor));

        public RelayCommand ClosePortFrequencyMotorCommand =>
            new RelayCommand(() => FrequencyMotorDevice.Instance.ClosePort(), FrequencyMotorDevice.Instance.IsOpen);

        public RelayCommand StopFrequencyMotorCommand =>
            new RelayCommand(() => FrequencyMotorDevice.Instance.SetFrequency(0), FrequencyMotorDevice.Instance.IsOpen);

        public RelayCommand SetSpeedFrequencyMotorCommand => new RelayCommand(SetSpeedFrequencyMotorMethodAsync,
            FrequencyMotorDevice.Instance.IsOpen);

        #endregion

        public RelayCommand OpenCloseConnectionMenuCommand => new RelayCommand(OpenCloseConnectionMenu);

        public RelayCommand OpenCloseDebuggingMenuCommand =>
            new RelayCommand(OpenCloseDebuggingMenu, OpenCloseDebuggingMenuValidation);

        public RelayCommand UpdateComPortsSourceCommand => new RelayCommand(UpdateComPortsSource);
        public RelayCommand OpenReferenceCommand => new RelayCommand(() => IsReference = !IsReference);

        public RelayCommand StartTestCommand => new RelayCommand(StartTest);

        #endregion

        #region Property

        private UserSettings _userSettings;
        private readonly object _loker = new object();

        private const string PathUserSettings = "UserSettings.txt";

        /// <summary>
        /// Флаг для отображения справки :)
        /// </summary>
        public bool IsReference { get; set; }

        public decimal FrequencyCounterValue { get; set; }
        public bool VisibilityConnectionMenu { get; set; }
        public bool VisibilityDebuggingMenu { get; set; }

        public ObservableCollection<string> PortsList { get; set; } =
            new ObservableCollection<string>(SerialPort.GetPortNames());

        public bool FrequencyCounterIsOpen { get; set; }
        public bool FrequencyMotorIsOpen { get; set; }

        /// <summary>
        /// Параметр для установки частотв трубы.
        /// </summary>
        public decimal SetFrequencyMotor { get; set; }

        /// <summary>
        /// Эталонное значение скорости с частотной трубы
        /// </summary>
        public decimal SpeedReferenceValue { get; set; }


        //Все свойства что ниже, должны сохранятся пре перезапуске.
        //TODO Позже сделать переключатель на интерфейсе.
        public TypeTest _typeTest = TypeTest.Dvs02;

        public TypeTest TypeTest
        {
            get => _typeTest;
            set
            {
                _typeTest = value;
                OnPropertyChanged(nameof(TypeTest));
                _userSettings.TypeTest = value;
                Serialization();
            }
        }

        private bool _filterChannel1;

        public bool FilterChannel1
        {
            get => _filterChannel1;
            set
            {
                _filterChannel1 = value;
                OnPropertyChanged(nameof(FilterChannel1));
                _userSettings.FilterChannel1 = value;
                Serialization();
            }
        }

        private bool _filterChannel2;

        public bool FilterChannel2
        {
            get => _filterChannel2;
            set
            {
                _filterChannel2 = value;
                OnPropertyChanged(nameof(FilterChannel2));
                _userSettings.FilterChannel2 = value;
                Serialization();
            }
        }

        private FrequencyChannel _frequencyChannel;

        public FrequencyChannel FrequencyChannel
        {
            get => _frequencyChannel;
            set
            {
                _frequencyChannel = value;
                OnPropertyChanged(nameof(FrequencyChannel));
                _userSettings.FrequencyChannel = value;
                Serialization();
            }
        }

        private GateTime _gateTime;

        public GateTime GateTime
        {
            get => _gateTime;
            set
            {
                _gateTime = value;
                OnPropertyChanged(nameof(GateTime));
                _userSettings.GateTime = value;
                Serialization();
            }
        }

        private string _comPortFrequencyMotor;

        public string ComPortFrequencyMotor
        {
            get => _comPortFrequencyMotor;
            set
            {
                _comPortFrequencyMotor = value;
                OnPropertyChanged(nameof(ComPortFrequencyMotor));
                _userSettings.ComPortFrequencyMotor = value;
                Serialization();
            }
        }

        private string _comPortFrequencyCounter;

        public string ComPortFrequencyCounter
        {
            get => _comPortFrequencyCounter;
            set
            {
                _comPortFrequencyCounter = value;
                OnPropertyChanged(nameof(ComPortFrequencyCounter));
                _userSettings.ComPortFrequencyCounter = value;
                Serialization();
            }
        }

        #endregion

        #region RelayCommand Method

        private static bool ValidationIsOpenPorts()
        {
            if (!FrequencyMotorDevice.Instance.IsOpen())
            {
                MessageBox.Show("Порт частотного двигателя закрыт", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            if (!FrequencyCounterDevice.Instance.IsOpen())
            {
                MessageBox.Show("Порт частотомера закрыт", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private void UpdateComPortsSource()
        {
            var newPortList = new ObservableCollection<string>(SerialPort.GetPortNames());

            //Добавляю новые итемы из полученной коллекции.
            foreach (var port in newPortList)
            {
                if (!PortsList.Contains(port))
                {
                    PortsList.Add(port);
                }
            }


            var deletePorts = new ObservableCollection<string>();

            //Записываю старые итемы в коллекцию на удаление
            foreach (var port in PortsList)
            {
                if (!newPortList.Contains(port))
                {
                    deletePorts.Add(port);
                }
            }

            //Удаляю лишние элементы
            foreach (var port in deletePorts)
            {
                PortsList.Remove(port);
            }
        }

        private void OpenCloseConnectionMenu() => VisibilityConnectionMenu = !VisibilityConnectionMenu;
        private void OpenCloseDebuggingMenu() => VisibilityDebuggingMenu = !VisibilityDebuggingMenu;

        private bool OpenCloseDebuggingMenuValidation() => FrequencyMotorIsOpen;

        #region Частотомер ЧЗ-85/6

        private void ReadValueOnFrequencyCounter()
        {
            FrequencyCounterValue = FrequencyCounterDevice.Instance.GetCurrentHzValue();
        }

        private void SetGateTime(GateTime gateTime)
        {
            //TODO Команда частотомера. Делать ли асинк ? Протестировать
            FrequencyCounterDevice.Instance.SetGateTime(gateTime);
            GateTime = gateTime;
        }

        private bool SetGateTime1SValidation() =>
            GateTime != GateTime.S1 && FrequencyCounterDevice.Instance.IsOpen();

        private bool SetGateTime4SValidation() =>
            GateTime != GateTime.S4 && FrequencyCounterDevice.Instance.IsOpen();

        private bool SetGateTime7SValidation() =>
            GateTime != GateTime.S7 && FrequencyCounterDevice.Instance.IsOpen();

        private bool SetGateTime10SValidation() =>
            GateTime != GateTime.S10 && FrequencyCounterDevice.Instance.IsOpen();

        private bool SetGateTime100SValidation() =>
            GateTime != GateTime.S100 && FrequencyCounterDevice.Instance.IsOpen();

        private void SetFrequencyChannel(FrequencyChannel frequencyChannel)
        {
            //TODO Команда частотомера. Делать ли асинк ? Протестировать
            FrequencyCounterDevice.Instance.SetChannelFrequency(frequencyChannel);
            FrequencyChannel = frequencyChannel;
        }

        private bool SetFrequencyChannel1Validation() =>
            FrequencyChannel != FrequencyChannel.Channel1 && FrequencyCounterDevice.Instance.IsOpen();

        private bool SetFrequencyChannel2Validation() =>
            FrequencyChannel != FrequencyChannel.Channel2 && FrequencyCounterDevice.Instance.IsOpen();

        private void OnOffFilter(int channel, bool isOn)
        {
            //TODO Команда частотомера. Делать ли асинк ? Протестировать
            switch (channel)
            {
                case 1:
                    FrequencyCounterDevice.Instance.SwitchFilter(1, isOn);
                    FilterChannel1 = isOn;
                    break;
                case 2:
                    FrequencyCounterDevice.Instance.SwitchFilter(2, isOn);
                    FilterChannel2 = isOn;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool OnFilterChannel1Validation() =>
            FrequencyCounterDevice.Instance.IsOpen() && !FilterChannel1;

        private bool OffFilterChannel1Validation() =>
            FrequencyCounterDevice.Instance.IsOpen() && FilterChannel1;

        private bool OnFilterChannel2Validation() =>
            FrequencyCounterDevice.Instance.IsOpen() && !FilterChannel2;

        private bool OffFilterChannel2Validation() =>
            FrequencyCounterDevice.Instance.IsOpen() && FilterChannel2;

        #endregion

        private void SetSpeedFrequencyMotorMethodAsync()
        {
            Task.Run(async () => await Task.Run(() =>
            {
                FrequencyMotorDevice.Instance.SetFrequency(SetFrequencyMotor);
                FrequencyMotorDevice.Instance.CorrectionSpeedMotor();
            }));
        }

        #endregion

        #region Test Method

        public ObservableCollection<DvsValue> CollectionDvsValue { get; set; }
            = new ObservableCollection<DvsValue>();

        private void StartTest()
        {
            var isValidation = ValidationIsOpenPorts();

            if (isValidation == false) return;

            switch (TypeTest)
            {
                case TypeTest.Dvs01:
                    StartTestDvs01();
                    break;
                case TypeTest.Dvs02:
                    StartTestDvs02();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void StartTestDvs01()
        {
            var countValueOnAverage = 3;

            foreach (var point in _controlPointSpeed)
            {
                if (point.Speed == 0.7m || point.Speed == 30)
                    continue;

                var value = new DvsValue(point.Speed);

                FrequencyMotorDevice.Instance.SetFrequency(point.SetFrequency);
                FrequencyMotorDevice.Instance.CorrectionSpeedMotor();
                //
            }
        }

        private void StartTestDvs02()
        {
            var countValueOnAverage = 6;

            CollectionDvsValue.Clear();

            foreach (var point in _controlPointSpeed)
            {
                var value = new DvsValue(point.Speed);

                Application.Current.Dispatcher?.Invoke(() => CollectionDvsValue.Add(value));

                FrequencyMotorDevice.Instance.SetFrequency(point.SetFrequency);
                FrequencyMotorDevice.Instance.CorrectionSpeedMotor();

                //TODO Думаю необходимо проверять скорость трубы перед каждым съемом значения.
                while (value.CollectionCount != countValueOnAverage)
                {
                    var hzValue = FrequencyCounterDevice.Instance.GetCurrentHzValue();

                    Application.Current.Dispatcher?.Invoke(() => value.AddValueInCollection(hzValue));

                    Thread.Sleep(GateTimeToMSec(GateTime) + 1000);

                    FrequencyMotorDevice.Instance.CorrectionSpeedMotor(false);
                }
            }
        }

        private int GateTimeToMSec(GateTime gateTime)
        {
            switch (gateTime)
            {
                case GateTime.S1:
                    return 1000;
                case GateTime.S4:
                    return 4000;
                case GateTime.S7:
                    return 7000;
                case GateTime.S10:
                    return 10000;
                case GateTime.S100:
                    return 100000;
                default:
                    throw new ArgumentOutOfRangeException(nameof(gateTime), gateTime, null);
            }
        }

        #endregion

        private readonly List<ControlPointSpeedToFrequency> _controlPointSpeed = new List<ControlPointSpeedToFrequency>
        {
            new ControlPointSpeedToFrequency(0.7m, 445),
            new ControlPointSpeedToFrequency(5, 2505),
            new ControlPointSpeedToFrequency(10, 4745),
            new ControlPointSpeedToFrequency(15, 7140),
            new ControlPointSpeedToFrequency(20, 9480),
            new ControlPointSpeedToFrequency(25, 12015),
            new ControlPointSpeedToFrequency(30, 15200),
        };

        public MainWindowVm()
        {
            FrequencyCounterDevice.Instance.IsOpenUpdate += FrequencyCounter_IsOpenUpdate;
            FrequencyMotorDevice.Instance.IsOpenUpdate += FrequencyMotor_IsOpenUpdate;
            FrequencyMotorDevice.Instance.UpdateReferenceValue += FrequencyMotor_UpdateReferenceValue;
            FrequencyMotorDevice.Instance.UpdateSetFrequency += FrequencyMotor_UpdateSetFrequency;

            var deserialization = Deserialization();
            _userSettings = deserialization ?? new UserSettings();
            FilterChannel1 = _userSettings.FilterChannel1;
            FilterChannel2 = _userSettings.FilterChannel2;
            FrequencyChannel = _userSettings.FrequencyChannel;
            GateTime = _userSettings.GateTime;
            ComPortFrequencyMotor = _userSettings.ComPortFrequencyMotor;
            ComPortFrequencyCounter = _userSettings.ComPortFrequencyCounter;


            #region Test Code

            //var x = new DvsValue(0.7m);

            //CollectionDvsValue = new ObservableCollection<DvsValue>
            //{
            //    x
            //};


            



            //Task.Run(async () => await Task.Run(() =>
            //{
            //    Thread.Sleep(1000);



            //    Application.Current.Dispatcher?.Invoke(() => x.AddValueInCollection(20));

            //    Thread.Sleep(2000);

            //    Application.Current.Dispatcher?.Invoke(() => x.AddValueInCollection(30));

            //    Thread.Sleep(2000);

            //    Application.Current.Dispatcher?.Invoke(() => x.AddValueInCollection(40));

            //    Thread.Sleep(2000);

            //    Application.Current.Dispatcher?.Invoke(() => x.AddValueInCollection(50));

            //    Thread.Sleep(2000);

            //    Application.Current.Dispatcher?.Invoke(() => x.AddValueInCollection(60));

            //    Thread.Sleep(2000);

            //    Application.Current.Dispatcher?.Invoke(() => x.AddValueInCollection(70));

            //    Thread.Sleep(2000);

            //}));

            #endregion
        }


        #region EventHandler Method

        private void FrequencyMotor_UpdateSetFrequency(object sender, UpdateSetFrequencyEventArgs e)
        {
            SetFrequencyMotor = e.SetFrequency;
        }

        private void FrequencyMotor_UpdateReferenceValue(object sender, UpdateReferenceValueEventArgs e)
        {
            SpeedReferenceValue = (decimal) e.ReferenceValue;
        }

        private void FrequencyMotor_IsOpenUpdate(object sender, IsOpenFrequencyMotorEventArgs e)
        {
            FrequencyMotorIsOpen = e.IsOpen;
        }

        private void FrequencyCounter_IsOpenUpdate(object sender, IsOpenFrequencyCounterEventArgs e)
        {
            FrequencyCounterIsOpen = e.IsOpen;
        }

        #endregion

        #region deserilize / serilize

        private void Serialization()
        {
            try
            {
                var serializer = new Serializer();

                lock (_loker)
                {
                    using (var file = File.Open(PathUserSettings, FileMode.Create))
                    {
                        using (var writer = new StreamWriter(file))
                        {
                            serializer.Serialize(writer, _userSettings);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private UserSettings Deserialization()
        {
            var deserializer = new Deserializer();

            using (var file = File.Open(PathUserSettings, FileMode.OpenOrCreate, FileAccess.Read))
            {
                using (var reader = new StreamReader(file))
                {
                    try
                    {
                        var userSettings = deserializer.Deserialize<UserSettings>(reader);

                        return userSettings;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
        }

        #endregion
    }

    public enum TypeTest
    {
        Dvs01 = 1,
        Dvs02 = 2
    }
}