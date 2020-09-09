using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace VerificationAirVelocitySensor.ViewModel.Services
{
    /// <summary>
    /// Управление частотомером
    /// </summary>
    public class FrequencyCounterDevice
    {
        private SerialPort _serialPort;
        public event EventHandler<DataReadEventArgs> DataReadUpdate;

        private string _comPort;
        private const int BaudRate = 9600;
        private string _dataRead;

        public List<GateTimeDescription> GateTimeList { get; } = new List<GateTimeDescription>
        {
            new GateTimeDescription(GateTime.S1,"1 сек"),
            new GateTimeDescription(GateTime.S4,"4 сек"),
            new GateTimeDescription(GateTime.S7,"7 сек"),
            new GateTimeDescription(GateTime.S10,"10 сек"),
            new GateTimeDescription(GateTime.S100,"100 сек"),
        };


        private FrequencyCounterDevice()
        {
        }

        private static FrequencyCounterDevice _instance;

        public static FrequencyCounterDevice Instance =>
            _instance ?? (_instance = new FrequencyCounterDevice());


        #region Open , Close , DataReceived

        /// <summary>
        /// Открывает или закрывает порт, в зависимости от его текущего состояния.
        /// </summary>
        /// <param name="comPort"></param>
        public void OpenClose(string comPort)
        {
            if (_serialPort.IsOpen)
            {
                ClosePort();
            }
            else
            {
                OpenPort(comPort);
            }
        }

        private void OpenPort(string comPort)
        {
            _comPort = comPort;
            _serialPort = new SerialPort(comPort, BaudRate);
            _serialPort.DataReceived += _serialPort_DataReceived;
            _serialPort.Open();
        }

        private void ClosePort()
        {
            _serialPort.DataReceived -= _serialPort_DataReceived;
            _serialPort.Close();
            _serialPort.Dispose();
        }

        private void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var sp = (SerialPort) sender;
            var indate = sp.ReadExisting();
            _dataRead = indate;

            DataReadUpdate?.Invoke(this, new DataReadEventArgs
            {
                DataRead = indate
            });
        }

        #endregion

        public void WriteCommandAsync(string command, int sleepTime = 1000)
        {
            Task.Run(async () => await Task.Run(() =>
            {
                _serialPort.Write(command);
                Thread.Sleep(sleepTime);
            }));
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
            if (channel != 1 || channel != 2 || channel != 3)
                throw new ArgumentOutOfRangeException();

            var stringIsOn = isOn
                ? "ON"
                : "OFF";

            WriteCommandAsync($":INPut{channel}:FILTer {stringIsOn}", sleepTime);
        }

        /// <summary>
        /// Запрос на значение частоты
        /// </summary>
        /// <param name="sleepTime"></param>
        public void GetCurrentHzValue(int sleepTime = 1000)
        {
            WriteCommandAsync("FETC?");
        }

        /// <summary>
        /// Запрос версии
        /// </summary>
        /// <param name="sleepTime"></param>
        public void GetModelVersion(int sleepTime = 1000)
        {
            WriteCommandAsync("*IDN?");
        }

        /// <summary>
        /// Установка времени опроса частотомером
        /// </summary>
        /// <param name="gateTime"></param>
        public void SetGateTime(GateTime gateTime)
        {
            WriteCommandAsync($":ARM:TIMer {(int)gateTime} S");
        }

        //Проверить работоспособность. Предположительно меняет измеряемый канал .
        //13.	To mearsure frequency(измерить частоту)
        //[:SENSe]:FUNCtion[:ON] FREQuency[1 | 2 | 3]

    }


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
        public GateTimeDescription(GateTime gateTime , string description)
        {
            GateTime = gateTime;
            Description = description;
        }

        public GateTime GateTime { get; }
        public string Description { get;}
    }

    public class DataReadEventArgs : EventArgs
    {
        public string DataRead { get; set; }
    }
}