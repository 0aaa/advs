using System;
using System.Collections.Generic;
using System.Windows;
using VerificationAirVelocitySensor.Model;
using VerificationAirVelocitySensor.ViewModel.Services;

namespace VerificationAirVelocitySensor.ViewModel.DvsVm
{
    public class Dvs01Vm
    {
        /// <summary>
        /// Значения скорости на которых нужно считать значения датчика.
        /// </summary>
        private readonly List<decimal> _controlPointSpeed = new List<decimal>
            {5, 10, 15, 20, 25};

        private const int CountValueOnAverage = 3;

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

            //Первый прогон: 5, 10, 15, 20, 25 м / с,
            //снимается по одному значению, скорость эталона считается по среднему 3х измерений,
            //можно сделать, чтоб записывались результаты при каждом прогоне.
            //второй и третий прогоны начинается с 5 м / с и те же точки

            //ResultToCsv(collectionValue);
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