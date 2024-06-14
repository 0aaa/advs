using System;

namespace VerificationAirVelocitySensor.Models.ClassLib
{
    internal class CounterOpeningEventArgs : EventArgs// Событие открытия или закрытия порта частотомера.
    {
        public bool IsOpen { get; set; }
    }
}