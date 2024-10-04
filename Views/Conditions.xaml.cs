namespace VerificationAirVelocitySensor.Views
{
    public partial class Conditions// Логика взаимодействия для SetMeasurementsData.xaml.
    {
        internal ViewModels.ConditionsVm ViewModel { get; }

        internal Conditions(ViewModels.ConditionsVm c)
        {
            InitializeComponent();
            DataContext = c;
			ViewModel = (ViewModels.ConditionsVm)DataContext;
			ViewModel.CloseWindow = () => Close();
        }
    }
}