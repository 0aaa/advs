using System.Collections.ObjectModel;
using VerificationAirVelocitySensor.ViewModel;
using VerificationAirVelocitySensor.ViewModel.BaseVm;

namespace VerificationAirVelocitySensor.View
{
    /// <summary>Логика взаимодействия для SpeedPointsView.xaml</summary>
    public partial class SpeedPointsView
    {
        internal SpeedPointsView(ObservableCollection<SpeedPoint> speedPointsList, RelayCommand saveSpeedsPointCommand)
        {
            DataContext = new SpeedPointsVm(speedPointsList, saveSpeedsPointCommand);
            InitializeComponent();
        }
    }
}