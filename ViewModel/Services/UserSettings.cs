﻿
using System.Collections.ObjectModel;

namespace VerificationAirVelocitySensor.ViewModel.Services
{
    public class UserSettings
    {
        public bool FilterChannel1 { get; set; } 
        public bool FilterChannel2 { get; set; } 
        public FrequencyChannel FrequencyChannel { get; set; } = FrequencyChannel.Channel1;
        public GateTime GateTime { get; set; } = GateTime.S1;
        public  string ComPortFrequencyMotor { get; set; } = string.Empty;
        public  string ComPortFrequencyCounter { get; set; } = string.Empty;
        public TypeTest TypeTest { get; set; }
        public ObservableCollection<ControlPointSpeedToFrequency> ControlPointSpeed { get; set; } = new ObservableCollection<ControlPointSpeedToFrequency>();
    }
}