namespace VerificationAirVelocitySensor.ViewModels.Base
{
    internal class RelayCommand : System.Windows.Input.ICommand// Управление командами для биндинга на UI.
    {
        private readonly System.Action<object> _e;
        private readonly System.Func<object, bool> _canE;
        public event System.EventHandler CanExecuteChanged
        {
            add => System.Windows.Input.CommandManager.RequerySuggested += value;
            remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
        }

        public RelayCommand(System.Action e, System.Func<bool> ce)
        {
            _e = _ => e();
            _canE = _ => ce();
        }

        public RelayCommand(System.Action e, System.Func<object, bool> ce = null)// Конструктор команды.
        {
            _e = _ => e();
            _canE = ce;
        }

        public RelayCommand(System.Action<object> e, System.Func<object, bool> ce = null)
        {
            _e = e;
            _canE = ce;
        }

        public bool CanExecute(object p)
        {
            return _canE == null || _canE(p);
        }

        public void Execute(object p)
        {
            _e(p);
        }
    }
}