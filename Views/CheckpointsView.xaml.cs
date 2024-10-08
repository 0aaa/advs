namespace ADVS.Views
{
    public partial class CheckpointsView// Логика взаимодействия для SpeedPointsView.xaml.
    {
        internal CheckpointsView(System.Collections.ObjectModel.ObservableCollection<Models.Classes.Checkpoint> c, ViewModels.Base.RelayCommand s)
        {
            DataContext = new ViewModels.CheckpointsVm(c, s);
            InitializeComponent();
        }
    }
}