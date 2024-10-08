using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows;
using ADVS.Models.Enums;
using ADVS.Models.Classes;

namespace ADVS.ViewModels.Services
{
    internal class Cymometer// Управление частотомером.
    {
        private const int B_RATE = 9600;
		private const string ID = "43-85/6";
		private static Cymometer _i;
        private SerialPort _p;
        public static Cymometer Inst => _i ??= new Cymometer();
        public Action Stop { get; set; }
        internal event EventHandler<CymometerOpening> IsOpenUpd;

        public void Write(string cmd, int lat = 2000)// Устройство очень долго думает. 2 сек. - это гарантия того, что при старте app все настройки будут отправлены.
        {
            try
            {
                _p.WriteLine(cmd);
                Thread.Sleep(lat);
            }
            catch (TimeoutException e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

		public string Read()
		{
			return _p.ReadTo(">");
		}

        public bool IsOpen()
			=> _p != null && _p.IsOpen;

        #region Open, Close.
        public bool Open(string p, int tOut)
        {
            try
            {
                _p = new SerialPort(p, B_RATE) { ReadTimeout = tOut, WriteTimeout = tOut };
                _p.Open();
				IsOpenUpd?.Invoke(this, new CymometerOpening { IsOpen = _p.IsOpen });
				return true;
            }
            catch (Exception e)
            {
                _p?.Close();
                MessageBox.Show($"Ошибка открытия порта частотомера. {e.Message}", "Ошибка открытия порта частотомера", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
            }
        }

        public void Close()
        {
            if (_p == null)
            {
                IsOpenUpd?.Invoke(this, new CymometerOpening { IsOpen = false });
                return;
            }
            _p.Close();
            _p.Dispose();
			IsOpenUpd?.Invoke(this, new CymometerOpening { IsOpen = _p.IsOpen });
        }
        #endregion

        public void Reset()// Reset device.
        {
            Write("*RST");
        }

        public void SwitchFilter(int ch, bool isOn, int lat = 2000)// Вкл., выкл. фильтра. 1, 2, 3 only.
        {
            if (ch != 1 && ch != 2 && ch != 3)
			{
                throw new ArgumentOutOfRangeException(nameof(ch));
			}
            Write($":INPut{ch}:FILTer {(isOn ? "ON" : "OFF")}", lat);
        }

        public decimal GetCurrHz(Checkpoint c, int lat, CancellationTokenSource t)
        {
            const string CMD = "FETC?";
            const int MAX_ATTEMPTS = 3;
            const int BYTES_CNT = 18;
            int writeAtts = 0;
            while (true)
            {
				if (t.Token.IsCancellationRequested)
				{
					return 0;
				}
                try
                {
                    _p.ReadExisting();// 1. Clear buffer.
                    if (writeAtts++ >= MAX_ATTEMPTS)
					{
						MessageBox.Show("Превышено кол-во попыток чтения значения частоты с частотомера", "Ошибка чтения частотомера", MessageBoxButton.OK, MessageBoxImage.Error);
						throw new Exception("Превышено кол-во попыток чтения значения частоты с частотомера");
					}
                    Write(CMD, 500);
                    var buff = new List<byte>();
                    int readAtts = 0;
					do// 3. Read.
					{
                        try
                        {
							if (t.Token.IsCancellationRequested)
							{
								return 0;
							}
                            buff.Add((byte)_p.ReadByte());
                        }
                        catch 
                        {
                            readAtts++;
                            if (c.S == 0.7m)
                            {
                                if (readAtts >= 5)// Для скорости 0.7.
								{
                                    readAtts = 0;
									if (1 != (int)MessageBox.Show("Проверьте, вращается ли датчик на текущей скорости и нажмите \"OK\", чтобы повторить попытку. Или \"Отмена\", чтобы завершить поверку.", "Ошибка чтения данных с частотомера", MessageBoxButton.OKCancel, MessageBoxImage.Error))
									{
										Stop();
										if (t.Token.IsCancellationRequested)
										{
											return 0;
										}
									}
                                }
                                continue;
                            }
                            if (readAtts >= 2)// Для остальных скоростей.
							{
                                Stop();
                            }
                            Write(CMD, 500);
                        }
                    } while (buff.Count != BYTES_CNT);
                    // 5.Calibration.
                    decimal res = Math.Round(decimal.Parse(Encoding.UTF8.GetString(buff.ToArray()).Replace("\r", "").Replace("\n", "").Replace(" ", ""), NumberStyles.Float, CultureInfo.InvariantCulture), 3);
        /// <summary>Метод для проверки полученного значения на вброс (баги со стороны частотометра).</summary><param name="value"></param><param name="checkpoint"></param>
                    if (res >= c.Min && res <= c.Max)// 6. Validation.
					{
                        return res;
					}
                    // "Невалидное значение частоты, полученное с частотометра".
                }
                catch (Exception e)
                {
                    if (writeAtts >= MAX_ATTEMPTS)
					{
						MessageBox.Show($"Превышено кол-во попыток чтения значения частоты с частотомера. {e.Message}", "Ошибка чтения частотомера", MessageBoxButton.OK, MessageBoxImage.Error);
						throw new Exception("Превышено кол-во попыток запроса частоты с частотомера", e);
					}
                    Thread.Sleep(lat);
                }
            }
        }

        public string GetModel(int lat = 1000)// Запрос версии.
        {
            Write("*IDN?", lat);
            Thread.Sleep(100);
			string ans = _p.ReadExisting();
            if (ans.Contains(ID))
            {
				return "counter";
            }
			else if (string.IsNullOrEmpty(ans) || ans.Contains("UNKNOWN COMMAND"))
			{
                Write("CLOSE");
                _p.ReadExisting();
				Write("OPEN 1");
                ans = _p.ReadTo(">");
                if (ans.Contains("DSNV"))
                {
				    return "WSS";
                }
			}
			return "error";
        }

        public void SetGt(Secs sec, int lat = 2000)// Установка времени опроса частотомером.
        {
            Write($":ARM:TIMer {(int)sec} S", lat);
        }

        //public void SetChannelFrequency(FreqChannel frequencyChannel, int sleepTime = 2000)// Устанавливает выбранный канал для считывания значения частоты. Доступные каналы: 1, 2, 3.
        //{
        //    WriteCommand($":FUNCtion FREQuency {(int) frequencyChannel}", sleepTime);
        //}
    }
}