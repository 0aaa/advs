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

        public SetMeasurementsDataVm(MainWindowVm mainWindowVm)
        {
            MeasurementsData = mainWindowVm.MeasurementsData;
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
    }
}
