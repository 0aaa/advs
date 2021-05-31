﻿using VerificationAirVelocitySensor.ViewModel.BaseVm;
using VerificationAirVelocitySensor.ViewModel.Services;

namespace VerificationAirVelocitySensor.Model
{
    public class SettingsModel : BaseVm
    {
        //Com порты
        public string ComPortFrequencyMotor { get; set; }
        public string ComPortFrequencyCounter { get; set; }

        //Настройки частотомера
        public FrequencyChannel FrequencyChannel { get; set; }
        public GateTime GateTime { get; set; }
        public bool FilterChannel1 { get; set; }
        public bool FilterChannel2 { get; set; }


        //Режим ручной настройки ПКЛ73
        public int SetFrequencyMotor { get; set; }
    }
}
    