using System;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ADVS.Models.Enums;
using ADVS.Models.Events;
using ADVS.Models.Evaluations;

namespace ADVS.ViewModels.Services
{
    internal class Tube// Управление частотным преобразователем ОВЕН ПЧВ3 и эталонным анемометром. Оба устройства находятся на одном порте. Это халтурный вариант работы с протоколом ModBus Rtu. Так как для работы программы нужны всего 4 запроса.
    {
        private const int B_RATE = 9600;
        private const int LAT = 4000;
        private const byte MOTOR_ADDR = 0x02;
        private const byte ANEMOMETER_ADDR = 0x01;
        private const byte MSG_06 = 0x06;
        private const byte MSG_04 = 0x04;
        private const byte MSG_03 = 0x03;
        private static Tube _inst;
        private readonly int _cmdReg = 49999;
        private readonly int _tubeReg = 50009;
        private readonly object _locker;
        private SerialPort _p;
        private decimal _currS;// Переменная для метода корректировки установленной частоты на двигателе.
        private bool _isInterview;// Флаг, отвечающий за уведомление о состоянии опроса эталонного значения.
        private bool _isSend;// Флаг для работы с портом, при включенном опросе эталонного значения. Для приостановки его в момент отправки команд.
        private int _currF;// Флаг, отвечающий за выставленную в данный момент скорость. Которая должна соотвествовать эталону.
        public static Tube Inst => _inst ??= new Tube();
        public int CurrF
        {
            get => _currF;
            private set
            {
                _currF = value;
                Fupd?.Invoke(this, new Fupd { F = value });
            }
        }
		#region Event handlers.
		public event EventHandler<Fupd> Fupd;
        public event EventHandler<TubeOpening> IsOpenUpd;
        public event EventHandler<RefUpd> RefUpd;
        #endregion

        private Tube()
		{
			for (int i = 1; i < 6; i++)
			{
				_aKoef[i - 1] = (_kPnts[i] - _kPnts[i - 1]) / (_vPnts[i] - _vPnts[i - 1]);
				_bKoef[i - 1] = _kPnts[i] - _aKoef[i - 1] * _vPnts[i];
			}
			_locker = new object();
		}

        public bool IsOpen()
			=> _p != null && _p.IsOpen;

        #region Open, Close.
        public bool Open(string p)
        {
            try
            {
                _p = new SerialPort(p, B_RATE) { ReadTimeout = 2000, WriteTimeout = 2000 };
                _p.Open();
                if (!ValidationPort())
                {
                    _p.Close();
                    _p.Dispose();
                    throw new Exception($"{p} не является ПЛК 73");
                }
                IsOpenUpd?.Invoke(this, new TubeOpening { IsOpen = _p.IsOpen });
                OnInterviewRef();
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
            var att = 0;
            while (true)
            {
                try
                {
                    GetRef();
                    return true;
                }
                catch
                {
                    att++;
                    if (att >= 3)
                    {
                        return false;
                    }
                }
            }
        }

        public void Close()
        {
			_isInterview = false;// Выкл. переодического опроса эталонного датчика.
            if (_p == null)
            {
				IsOpenUpd?.Invoke(this, new TubeOpening { IsOpen = false });
                return;
            }
			_p.Close();
            _p.Dispose();
			IsOpenUpd?.Invoke(this, new TubeOpening { IsOpen = _p.IsOpen });
        }
		#endregion

		public void SetF(double f, decimal s)
		{
			CurrF = (int)f;
			_currS = s;
			if (f < 0 || f > 16384)
			{
				throw new ArgumentOutOfRangeException(f.ToString(CultureInfo.CurrentCulture), "Попытка установить значение частоты вне диапазона от 0 до 16384");
			}
			var cmd = new byte[8] { MOTOR_ADDR, MSG_06, 0, 0, 0, 0, 0, 0 };
            if (f == 0)// Отправка командного слова.
			{
                cmd[2] = (byte)(_cmdReg / 256);
                cmd[3] = (byte)_cmdReg;
                // Определенные настройки командного слова для остановки двигателя.
                cmd[4] = 132; // 0x84.
                cmd[5] = 188; // 0xBC.
            }
			else// Отправка частоты.
			{
                cmd[2] = (byte)(_tubeReg / 256);
                cmd[3] = (byte)_tubeReg;
                cmd[4] = (byte)(f / 256);
                cmd[5] = (byte)f;
            }
            var (crc1, crc2) = GetCrc16(cmd, 6);
            cmd[6] = crc1;
            cmd[7] = crc2;
            lock (_locker)
            {
                _p.Write(cmd, 0, cmd.Length);
            }
            Thread.Sleep(100);
			if (f != 0)// Если была отправлена частота, отправляю командное слово, чтобы её закрепить. Байты командного слова были стырены с проги института, предоставившего сборку.
			{
				cmd = [MOTOR_ADDR, MSG_06, (byte)(_cmdReg / 256), (byte)_cmdReg, 4 /*0x04*/, 124 /*0x7C*/, 0, 0];
                (crc1, crc2) = GetCrc16(cmd, 6);
                cmd[6] = crc1;
                cmd[7] = crc2;
                lock (_locker)
                {
                    _p.Write(cmd, 0, cmd.Length);
                }
            }
        }

        private double GetRef()// Получить значение анемометра.
        {
            Thread.Sleep(250);
            if (_p.BytesToRead != 0)// Чистка буфера от старых трейдов.
			{
                _p.ReadExisting();
			}
            var cmd = new byte[] { ANEMOMETER_ADDR, MSG_04, 0, 0, 0, 1, 0, 0 };
            var (crc1, crc2) = GetCrc16(cmd, 6);
            cmd[6] = crc1;
            cmd[7] = crc2;
            _p.Write(cmd, 0, cmd.Length);// Запрос значения эталон.
			var att = 0;
            while (_p.BytesToRead < 7)
            {
                Thread.Sleep(100);
                att++;
                if (att > 10)
                {
                    throw new Exception("Нет ответа от устройства");
                }
            }
            var buff = new byte[_p.BytesToRead];
            _p.Read(buff, 0, buff.Length);
            cmd = new byte[7];
            for (var i = 0; i < buff.Length; i++)
            {
				if (buff[i] != 0x01 || buff[i + 1] != 0x04)// Watch out for the index out of range.
				{
					continue;
				}
                if (buff[i + 2] == 0x02)
				{
                    cmd = buff.Skip(i).Take(7).ToArray();
				}
            }
            if (cmd[0] == 0)
			{
                throw new Exception("Не удалось выделить из массива данных, значение скорости эталона");
			}
            if (cmd.Length != 7)
			{
                throw new Exception("Пакет данных (значения эталона) меньше ожидаемого");
			}
            (crc1, crc2) = GetCrc16(cmd, 5);
            if (crc1 != cmd[5] || crc2 != cmd[6])
			{
                throw new Exception("Пакет данных (значения эталона) имеет неправильное CRC-16");
			}
            var res = cmd[3] * 256 + cmd[4];
            if (res > short.MaxValue)// TODO: что это за обработка? 
			{
                res -= 65536;
			}
            return (double)res / 100;
        }

        public void ZeroingRef()// Обнуление значения анемометра.
        {
            // Байты взяты с исходников проги А-02 от создателей трубы.
            var cmd = new byte[] { ANEMOMETER_ADDR, MSG_03, byte.MaxValue, byte.MaxValue, 0, 0 };
            var (crc1, crc2) = GetCrc16(cmd, 4);
            cmd[4] = crc1;
            cmd[5] = crc2;
            lock (_locker)
            {
                _p.Write(cmd, 0, cmd.Length);
            }
        }

        public async void OnInterviewRef()// Вкл. переодический опрос эталонного датчика.
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
                    if (!_isSend)
                    {
                        lock (_locker)
                        {
                            _isSend = true;
                            while (_isInterview)
                            {
                                try
                                {
									RefUpd?.Invoke(this, new RefUpd { Ref = (double)CalculateS((decimal)GetRef()) });
									break;
                                }
                                catch {}
                            }
                            _isSend = false;
                        }
                    }
                }
            });
        }

        private decimal GetErr()// Расчёт допустимой погрешности в зависимости от установленной скорости.
        {
            if (_currS > 0 && _currS <= 0.7m)
            {
                return 0.02m;
            }
            if (_currS > 0.7m && _currS <= 30m)
            {
                //return 0.1m;
                return 0.05m;
            }
            throw new ArgumentOutOfRangeException(_currS.ToString(), "Недопустимое значение скорости");
        }

        public void AdjustS(ref decimal avgRefS, Checkpoint c, CancellationTokenSource t)// Метод для корректировки скорости эталона к установленному значению скорости.
        {
            var eCnt = 0;
            var step = c.Step;
            var signChangeCnt = 0;// Переменная для отслеживания смены знака у шага, с помощью которого корректируется частота.
            var currSign = Sing.Plus;// Знак шага, плюс или минус.
			Sing prevSign;// Старое значение для сравнения при изменении нового.
			var is1stStart = true;// Флаг для первого прохода, чтобы в случае смены знака stepValue, это не пошло в счётчик.
			while (true)
            {
				if (t.Token.IsCancellationRequested)
				{
					return;
				}
                if (signChangeCnt == 2)
                {
                    step = 10;
                }
                prevSign = currSign;
                if (IsErrValidation(ref avgRefS) && eCnt++ == 2)// Делаю проверку на 2 корректировки, чтобы в случае первой корректировки значение не уплыло из-за быстрой смены частоты вращения двигателя аэротрубы.
				{
					return;
                }
                currSign = _currS - avgRefS > 0 ? Sing.Plus : Sing.Minus;
				if (!is1stStart && currSign != prevSign)// Если это не первый прогон цикла.
				{
					signChangeCnt++;
                }
                CurrF += currSign == Sing.Plus ? step : -step;
                SetF(_currF, _currS);
                Thread.Sleep(LAT);
                is1stStart = false;
            }
        }

        private bool IsErrValidation(ref decimal avgRefS)// Проверка валидности эталонной скорости, относительно выставленной.
        {
            var e = GetErr();// Допустимая погрешность (0,02 или 0,1).
            var diff = _currS - avgRefS;// Разница между установленной скоростью и полученной с эталона.
            return e >= diff && diff >= -e;// Флаг, отвечающий за совпадение скоростей эталона и выставленной с учётом допустимой погрешности.
		}

        private static (byte, byte) GetCrc16(byte[] buff, int buffSize)// Возвращает CRC-16 в виде двух bytes.
        {
            var num1 = ushort.MaxValue;
            ushort buffInd = 0;
            while (buffSize > 0)
            {
                num1 ^= buff[buffInd];
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
        private readonly decimal[] _vPnts = [ 0m, 0.72m, 5m, 10m, 15m, 30m ];// Скоростные точки для расчёта коэффициента. Данные от сотрудников MD.
        private readonly decimal[] _kPnts = [ 0.866m, 0.866m, 0.96m, 0.94m, 0.953m, 1.03m ];// Коэффициенты, расчитанные для v_point (для каждого диапазона). Данные от сотрудников MD.
        private readonly decimal[] _aKoef = new decimal[5];
        private readonly decimal[] _bKoef = new decimal[5];

        private decimal CalculateS(decimal rawS)
        {
            var r = GetRange(rawS);
            return Math.Round(rawS * (_aKoef[r - 1] * rawS + _bKoef[r - 1]), 2);
        }

        private int GetRange(decimal rawS)
        {
            if (rawS < _vPnts[1])
			{
                return 1;
			}
            for (int i = 4; i > 0; i--)
			{
				if (rawS >= _vPnts[i])
				{
					return i + 1;
				}
			}
            throw new ArgumentOutOfRangeException(nameof(rawS));
        }
        #endregion
    }
}