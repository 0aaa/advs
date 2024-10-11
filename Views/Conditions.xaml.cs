namespace ADVS.Views
{
    public partial class Conditions
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