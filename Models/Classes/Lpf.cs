namespace VerificationAirVelocitySensor.Models.Classes
{
    internal class Lpf(Enums.LpfCh c, string d)
    {
        public Enums.LpfCh LpfCh { get; } = c;
        public string Descr { get; } = d;
    }
}