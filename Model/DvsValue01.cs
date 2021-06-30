﻿using VerificationAirVelocitySensor.ViewModel.BaseVm;

namespace VerificationAirVelocitySensor.Model
{
    public class DvsValue01 : BaseVm
    {
        public DvsValue01(decimal speedValue)
        {
            SpeedValue = speedValue;
        }

        /// <summary>
        /// Скорость потока воздуха на которой снимается значение
        /// </summary>
        public decimal SpeedValue { get; }

        /// <summary>
        /// Снимаемое значение скорости с эталона
        /// </summary>
        public decimal? ReferenceSpeedValue { get; set; }

        public SpeedValue DeviceSpeedValue1 { get; set; } = new SpeedValue();
        public SpeedValue DeviceSpeedValue2 { get; set; } = new SpeedValue();
        public SpeedValue DeviceSpeedValue3 { get; set; } = new SpeedValue();
    }
}
