using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using VerificationAirVelocitySensor.Model;
using VerificationAirVelocitySensor.ViewModel.BaseVm;
using VerificationAirVelocitySensor.ViewModel.Services;

namespace VerificationAirVelocitySensor.ViewModel
{
    internal class SettingsVm : BaseVm.BaseVm
    {
        private readonly Action _serialization;
        private FrequencyChannel _frequencyChannel;
        private GateTime _gateTime;
        private string _comPortFrequencyMotor;
        private string _comPortFrequencyCounter;
        private int _setFrequencyMotor;
        private bool _filterChannel1;
        private bool _filterChannel2;
        #region Collections
        public FrequencyChannelDescription[] FrequencyChannelArr { get; }
        public GateTimeDescription[] GateTimeArr { get; }
        public ObservableCollection<string> PortsList { get; }
        #endregion
        public SettingsModel SettingsModel { get; }
        #region RelayCommand
        public RelayCommand UpdateComPortsSourceCommand { get; }
        #endregion
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
        public bool FilterChannel1
        {
            get => _filterChannel1;
            set
            {
                _filterChannel1 = value;
                OnPropertyChanged(nameof(FilterChannel1));
				SettingsModel.FilterChannels[0] = value;
                _serialization();
            }
        }
        public bool FilterChannel2
        {
            get => _filterChannel2;
            set
            {
                _filterChannel2 = value;
                OnPropertyChanged(nameof(FilterChannel2));
				SettingsModel.FilterChannels[1] = value;
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
            FilterChannel1 = SettingsModel.FilterChannels[0];
            FilterChannel2 = SettingsModel.FilterChannels[1];
            FrequencyChannel = SettingsModel.FrequencyChannel;
            SetFrequencyMotor = SettingsModel.SetFrequencyMotor;
			GateTimeArr = new GateTimeDescription[] {
				//new GateTimeDescription(GateTime.S1, "1 сек"),
				new GateTimeDescription(GateTime.S4, "4 сек")
				, new GateTimeDescription(GateTime.S7, "7 сек")
				, new GateTimeDescription(GateTime.S10, "10 сек")
				, new GateTimeDescription(GateTime.S100, "100 сек")
			};
			FrequencyChannelArr = new FrequencyChannelDescription[] {
				new FrequencyChannelDescription(FrequencyChannel.Channel1, "1-ый канал")
				, new FrequencyChannelDescription(FrequencyChannel.Channel2, "2-ой канал")
			};
			PortsList = new ObservableCollection<string>(SerialPort.GetPortNames());
			UpdateComPortsSourceCommand = new RelayCommand(() => {
				var newPortList = new ObservableCollection<string>(SerialPort.GetPortNames());
				foreach (var port in newPortList)// Добавляю новые итемы из полученной коллекции.
				{
					if (!PortsList.Contains(port))
					{
						PortsList.Add(port);
					}
				}
				var deletePorts = new ObservableCollection<string>();
				foreach (var port in PortsList)// Записываю старые итемы в коллекцию на удаление.
				{
					if (!newPortList.Contains(port))
					{
						deletePorts.Add(port);
					}
				}
				foreach (var port in deletePorts)// Удаляю лишние элементы.
				{
					PortsList.Remove(port);
				}
			});
        }
    }
}