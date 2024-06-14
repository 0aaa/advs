using VerificationAirVelocitySensor.Model.EnumLib;

namespace VerificationAirVelocitySensor.Model.Lib
{
    internal class RevisionType// Класс для биндинга коллекции на чекбокс.
    {
        public RevisionIteration Iteration { get; set; }// Значение.
        public string Description { get; set; }// Описание.
    }
}