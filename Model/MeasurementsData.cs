namespace VerificationAirVelocitySensor.Model
{
    public class MeasurementsData
    {
        public int Temperature { get; set; }
        public int Humidity { get; set; }
        public int Pressure { get; set; }
        public TypeVerification TypeVerification { get; set; }
        /// <summary>
        /// Поверитель , в месте использования будет сделана коллекция имен или типо того
        /// </summary>
        public string Verifier { get; set; }
    }

    public enum TypeVerification
    {
        Periodic, // Переодическая поверка
        Primary   // Первичная поверка
    }

    /// <summary>
    /// Класс для биндинга коллекции на чекбокс
    /// </summary>
    public class ListTypeVerification
    {
        /// <summary>
        /// Значение
        /// </summary>
        public TypeVerification TypeVerification { get; set; }
        /// <summary>
        /// Описание
        /// </summary>
        public string Description { get; set; }
    }
}
