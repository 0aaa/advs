using System;
using System.Windows.Input;

namespace VerificationAirVelocitySensor.ViewModels.BaseVm
{
    internal class RelayCommand : ICommand// Управление командами для биндинга на UI.
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public RelayCommand(Action execute, Func<bool> canExecute)
        {
            _execute = _ => execute();
            _canExecute = _ => canExecute();
        }

        /// <summary>Конструктор команды</summary><param name="execute"></param><param name="canExecute"></param>
        public RelayCommand(Action execute, Func<object, bool> canExecute = null)
        {
            _execute = _ => execute();
            _canExecute = canExecute;
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }
    }
}