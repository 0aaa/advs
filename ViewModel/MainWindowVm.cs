using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Threading.Tasks;
using VerificationAirVelocitySensor.ViewModel.BaseVm;
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

        #endregion

        #region Property

        private UserSettings _userSettings;
        private readonly object _loker;

        private const string _pathUserSettings = "UserSettings.txt";

        /// <summary>
        /// Флаг для отображения справки :)
        /// </summary>
        public bool IsReference { get; set; }

        /// <summary>
        /// Флаг для режима отладки на вкыл/выкл авто корректировки коэффициента расчета отправляемой частоты
        /// </summary>
        public bool IsAutoCorrectionCoefficient { get; set; }

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
                if (IsAutoCorrectionCoefficient)
                {
                    FrequencyMotorDevice.Instance.CorrectionSpeedMotor();
                }
            }));
        }

        #endregion

        public MainWindowVm()
        {
            FrequencyCounterDevice.Instance.IsOpenUpdate += FrequencyCounter_IsOpenUpdate;

            FrequencyMotorDevice.Instance.IsOpenUpdate += FrequencyMotor_IsOpenUpdate;

            FrequencyMotorDevice.Instance.UpdateReferenceValue += FrequencyMotor_UpdateReferenceValue;

            var deserialization = Deserialization();
            _userSettings = deserialization ?? new UserSettings();

            FilterChannel1 = _userSettings.FilterChannel1;
            FilterChannel2 = _userSettings.FilterChannel2;
            FrequencyChannel = _userSettings.FrequencyChannel;
            GateTime = _userSettings.GateTime;
            ComPortFrequencyMotor = _userSettings.ComPortFrequencyMotor;
            ComPortFrequencyCounter = _userSettings.ComPortFrequencyCounter;
        }


        #region EventHandler Method

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

        public void Serialization()
        {
            try
            {
                var serializer = new Serializer();

                lock (_loker)
                {
                    using (var file = File.Open(_pathUserSettings, FileMode.Create))
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

            using (var file = File.Open(_pathUserSettings, FileMode.OpenOrCreate, FileAccess.Read))
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
}