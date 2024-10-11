using ADVS.Models.Evaluations;

namespace ADVS.Views
{
    public partial class CheckpointsView
    {
        internal CheckpointsView(System.Collections.ObjectModel.ObservableCollection<Checkpoint> c, ViewModels.Base.RelayCommand s)
        {
            DataContext = new ViewModels.CheckpointsVm(c, s);
            InitializeComponent();
        }
    }
}