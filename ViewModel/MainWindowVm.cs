using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using VerificationAirVelocitySensor.ViewModel.BaseVm;
using VerificationAirVelocitySensor.ViewModel.Services;

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

        public RelayCommand ReadValueOnFrequencyCounterCommand => new RelayCommand(ReadValueOnFrequencyCounter , FrequencyCounterDevice.Instance.IsOpen);

        public RelayCommand ResetCommand => new RelayCommand(FrequencyCounterDevice.Instance.RstCommand , FrequencyCounterDevice.Instance.IsOpen);

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
            new RelayCommand(() => FrequencyCounterDevice.Instance.ClosePort() , FrequencyCounterDevice.Instance.IsOpen);

        #endregion

        #region Анемометр / Частотный двигатель

        public RelayCommand OpenPortFrequencyMotorCommand =>
            new RelayCommand(() => FrequencyMotorDevice.Instance.OpenPort(ComPortFrequencyMotor));
        public RelayCommand ClosePortFrequencyMotorCommand =>
            new RelayCommand(() => FrequencyMotorDevice.Instance.ClosePort(), FrequencyMotorDevice.Instance.IsOpen);

        public RelayCommand StopFrequencyMotorCommand => new RelayCommand(() => FrequencyMotorDevice.Instance.SetFrequency(0) , FrequencyMotorDevice.Instance.IsOpen);
        public RelayCommand SetSpeedFrequencyMotorCommand => new RelayCommand(SetSpeedFrequencyMotorMethodAsync, FrequencyMotorDevice.Instance.IsOpen);

        #endregion
        public RelayCommand OpenCloseConnectionMenuCommand => new RelayCommand(OpenCloseConnectionMenu);
        public RelayCommand OpenCloseDebuggingMenuCommand => new RelayCommand(OpenCloseDebuggingMenu , OpenCloseDebuggingMenuValidation);
        public RelayCommand UpdateComPortsSourceCommand => new RelayCommand(UpdateComPortsSource);

        #endregion

        #region Property


        public decimal FrequencyCounterValue { get; set; }
        public bool VisibilityConnectionMenu { get; set; }
        public bool VisibilityDebuggingMenu { get; set; }
        public ObservableCollection<string> PortsList { get; set; } = new ObservableCollection<string>(SerialPort.GetPortNames());
        public bool FrequencyCounterIsOpen { get; set; }
        public bool FrequencyMotorIsOpen { get; set; }

        /// <summary>
        /// Параметр для установки ожидаемой скорости трубы.
        /// Внутри метода установки, переводится в частоту и отправляется на устройство.
        /// После чего, сверяемся по эталонному значению скорости с трубы и корректируем отправляемую частоту на трубу,
        /// что бы добится почти точного соответствия эталонной скорости и ожидаемой.
        /// Корректируем с помощью изменения коеффициента в формуле расчета отправляемой частоты.
        /// </summary>
        public decimal SpeedFrequencyMotor { get; set; }
        /// <summary>
        /// Эталонное значение скорости с частотной трубы
        /// </summary>
        public decimal SpeedReferenceValue { get; set; }
        /// <summary>
        /// Коеффициент для корректировки скорости вращения трубы ,
        /// при переводе ожидаемой скорости в частоту (вращения двигателя или типо того) 
        /// </summary>
        private decimal coefficient = 1;


        //TODO Все свойства что ниже, должны сохранятся пре перезапуске.
        public bool FilterChannel1 { get; set; }
        public bool FilterChannel2 { get; set; }
        public FrequencyChannel FrequencyChannel { get; set; } = FrequencyChannel.Channel1;
        public GateTime GateTime { get; set; } = GateTime.S1;
        public string ComPortFrequencyMotor { get; set; }
        public string ComPortFrequencyCounter { get; set; }

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
                FrequencyMotorDevice.Instance.SetFrequency(SpeedFrequencyMotor , coefficient);
                Thread.Sleep(250);

                //TODO здесь должна быть корректировка коеффицента и переотправка частоты на трубу.
            }));
        }

        #endregion

        public MainWindowVm()
        {
            FrequencyCounterDevice.Instance.IsOpenUpdate += FrequencyCounter_IsOpenUpdate;

            FrequencyMotorDevice.Instance.IsOpenUpdate += FrequencyMotor_IsOpenUpdate;

            FrequencyMotorDevice.Instance.UpdateReferenceValue += FrequencyMotor_UpdateReferenceValue;
        }

        #region EventHandler Method
        private void FrequencyMotor_UpdateReferenceValue(object sender, UpdateReferenceValueEventArgs e)
        {
            SpeedReferenceValue = (decimal)e.ReferenceValue;
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

    }
}