using System;
using ADVS.ViewModels.Base;
using ADVS.ViewModels.Services;

namespace ADVS.ViewModels
{
    internal class ConditionsVm : BaseVm// Vm страницы с условиями поверки.
    {
        private Settings _s;
        public RelayCommand[] Rcs { get; }// Continue, Cancel, SetLogSaveWay.
        public Settings Settings
		{
			get => _s;
            private set
            {
				_s = value;
                OnPropertyChanged(nameof(Settings));
			}
		}
        public bool IsContinue { get; private set; }
        public Action CloseWindow { get; set; }

        public ConditionsVm(Settings s)
        {
			Settings = s;
			//TypeVerificationsList = [
			//	new RevisionType { Iteration = RevisionIteration.Periodic, Description = "Периодическая" }
			//	, new RevisionType { Iteration = RevisionIteration.Primary, Description = "Первичная" }
			//];
			Rcs = [
				new RelayCommand(() => { IsContinue = true; CloseWindow(); })
				, new RelayCommand(() => { IsContinue = false; Settings.Serialize(); CloseWindow(); })
				, new RelayCommand(() => {
					var d = new System.Windows.Forms.FolderBrowserDialog();
					d.ShowDialog();
					if (!string.IsNullOrWhiteSpace(d.SelectedPath))
					{
						Settings.SavePath = d.SelectedPath;
					}
				})
			];
        }
    }
}