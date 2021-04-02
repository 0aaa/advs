using System;
using System.Collections.Generic;
using VerificationAirVelocitySensor.Model;
using VerificationAirVelocitySensor.ViewModel.BaseVm;

namespace VerificationAirVelocitySensor.ViewModel
{
    /// <summary>
    /// Vm страницы с условиями поверки
    /// </summary>
    public class SetMeasurementsDataVm : BaseVm.BaseVm
    {
        public Action CloseWindow;

        public MainWindowVm MainWindowVm { get; set; }

        private MeasurementsData _measurementsData;

        public MeasurementsData MeasurementsData
        {
            get => _measurementsData;
            set
            {
                _measurementsData = value;
                OnPropertyChanged(nameof(MeasurementsData));
            }
        }

        public bool IsContinue { get; set; }

        public List<ListTypeVerification> TypeVerificationsList { get; set; } = new List<ListTypeVerification>
        {
            new ListTypeVerification
            {
                TypeVerification = TypeVerification.Periodic, Description = "Переодическая"
            },
            new ListTypeVerification
            {
                TypeVerification = TypeVerification.Primary, Description = "Первичная"
            }
        };


        public RelayCommand ContinueCommand => new RelayCommand(Continue);
        public RelayCommand CancelCommand => new RelayCommand(Cancel);

        public RelayCommand SetLogSaveWay => new RelayCommand(SaveLogDialogPath);

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
        }

        private void Cancel()
        {
            IsContinue = false;
            CloseWindow();
        }

        private void Continue()
        {
            IsContinue = true;
            CloseWindow();
        }

        private void SaveLogDialogPath()
        {
            var folderBrowser = new System.Windows.Forms.FolderBrowserDialog();

            folderBrowser.ShowDialog();

            if (!string.IsNullOrWhiteSpace(folderBrowser.SelectedPath))
            {
                MainWindowVm.PathSave = folderBrowser.SelectedPath;
            }
        }
    }
}
