using VerificationAirVelocitySensor.Model.EnumLib;

namespace VerificationAirVelocitySensor.Models.ClassLib
{
    internal class Lpf(LpfChannel frequencyChannel, string description)
    {
        public LpfChannel FrequencyChannel { get; } = frequencyChannel;
        public string Description { get; } = description;
    }
}