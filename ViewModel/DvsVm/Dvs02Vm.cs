using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using VerificationAirVelocitySensor.Model;
using VerificationAirVelocitySensor.ViewModel.Services;

namespace VerificationAirVelocitySensor.ViewModel.DvsVm
{
    public class Dvs02Vm
    {
        /// <summary>
        /// Значения скорости на которых нужно считать значения датчика.
        /// </summary>
        private readonly List<ControlPointSpeedToFrequency> _controlPointSpeed = new List<ControlPointSpeedToFrequency>
        {
            new ControlPointSpeedToFrequency(0.7m, 445),
            new ControlPointSpeedToFrequency(5, 2505),
            new ControlPointSpeedToFrequency(10, 4745),
            new ControlPointSpeedToFrequency(15, 7140),
            new ControlPointSpeedToFrequency(20, 9480),
            new ControlPointSpeedToFrequency(25, 12015),
            new ControlPointSpeedToFrequency(30, 15200),
        };

        private const int CountValueOnAverage = 6;

        public void StartTest(GateTime gateTime)
        {
            var collectionValue = new List<DvsValue>();

            foreach (var point in _controlPointSpeed)
            {
                var value = new DvsValue(point.Speed);

                FrequencyMotorDevice.Instance.SetFrequency(point.SetFrequency);
                FrequencyMotorDevice.Instance.CorrectionSpeedMotor();

                //TODO Думаю необходимо проверять скорость трубы перед каждым съемом значения.
                while (value.CollectionCount != CountValueOnAverage)
                {
                    var hzValue = FrequencyCounterDevice.Instance.GetCurrentHzValue();
                    value.AddValueInCollection(hzValue);


                    Thread.Sleep(GateTimeToMSec(gateTime) + 1000);

                    FrequencyMotorDevice.Instance.CorrectionSpeedMotor(false);
                }

                collectionValue.Add(value);
            }

            ResultToCsv(collectionValue);
        }

        private void ResultToCsv(List<DvsValue> resultValues)
        {
            //TODO Здесь будут создаваться csv результаты
        }

        private int GateTimeToMSec(GateTime gateTime)
        {
            switch (gateTime)
            {
                case GateTime.S1:
                    return 1000;
                case GateTime.S4:
                    return 4000;
                case GateTime.S7:
                    return 7000;
                case GateTime.S10:
                    return 10000;
                case GateTime.S100:
                    return 100000;
                default:
                    throw new ArgumentOutOfRangeException(nameof(gateTime), gateTime, null);
            }
        }
    }

    public class ControlPointSpeedToFrequency
    {
        public ControlPointSpeedToFrequency(decimal speed, decimal setFrequency)
        {
            Speed = speed;
            SetFrequency = setFrequency;
        }

        public decimal Speed { get; set; }
        public decimal SetFrequency { get; set; }
    }
}