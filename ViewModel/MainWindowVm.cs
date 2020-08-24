using VerificationAirVelocitySensor.ViewModel.BaseVm;
using VerificationAirVelocitySensor.ViewModel.Services;

namespace VerificationAirVelocitySensor.ViewModel
{
    public class MainWindowVm : BaseVm.BaseVm
    {
        public RelayCommand OnFilterCommand => new RelayCommand(() => FrequencyDevice.Instance.SwitchFilter(1, true));
        public RelayCommand OffFilterCommand => new RelayCommand(() => FrequencyDevice.Instance.SwitchFilter(1, false));
        public RelayCommand ResetCommand => new RelayCommand(FrequencyDevice.Instance.RstCommand);
        public RelayCommand SendCustomCommand => new RelayCommand(() => FrequencyDevice.Instance.WriteCommandAsync(CustomCommandText));
        public RelayCommand OpenClose => new RelayCommand(() => FrequencyDevice.Instance.OpenClose(ComPort));
        
        public string CustomCommandText { get; set; }
        public string DataRead { get; set; }
        public string ComPort { get; set; }

        public MainWindowVm()
        {
            FrequencyDevice.Instance.DataReadUpdate += Instance_DataReadUpdate;
        }

        private void Instance_DataReadUpdate(object sender, DataReadEventArgs e)
        {
            DataRead = e.DataRead;
        }
    }   
}
