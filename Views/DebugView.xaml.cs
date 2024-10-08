namespace ADVS.Views
{
    public partial class DebugView// Логика взаимодействия для DebugView.xaml.
    {
        private readonly ViewModels.DebugVm _debugVm;

        public DebugView()
        {
            InitializeComponent();
			_debugVm = new ViewModels.DebugVm();
            DataContext = _debugVm;
        }

        private void DebugView_OnUnloaded(object s, System.Windows.RoutedEventArgs e)
        {
            _debugVm.Unload();
        }
    }
}