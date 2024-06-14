using System;

namespace VerificationAirVelocitySensor.Model.Lib
{
    public class ReferenceUpdateEventArgs : EventArgs
    {
        public double ReferenceValue { get; set; }// Эталонное значение скорости на анемометре.
    }
}