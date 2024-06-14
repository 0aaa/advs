using System;

namespace VerificationAirVelocitySensor.Model.Lib
{
    public class TubeOpeningEventArgs : EventArgs// Событие открытия или закрытия порта частотного двигателя.
    {
        public bool IsOpen { get; set; }
    }
}