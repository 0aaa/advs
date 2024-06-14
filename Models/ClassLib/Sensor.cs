using VerificationAirVelocitySensor.Model.EnumLib;

namespace VerificationAirVelocitySensor.Models.ClassLib
{
    internal class Sensor(SensorModel typeTest, string description)
    {
        public SensorModel TypeTest { get; set; } = typeTest;
        public string Description { get; set; } = description;
    }
}