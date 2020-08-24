using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace VerificationAirVelocitySensor.ViewModel.Services
{
    public class FrequencyDevice
    {
        private SerialPort _serialPort;
        public event EventHandler<DataReadEventArgs> DataReadUpdate;

        private string _comPort;
        private const int BaudRate = 9600;
        private string _dataRead;


        private FrequencyDevice()
        {
        }

        private static FrequencyDevice _instance;

        public static FrequencyDevice Instance =>
            _instance ?? (_instance = new FrequencyDevice());


        #region Open , Close , DataReceived

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
    }

    public class DataReadEventArgs : EventArgs
    {
        public string DataRead { get; set; }
    }
}