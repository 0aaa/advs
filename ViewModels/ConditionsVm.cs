using System;
using ADVS.ViewModels.Base;
using ADVS.ViewModels.Services;

namespace ADVS.ViewModels
{
    internal partial class ConditionsVm : BaseVm
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
			Rcs = [
				new RelayCommand(() => { IsContinue = true; CloseWindow(); })
				, new RelayCommand(() => { IsContinue = false; Settings.Serialize(); CloseWindow(); })
				, new RelayCommand(() => {
					var d = new System.Windows.Forms.FolderBrowserDialog();
					d.ShowDialog();
					if (!string.IsNullOrWhiteSpace(d.SelectedPath))
					{
						Settings.Path = d.SelectedPath;
					}
				})
			];
        }
	}
}