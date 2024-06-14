using VerificationAirVelocitySensor.Model.EnumLib;
using VerificationAirVelocitySensor.ViewModels.BaseVm;

namespace VerificationAirVelocitySensor.Models
{
    internal class MeasurementsData : BaseVm
    {
        public RevisionIteration TypeVerification { get; set; }// Настройка на будущее, тип поверки.
        public string DeviceId { get; set; }// Серийный номер.
        public string Temperature { get; set; }// Температура.
        public string Humidity { get; set; }// Влажность.
        public string Pressure { get; set; }// Давление.
        public string Verifier { get; set; }// Поверитель, в месте использования будет сделана коллекция имен или типа того.
    }
}