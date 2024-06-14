using VerificationAirVelocitySensor.ViewModels;

namespace VerificationAirVelocitySensor.Views
{
    public partial class SetMeasurementsData// Логика взаимодействия для SetMeasurementsData.xaml.
    {
        internal SetMeasurementsDataVm ViewModel { get; }

        internal SetMeasurementsData(SetMeasurementsDataVm setMeasurementsDataVm)
        {
            InitializeComponent();
            DataContext = setMeasurementsDataVm;
			ViewModel = (SetMeasurementsDataVm)DataContext;
			ViewModel.CloseWindow = () => Close();
        }
    }
}