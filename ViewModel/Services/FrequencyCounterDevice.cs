using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Windows;

namespace VerificationAirVelocitySensor.ViewModel.Services
{
    /// <summary>
    /// Управление частотомером
    /// </summary>
    public class FrequencyCounterDevice
    {
        private SerialPort _serialPort;
        private string _comPort;
        private const int BaudRate = 9600;

        #region EventHandler Open/Close Port

        public event EventHandler<IsOpenFrequencyCounterEventArgs> IsOpenUpdate;

        private void IsOpenUpdateMethod(bool isOpen)
        {
            IsOpenUpdate?.Invoke(this, new IsOpenFrequencyCounterEventArgs
            {
                IsOpen = isOpen
            });
        }

        #endregion


        public List<GateTimeDescription> GateTimeList { get; } = new List<GateTimeDescription>
        {
            new GateTimeDescription(GateTime.S1, "1 сек"),
            new GateTimeDescription(GateTime.S4, "4 сек"),
            new GateTimeDescription(GateTime.S7, "7 сек"),
            new GateTimeDescription(GateTime.S10, "10 сек"),
            new GateTimeDescription(GateTime.S100, "100 сек"),
        };


        private FrequencyCounterDevice()
        {
        }

        private static FrequencyCounterDevice _instance;

        public static FrequencyCounterDevice Instance =>
            _instance ?? (_instance = new FrequencyCounterDevice());

        public bool IsOpen() => _serialPort != null && _serialPort.IsOpen;


        #region Open , Close 

        public void OpenPort(string comPort)
        {
            try
            {
                _comPort = comPort;
                _serialPort = new SerialPort(_comPort, BaudRate);
                _serialPort.Open();

                IsOpenUpdateMethod(_serialPort.IsOpen);
            }
            catch (Exception e)
            {
                MessageBox.Show($"{e.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ClosePort()
        {
            _serialPort.Close();
            _serialPort.Dispose();

            if (_serialPort == null)
            {
                IsOpenUpdateMethod(false);
                return;
            }


            IsOpenUpdateMethod(_serialPort.IsOpen);
        }

        #endregion

        private void WriteCommandAsync(string command, int sleepTime = 2000)
        {
            _serialPort.WriteLine(command);
            Thread.Sleep(sleepTime);
        }

        /// <summary>
        /// Reset device
        /// </summary>
        public void RstCommand()
        {
            WriteCommandAsync("*RST");
        }

        /// <summary>
        /// Вкыл, выкл фильтра
        /// </summary>
        /// <param name="channel">1 , 2 , 3 only</param>
        /// <param name="isOn"></param>
        /// <param name="sleepTime"></param>
        public void SwitchFilter(int channel, bool isOn, int sleepTime = 1000)
        {
            if (channel != 1 && channel != 2 && channel != 3)
                throw new ArgumentOutOfRangeException();

            var stringIsOn = isOn
                ? "ON"
                : "OFF";

            var command = $":INPut{channel}:FILTer {stringIsOn}";

            WriteCommandAsync(command, sleepTime);
        }

        /// <summary>
        /// Запрос на значение частоты
        /// </summary>
        /// <param name="sleepTime"></param>
        public decimal GetCurrentHzValue(int sleepTime = 1000)
        {

            while (true)
            {
                try
                {
                    _ = _serialPort.ReadExisting();

                    WriteCommandAsync("FETC?", sleepTime);

                    Thread.Sleep(500);
                    var data = _serialPort.ReadExisting();


                    if (string.IsNullOrEmpty(data))
                        continue;

                    data = data.Replace("\r", "").Replace("\n", "").Replace(" ", "");

                    if (data[0] == '-' || data[0] == '+')
                    {
                        data = data.Remove(0, 1);
                        var value = decimal.Parse(data, NumberStyles.Float | NumberStyles.AllowExponent, CultureInfo.InvariantCulture);

                        var mathValue = Math.Round(value, 3);

                        if (mathValue == 0)
                            continue;

                        return mathValue;
                    }
                    else
                    {
                        var value = decimal.Parse(data);
                        var mathValue = Math.Round(value, 3);


                        if (mathValue == 0)
                            continue;

                        return mathValue;
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }


        public decimal GetCurrentHzValueAverage()
        {
            var averageCollection = new List<decimal>();

            while (averageCollection.Count != 5)
            {
                var value = GetCurrentHzValue();
                averageCollection.Add(value);
            }

            var average = Math.Round(averageCollection.Average(), 2);
            return average;
        }

        /// <summary>
        /// Запрос версии
        /// </summary>
        /// <param name="sleepTime"></param>
        public string GetModelVersion(int sleepTime = 1000)
        {
            WriteCommandAsync("*IDN?");

            Thread.Sleep(100);
            var data = _serialPort.ReadExisting();

            return string.IsNullOrEmpty(data)
                ? "Error"
                : data;
        }

        /// <summary>
        /// Установка времени опроса частотомером
        /// </summary>
        /// <param name="gateTime"></param>
        public void SetGateTime(GateTime gateTime)
        {
            WriteCommandAsync($":ARM:TIMer {(int)gateTime} S");
        }

        /// <summary>
        /// Устанавливает выбранный канал для считывания значения частоты.
        /// Доступных каналы : 1, 2, 3
        /// </summary>
        public void SetChannelFrequency(FrequencyChannel frequencyChannel)
        {
            WriteCommandAsync($":FUNCtion FREQuency {(int)frequencyChannel}");
        }




        public int GateTimeToMSec(GateTime gateTime)
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

    /// <summary>
    /// Каналы частотометра 1 и 2.
    /// 3-ий использоваться не планируется.
    /// </summary>
    public enum FrequencyChannel
    {
        Channel1 = 1,
        Channel2 = 2,
        Channel3 = 3
    }

    /// <summary>
    /// Период опроса частотомера в секундах
    /// </summary>
    public enum GateTime
    {
        S1 = 1,
        S4 = 4,
        S7 = 7,
        S10 = 10,
        S100 = 100
    }

    /// <summary>
    /// Класс для создания коллекции доступных enum GateTime  для биндинга на интерфейс.
    /// </summary>
    public class GateTimeDescription
    {
        public GateTimeDescription(GateTime gateTime, string description)
        {
            GateTime = gateTime;
            Description = description;
        }

        public GateTime GateTime { get; }
        public string Description { get; }
    }

    /// <summary>
    /// Событие открытия или закрытие порта частотомера
    /// </summary>
    public class IsOpenFrequencyCounterEventArgs : EventArgs
    {
        public bool IsOpen { get; set; }
    }
}