using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Threading.Tasks;
using VerificationAirVelocitySensor.Model;
using VerificationAirVelocitySensor.ViewModel.BaseVm;
using VerificationAirVelocitySensor.ViewModel.Services;

namespace VerificationAirVelocitySensor.ViewModel
{
    public class SettingsVm : BaseVm.BaseVm
    {
        #region RelayCommand

        public RelayCommand UpdateComPortsSourceCommand => new RelayCommand(UpdateComPortsSource);

        public RelayCommand StopFrequencyMotorCommand =>
            new RelayCommand(() => FrequencyMotorDevice.Instance.SetFrequency(0, 0),
                FrequencyMotorDevice.Instance.IsOpen);

        public RelayCommand SetSpeedFrequencyMotorCommand => new RelayCommand(SetSpeedFrequencyMotorMethodAsync,
            FrequencyMotorDevice.Instance.IsOpen);

        #endregion


        public SettingsModel SettingsModel { get; set; }

        #region Collection

        public ObservableCollection<string> PortsList { get; set; } =
            new ObservableCollection<string>(SerialPort.GetPortNames());

        public List<GateTimeDescription> GateTimeList { get; } = new List<GateTimeDescription>
        {
            //new GateTimeDescription(GateTime.S1, "1 сек"),
            new GateTimeDescription(GateTime.S4, "4 сек"),
            new GateTimeDescription(GateTime.S7, "7 сек"),
            new GateTimeDescription(GateTime.S10, "10 сек"),
            new GateTimeDescription(GateTime.S100, "100 сек"),
        };

        public List<FrequencyChannelDescription> FrequencyChannelList { get; } = new List<FrequencyChannelDescription>
        {
            new FrequencyChannelDescription(FrequencyChannel.Channel1, "1-ый канал"),
            new FrequencyChannelDescription(FrequencyChannel.Channel2, "2-ой канал"),
        };

        #endregion

        private Action _serialization;

        private bool _filterChannel1;

        public bool FilterChannel1
        {
            get => _filterChannel1;
            set
            {
                _filterChannel1 = value;
                OnPropertyChanged(nameof(FilterChannel1));
                SettingsModel.FilterChannel1 = value;
                _serialization();
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
                SettingsModel.FilterChannel2 = value;
                _serialization();
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
                SettingsModel.FrequencyChannel = value;
                _serialization();
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
                SettingsModel.GateTime = value;
                _serialization();
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
                SettingsModel.ComPortFrequencyMotor = value;
                _serialization();
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
                SettingsModel.ComPortFrequencyCounter = value;
                _serialization();
            }
        }

        private int _setFrequencyMotor;

        public int SetFrequencyMotor
        {
            get => _setFrequencyMotor;
            set
            {
                _setFrequencyMotor = value;
                OnPropertyChanged(nameof(SetFrequencyMotor));
                SettingsModel.SetFrequencyMotor = value;
                _serialization();
            }
        }

        public SettingsVm(SettingsModel settingsModel, Action serialization)
        {
            SettingsModel = settingsModel;
            _serialization = serialization;

            ComPortFrequencyCounter = SettingsModel.ComPortFrequencyCounter;
            ComPortFrequencyMotor = SettingsModel.ComPortFrequencyMotor;
            GateTime = SettingsModel.GateTime;
            FilterChannel1 = SettingsModel.FilterChannel1;
            FilterChannel2 = SettingsModel.FilterChannel2;
            FrequencyChannel = SettingsModel.FrequencyChannel;

            SetFrequencyMotor = SettingsModel.SetFrequencyMotor;



        }

        private void SetSpeedFrequencyMotorMethodAsync()
        {
            Task.Run(async () =>
                await Task.Run(() => FrequencyMotorDevice.Instance.SetFrequency(SettingsModel.SetFrequencyMotor, 0)));
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
    }
}