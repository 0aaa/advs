using VerificationAirVelocitySensor.ViewModel.BaseVm;

namespace VerificationAirVelocitySensor.Model
{
    internal class MeasurementsData : BaseVm
    {
        /// <summary>Настройка на будущее, тип поверки</summary>
        public TypeVerification TypeVerification { get; set; }
        /// <summary>Серийный номер</summary>
        public string DeviceId { get; set; }
        /// <summary>Температура</summary>
        public string Temperature { get; set; }
        /// <summary>Влажность</summary>
        public string Humidity { get; set; }
        /// <summary>Давление</summary>
        public string Pressure { get; set; }
        /// <summary>Поверитель, в месте использования будет сделана коллекция имен или типо того</summary>
        public string Verifier { get; set; }
    }

    /// <summary>Класс для биндинга коллекции на чекбокс</summary>
    internal class ListTypeVerification
    {
        /// <summary>Значение</summary>
        public TypeVerification TypeVerification { get; set; }
        /// <summary>Описание</summary>
        public string Description { get; set; }
    }

    internal enum TypeVerification
    {
        Periodic, // Переодическая поверка.
        Primary   // Первичная поверка.
    }
}