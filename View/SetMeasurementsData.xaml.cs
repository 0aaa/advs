using VerificationAirVelocitySensor.ViewModel;

namespace VerificationAirVelocitySensor.View
{
    /// <summary>Логика взаимодействия для SetMeasurementsData.xaml</summary>
    public partial class SetMeasurementsData
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