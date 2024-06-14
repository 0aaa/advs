using VerificationAirVelocitySensor.Model.EnumLib;

namespace VerificationAirVelocitySensor.Models.ClassLib
{
    internal class GateTime(GateTimeSec gateTime, string description)// Класс для создания коллекции доступных enum GateTime для биндинга на UI.
    {
        public GateTimeSec GateTimeSec { get; } = gateTime;
        public string Description { get; } = description;
    }
}