using System.Collections.ObjectModel;
using VerificationAirVelocitySensor.Models.ClassLib;
using VerificationAirVelocitySensor.ViewModels;
using VerificationAirVelocitySensor.ViewModels.BaseVm;

namespace VerificationAirVelocitySensor.Views
{
    public partial class CheckpointsView// Логика взаимодействия для SpeedPointsView.xaml.
    {
        internal CheckpointsView(ObservableCollection<Checkpoint> speedPointsList, RelayCommand saveSpeedsPointCommand)
        {
            DataContext = new CheckpointsVm(speedPointsList, saveSpeedsPointCommand);
            InitializeComponent();
        }
    }
}