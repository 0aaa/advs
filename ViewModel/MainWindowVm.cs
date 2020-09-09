using VerificationAirVelocitySensor.ViewModel.BaseVm;
using VerificationAirVelocitySensor.ViewModel.Services;

namespace VerificationAirVelocitySensor.ViewModel
{
    public class MainWindowVm : BaseVm.BaseVm
    {
        public RelayCommand OnFilterCommand => new RelayCommand(() => FrequencyCounterDevice.Instance.SwitchFilter(1, true));
        public RelayCommand OffFilterCommand => new RelayCommand(() => FrequencyCounterDevice.Instance.SwitchFilter(1, false));
        public RelayCommand ResetCommand => new RelayCommand(FrequencyCounterDevice.Instance.RstCommand);
        public RelayCommand SendCustomCommand => new RelayCommand(() => FrequencyCounterDevice.Instance.WriteCommandAsync(CustomCommandText));
        public RelayCommand OpenClose => new RelayCommand(() => FrequencyCounterDevice.Instance.OpenClose(ComPort));
        
        public string CustomCommandText { get; set; }
        public string DataRead { get; set; }
        public string ComPort { get; set; }

        public MainWindowVm()
        {
            FrequencyCounterDevice.Instance.DataReadUpdate += Instance_DataReadUpdate;
        }

        private void Instance_DataReadUpdate(object sender, DataReadEventArgs e)
        {
            DataRead += e.DataRead;
        }
    }   
}
