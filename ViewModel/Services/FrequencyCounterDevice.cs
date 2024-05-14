using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows;

namespace VerificationAirVelocitySensor.ViewModel.Services
{
    /// <summary>Управление частотомером</summary>
    internal class FrequencyCounterDevice
    {
        private const int BAUD_RATE = 9600;
        private static FrequencyCounterDevice _instance;
        private SerialPort _serialPort;
        private string _comPort;
        #region EventHandler Open/Close Port
        internal event EventHandler<IsOpenFrequencyCounterEventArgs> IsOpenUpdate;
        #endregion
        public static FrequencyCounterDevice Instance => _instance ?? (_instance = new FrequencyCounterDevice());
        public Action StopTest { get; set; }

        /// <summary></summary><param name="command"></param><param name="sleepTime">Устройство очень долго думает. 2 сек, это гарантия того, что при старте программы все настройки будут отправлены</param>
        private void WriteCommand(string command, int sleepTime = 2000)
        {
            _serialPort.WriteLine(command);
            Thread.Sleep(sleepTime);
        }

        public bool IsOpen() => _serialPort != null && _serialPort.IsOpen;

        #region Open , Close
        public void OpenPort(string comPort, int timeOut)
        {
            try
            {
                _comPort = comPort;
                _serialPort = new SerialPort(_comPort, BAUD_RATE) { ReadTimeout = 7000, WriteTimeout = 7000 };
                _serialPort.Open();
				IsOpenUpdate?.Invoke(this, new IsOpenFrequencyCounterEventArgs { IsOpen = _serialPort.IsOpen });
            }
            catch (Exception e)
            {
                _serialPort?.Close();
                MessageBox.Show(e.Message, "Ошибка открытия порта Частотомера", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ClosePort()
        {
            _serialPort.Close();
            _serialPort.Dispose();
            if (_serialPort == null)
            {
                IsOpenUpdate?.Invoke(this, new IsOpenFrequencyCounterEventArgs { IsOpen = false });
                return;
            }
			IsOpenUpdate?.Invoke(this, new IsOpenFrequencyCounterEventArgs { IsOpen = _serialPort.IsOpen });
        }
        #endregion

        /// <summary>Reset device</summary>
        public void RstCommand()
        {
            WriteCommand("*RST");
        }

        /// <summary>Вкыл, выкл фильтра</summary><param name="channel">1 , 2 , 3 only</param><param name="isOn"></param><param name="sleepTime"></param>
        public void SwitchFilter(int channel, bool isOn, int sleepTime = 2000)
        {
            if (channel != 1 && channel != 2 && channel != 3)
			{
                throw new ArgumentOutOfRangeException();
			}
            WriteCommand($":INPut{channel}:FILTer {(isOn ? "ON" : "OFF")}", sleepTime);
        }

        public decimal GetCurrentHzValue(SpeedPoint speedPoint, int whileWait, CancellationTokenSource ctsTask)
        {
            var attemptCountSendRequest = 0;
            const string COMMAND = "FETC?";
            const int MAX_ATTEMPTS = 3;
            const int CNT_READ_BYTE = 18;
            while (true)
            {
				if (ctsTask.Token.IsCancellationRequested)
				{
					return 0;
				}
                try
                {
                    _serialPort.ReadExisting();// 1. Clear buffer.
					attemptCountSendRequest++;
                    if (attemptCountSendRequest >= MAX_ATTEMPTS)
					{
                        throw new Exception("Превышено кол-во попыток чтения значения частоты с частотомера");
					}
                    WriteCommand(COMMAND, 500);
                    var bytesList = new List<byte>();
                    var attemptRead = 0;
					while (true)// 3. Read.
					{
                        try
                        {
							if (ctsTask.Token.IsCancellationRequested)
							{
								return 0;
							}
                            bytesList.Add((byte)_serialPort.ReadByte());
                            if (bytesList.Count == CNT_READ_BYTE)
                            {
                                break;
                            }
                        }
                        catch 
                        {
                            attemptRead++;
                            if (speedPoint.Speed == 0.7m)
                            {
                                if (attemptRead >= 5)// Для скорости 0.7.
								{
                                    attemptRead = 0;
									if (MessageBoxResult.Cancel != MessageBox.Show("Проверьте, вращается ли датчик на текущей скорости и нажмите \"OK\", чтобы повторить попытку. Или \"Отмена\", чтобы завершить поверку.",
										"Ошибка чтения данных с частотомера", MessageBoxButton.OKCancel, MessageBoxImage.Error, MessageBoxResult.Cancel))
									{
										continue;
									}
                                    StopTest();
									if (ctsTask.Token.IsCancellationRequested)
									{
										return 0;
									}
                                }
                                continue;
                            }
                            if (attemptRead >= 2)// Для остальных скоростей.
							{
                                StopTest();
                            }
                            WriteCommand(COMMAND, 500);
                        }
                    }
                    // 5.Calibration.
                    var mathValue = Math.Round(decimal.Parse(Encoding.UTF8.GetString(bytesList.ToArray()).Replace("\r", "").Replace("\n", "").Replace(" ", ""), NumberStyles.Float, CultureInfo.InvariantCulture), 3);
        /// <summary>Метод для проверки полученного значения на вброс (баги со стороны частотометра)</summary><param name="value"></param><param name="speedPoint"></param>
                    if (mathValue >= speedPoint.MinEdge && mathValue <= speedPoint.MaxEdge)// 6. Validation.
					{
                        return mathValue;
					}
                    // "Невалидное значение частоты полученное с частотометра".
                }
                catch (Exception exception)
                {
                    if (attemptCountSendRequest >= MAX_ATTEMPTS)
					{
                        throw new Exception("Превышено кол-во попыток запроса частоты с частотомера", exception);
					}
                    Thread.Sleep(whileWait);
                }
            }
        }

        /// <summary>Запрос версии</summary><param name="sleepTime"></param>
        public string GetModelVersion(int sleepTime = 1000)
        {
            // TODO сделать возвращаемый тип bool. Поместить в метод проверку на валидность устройства.
            WriteCommand("*IDN?", sleepTime);
            Thread.Sleep(100);
            var data = _serialPort.ReadExisting();
            return string.IsNullOrEmpty(data) ? "Error" : data;
        }

        /// <summary>Установка времени опроса частотомером</summary><param name="gateTime"></param><param name="sleepTime"></param>
        public void SetGateTime(GateTime gateTime, int sleepTime = 2000)
        {
            WriteCommand($":ARM:TIMer {(int) gateTime} S", sleepTime);
        }

        /// <summary>Устанавливает выбранный канал для считывания значения частоты. Доступные каналы : 1, 2, 3</summary>
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

    internal class FrequencyChannelDescription
    {
        public FrequencyChannel FrequencyChannel { get; }
        public string Description { get; }

        public FrequencyChannelDescription(FrequencyChannel frequencyChannel, string description)
        {
            FrequencyChannel = frequencyChannel;
            Description = description;
        }
    }

    /// <summary>Класс для создания коллекции доступных enum GateTime для биндинга на интерфейс.</summary>
    internal class GateTimeDescription
    {
        public GateTime GateTime { get; }
        public string Description { get; }

        public GateTimeDescription(GateTime gateTime, string description)
        {
            GateTime = gateTime;
            Description = description;
        }
    }

    /// <summary>Событие открытия или закрытие порта частотомера</summary>
    internal class IsOpenFrequencyCounterEventArgs : EventArgs
    {
        public bool IsOpen { get; set; }
    }

    /// <summary>Каналы частотометра 1 и 2. 3-ий использоваться не планируется.</summary>
    internal enum FrequencyChannel
    {
        Channel1 = 1,
        Channel2 = 2,
        Channel3 = 3
    }

    /// <summary>Период опроса частотомера в секундах</summary>
    internal enum GateTime
    {
        //S1 = 1,
        S4 = 4,
        S7 = 7,
        S10 = 10,
        S100 = 100
    }
}