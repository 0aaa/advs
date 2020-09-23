using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        private void WriteCommandAsync(string command, int sleepTime = 1000)
        {
            //Task.Run(async () =>
            //{
            //    try
            //    {
            //        await Task.Run(() =>
            //        {
            //            _serialPort.Write(command + "\r\n");
            //            Thread.Sleep(sleepTime);
            //        });
            //    }
            //    catch
            //    {
            //        throw new Exception($"Error Command ({command})");
            //    }
            //});
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
            WriteCommandAsync("FETC?");

            Thread.Sleep(100);
            var data = _serialPort.ReadExisting();

            if (string.IsNullOrEmpty(data))
                return 0;

            var substringValue = data.Substring(1, 7).Replace("." , ",");
            var value = Decimal.Parse(substringValue);

            var mathValue = Math.Round(value, 3);

            return mathValue;
        }

        /// <summary>
        /// Запрос версии
        /// </summary>
        /// <param name="sleepTime"></param>
        public string GetModelVersion(int sleepTime = 1000)
        {
            WriteCommandAsync("*IDN?");

            //TODO Здесь должен быть возврат версии.
            return null;
        }

        /// <summary>
        /// Установка времени опроса частотомером
        /// </summary>
        /// <param name="gateTime"></param>
        public void SetGateTime(GateTime gateTime)
        {
            WriteCommandAsync($":ARM:TIMer {(int) gateTime} S");
        }

        /// <summary>
        /// Устанавливает выбранный канал для считывания значения частоты.
        /// Доступных каналы : 1, 2, 3
        /// </summary>
        public void SetChannelFrequency(FrequencyChannel frequencyChannel)
        {
            WriteCommandAsync($":FUNCtion FREQuency {(int) frequencyChannel}");
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