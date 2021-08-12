﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Text;
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


        private FrequencyCounterDevice()
        {
        }

        private static FrequencyCounterDevice _instance;

        public static FrequencyCounterDevice Instance =>
            _instance ?? (_instance = new FrequencyCounterDevice());

        public bool IsOpen() => _serialPort != null && _serialPort.IsOpen;


        public Action StopTest;


        #region Open , Close

        public void OpenPort(string comPort, int timeOut)
        {
            try
            {
                _comPort = comPort;
                _serialPort = new SerialPort(_comPort, BaudRate) {ReadTimeout = 7000, WriteTimeout = 7000};
                _serialPort.Open();

                IsOpenUpdateMethod(_serialPort.IsOpen);
            }
            catch (Exception e)
            {
                _serialPort?.Close();
                MessageBox.Show($"{e.Message}", "Ошибка открытия порта Частотомера", MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        /// <param name="sleepTime">Устройство очень долго думает. 2сек, это гарантие того, что при старте программы все настройки будут отправлены</param>
        private void WriteCommand(string command, int sleepTime = 2000)
        {
            _serialPort.WriteLine(command);
            Thread.Sleep(sleepTime);
        }


        /// <summary>
        /// Reset device
        /// </summary>
        public void RstCommand()
        {
            WriteCommand("*RST");
        }

        /// <summary>
        /// Вкыл, выкл фильтра
        /// </summary>
        /// <param name="channel">1 , 2 , 3 only</param>
        /// <param name="isOn"></param>
        /// <param name="sleepTime"></param>
        public void SwitchFilter(int channel, bool isOn, int sleepTime = 2000)
        {
            if (channel != 1 && channel != 2 && channel != 3)
                throw new ArgumentOutOfRangeException();

            var stringIsOn = isOn
                ? "ON"
                : "OFF";

            var command = $":INPut{channel}:FILTer {stringIsOn}";

            WriteCommand(command, sleepTime);
        }

        private bool IsCancellationRequested(CancellationTokenSource ctSource) =>
            ctSource.Token.IsCancellationRequested;


        public decimal GetCurrentHzValue(SpeedPoint speedPoint, int whileWait, CancellationTokenSource ctsTask)
        {
            var attemptCountSendRequest = 0;
            const int maxCountAttempt = 3;
            const string command = "FETC?";
            const int countReadByte = 18;

            while (true)
            {
                if (IsCancellationRequested(ctsTask)) return 0;

                try
                {
                    //1.Clear buffer
                    // ReSharper disable once AssignmentIsFullyDiscarded
                    _ = _serialPort.ReadExisting();

                    attemptCountSendRequest++;
                    if (attemptCountSendRequest >= maxCountAttempt)
                        throw new Exception("Превышено кол-во попыток чтения значения частоты с частотомера");

                    WriteCommand(command, 500);

                    //3 Read
                    var bytesList = new List<byte>();
                    var attemptRead = 0;
                    //Для скорости 0.7
                    var maxAttemptRead07 = 5;
                    //Для остальных скоростей
                    var maxAttemptReadAll = 2;

                    while (true)
                    {
                        try
                        {
                            var readByte = (byte) _serialPort.ReadByte();
                            if (IsCancellationRequested(ctsTask)) return 0;

                            bytesList.Add(readByte);

                            if (bytesList.Count == countReadByte)
                            {
                                break;
                            }
                        }
                        catch 
                        {
                            attemptRead++;

                            if (speedPoint.Speed == 0.7m)
                            {
                                if (attemptRead >= maxAttemptRead07)
                                {
                                    attemptRead = 0;
                                    var messageBoxResult = MessageBox.Show(
                                        "Проверьте вращается ли датчик на текущей скорости и нажмите Ок, что бы повторить попытку. Или Отмена, что бы завершить поверку.",
                                        "Ошибка чтения данных с частотомера", MessageBoxButton.OKCancel,
                                        MessageBoxImage.Error,
                                        MessageBoxResult.Cancel);

                                    if (messageBoxResult != MessageBoxResult.Cancel) continue;

                                    StopTest();
                                    if (IsCancellationRequested(ctsTask)) return 0;
                                }

                                continue;
                            }


                            if (attemptRead >= maxAttemptReadAll)
                            {
                                StopTest();
                            }

                            WriteCommand(command, 500);
                        }
                    }

                    var byteArray = bytesList.ToArray();

                    var data = Encoding.UTF8.GetString(byteArray);

                    //5.Calibration
                    data = data.Replace("\r", "").Replace("\n", "").Replace(" ", "");

                    var value = decimal.Parse(data, NumberStyles.Float, CultureInfo.InvariantCulture);

                    var mathValue = Math.Round(value, 3);
                    //6.Validation
                    var isValidation = ValidationHzValue(mathValue, speedPoint);

                    if (isValidation)
                        return mathValue;

                    //"Невалидное значение частоты полученное с частотометра"
                }
                catch (Exception exception)
                {
                    if (attemptCountSendRequest >= maxCountAttempt)
                        throw new Exception("Превышено кол-во попыток запроса частоты с частотомера", exception);

                    Thread.Sleep(whileWait);
                }
            }
        }

        /// <summary>
        /// Метод для проверки полученного значения на вброс ( баги со стороны частотометра)
        /// </summary>
        /// <param name="value"></param>
        /// <param name="speedPoint"></param>
        /// <returns></returns>
        private bool ValidationHzValue(decimal value, SpeedPoint speedPoint)
            => value >= speedPoint.MinEdge && value <= speedPoint.MaxEdge;

        /// <summary>
        /// Запрос версии
        /// </summary>
        /// <param name="sleepTime"></param>
        public string GetModelVersion(int sleepTime = 1000)
        {
            //TODO сделать возвращаемый тип bool. Поместить в метод проверку на валидность устройства.
            WriteCommand("*IDN?", sleepTime);

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
        /// <param name="sleepTime"></param>
        public void SetGateTime(GateTime gateTime, int sleepTime = 2000)
        {
            WriteCommand($":ARM:TIMer {(int) gateTime} S", sleepTime);
        }

        /// <summary>
        /// Устанавливает выбранный канал для считывания значения частоты.
        /// Доступных каналы : 1, 2, 3
        /// </summary>
        public void SetChannelFrequency(FrequencyChannel frequencyChannel, int sleepTime = 2000)
        {
            WriteCommand($":FUNCtion FREQuency {(int) frequencyChannel}", sleepTime);
        }


        public int GateTimeToMSec(GateTime gateTime)
        {
            switch (gateTime)
            {
                //case GateTime.S1:
                //    return 1000;
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

    public class FrequencyChannelDescription
    {
        public FrequencyChannelDescription(FrequencyChannel frequencyChannel, string description)
        {
            FrequencyChannel = frequencyChannel;
            Description = description;
        }

        public FrequencyChannel FrequencyChannel { get; }
        public string Description { get; }
    }

    /// <summary>
    /// Период опроса частотомера в секундах
    /// </summary>
    public enum GateTime
    {
        //S1 = 1,
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