namespace VerificationAirVelocitySensor.Models
{
    internal class Conditions : ViewModels.Base.BaseVm
    {
        public Enums.Rev Revis { get; set; }// Настройка на будущее, тип поверки.
        public string Snum { get; set; }// Серийный номер.
        public string T { get; set; }// Температура.
        public string H { get; set; }// Влажность.
        public string P { get; set; }// Давление.
        public string Verifier { get; set; }// Поверитель, в месте использования будет сделана коллекция имен или типа того.
    }
}