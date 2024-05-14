using System;
using VerificationAirVelocitySensor.Model;
using VerificationAirVelocitySensor.ViewModel.BaseVm;

namespace VerificationAirVelocitySensor.ViewModel
{
    /// <summary>Vm страницы с условиями поверки</summary>
    internal class SetMeasurementsDataVm : BaseVm.BaseVm
    {
        private MeasurementsData _measurementsData;
        public ListTypeVerification[] TypeVerificationsList { get; }
        public RelayCommand[] SettingsCommands { get; }// Continue, Cancel, SetLogSaveWay.
        public MainWindowVm MainWindowVm { get; set; }
        public MeasurementsData MeasurementsData
        {
            get => _measurementsData;
            set
            {
                _measurementsData = value;
                OnPropertyChanged(nameof(MeasurementsData));
            }
        }
        public Action CloseWindow { get; set; }
        public bool IsContinue { get; set; }

        public SetMeasurementsDataVm(MainWindowVm mainWindowVm)
        {
            if (mainWindowVm.MeasurementsData != null)
            {
                MainWindowVm = mainWindowVm;
                MeasurementsData = mainWindowVm.MeasurementsData;
            }
            else
            {
                mainWindowVm.MeasurementsData = new MeasurementsData();
                MeasurementsData = mainWindowVm.MeasurementsData;
                MainWindowVm = mainWindowVm;
            }
			TypeVerificationsList = new ListTypeVerification[] {
				new ListTypeVerification { TypeVerification = TypeVerification.Periodic, Description = "Переодическая" }
				, new ListTypeVerification { TypeVerification = TypeVerification.Primary, Description = "Первичная" }
			};
			SettingsCommands = new RelayCommand[] {
				new RelayCommand(() => { IsContinue = true; CloseWindow(); })
				, new RelayCommand(() => { IsContinue = false; CloseWindow(); })
				, new RelayCommand(() => {
					var folderBrowser = new System.Windows.Forms.FolderBrowserDialog();
					folderBrowser.ShowDialog();
					if (!string.IsNullOrWhiteSpace(folderBrowser.SelectedPath))
					{
						MainWindowVm.PathSave = folderBrowser.SelectedPath;
					}
				})
			};
        }
    }
}