using System;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace VerificationAirVelocitySensor.ViewModel.Services
{
    /// <summary>Управление частотным преобразователем ОВЕН ПЧВ3 и эталонным анемометром. Оба устройства находятся на одном порте. Это халтурный вариант работы с протоколом ModBus Rtu. Так как для работы программы нужны всего 4 запроса.</summary>
    internal class FrequencyMotorDevice
    {
        private const int BAUD_RATE = 9600;
        private const int TIMEOUT_SET_FREQUENCY = 4000;
        private const byte ADDRESS_MOTOR_DEVICE = 0x02;
        private const byte ADDRESS_ANEMOMETER_DEVICE = 0x01;
        private const byte TYPE_MSG_06 = 0x06;
        private const byte TYPE_MSG_04 = 0x04;
        private const byte TYPE_MSG_03 = 0x03;
        private static FrequencyMotorDevice _instance;
        private readonly int CommandWordRegister = 49999;
        private readonly int FrequencyMotorRegister = 50009;
        private readonly object _locker;
        private SerialPort _serialPort;
        private string _comPort;
        private decimal _setSpeed;// Переменная для метода корректировки установленной частоты на двигателе.
        /// <summary>Эталонное значение скорости</summary>
        private double _referenceSpeedValue;
        /// <summary>Флаг отвечающий за уведомление о состоянии опроса эталонного значения.</summary>
        private bool _isInterview;
        /// <summary>Флаг для работы с портом, при включенном опросе эталонного значения. Для приостановки его в момент отправки команд.</summary>
        private bool _isSendCommand;
        /// <summary>Флаг отвечающий за выставленную в данный момент скорость. Которая должна соотвествовать эталону.</summary>
        private int _setFrequencyValue;
        public static FrequencyMotorDevice Instance => _instance ?? (_instance = new FrequencyMotorDevice());
        public int SetFrequencyValue
        {
            get => _setFrequencyValue;
            set
            {
                _setFrequencyValue = value;
                UpdateSetFrequency?.Invoke(this, new UpdateSetFrequencyEventArgs { SetFrequency = value });
            }
        }
		#region EventHandler
		public event EventHandler<UpdateSetFrequencyEventArgs> UpdateSetFrequency;
        public event EventHandler<IsOpenFrequencyMotorEventArgs> IsOpenUpdate;
        public event EventHandler<UpdateReferenceValueEventArgs> UpdateReferenceValue;
        #endregion

        private FrequencyMotorDevice()
		{
			for (var i = 0; i < 6; i++)
			{
				if (i == 0)
				{
					continue;
				}
				_aKoef[i - 1] = (_kPoint[i] - _kPoint[i - 1]) / (_vPoint[i] - _vPoint[i - 1]);
				_bKoef[i - 1] = _kPoint[i] - _aKoef[i - 1] * _vPoint[i];
			}
			_locker = new object();
		}

        public bool IsOpen() => _serialPort != null && _serialPort.IsOpen;

        #region Open, Close
        public bool OpenPort(string comPort)
        {
            try
            {
                _comPort = comPort;
                _serialPort = new SerialPort(_comPort, BAUD_RATE) { ReadTimeout = 2000, WriteTimeout = 2000 };
                _serialPort.Open();
                if (!ValidationComPort())
                {
                    _serialPort.Close();
                    _serialPort.Dispose();
                    throw new Exception($"{comPort} не является ПЛК 73");
                }
                IsOpenUpdate?.Invoke(this, new IsOpenFrequencyMotorEventArgs { IsOpen = _serialPort.IsOpen });
                OnInterviewReferenceValue();
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool ValidationComPort()
        {
            var attempt = 0;
            while (true)
            {
                try
                {
                    GetReferenceValue();
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

        public void ClosePort()
        {
        /// <summary>Выкл переодического опроса эталонного датчика</summary>
			_isInterview = false;
			_serialPort.Close();
            _serialPort.Dispose();
            if (_serialPort == null)
            {
				IsOpenUpdate?.Invoke(this, new IsOpenFrequencyMotorEventArgs { IsOpen = false });
                return;
            }
			IsOpenUpdate?.Invoke(this, new IsOpenFrequencyMotorEventArgs { IsOpen = _serialPort.IsOpen });
        }
		#endregion

		public void SetFrequency(int freqInt, decimal speed)
		{
			SetFrequencyValue = freqInt;
			_setSpeed = speed;
			double freq = freqInt;
			if (freq < 0 || freq > 16384)
			{
				throw new ArgumentOutOfRangeException(freq.ToString(CultureInfo.CurrentCulture), "Попытка установить значение частоты вне диапазона от 0 до 16384");
			}
			var freqArray = new byte[8] { ADDRESS_MOTOR_DEVICE, TYPE_MSG_06, 0, 0, 0, 0, 0, 0 };
            if (freq == 0)// Отправка командного слова.
			{
                freqArray[2] = (byte)(CommandWordRegister / 256);
                freqArray[3] = (byte)CommandWordRegister;
                // Определенные настроки командного слова для остановки двигателя.
                freqArray[4] = 132; //0x84
                freqArray[5] = 188; //0xBC
            }
			else// Отправка частоты.
			{
                freqArray[2] = (byte)(FrequencyMotorRegister / 256);
                freqArray[3] = (byte)FrequencyMotorRegister;
                freqArray[4] = (byte)(freq / 256);
                freqArray[5] = (byte)freq;
            }
            var (freqCrc1, freqCrc2) = GetCrc16(freqArray, 6);
            freqArray[6] = freqCrc1;
            freqArray[7] = freqCrc2;
            lock (_locker)
            {
                _serialPort.Write(freqArray, 0, freqArray.Length);
            }
            Thread.Sleep(100);
			if (freq != 0)// Если была отправленна частота, отправляю командное словы чтобы ее закрепить. Байты командного слова были стырены с проги института предоставившего сборку.
			{
				var commandWord = new byte[] { ADDRESS_MOTOR_DEVICE, TYPE_MSG_06, (byte)(CommandWordRegister / 256), (byte)CommandWordRegister, 4 /*0x04*/, 124 /*0x7C*/, 0, 0 };
                var (wordCrc1, wordCrc2) = GetCrc16(commandWord, 6);
                commandWord[6] = wordCrc1;
                commandWord[7] = wordCrc2;
                lock (_locker)
                {
                    _serialPort.Write(commandWord, 0, commandWord.Length);
                }
            }
        }

        /// <summary>Получить значение анемометра</summary>
        private double GetReferenceValue()
        {
            Thread.Sleep(250);
            if (_serialPort.BytesToRead != 0)// Чистка буфера от старых трейдов.
			{
                _serialPort.ReadExisting();
			}
            var sendPack = new byte[] { ADDRESS_ANEMOMETER_DEVICE, TYPE_MSG_04, 0, 0, 0, 1, 0, 0 };
            var (sendPackCrc1, sendPackCrc2) = GetCrc16(sendPack, 6);
            sendPack[6] = sendPackCrc1;
            sendPack[7] = sendPackCrc2;
            _serialPort.Write(sendPack, 0, sendPack.Length);// Запрос значения эталон.
			var attempt = 0;
            while (_serialPort.BytesToRead < 7)
            {
                Thread.Sleep(100);
                attempt++;
                if (attempt > 10)
                {
                    throw new Exception("Нет ответа от устройства");
                }
            }
            var getPack = new byte[_serialPort.BytesToRead];
            _serialPort.Read(getPack, 0, getPack.Length);
            var packValue = new byte[7];
            for (var i = 0; i < getPack.Length; i++)
            {
				if (getPack[i] != 0x01 || getPack[i + 1] != 0x04)
				{
					continue;
				}
                if (getPack[i + 2] == 0x02)
				{
                    packValue = getPack.Skip(i).Take(7).ToArray();
				}
            }
            if (packValue[0] == 0)
			{
                throw new Exception("Не удалось выделить из массива данных, значение скорости эталона");
			}
            if (packValue.Length != 7)
			{
                throw new Exception("Пакет данных (значения эталона) меньше ожидаемого");
			}
            var (getPackCrc1, getPackCrc2) = GetCrc16(packValue, 5);
            if (getPackCrc1 != packValue[5] || getPackCrc2 != packValue[6])
			{
                throw new Exception("Пакет данных (значения эталона) имеет неправильное crc16");
			}
            var value = packValue[3] * 256 + packValue[4];
            // TODO  что это за обработка ? 
            if (value > short.MaxValue)
			{
                value -= 65536;
			}
            return (double)value / 100;
        }

        /// <summary>Обнуление значения анемометра</summary>
        public void ZeroReferenceValue()
        {
            // Байты взяты с исходников проги А-02 от создателей трубы.
            var setNullArray = new byte[] { ADDRESS_ANEMOMETER_DEVICE, TYPE_MSG_03, byte.MaxValue, byte.MaxValue, 0, 0 };
            var (crc1, crc2) = GetCrc16(setNullArray, 4);
            setNullArray[4] = crc1;
            setNullArray[5] = crc2;
            lock (_locker)
            {
                _serialPort.Write(setNullArray, 0, setNullArray.Length);
            }
        }

        /// <summary>Вкл переодический опрос эталонного датчика</summary>
        public void OnInterviewReferenceValue()
        {
            // В случае если опрос уже запущен, не запускать доп. задачи по опросу.
            if (_isInterview)
			{
                return;
			}
            _isInterview = true;
            Task.Run(async () => await Task.Run(() =>
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
                                    var rawValue = GetReferenceValue();
                                    _referenceSpeedValue = (double)SpeedCalculation((decimal)rawValue);
									UpdateReferenceValue?.Invoke(this, new UpdateReferenceValueEventArgs { ReferenceValue = _referenceSpeedValue });
                                }
                                catch
                                {
                                    continue;
                                }
                                break;
                            }
                            _isSendCommand = false;
                        }
                    }
                }
            }));
        }

        /// <summary>Расчет допустимой погрешности в зависимости от установленной скорости</summary>
        private decimal GetErrorValue()
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

        /// <summary>Метод для корректировки скорости эталона к установленному значению скорости</summary>
        /// <param name="averageReferenceSpeedValue"></param><param name="speedPoint"></param><param name="ctsTask"></param>
        public void CorrectionSpeedMotor(ref decimal averageReferenceSpeedValue, SpeedPoint speedPoint, ref CancellationTokenSource ctsTask)
        {
            var countAcceptValueErrorValidation = 0;
            var stepValue = speedPoint.MaxStep;
            var countChangeSign = 0;// Переменная для отслеживания смены знака у шага, с помощью которого корректируется частота.
            var currentSing = SingValue.Plus;// Знак шага, плюс или минус.
			SingValue oldSingValue;// Старое значение для сравнения при изменении нового.
			var isFirstStart = true;// Флаг для первого прохода, чтобы в случае смены знака stepValue, это не пошло в счётчик.
			while (true)
            {
				if (ctsTask.Token.IsCancellationRequested)
				{
					return;
				}
                if (countChangeSign == 2)
                {
                    stepValue = 10;
                }
                oldSingValue = currentSing;
                if (IsValueErrorValidation(ref averageReferenceSpeedValue))// Делаю проверку, на 2 корректировки, чтобы в случае первой корректировки значение не уплыло, из-за быстрой смены частоты вращения двигателя аэро трубы.
				{
                    countAcceptValueErrorValidation++;
                    if (countAcceptValueErrorValidation == 2)
					{
                        return;
					}
                }
                currentSing = (_setSpeed - averageReferenceSpeedValue > 0) ? SingValue.Plus : SingValue.Minus;
				if (!isFirstStart && currentSing != oldSingValue)// Если это не первый прогон цикла.
				{
                        countChangeSign++;
                }
                SetFrequencyValue += currentSing == SingValue.Plus ? stepValue : -stepValue;
                SetFrequency(_setFrequencyValue, _setSpeed);
                Thread.Sleep(TIMEOUT_SET_FREQUENCY);
                isFirstStart = false;
            }
        }

        /// <summary>Проверка валидности эталонной скорости, относительно выставленной</summary>
        private bool IsValueErrorValidation(ref decimal averageReferenceSpeedValue)
        {
            var errorValue = GetErrorValue();// Допустимая погрешность (0,02 или 0,1).
            var differenceValue = _setSpeed - averageReferenceSpeedValue;// Разница между установленной скоростью и полученной с эталона.
            return errorValue >= differenceValue && differenceValue >= -errorValue;// Флаг, отвечающий за совпадение скоростей эталона и выставленной с учётом допустимой погрешности.
		}

        /// <summary>Возвращает crc16  в виде двух byte</summary><param name="buf"></param><param name="bufSize"></param>
        private (byte, byte) GetCrc16(byte[] buf, int bufSize)
        {
            var num1 = ushort.MaxValue;
            ushort num2 = 0;
            while (bufSize > 0)
            {
                num1 ^= buf[num2];
                for (ushort index = 0; index < 8; ++index)
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
                --bufSize;
                ++num2;
            }
            return ((byte)num1, (byte)(num1 / 256U));
        }

        #region Работа с коэффициентом для обработки получаемого с анемометра значения
        /// <summary>Скоростные точки для расчета коефа . Данные от сотрудников Аэро Трубы</summary>
        private readonly decimal[] _vPoint = { 0m, 0.72m, 5m, 10m, 15m, 30m };
        /// <summary>Коефы расчитанные для v_point (для каждого диапазона) . Данные от сотрудников Аэро Трубы</summary>
        private readonly decimal[] _kPoint = { 0.866m, 0.866m, 0.96m, 0.94m, 0.953m, 1.03m };
        private readonly decimal[] _aKoef = new decimal[5];
        private readonly decimal[] _bKoef = new decimal[5];

        private decimal SpeedCalculation(decimal rawSpeed)
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
            if (rawSpeed >= _vPoint[4])
			{
                return 5;
			}
            if (rawSpeed >= _vPoint[3])
			{
                return 4;
			}
            if (rawSpeed >= _vPoint[2])
			{
                return 3;
			}
            if (rawSpeed >= _vPoint[1])
			{
                return 2;
			}
            throw new ArgumentOutOfRangeException("Значение эталона вне диапазона от 0 до 30");
        }
        #endregion
    }

    /// <summary>Событие открытия или закрытие порта частотного двигателя</summary>
    public class IsOpenFrequencyMotorEventArgs : EventArgs
    {
        public bool IsOpen { get; set; }
    }

    public class UpdateReferenceValueEventArgs : EventArgs
    {
        /// <summary>Эталонное значение скорости на анемометре</summary>
        public double ReferenceValue { get; set; }
    }

    public class UpdateSetFrequencyEventArgs : EventArgs
    {
        public int SetFrequency { get; set; }
    }

    public enum SingValue
    {
        Plus,
        Minus
    }
}