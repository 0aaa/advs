namespace VerificationAirVelocitySensor.ViewModels.Base
{
    internal class BaseVm : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string p = "")
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(p));
        }
    }
}