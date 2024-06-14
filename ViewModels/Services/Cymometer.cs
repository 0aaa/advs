using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows;
using VerificationAirVelocitySensor.Model.EnumLib;
using VerificationAirVelocitySensor.Models.ClassLib;

namespace VerificationAirVelocitySensor.ViewModels.Services
{
    internal class Cymometer// Управление частотомером.
    {
        private const int B_RATE = 9600;
		private const string ID_FRE_COUNTER = "43-85/6";
		private static Cymometer _instance;
        private SerialPort _com;
        public static Cymometer Instance => _instance ??= new Cymometer();
        public Action Stop { get; set; }
        internal event EventHandler<CounterOpeningEventArgs> IsOpenUpdate;

        /// <param name="cmd"></param><param name="latency">Устройство очень долго думает. 2 сек. - это гарантия того, что при старте app все настройки будут отправлены</param>
        public void Write(string cmd, int latency = 2000)
        {
            _com.WriteLine(cmd);
            Thread.Sleep(latency);
        }

		public string Read()
		{
			return _com.ReadTo(">");
		}

        public bool IsOpen()
			=> _com != null && _com.IsOpen;

        #region Open, Close.
        public bool Open(string portName, int timeout)
        {
            try
            {
                _com = new SerialPort(portName, B_RATE) { ReadTimeout = timeout, WriteTimeout = timeout };
                _com.Open();
				IsOpenUpdate?.Invoke(this, new CounterOpeningEventArgs { IsOpen = _com.IsOpen });
				return true;
            }
            catch (Exception e)
            {
                _com?.Close();
                MessageBox.Show($"Ошибка открытия порта частотомера. {e.Message}", "Ошибка открытия порта частотомера", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
            }
        }

        public void Close()
        {
            if (_com == null)
            {
                IsOpenUpdate?.Invoke(this, new CounterOpeningEventArgs { IsOpen = false });
                return;
            }
            _com.Close();
            _com.Dispose();
			IsOpenUpdate?.Invoke(this, new CounterOpeningEventArgs { IsOpen = _com.IsOpen });
        }
        #endregion

        public void Reset()// Reset device.
        {
            Write("*RST");
        }

        /// <summary>Вкл., выкл. фильтра.</summary><param name="ch">1, 2, 3 only</param><param name="isOn"></param><param name="latency"></param>
        public void SwitchFilter(int ch, bool isOn, int latency = 2000)
        {
            if (ch != 1 && ch != 2 && ch != 3)
			{
                throw new ArgumentOutOfRangeException(nameof(ch));
			}
            Write($":INPut{ch}:FILTer {(isOn ? "ON" : "OFF")}", latency);
        }

        public decimal GetCurrentHz(Checkpoint cp, int latency, CancellationTokenSource ctsTask)
        {
            const string COMMAND = "FETC?";
            const int MAX_ATTEMPTS = 3;
            const int CNT_READ_BYTE = 18;
            int writeAttemptsCnt = 0;
            while (true)
            {
				if (ctsTask.Token.IsCancellationRequested)
				{
					return 0;
				}
                try
                {
                    _com.ReadExisting();// 1. Clear buffer.
                    if (writeAttemptsCnt++ >= MAX_ATTEMPTS)
					{
						MessageBox.Show("Превышено кол-во попыток чтения значения частоты с частотомера", "Ошибка чтения частотомера", MessageBoxButton.OK, MessageBoxImage.Error);
						throw new Exception("Превышено кол-во попыток чтения значения частоты с частотомера");
					}
                    Write(COMMAND, 500);
                    var buffer = new List<byte>();
                    int readAttemptsCnt = 0;
					do// 3. Read.
					{
                        try
                        {
							if (ctsTask.Token.IsCancellationRequested)
							{
								return 0;
							}
                            buffer.Add((byte)_com.ReadByte());
                        }
                        catch 
                        {
                            readAttemptsCnt++;
                            if (cp.Speed == 0.7m)
                            {
                                if (readAttemptsCnt >= 5)// Для скорости 0.7.
								{
                                    readAttemptsCnt = 0;
									if (1 != (int)MessageBox.Show("Проверьте, вращается ли датчик на текущей скорости и нажмите \"OK\", чтобы повторить попытку. Или \"Отмена\", чтобы завершить поверку.", "Ошибка чтения данных с частотомера", MessageBoxButton.OKCancel, MessageBoxImage.Error))
									{
										Stop();
										if (ctsTask.Token.IsCancellationRequested)
										{
											return 0;
										}
									}
                                }
                                continue;
                            }
                            if (readAttemptsCnt >= 2)// Для остальных скоростей.
							{
                                Stop();
                            }
                            Write(COMMAND, 500);
                        }
                    } while (buffer.Count != CNT_READ_BYTE);
                    // 5.Calibration.
                    decimal res = Math.Round(decimal.Parse(Encoding.UTF8.GetString(buffer.ToArray()).Replace("\r", "").Replace("\n", "").Replace(" ", ""), NumberStyles.Float, CultureInfo.InvariantCulture), 3);
        /// <summary>Метод для проверки полученного значения на вброс (баги со стороны частотометра).</summary><param name="value"></param><param name="checkpoint"></param>
                    if (res >= cp.MinEdge && res <= cp.MaxEdge)// 6. Validation.
					{
                        return res;
					}
                    // "Невалидное значение частоты, полученное с частотометра".
                }
                catch (Exception e)
                {
                    if (writeAttemptsCnt >= MAX_ATTEMPTS)
					{
						MessageBox.Show($"Превышено кол-во попыток чтения значения частоты с частотомера. {e.Message}", "Ошибка чтения частотомера", MessageBoxButton.OK, MessageBoxImage.Error);
						throw new Exception("Превышено кол-во попыток запроса частоты с частотомера", e);
					}
                    Thread.Sleep(latency);
                }
            }
        }

        /// <summary>Запрос версии.</summary><param name="latency"></param>
        public string GetModel(int latency = 1000)
        {
            Write("*IDN?", latency);
            Thread.Sleep(100);
			string data = _com.ReadExisting();
            if (data.Contains(ID_FRE_COUNTER))
            {
				return "counter";
            }
			else if (data.Contains("DSV"))
			{
				Write("OPEN 1");
				Write("TEST");
				return "WSS";
			}
			return "error";
        }

        /// <summary>Установка времени опроса частотомером.</summary><param name="gt"></param><param name="latency"></param>
        public void SetGateTime(GateTimeSec gt, int latency = 2000)
        {
            Write($":ARM:TIMer {(int)gt} S", latency);
        }

        //public void SetChannelFrequency(FreqChannel frequencyChannel, int sleepTime = 2000)// Устанавливает выбранный канал для считывания значения частоты. Доступные каналы: 1, 2, 3.
        //{
        //    WriteCommand($":FUNCtion FREQuency {(int) frequencyChannel}", sleepTime);
        //}
    }
}