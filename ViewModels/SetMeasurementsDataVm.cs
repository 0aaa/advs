using System;
using VerificationAirVelocitySensor.ViewModels.BaseVm;
using VerificationAirVelocitySensor.ViewModels.Services;

namespace VerificationAirVelocitySensor.ViewModels
{
    internal class SetMeasurementsDataVm : BaseVm.BaseVm// Vm страницы с условиями поверки.
    {
        private UserSettings _userSettings;
        public RelayCommand[] SettingsCommands { get; }// Continue, Cancel, SetLogSaveWay.
        public UserSettings UserSettings
		{
			get => _userSettings;
			private set
			{
				_userSettings = value;
                OnPropertyChanged(nameof(UserSettings));
			}
		}
        public bool IsContinue { get; private set; }
        public Action CloseWindow { get; set; }

        public SetMeasurementsDataVm(UserSettings userSettings)
        {
			UserSettings = userSettings;
			//TypeVerificationsList = [
			//	new RevisionType { Iteration = RevisionIteration.Periodic, Description = "Периодическая" }
			//	, new RevisionType { Iteration = RevisionIteration.Primary, Description = "Первичная" }
			//];
			SettingsCommands = [
				new RelayCommand(() => { IsContinue = true; CloseWindow(); })
				, new RelayCommand(() => { IsContinue = false; UserSettings.Serialize(); CloseWindow(); })
				, new RelayCommand(() => {
					var folderBrowser = new System.Windows.Forms.FolderBrowserDialog();
					folderBrowser.ShowDialog();
					if (!string.IsNullOrWhiteSpace(folderBrowser.SelectedPath))
					{
						UserSettings.SavePath = folderBrowser.SelectedPath;
					}
				})
			];
        }
    }
}