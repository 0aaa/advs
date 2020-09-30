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
        private readonly List<decimal> _controlPointSpeed = new List<decimal>
            {0.7m, 5, 10, 15, 20, 25, 30};

        private const int CountValueOnAverage = 6;

        public void StartTest(GateTime gateTime)
        {
            if (!FrequencyMotorDevice.Instance.IsOpen())
            {
                MessageBox.Show("Порт частотного двигателя закрыт", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            if (!FrequencyCounterDevice.Instance.IsOpen())
            {
                MessageBox.Show("Порт частотомера закрыт", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var collectionValue = new List<DvsValue>();

            foreach (var speed in _controlPointSpeed)
            {
                var value = new DvsValue(speed);

                FrequencyMotorDevice.Instance.SetFrequency(speed);
                FrequencyMotorDevice.Instance.CorrectionSpeedMotor();


                while (value.CollectionCount != CountValueOnAverage)
                {
                    var hzValue = FrequencyCounterDevice.Instance.GetCurrentHzValue();
                    value.AddValueInCollection(hzValue);

                    Thread.Sleep(GateTimeToMSec(gateTime) + 250 /*Небольшая страховка*/);
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
}