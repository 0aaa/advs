using System;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using VerificationAirVelocitySensor.Model.EnumLib;
using VerificationAirVelocitySensor.Model.Lib;
using VerificationAirVelocitySensor.Models.ClassLib;

namespace VerificationAirVelocitySensor.ViewModels.Services
{
    internal class Tube// Управление частотным преобразователем ОВЕН ПЧВ3 и эталонным анемометром. Оба устройства находятся на одном порте. Это халтурный вариант работы с протоколом ModBus Rtu. Так как для работы программы нужны всего 4 запроса.
    {
        private const int B_RATE = 9600;
        private const int T_OUT_SET_FREQ = 4000;
        private const byte MOTOR_ADDR = 0x02;
        private const byte ANEMOMETER_ADDR = 0x01;
        private const byte MSG_06 = 0x06;
        private const byte MSG_04 = 0x04;
        private const byte MSG_03 = 0x03;
        private static Tube _instance;
        private readonly int _commandWordReg = 49999;
        private readonly int _tubeReg = 50009;
        private readonly object _locker;
        private SerialPort _com;
        private decimal _setSpeed;// Переменная для метода корректировки установленной частоты на двигателе.
        private bool _isInterview;// Флаг, отвечающий за уведомление о состоянии опроса эталонного значения.
        private bool _isSendCommand;// Флаг для работы с портом, при включенном опросе эталонного значения. Для приостановки его в момент отправки команд.
        private int _setFreq;// Флаг, отвечающий за выставленную в данный момент скорость. Которая должна соотвествовать эталону.
        public static Tube Instance => _instance ??= new Tube();
        public int SetFrequencyVal
        {
            get => _setFreq;
            private set
            {
                _setFreq = value;
                SetFrequencyUpdate?.Invoke(this, new SetFrequencyUpdateEventArgs { SetFrequency = value });
            }
        }
		#region Event handlers.
		public event EventHandler<SetFrequencyUpdateEventArgs> SetFrequencyUpdate;
        public event EventHandler<TubeOpeningEventArgs> IsOpenUpdate;
        public event EventHandler<ReferenceUpdateEventArgs> ReferenceUpdate;
        #endregion

        private Tube()
		{
			for (int i = 1; i < 6; i++)
			{
				_aKoef[i - 1] = (_kPoint[i] - _kPoint[i - 1]) / (_vPoint[i] - _vPoint[i - 1]);
				_bKoef[i - 1] = _kPoint[i] - _aKoef[i - 1] * _vPoint[i];
			}
			_locker = new object();
		}

        public bool IsOpen()
			=> _com != null && _com.IsOpen;

        #region Open, Close.
        public bool Open(string p)
        {
            try
            {
                _com = new SerialPort(p, B_RATE) { ReadTimeout = 2000, WriteTimeout = 2000 };
                _com.Open();
                if (!ValidationPort())
                {
                    _com.Close();
                    _com.Dispose();
                    throw new Exception($"{p} не является ПЛК 73");
                }
                IsOpenUpdate?.Invoke(this, new TubeOpeningEventArgs { IsOpen = _com.IsOpen });
                OnInterviewReference();
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool ValidationPort()
        {
            var attempt = 0;
            while (true)
            {
                try
                {
                    GetReference();
                    return true;
                }
                catch
                {
                    attempt++;
                    if (attempt >= 3)
                    {
                        return false;
                    }
                }
            }
        }

        public void Close()
        {
			_isInterview = false;// Выкл. переодического опроса эталонного датчика.
            if (_com == null)
            {
				IsOpenUpdate?.Invoke(this, new TubeOpeningEventArgs { IsOpen = false });
                return;
            }
			_com.Close();
            _com.Dispose();
			IsOpenUpdate?.Invoke(this, new TubeOpeningEventArgs { IsOpen = _com.IsOpen });
        }
		#endregion

		public void SetFreq(double freq, decimal sp)
		{
			SetFrequencyVal = (int)freq;
			_setSpeed = sp;
			if (freq < 0 || freq > 16384)
			{
				throw new ArgumentOutOfRangeException(freq.ToString(CultureInfo.CurrentCulture), "Попытка установить значение частоты вне диапазона от 0 до 16384");
			}
			var freqArr = new byte[8] { MOTOR_ADDR, MSG_06, 0, 0, 0, 0, 0, 0 };
            if (freq == 0)// Отправка командного слова.
			{
                freqArr[2] = (byte)(_commandWordReg / 256);
                freqArr[3] = (byte)_commandWordReg;
                // Определенные настроки командного слова для остановки двигателя.
                freqArr[4] = 132; // 0x84.
                freqArr[5] = 188; // 0xBC.
            }
			else// Отправка частоты.
			{
                freqArr[2] = (byte)(_tubeReg / 256);
                freqArr[3] = (byte)_tubeReg;
                freqArr[4] = (byte)(freq / 256);
                freqArr[5] = (byte)freq;
            }
            var (freqCrc1, freqCrc2) = GetCrc16(freqArr, 6);
            freqArr[6] = freqCrc1;
            freqArr[7] = freqCrc2;
            lock (_locker)
            {
                _com.Write(freqArr, 0, freqArr.Length);
            }
            Thread.Sleep(100);
			if (freq != 0)// Если была отправлена частота, отправляю командное слово, чтобы её закрепить. Байты командного слова были стырены с проги института, предоставившего сборку.
			{
				var cmd = new byte[] { MOTOR_ADDR, MSG_06, (byte)(_commandWordReg / 256), (byte)_commandWordReg, 4 /*0x04*/, 124 /*0x7C*/, 0, 0 };
                var (crc1, crc2) = GetCrc16(cmd, 6);
                cmd[6] = crc1;
                cmd[7] = crc2;
                lock (_locker)
                {
                    _com.Write(cmd, 0, cmd.Length);
                }
            }
        }

        private double GetReference()// Получить значение анемометра.
        {
            Thread.Sleep(250);
            if (_com.BytesToRead != 0)// Чистка буфера от старых трейдов.
			{
                _com.ReadExisting();
			}
            var cmd = new byte[] { ANEMOMETER_ADDR, MSG_04, 0, 0, 0, 1, 0, 0 };
            var (crc1, crc2) = GetCrc16(cmd, 6);
            cmd[6] = crc1;
            cmd[7] = crc2;
            _com.Write(cmd, 0, cmd.Length);// Запрос значения эталон.
			var attempt = 0;
            while (_com.BytesToRead < 7)
            {
                Thread.Sleep(100);
                attempt++;
                if (attempt > 10)
                {
                    throw new Exception("Нет ответа от устройства");
                }
            }
            var buff = new byte[_com.BytesToRead];
            _com.Read(buff, 0, buff.Length);
            var pack = new byte[7];
            for (var i = 0; i < buff.Length; i++)
            {
				if (buff[i] != 0x01 || buff[i + 1] != 0x04)// Watch out for the index out of range.
				{
					continue;
				}
                if (buff[i + 2] == 0x02)
				{
                    pack = buff.Skip(i).Take(7).ToArray();
				}
            }
            if (pack[0] == 0)
			{
                throw new Exception("Не удалось выделить из массива данных, значение скорости эталона");
			}
            if (pack.Length != 7)
			{
                throw new Exception("Пакет данных (значения эталона) меньше ожидаемого");
			}
            var (resCrc1, resCrc2) = GetCrc16(pack, 5);
            if (resCrc1 != pack[5] || resCrc2 != pack[6])
			{
                throw new Exception("Пакет данных (значения эталона) имеет неправильное CRC-16");
			}
            var res = pack[3] * 256 + pack[4];
            if (res > short.MaxValue)// TODO: что это за обработка? 
			{
                res -= 65536;
			}
            return (double)res / 100;
        }

        public void ZeroingReference()// Обнуление значения анемометра.
        {
            // Байты взяты с исходников проги А-02 от создателей трубы.
            var cmd = new byte[] { ANEMOMETER_ADDR, MSG_03, byte.MaxValue, byte.MaxValue, 0, 0 };
            var (crc1, crc2) = GetCrc16(cmd, 4);
            cmd[4] = crc1;
            cmd[5] = crc2;
            lock (_locker)
            {
                _com.Write(cmd, 0, cmd.Length);
            }
        }

        public async void OnInterviewReference()// Вкл. переодический опрос эталонного датчика.
        {
            if (_isInterview)// В случае если опрос уже запущен, не запускать доп. задачи по опросу.
			{
                return;
			}
            _isInterview = true;
            await Task.Run(() =>
            {
                while (_isInterview)
                {
                    if (!_isSendCommand)
                    {
                        lock (_locker)
                        {
                            _isSendCommand = true;
                            while (_isInterview)
                            {
                                try
                                {
									ReferenceUpdate?.Invoke(this, new ReferenceUpdateEventArgs { ReferenceValue = (double)CalculateSpeed((decimal)GetReference()) });
									break;
                                }
                                catch {}
                            }
                            _isSendCommand = false;
                        }
                    }
                }
            });
        }

        private decimal GetError()// Расчёт допустимой погрешности в зависимости от установленной скорости.
        {
            if (_setSpeed > 0 && _setSpeed <= 0.7m)
            {
                return 0.02m;
            }
            if (_setSpeed > 0.7m && _setSpeed <= 30m)
            {
                //return 0.1m;
                return 0.05m;
            }
            throw new ArgumentOutOfRangeException(_setSpeed.ToString(), "Недопустимое значение скорости");
        }

        /// <summary>Метод для корректировки скорости эталона к установленному значению скорости.</summary><param name="avgReferenceSpeed"></param><param name="checkpoint"></param><param name="ctsTask"></param>
        public void AdjustSp(ref decimal avgReferenceSpeed, Checkpoint checkpoint, ref CancellationTokenSource ctsTask)
        {
            var acceptErrValidationCnt = 0;
            var step = checkpoint.MaxStep;
            var signChangeCnt = 0;// Переменная для отслеживания смены знака у шага, с помощью которого корректируется частота.
            var currSign = SingValue.Plus;// Знак шага, плюс или минус.
			SingValue prevSign;// Старое значение для сравнения при изменении нового.
			var is1stStart = true;// Флаг для первого прохода, чтобы в случае смены знака stepValue, это не пошло в счётчик.
			while (true)
            {
				if (ctsTask.Token.IsCancellationRequested)
				{
					return;
				}
                if (signChangeCnt == 2)
                {
                    step = 10;
                }
                prevSign = currSign;
                if (IsErrorValidation(ref avgReferenceSpeed) && acceptErrValidationCnt++ == 2)// Делаю проверку на 2 корректировки, чтобы в случае первой корректировки значение не уплыло из-за быстрой смены частоты вращения двигателя аэротрубы.
				{
					MessageBox.Show("Достигнуто максимальное количество коррекций скорости мотора на заданной точке. Коррекция прервана.", "Прерывание коррекции", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
                }
                currSign = _setSpeed - avgReferenceSpeed > 0 ? SingValue.Plus : SingValue.Minus;
				if (!is1stStart && currSign != prevSign)// Если это не первый прогон цикла.
				{
					signChangeCnt++;
                }
                SetFrequencyVal += currSign == SingValue.Plus ? step : -step;
                SetFreq(_setFreq, _setSpeed);
                Thread.Sleep(T_OUT_SET_FREQ);
                is1stStart = false;
            }
        }

        private bool IsErrorValidation(ref decimal avgRefSp)// Проверка валидности эталонной скорости, относительно выставленной.
        {
            var e = GetError();// Допустимая погрешность (0,02 или 0,1).
            var diff = _setSpeed - avgRefSp;// Разница между установленной скоростью и полученной с эталона.
            return e >= diff && diff >= -e;// Флаг, отвечающий за совпадение скоростей эталона и выставленной с учётом допустимой погрешности.
		}

        /// <summary>Возвращает crc16 в виде двух bytes.</summary><param name="buf"></param><param name="buffSize"></param>
        private static (byte, byte) GetCrc16(byte[] buf, int buffSize)
        {
            var num1 = ushort.MaxValue;
            ushort buffInd = 0;
            while (buffSize > 0)
            {
                num1 ^= buf[buffInd];
                for (ushort i = 0; i < 8; ++i)
                {
                    if ((num1 & 1) != 0)
					{
                        num1 = (ushort)((ushort)((uint)num1 >> 1) ^ 40961U);
					}
                    else
					{
                        num1 >>= 1;
					}
                }
                --buffSize;
                ++buffInd;
            }
            return ((byte)num1, (byte)(num1 / 256U));
        }

        #region Работа с коэффициентом для обработки получаемого с анемометра значения.
        private readonly decimal[] _vPoint = [ 0m, 0.72m, 5m, 10m, 15m, 30m ];// Скоростные точки для расчёта коэффициента. Данные от сотрудников MD.
        private readonly decimal[] _kPoint = [ 0.866m, 0.866m, 0.96m, 0.94m, 0.953m, 1.03m ];// Коэффициенты, расчитанные для v_point (для каждого диапазона). Данные от сотрудников MD.
        private readonly decimal[] _aKoef = new decimal[5];
        private readonly decimal[] _bKoef = new decimal[5];

        private decimal CalculateSpeed(decimal rawSpeed)
        {
            var rangeValue = GetRange(rawSpeed);
            return Math.Round(rawSpeed * (_aKoef[rangeValue - 1] * rawSpeed + _bKoef[rangeValue - 1]), 2);
        }

        private int GetRange(decimal rawSpeed)
        {
            if (rawSpeed < _vPoint[1])
			{
                return 1;
			}
            for (int i = 4; i > 0; i--)
			{
				if (rawSpeed >= _vPoint[i])
				{
					return i + 1;
				}
			}
            throw new ArgumentOutOfRangeException(nameof(rawSpeed));
        }
        #endregion
    }
}