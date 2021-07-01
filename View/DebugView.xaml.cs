using System.Windows;
using VerificationAirVelocitySensor.ViewModel;

namespace VerificationAirVelocitySensor.View
{
    /// <summary>
    /// Логика взаимодействия для DebugView.xaml
    /// </summary>
    public partial class DebugView
    {
        private readonly DebugVm _debugVm = new DebugVm();

        public DebugView(ref decimal speedReferenceValue)
        {
            InitializeComponent();
            _debugVm.SpeedReferenceValue = speedReferenceValue;
            DataContext = _debugVm;
        }

        private void DebugView_OnUnloaded(object sender, RoutedEventArgs e)
        {
            _debugVm.Unloaded();
        }
    }
}