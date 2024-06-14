using System.Windows;
using VerificationAirVelocitySensor.ViewModels;

namespace VerificationAirVelocitySensor.Views
{
    public partial class DebugView// Логика взаимодействия для DebugView.xaml.
    {
        private readonly DebugVm _debugVm;

        public DebugView()
        {
            InitializeComponent();
			_debugVm = new DebugVm();
            DataContext = _debugVm;
        }

        private void DebugView_OnUnloaded(object sender, RoutedEventArgs e)
        {
            _debugVm.Unloaded();
        }
    }
}