using System;
using System.Globalization;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace VerificationAirVelocitySensor.ViewModel.Services
{
    /// <summary>
    /// Управление частотным преобразователем ОВЕН ПЧВ3 и эталонным анемометром
    /// Оба устройства находятся на одном порте
    ///
    /// Это халтурный вариант работы с протоколом ModBus Rtu.
    /// Так как для работы программы нужны всего 4 запроса.
    /// </summary>
    public class FrequencyMotorDevice
    {
        private SerialPort _serialPort;

        private string _comPort;
        private const int BaudRate = 9600;
        private const byte AddressMotorDevice = 0x02;
        private const byte AddressAnemometerDevice = 0x01;
        private const byte TypeMessage06 = 0x06;
        private const byte TypeMessage04 = 0x04;
        private const byte TypeMessage03 = 0x03;
        private readonly int CommandWordRegister = 49999;
        private readonly int FrequencyMotorRegister = 50009;

        /// <summary>
        /// Эталонное значение
        /// </summary>
        private double _referenceValue;

        /// <summary>
        /// Флаг отвечающий за уведомление о состоянии опроса эталонного значения.
        /// </summary>
        private bool _isInterview;

        /// <summary>
        /// Флаг для работы с портом, при включенном опросе эталонного значения.
        /// Для приостановки его в момент отправки команд.
        /// </summary>
        private bool _isSendCommand;

        private readonly int _periodInterview = 1000;
        private object _locker = new object();

        #region EventHandler 

        public event EventHandler<IsOpenFrequencyMotorEventArgs> IsOpenUpdate;

        private void IsOpenUpdateMethod(bool isOpen)
        {
            IsOpenUpdate?.Invoke(this, new IsOpenFrequencyMotorEventArgs
            {
                IsOpen = isOpen
            });
        }

        public event EventHandler<UpdateReferenceValueEventArgs> UpdateReferenceValue;

        private void UpdateReferenceValueMethod(double referenceValue)
        {
            UpdateReferenceValue?.Invoke(this, new UpdateReferenceValueEventArgs
            {
                ReferenceValue = referenceValue
            });
        }

        #endregion

        private FrequencyMotorDevice()
        {
        }

        private static FrequencyMotorDevice _instance;

        public static FrequencyMotorDevice Instance =>
            _instance ?? (_instance = new FrequencyMotorDevice());

        public bool IsOpen() => _serialPort != null && _serialPort.IsOpen;

        #region Open , Close

        public void OpenPort(string comPort)
        {
            try
            {
                _comPort = comPort;
                _serialPort = new SerialPort(_comPort, BaudRate) {ReadTimeout = 2000, WriteTimeout = 2000};
                _serialPort.Open();

                IsOpenUpdateMethod(_serialPort.IsOpen);
                OnInterviewReferenceValue();
            }
            catch (Exception e)
            {
                MessageBox.Show($"{e.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ClosePort()
        {
            OffInterviewReferenceValue();
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

        public void SetFrequency(decimal speed , decimal coefficient = 1)
        {
            const int fMax = 80;
            var freq = (double)speed * (double)coefficient * 16384.0 / fMax;

            if (freq < 0 || freq > 16384)
            {
                const string errorMessage = "Попытка установить значение частоты вне диапазона от 0 до 16384.0";
                throw new ArgumentOutOfRangeException(freq.ToString(CultureInfo.CurrentCulture), errorMessage);
            }

            var freqArray = new byte[8];

            freqArray[0] = AddressMotorDevice;
            freqArray[1] = TypeMessage06;

            //Отправка командного слова
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (freq == 0)
            {
                freqArray[2] = (byte) (CommandWordRegister / 256);
                freqArray[3] = (byte) CommandWordRegister;
                //Определенные настроки командного слова для остановки двигателя.
                freqArray[4] = 132;
                freqArray[5] = 188;
            }
            //Отправка частоты 
            else
            {
                freqArray[2] = (byte) (FrequencyMotorRegister / 256);
                freqArray[3] = (byte) FrequencyMotorRegister;
                freqArray[4] = (byte) (freq / 256);
                freqArray[5] = (byte) freq;
            }

            var (freqCrc1, freqCrc2) = GetCrc16(freqArray, 6);

            freqArray[6] = freqCrc1;
            freqArray[7] = freqCrc2;

            lock (_locker)
            {
                _serialPort.Write(freqArray, 0, freqArray.Length);
            }

            Thread.Sleep(100);


            //Если была отправленна частота, отправляю командное словы что бы ее закрепить
            //Байты командного слова были стырены с проги института предоставившего сборку.
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (freq != 0)
            {
                var commandWord = new byte[8];
                commandWord[0] = AddressMotorDevice;
                commandWord[1] = TypeMessage06;
                commandWord[2] = (byte) (CommandWordRegister / 256);
                commandWord[3] = (byte) CommandWordRegister;

                commandWord[4] = 4;
                commandWord[5] = 124;
                var (wordCrc1, wordCrc2) = GetCrc16(commandWord, 6);
                commandWord[6] = wordCrc1;
                commandWord[7] = wordCrc2;

                lock (_locker)
                {
                    _serialPort.Write(commandWord, 0, commandWord.Length);
                }
            }
        }

        /// <summary>
        /// Получить значение анемометра
        /// </summary>
        /// <returns></returns>
        private double GetReferenceValue()
        {
            //Чистка буфера от старых трейдов.
            Thread.Sleep(1000);
            if (_serialPort.BytesToRead != 0)
                _ = _serialPort.ReadExisting();

            //Запрос значения эталон
            var sendPack = new byte[]
            {
                AddressAnemometerDevice,
                TypeMessage04,
                0,
                0,
                0,
                1,
                0,
                0
            };


            var (sendPackCrc1, sendPackCrc2) = GetCrc16(sendPack, 6);
            sendPack[6] = sendPackCrc1;
            sendPack[7] = sendPackCrc2;

            _serialPort.Write(sendPack, 0, sendPack.Length);
            while (_serialPort.BytesToRead == 0)
            {
                Thread.Sleep(100);
            }

            var bytesToRead = _serialPort.BytesToRead;
            var getPack = new byte[bytesToRead];
            _serialPort.Read(getPack, 0, getPack.Length);

            if (getPack.Length != 7)
                throw new Exception("Пакет данных (значения эталона) меньше ожидаемого");

            var (getPackCrc1, getPackCrc2) = GetCrc16(getPack, 5);

            if (getPackCrc1 != getPack[5] || getPackCrc2 != getPack[6])
                throw new Exception("Пакет данных (значения эталона) имеет неправильное crc16");

            var valueArray = new[]
            {
                getPack[3],
                getPack[4]
            };
            var value = valueArray[0] * 256 + valueArray[1];

            //TODO  что это за обработка ? 
            if (value > short.MaxValue)
                value -= 65536;

            return (double) value / 100;
        }

        /// <summary>
        /// Обнуление значения анемометра
        /// </summary>
        public void ZeroReferenceValue()
        {
            //байты взяты с исходников проги А-02 от создателей трубы
            var setNullArray = new byte[]
            {
                AddressAnemometerDevice,
                TypeMessage03,
                byte.MaxValue,
                byte.MaxValue,
                0,
                0
            };

            var (crc1, crc2) = GetCrc16(setNullArray, 4);
            setNullArray[4] = crc1;
            setNullArray[5] = crc2;

            lock (_locker)
            {
                _serialPort.Write(setNullArray, 0, setNullArray.Length);
            }
        }


        /// <summary>
        /// Вкл переодический опрос эталонного датчика
        /// </summary>
        public void OnInterviewReferenceValue()
        {
            //В случае если опрос уже запущен  , не запускать доп задачи по опросу.
            if (_isInterview)
                return;


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

                            _referenceValue = GetReferenceValue();

                            UpdateReferenceValueMethod(_referenceValue);

                            _isSendCommand = false;
                        }
                    }

                    Thread.Sleep(_periodInterview);
                }
            }));
        }

        /// <summary>
        /// Выкл переодического опроса эталонного датчика
        /// </summary>
        public void OffInterviewReferenceValue()
        {
            _isInterview = false;
        }


        /// <summary>
        /// Возвращает crc16  в виде двух byte
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="bufSize"></param>
        /// <returns></returns>
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
                        num1 = (ushort) ((ushort) ((uint) num1 >> 1) ^ 40961U);
                    else
                        num1 >>= 1;
                }

                --bufSize;
                ++num2;
            }


            var crc1 = (byte) num1;
            var crc2 = (byte) (num1 / 256U);

            return (crc1, crc2);
        }
    }

    /// <summary>
    /// Событие открытия или закрытие порта частотного двигателя
    /// </summary>
    public class IsOpenFrequencyMotorEventArgs : EventArgs
    {
        public bool IsOpen { get; set; }
    }

    public class UpdateReferenceValueEventArgs : EventArgs
    {
        /// <summary>
        /// Эталонное значение скорости на анемометре
        /// </summary>
        public double ReferenceValue { get; set; }
    }
}