using OfficeOpenXml;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;
using VerificationAirVelocitySensor.Model;
using VerificationAirVelocitySensor.View;
using VerificationAirVelocitySensor.ViewModel.BaseVm;
using VerificationAirVelocitySensor.ViewModel.Services;
using YamlDotNet.Serialization;

namespace VerificationAirVelocitySensor.ViewModel
{
    public class MainWindowVm : BaseVm.BaseVm
    {
        #region RealyCommand

        #region Частотомер ЧЗ-85/6

        #region Set Gate Time

        public RelayCommand SetGateTime1SCommand =>
            new RelayCommand(() => SetGateTime(GateTime.S1), SetGateTime1SValidation);

        public RelayCommand SetGateTime4SCommand =>
            new RelayCommand(() => SetGateTime(GateTime.S4), SetGateTime4SValidation);

        public RelayCommand SetGateTime7SCommand =>
            new RelayCommand(() => SetGateTime(GateTime.S7), SetGateTime7SValidation);

        public RelayCommand SetGateTime10SCommand =>
            new RelayCommand(() => SetGateTime(GateTime.S10), SetGateTime10SValidation);

        public RelayCommand SetGateTime100SCommand =>
            new RelayCommand(() => SetGateTime(GateTime.S100), SetGateTime100SValidation);

        #endregion

        public RelayCommand ResetCommand => new RelayCommand(FrequencyCounterDevice.Instance.RstCommand,
            FrequencyCounterDevice.Instance.IsOpen);

        public RelayCommand SetFrequencyChannel1Command =>
            new RelayCommand(() => SetFrequencyChannel(FrequencyChannel.Channel1), SetFrequencyChannel1Validation);

        public RelayCommand SetFrequencyChannel2Command =>
            new RelayCommand(() => SetFrequencyChannel(FrequencyChannel.Channel2), SetFrequencyChannel2Validation);

        public RelayCommand OnFilterChannel1Command =>
            new RelayCommand(() => OnOffFilter(1, true), OnFilterChannel1Validation);

        public RelayCommand OffFilterChannel1Command =>
            new RelayCommand(() => OnOffFilter(1, false), OffFilterChannel1Validation);

        public RelayCommand OnFilterChannel2Command =>
            new RelayCommand(() => OnOffFilter(2, true), OnFilterChannel2Validation);

        public RelayCommand OffFilterChannel2Command =>
            new RelayCommand(() => OnOffFilter(2, false), OffFilterChannel2Validation);

        public RelayCommand OpenPortFrequencyCounterCommand => new RelayCommand(OpenPortFrequencyCounterDevice);

        public RelayCommand ClosePortFrequencyCounterCommand =>
            new RelayCommand(() => FrequencyCounterDevice.Instance.ClosePort(), FrequencyCounterDevice.Instance.IsOpen);

        #endregion

        #region Анемометр / Частотный двигатель

        public RelayCommand OpenPortFrequencyMotorCommand =>
            new RelayCommand(() => FrequencyMotorDevice.Instance.OpenPort(ComPortFrequencyMotor));

        public RelayCommand ClosePortFrequencyMotorCommand =>
            new RelayCommand(() => FrequencyMotorDevice.Instance.ClosePort(), FrequencyMotorDevice.Instance.IsOpen);

        public RelayCommand StopFrequencyMotorCommand =>
            new RelayCommand(() => FrequencyMotorDevice.Instance.SetFrequency(0, 0),
                FrequencyMotorDevice.Instance.IsOpen);

        public RelayCommand SetSpeedFrequencyMotorCommand => new RelayCommand(SetSpeedFrequencyMotorMethodAsync,
            FrequencyMotorDevice.Instance.IsOpen);

        #endregion

        public RelayCommand ChangeTypeTestOnDvs2Command =>
            new RelayCommand(() => TypeTest = TypeTest.Dvs02, ChangeTypeTestOnDvs2Validation);

        public RelayCommand ChangeTypeTestOnDvs1Command =>
            new RelayCommand(() => TypeTest = TypeTest.Dvs01, ChangeTypeTestOnDvs1Validation);

        public RelayCommand OpenCloseConnectionMenuCommand => new RelayCommand(OpenCloseConnectionMenu);

        public RelayCommand OpenCloseDebuggingMenuCommand =>
            new RelayCommand(OpenCloseDebuggingMenu, OpenCloseDebuggingMenuValidation);

        public RelayCommand UpdateComPortsSourceCommand => new RelayCommand(UpdateComPortsSource);

        public RelayCommand StartTestCommand => new RelayCommand(StartTest, StartTestValidation);

        public RelayCommand StopTestCommand => new RelayCommand(StopTest, StopTestValidation);

        public RelayCommand OpenCloseSetSpeedPointsMenuCommand => new RelayCommand(OpenCloseSetSpeedPoints);

        public RelayCommand SaveSpeedsPointCommand => new RelayCommand(SaveSpeedPointsCollection);
        public RelayCommand SetDefaultSpeedPointsCommand => new RelayCommand(SetDefaultSpeedPoints);

        #endregion

        #region Property

        private CancellationTokenSource _ctsTask;
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
#pragma warning disable IDE0044 // Добавить модификатор только для чтения
        private UserSettings _userSettings;
#pragma warning restore IDE0044 // Добавить модификатор только для чтения
        private readonly object _locker = new object();

        private MeasurementsData _measurementsData;

        /// <summary>
        /// Свойство для хранения условий поверки
        /// </summary>
        public MeasurementsData MeasurementsData
        {
            get => _measurementsData;
            set
            {
                _measurementsData = value;
                OnPropertyChanged(nameof(MeasurementsData));
                _userSettings.MeasurementsData = value;
                Serialization();
            }
        }

        private string _pathSave;

        public string PathSave
        {
            get => _pathSave;
            set
            {
                _pathSave = value;
                OnPropertyChanged(nameof(PathSave));
                _userSettings.PathSave = value;
                Serialization();
            }
        }

        /// <summary>
        /// Свойство, для биндинга на интерфейс текущее действие внутри программы
        /// </summary>
        public string StatusCurrentAction { get; set; }

        private const string PathUserSettings = "UserSettings.txt";
        private const string FrequencyCounterId = "43-85/6";

        /// <summary>
        /// Активность BusyIndicator
        /// </summary>
        public bool IsBusy { get; set; }

        /// <summary>
        /// Текст busyIndicator
        /// </summary>
        public string BusyContent { get; set; }

        /// <summary>
        /// Флаг показывающий активно ли тестирование.
        /// </summary>
        public bool IsTestActive { get; set; }

        public decimal FrequencyCounterValue { get; set; }
        public bool VisibilityConnectionMenu { get; set; }
        public bool VisibilityDebuggingMenu { get; set; }
        public bool VisibilitySetSpeedPoints { get; set; }

        public ObservableCollection<string> PortsList { get; set; } =
            new ObservableCollection<string>(SerialPort.GetPortNames());

        public bool FrequencyCounterIsOpen { get; set; }
        public bool FrequencyMotorIsOpen { get; set; }

        /// <summary>
        /// Параметр для установки частотв трубы.
        /// </summary>
        public int SetFrequencyMotor { get; set; }

        /// <summary>
        /// Эталонное значение скорости с частотной трубы
        /// </summary>
        public decimal SpeedReferenceValue { get; set; }

        public ObservableCollection<decimal> AverageSpeedReferenceCollection { get; set; } =
            new ObservableCollection<decimal>();

        private decimal _averageSpeedReferenceValue;

        private void UpdateAverageSpeedReferenceValue(decimal newValue)
        {
            if (AverageSpeedReferenceCollection.Count > 5 && _acceptCorrectionReference == false)
            {
                AverageSpeedReferenceCollection.RemoveAt(0);
                //AverageSpeedReferenceCollection.Clear();
            }

            AverageSpeedReferenceCollection.Add(newValue);
            _averageSpeedReferenceValue = Math.Round(AverageSpeedReferenceCollection.Average(), 2);
        }

        /// <summary>
        /// Флаг для включения в колекцию среднего значения эталона , 
        /// всех его значений за время теста скоростной точки после прохождения корректировки.
        /// </summary>
        private bool _acceptCorrectionReference;

        /// <summary>
        /// Время ожидания после установки значения частоты, что бы дать аэротрубе стабилизировать значение
        /// </summary>
        public int WaitSetFrequency { get; set; } = 5000;

        /// <summary>
        /// Флаг для биндинга отображения таблицы разультатов для Dvs1
        /// </summary>
        public bool Dvs1ContentVisibility { get; set; }

        /// <summary>
        /// Флаг для биндинга отображения таблицы разультатов для Dvs2
        /// </summary>
        public bool Dvs2ContentVisibility { get; set; } = true;

        //Все свойства что ниже, должны сохранятся пре перезапуске.
        //TODO Позже сделать переключатель на интерфейсе.
        private TypeTest _typeTest = TypeTest.Dvs02;

        public TypeTest TypeTest
        {
            get => _typeTest;
            set
            {
                _typeTest = value;
                OnPropertyChanged(nameof(TypeTest));
                _userSettings.TypeTest = value;
                Serialization();

                switch (value)
                {
                    case TypeTest.Dvs01:
                        Dvs1ContentVisibility = true;
                        Dvs2ContentVisibility = false;
                        break;
                    case TypeTest.Dvs02:
                        Dvs1ContentVisibility = false;
                        Dvs2ContentVisibility = true;
                        break;
                }
            }
        }

        private bool _filterChannel1;

        public bool FilterChannel1
        {
            get => _filterChannel1;
            set
            {
                _filterChannel1 = value;
                OnPropertyChanged(nameof(FilterChannel1));
                _userSettings.FilterChannel1 = value;
                Serialization();
            }
        }

        private bool _filterChannel2;

        public bool FilterChannel2
        {
            get => _filterChannel2;
            set
            {
                _filterChannel2 = value;
                OnPropertyChanged(nameof(FilterChannel2));
                _userSettings.FilterChannel2 = value;
                Serialization();
            }
        }

        private FrequencyChannel _frequencyChannel;

        public FrequencyChannel FrequencyChannel
        {
            get => _frequencyChannel;
            set
            {
                _frequencyChannel = value;
                OnPropertyChanged(nameof(FrequencyChannel));
                _userSettings.FrequencyChannel = value;
                Serialization();
            }
        }

        private GateTime _gateTime;

        public GateTime GateTime
        {
            get => _gateTime;
            set
            {
                _gateTime = value;
                OnPropertyChanged(nameof(GateTime));
                _userSettings.GateTime = value;
                Serialization();
            }
        }

        private string _comPortFrequencyMotor;

        public string ComPortFrequencyMotor
        {
            get => _comPortFrequencyMotor;
            set
            {
                _comPortFrequencyMotor = value;
                OnPropertyChanged(nameof(ComPortFrequencyMotor));
                _userSettings.ComPortFrequencyMotor = value;
                Serialization();
            }
        }

        private string _comPortFrequencyCounter;

        public string ComPortFrequencyCounter
        {
            get => _comPortFrequencyCounter;
            set
            {
                _comPortFrequencyCounter = value;
                OnPropertyChanged(nameof(ComPortFrequencyCounter));
                _userSettings.ComPortFrequencyCounter = value;
                Serialization();
            }
        }

        #endregion

        #region RelayCommand Method

        private void SaveSpeedPointsCollection()
        {
            _userSettings.SpeedPointsList.Clear();

            foreach (var speedPoint in SpeedPointsList)
                _userSettings.SpeedPointsList.Add(speedPoint);

            Serialization();
        }

        private void SetDefaultSpeedPoints()
        {
            SpeedPointsList.Clear();

            foreach (var defaultSpeedPoint in _defaultSpeedPoints)
                SpeedPointsList.Add(defaultSpeedPoint);
        }

        private void OpenPortFrequencyCounterDevice()
        {       
            FrequencyCounterDevice.Instance.OpenPort(ComPortFrequencyCounter);

            if (!FrequencyCounterIsOpen) return;


            Task.Run(async () => await Task.Run(() =>
            {
                IsBusy = true;
                //TODO доделать когда буду работать с устройством.
                var answer = FrequencyCounterDevice.Instance.GetModelVersion();
        
                // 43-85/6
                OnOffFilter(1, true);
                OnOffFilter(2, true);
                SetGateTime(GateTime.S4);
                IsBusy = false;
            }));
        }

        private static bool ValidationIsOpenPorts()
        {
            if (!FrequencyMotorDevice.Instance.IsOpen())
            {
                MessageBox.Show("Порт частотного двигателя закрыт", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            if (!FrequencyCounterDevice.Instance.IsOpen())
            {
                MessageBox.Show("Порт частотомера закрыт", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private void UpdateComPortsSource()
        {
            var newPortList = new ObservableCollection<string>(SerialPort.GetPortNames());

            //Добавляю новые итемы из полученной коллекции.
            foreach (var port in newPortList)
            {
                if (!PortsList.Contains(port))
                {
                    PortsList.Add(port);
                }
            }


            var deletePorts = new ObservableCollection<string>();

            //Записываю старые итемы в коллекцию на удаление
            foreach (var port in PortsList)
            {
                if (!newPortList.Contains(port))
                {
                    deletePorts.Add(port);
                }
            }

            //Удаляю лишние элементы
            foreach (var port in deletePorts)
            {
                PortsList.Remove(port);
            }
        }

        private void OpenCloseConnectionMenu() => VisibilityConnectionMenu = !VisibilityConnectionMenu;
        private void OpenCloseDebuggingMenu() => VisibilityDebuggingMenu = !VisibilityDebuggingMenu;

        private void OpenCloseSetSpeedPoints()
        {
            VisibilitySetSpeedPoints = !VisibilitySetSpeedPoints;
            //Если закрываем окошко, то выполняем сохранение коллекции скоростей в пользовательские настройки
            if (VisibilitySetSpeedPoints) return;

            SaveSpeedPointsCollection();
            Serialization();
        }

        private bool OpenCloseDebuggingMenuValidation() => FrequencyMotorIsOpen;

        #region Частотомер ЧЗ-85/6

        private void SetGateTime(GateTime gateTime)
        {
            FrequencyCounterDevice.Instance.SetGateTime(gateTime);
            GateTime = gateTime;
        }

        private bool SetGateTime1SValidation() =>
            GateTime != GateTime.S1 && FrequencyCounterDevice.Instance.IsOpen();

        private bool SetGateTime4SValidation() =>
            GateTime != GateTime.S4 && FrequencyCounterDevice.Instance.IsOpen();

        private bool SetGateTime7SValidation() =>
            GateTime != GateTime.S7 && FrequencyCounterDevice.Instance.IsOpen();

        private bool SetGateTime10SValidation() =>
            GateTime != GateTime.S10 && FrequencyCounterDevice.Instance.IsOpen();

        private bool SetGateTime100SValidation() =>
            GateTime != GateTime.S100 && FrequencyCounterDevice.Instance.IsOpen();

        private void SetFrequencyChannel(FrequencyChannel frequencyChannel)
        {
            FrequencyCounterDevice.Instance.SetChannelFrequency(frequencyChannel);
            FrequencyChannel = frequencyChannel;
        }

        private bool SetFrequencyChannel1Validation() =>
            FrequencyChannel != FrequencyChannel.Channel1 && FrequencyCounterDevice.Instance.IsOpen();

        private bool SetFrequencyChannel2Validation() =>
            FrequencyChannel != FrequencyChannel.Channel2 && FrequencyCounterDevice.Instance.IsOpen();

        private void OnOffFilter(int channel, bool isOn)
        {
            switch (channel)
            {
                case 1:
                    FrequencyCounterDevice.Instance.SwitchFilter(1, isOn);
                    FilterChannel1 = isOn;
                    break;
                case 2:
                    FrequencyCounterDevice.Instance.SwitchFilter(2, isOn);
                    FilterChannel2 = isOn;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool OnFilterChannel1Validation() =>
            FrequencyCounterDevice.Instance.IsOpen() && !FilterChannel1;

        private bool OffFilterChannel1Validation() =>
            FrequencyCounterDevice.Instance.IsOpen() && FilterChannel1;

        private bool OnFilterChannel2Validation() =>
            FrequencyCounterDevice.Instance.IsOpen() && !FilterChannel2;

        private bool OffFilterChannel2Validation() =>
            FrequencyCounterDevice.Instance.IsOpen() && FilterChannel2;

        #endregion

        private void SetSpeedFrequencyMotorMethodAsync()
        {
            Task.Run(async () =>
                await Task.Run(() => FrequencyMotorDevice.Instance.SetFrequency(SetFrequencyMotor, 0)));
        }

        #endregion


        /// <summary>
        /// Значения скорости на которых нужно считать значения датчика.
        /// </summary>
        public ObservableCollection<SpeedPoint> SpeedPointsList { get; set; } =
            new ObservableCollection<SpeedPoint>
            {
                new SpeedPoint
                    {Id = 1, Speed = 0.7m, SetFrequency = 445, MaxStep = 10, MinEdge = 0m, MaxEdge = 3.007m},
                new SpeedPoint
                    {Id = 2, Speed = 5m, SetFrequency = 2605, MaxStep = 20, MinEdge = 3.320m, MaxEdge = 8.837m},
                new SpeedPoint
                    {Id = 3, Speed = 10m, SetFrequency = 5650, MaxStep = 20, MinEdge = 9.634m, MaxEdge = 15.595m},
                new SpeedPoint
                    {Id = 4, Speed = 15m, SetFrequency = 7750, MaxStep = 20, MinEdge = 15.935m, MaxEdge = 22.366m},
                new SpeedPoint
                    {Id = 5, Speed = 20m, SetFrequency = 10600, MaxStep = 30, MinEdge = 22.248m, MaxEdge = 29.124m},
                new SpeedPoint
                    {Id = 6, Speed = 25m, SetFrequency = 13600, MaxStep = 30, MinEdge = 28.549m, MaxEdge = 35.895m},
                new SpeedPoint
                    {Id = 7, Speed = 30m, SetFrequency = 16384, MaxStep = 30, MinEdge = 32.340m, MaxEdge = 39.948m}
            };

        /// <summary>
        /// коллекция для востановления дефолтных настроек
        /// </summary>
        private readonly ObservableCollection<SpeedPoint> _defaultSpeedPoints = new ObservableCollection<SpeedPoint>
        {
            new SpeedPoint
                {Id = 1, Speed = 0.7m, SetFrequency = 445, MaxStep = 10, MinEdge = 0m, MaxEdge = 3.007m},
            new SpeedPoint
                {Id = 2, Speed = 5m, SetFrequency = 2605, MaxStep = 20, MinEdge = 3.320m, MaxEdge = 8.837m},
            new SpeedPoint
                {Id = 3, Speed = 10m, SetFrequency = 5650, MaxStep = 20, MinEdge = 9.634m, MaxEdge = 15.595m},
            new SpeedPoint
                {Id = 4, Speed = 15m, SetFrequency = 7750, MaxStep = 20, MinEdge = 15.935m, MaxEdge = 22.366m},
            new SpeedPoint
                {Id = 5, Speed = 20m, SetFrequency = 10600, MaxStep = 30, MinEdge = 22.248m, MaxEdge = 29.124m},
            new SpeedPoint
                {Id = 6, Speed = 25m, SetFrequency = 13600, MaxStep = 30, MinEdge = 28.549m, MaxEdge = 35.895m},
            new SpeedPoint
                {Id = 7, Speed = 30m, SetFrequency = 16384, MaxStep = 30, MinEdge = 32.340m, MaxEdge = 39.948m}
        };

        #region Работа с коэффициентом для обработки получаемого с анемометра значения

        /// <summary>
        /// Скоростные точки для расчета коефа . Данные от сотрудников Аэро Трубы
        /// </summary>
        private readonly decimal[] _vPoint = {0m, 0.72m, 5m, 10m, 15m, 30m};

        /// <summary>
        /// Коефы расчитанные для v_point (для каждого диапазона) . Данные от сотрудников Аэро Трубы
        /// </summary>
        private readonly decimal[] _kPoint = {0.866m, 0.866m, 0.96m, 0.94m, 0.953m, 1.03m};

        private readonly decimal[] _aKoef = new decimal[5];
        private readonly decimal[] _bKoef = new decimal[5];

        private void Get_a_b_koef()
        {
            for (var i = 0; i < 6; i++)
            {
                if (i == 0) continue;
                _aKoef[i - 1] = (_kPoint[i] - _kPoint[i - 1]) / (_vPoint[i] - _vPoint[i - 1]);
                _bKoef[i - 1] = _kPoint[i] - _aKoef[i - 1] * _vPoint[i];
            }
        }

        private decimal SpeedCalculation(decimal rawSpeed)
        {
            var rangeValue = GetRange(rawSpeed);


            var a = _aKoef[rangeValue - 1];
            var b = _bKoef[rangeValue - 1];

            var speedCoefficient = a * rawSpeed + b;

            var newSpeed = Math.Round(rawSpeed * speedCoefficient, 2);

            return newSpeed;
        }

        private int GetRange(decimal rawSpeed)
        {
            if (rawSpeed < _vPoint[1])
                return 1;
            if (rawSpeed >= _vPoint[4])
                return 5;
            if (rawSpeed >= _vPoint[3])
                return 4;
            if (rawSpeed >= _vPoint[2])
                return 3;
            if (rawSpeed >= _vPoint[1])
                return 2;

            var errorMessage = "Значение эталона вне диапазона от 0 до 30";
            throw new ArgumentOutOfRangeException(errorMessage);
        }

        #endregion


        /// <summary>
        /// Проверка запроса на отмену
        /// </summary>
        /// <param name="ctSource"></param>
        /// <returns></returns>
        private bool IsCancellationRequested(CancellationTokenSource ctSource) =>
            ctSource.Token.IsCancellationRequested;

        #region Test Method

        public ObservableCollection<DvsValue> CollectionDvsValue { get; set; }
            = new ObservableCollection<DvsValue>();

        private void StopTest()
        {
            BusyContent = "Тестирование прервано пользователем \r\n Ожидание завершение процесса";
            IsBusy = true;
            IsTestActive = false;

            _ctsTask.Cancel();
        }

        private bool StopTestValidation() =>
            IsTestActive;

        private bool StartTestValidation() =>
            !IsTestActive;

        private bool ChangeTypeTestOnDvs1Validation() => !IsTestActive && TypeTest == TypeTest.Dvs02;
        private bool ChangeTypeTestOnDvs2Validation() => !IsTestActive && TypeTest == TypeTest.Dvs01;

        private void StartTest()
        {
            if (!ValidationIsOpenPorts()) return;

            if (OpenMeasurementsData()) return;

            IsTestActive = true;

            Task.Run(async () => await Task.Run(() =>
            {
                _ctsTask = new CancellationTokenSource();

                //IsBusy = true;
                //BusyContent = "Отправка сохраненных настроек на Частотомер";
                //FrequencyCounterDevice.Instance.RstCommand();
                //FrequencyCounterDevice.Instance.SetUserSettings();
                //IsBusy = false;
                //BusyContent = string.Empty;

                switch (TypeTest)
                {
                    case TypeTest.Dvs01:

                        break;
                    case TypeTest.Dvs02:
                        LoadDefaultValueCollectionDvs2Value();
                        try
                        {
                            StartTestDvs02(GateTime);
                        }
                        catch (Exception e)
                        {
                            GlobalLog.Log.Debug(e, e.Message);
                        }
                        finally
                        {
                            FrequencyMotorDevice.Instance.SetFrequency(0, 0);

                            ResultToXlsxDvs2();

                            IsTestActive = false;

                            MessageBox.Show("Поверка завершена", "Внимание", MessageBoxButton.OK, MessageBoxImage.Asterisk, MessageBoxResult.OK);
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }));
        }

        private bool OpenMeasurementsData()
        {
            var setMeasurementsDataVm = new SetMeasurementsDataVm(this);
            var setMeasurementsData = new SetMeasurementsData(setMeasurementsDataVm);
            setMeasurementsData.ShowDialog();
            var isContinue = setMeasurementsData.ViewModel.IsContinue;
            setMeasurementsData.Close();

            Serialization();

            return !isContinue;
        }

        /// <summary>
        /// Метод для очистки от старых значений  CollectionDvsValue и заполнением пустых значений. Для ДВС2
        /// </summary>
        public void LoadDefaultValueCollectionDvs2Value()
        {
            Application.Current.Dispatcher?.Invoke(() =>
            {
                CollectionDvsValue?.Clear();

                CollectionDvsValue = new ObservableCollection<DvsValue>();

                foreach (var point in SpeedPointsList)
                    CollectionDvsValue.Add(new DvsValue(point.Speed));
            });
        }


        public void StartTestDvs02(GateTime gateTime)
        {
            StatusCurrentAction = "Запуск тестирования";

            var timeOutCounter = FrequencyCounterDevice.Instance.GateTimeToMSec(gateTime) + 1000;

            if (IsCancellationRequested(_ctsTask)) return;

            foreach (var point in SpeedPointsList)
            {
                StatusCurrentAction = $"Точка {point.Speed}";

                _acceptCorrectionReference = false;

                FrequencyMotorDevice.Instance.SetFrequency(point.SetFrequency, point.Speed);

                //Время ожидания для стабилизации трубы
                Thread.Sleep(WaitSetFrequency);

                Application.Current.Dispatcher?.Invoke(AverageSpeedReferenceCollection.Clear);

                StatusCurrentAction = $"Точка {point.Speed} : Корректировка скорости";

                if (point.Speed == 30)
                    Thread.Sleep(15000);

                //Для скоростной точки 30, отключаю коррекцию скорости, так как труба не может разогнаться до 30 м/с . 
                //А где-то до 27-29 м/с
                if (point.Speed != 30)
                    FrequencyMotorDevice.Instance.CorrectionSpeedMotor(ref _averageSpeedReferenceValue, point);

                if (IsCancellationRequested(_ctsTask)) return;

                _acceptCorrectionReference = true;

                Thread.Sleep(100);

                //На данной скорости, датчик вращается очень медленно. 
                //А значение поступает на частотомер, в момент полного оборота датчика.
                if (point.Speed == 0.7m)
                    timeOutCounter = 5000;

                //Так как номеровка идет с 1 , а коллекция с 0
                var id = point.Id - 1;


                CollectionDvsValue[id].DeviceSpeedValue1.IsСheckedNow = true;
                StatusCurrentAction = $"Точка {point.Speed} : Снятие значения 1";
                var value1 = FrequencyCounterDevice.Instance.GetCurrentHzValue(point, timeOutCounter);
                if (IsCancellationRequested(_ctsTask)) return;
                CollectionDvsValue[id].DeviceSpeedValue1.ResultValue = value1;
                CollectionDvsValue[id].DeviceSpeedValue1.IsVerified = true;
                Thread.Sleep(timeOutCounter);
                if (IsCancellationRequested(_ctsTask)) return;

                CollectionDvsValue[id].DeviceSpeedValue2.IsСheckedNow = true;
                StatusCurrentAction = $"Точка {point.Speed} : Снятие значения 2";
                var value2 = FrequencyCounterDevice.Instance.GetCurrentHzValue(point, timeOutCounter);
                if (IsCancellationRequested(_ctsTask)) return;
                CollectionDvsValue[id].DeviceSpeedValue2.ResultValue = value2;
                CollectionDvsValue[id].DeviceSpeedValue2.IsVerified = true;
                Thread.Sleep(timeOutCounter);
                if (IsCancellationRequested(_ctsTask)) return;

                CollectionDvsValue[id].DeviceSpeedValue3.IsСheckedNow = true;
                StatusCurrentAction = $"Точка {point.Speed} : Снятие значения 3";
                var value3 = FrequencyCounterDevice.Instance.GetCurrentHzValue(point, timeOutCounter);
                if (IsCancellationRequested(_ctsTask)) return;
                CollectionDvsValue[id].DeviceSpeedValue3.ResultValue = value3;
                CollectionDvsValue[id].DeviceSpeedValue3.IsVerified = true;
                Thread.Sleep(timeOutCounter);
                if (IsCancellationRequested(_ctsTask)) return;

                CollectionDvsValue[id].DeviceSpeedValue4.IsСheckedNow = true;
                StatusCurrentAction = $"Точка {point.Speed} : Снятие значения 4";
                var value4 = FrequencyCounterDevice.Instance.GetCurrentHzValue(point, timeOutCounter);
                if (IsCancellationRequested(_ctsTask)) return;
                CollectionDvsValue[id].DeviceSpeedValue4.ResultValue = value4;
                CollectionDvsValue[id].DeviceSpeedValue4.IsVerified = true;
                Thread.Sleep(timeOutCounter);
                if (IsCancellationRequested(_ctsTask)) return;

                CollectionDvsValue[id].DeviceSpeedValue5.IsСheckedNow = true;
                StatusCurrentAction = $"Точка {point.Speed} : Снятие значения 5";
                var value5 = FrequencyCounterDevice.Instance.GetCurrentHzValue(point, timeOutCounter);
                if (IsCancellationRequested(_ctsTask)) return;
                CollectionDvsValue[id].DeviceSpeedValue5.ResultValue = value5;
                CollectionDvsValue[id].DeviceSpeedValue5.IsVerified = true;
                Thread.Sleep(timeOutCounter);
                if (IsCancellationRequested(_ctsTask)) return;

                BusyContent = string.Empty;
                IsBusy = false;

                CollectionDvsValue[id].ReferenceSpeedValue = _averageSpeedReferenceValue;
            }


            StatusCurrentAction = $"Поверка завершена ";
        }

        private void ResultToXlsxDvs2()
        {
            var pathExampleXlsxFile = @"Resources\Dvs2.xlsx";
            while (true)
            {
                if (File.Exists(pathExampleXlsxFile))
                    break;

                var errorMessage =
                    "Отсутствует файл образец test_cs_protocol.xlsx  " +
                    "Пожалуйста поместите файл и повторите попытку(ОК). Или нажмите отмена для пропуска создания .xlsx";

                var mb = MessageBox.Show(errorMessage, "Ошибка", MessageBoxButton.OKCancel);

                //Если было нажато ОК
                if (mb == MessageBoxResult.OK)
                    continue;

                //Если была нажата отмена
                return;
            }

            using (var package = new ExcelPackage(new FileInfo(pathExampleXlsxFile)))
            {
                var ws = package.Workbook.Worksheets.First();

                #region Заполнение значений

                for (var i = 0; i < 7; i++)
                {
                    AddValueInCell(ws.Cells[i + 12, 12], CollectionDvsValue[i].ReferenceSpeedValue);
                    AddValueInCell(ws.Cells[i + 12, 13], CollectionDvsValue[i].DeviceSpeedValue1.ResultValue);
                    AddValueInCell(ws.Cells[i + 12, 14], CollectionDvsValue[i].DeviceSpeedValue2.ResultValue);
                    AddValueInCell(ws.Cells[i + 12, 15], CollectionDvsValue[i].DeviceSpeedValue3.ResultValue);
                    AddValueInCell(ws.Cells[i + 12, 16], CollectionDvsValue[i].DeviceSpeedValue4.ResultValue);
                    AddValueInCell(ws.Cells[i + 12, 17], CollectionDvsValue[i].DeviceSpeedValue5.ResultValue);
                }


                //Условия поверки

                ws.Cells[47, 16].Value = MeasurementsData.Verifier;
                ws.Cells[47, 21].Value = $"{DateTime.Now:dd.MM.yyyy}";

                ws.Cells[23, 5].Value = MeasurementsData.Temperature;
                ws.Cells[24, 5].Value = MeasurementsData.Humidity;
                ws.Cells[25, 5].Value = MeasurementsData.Pressure;
                ws.Cells[14, 6].Value = MeasurementsData.DeviceId;
                //ws.Cells[5, 4].Value = "Протокол ДВС-02 №00212522 от 10.01.2021";

                #endregion

                var path = $"{DateTime.Now:dd.MM.yyyy_HH-mm-ss}.xlsx";

                package.SaveAs(new FileInfo(Path.Combine(PathSave, path)));
            }
        }

        /// <summary>
        /// Метод для добавления значения в ячейку exel и ее обработка
        /// </summary>
        private void AddValueInCell(ExcelRange excelRange, decimal? value)
        {
            if (value != null)
                excelRange.Value = value;

            excelRange.Style.Numberformat.Format = "#,###0.000";
        }

        #endregion


        public MainWindowVm()
        {
            FrequencyCounterDevice.Instance.IsOpenUpdate += FrequencyCounter_IsOpenUpdate;
            FrequencyMotorDevice.Instance.IsOpenUpdate += FrequencyMotor_IsOpenUpdate;
            FrequencyMotorDevice.Instance.UpdateReferenceValue += FrequencyMotor_UpdateReferenceValue;
            FrequencyMotorDevice.Instance.UpdateSetFrequency += FrequencyMotor_UpdateSetFrequency;

            SpeedPointsList.CollectionChanged += DefaultSpeedPoints_CollectionChanged;

            var deserialization = Deserialization();
            _userSettings = deserialization ?? new UserSettings();
            FilterChannel1 = _userSettings.FilterChannel1;
            FilterChannel2 = _userSettings.FilterChannel2;
            FrequencyChannel = _userSettings.FrequencyChannel;
            GateTime = _userSettings.GateTime;
            ComPortFrequencyMotor = _userSettings.ComPortFrequencyMotor;
            ComPortFrequencyCounter = _userSettings.ComPortFrequencyCounter;
            MeasurementsData = _userSettings.MeasurementsData;
            PathSave = _userSettings.PathSave;

            if (_userSettings.SpeedPointsList != null && _userSettings.SpeedPointsList.Count != 0)
            {
                SpeedPointsList.Clear();
                foreach (var speedPoint in _userSettings.SpeedPointsList)
                    SpeedPointsList.Add(speedPoint);
            }

            Get_a_b_koef();

            #region Код для теста

            //var dvsValue1 = new DvsValue(5)
            //{
            //    DeviceSpeedValue1 = new SpeedValue {IsVerified = true, IsСheckedNow = true, ResultValue = 4.32m},
            //    DeviceSpeedValue2 = new SpeedValue(),
            //    DeviceSpeedValue3 = new SpeedValue(),
            //    DeviceSpeedValue4 = new SpeedValue(),
            //    DeviceSpeedValue5 = new SpeedValue(),
            //    ReferenceSpeedValue = 5
            //};
            //var dvsValue2 = new DvsValue(10)
            //{
            //    DeviceSpeedValue1 = new SpeedValue {IsVerified = false, IsСheckedNow = false, ResultValue = 14.32m},
            //    ReferenceSpeedValue = 10
            //};
            //var dvsValue3 = new DvsValue(15)
            //{
            //    DeviceSpeedValue1 = new SpeedValue {IsVerified = false, IsСheckedNow = true, ResultValue = 24.32m},
            //    ReferenceSpeedValue = 15
            //};
            //var dvsValue4 = new DvsValue(20)
            //{
            //    DeviceSpeedValue1 = new SpeedValue { IsVerified = false, IsСheckedNow = false, ResultValue = 14.32m },
            //    ReferenceSpeedValue = 20
            //};
            //var dvsValue5 = new DvsValue(25)
            //{
            //    DeviceSpeedValue1 = new SpeedValue { IsVerified = false, IsСheckedNow = true, ResultValue = 24.32m },
            //    ReferenceSpeedValue = 25
            //};
            //var dvsValue6 = new DvsValue(30)
            //{
            //    DeviceSpeedValue1 = new SpeedValue { IsVerified = false, IsСheckedNow = false, ResultValue = 14.32m },
            //    ReferenceSpeedValue = 30
            //};

            //CollectionDvsValue.Add(dvsValue1);
            //CollectionDvsValue.Add(dvsValue2);
            //CollectionDvsValue.Add(dvsValue3);
            //CollectionDvsValue.Add(dvsValue4);
            //CollectionDvsValue.Add(dvsValue5);
            //CollectionDvsValue.Add(dvsValue6);

            #endregion
        }

        private void DefaultSpeedPoints_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            for (var i = 0; i < SpeedPointsList.Count; i++)
                SpeedPointsList[i].Id = i + 1;
        }


        #region EventHandler Method

        private void FrequencyMotor_UpdateSetFrequency(object sender, UpdateSetFrequencyEventArgs e)
        {
            SetFrequencyMotor = e.SetFrequency;
        }

        private void FrequencyMotor_UpdateReferenceValue(object sender, UpdateReferenceValueEventArgs e)
        {
            var newSpeed = SpeedCalculation((decimal) e.ReferenceValue);
            SpeedReferenceValue = newSpeed;
            UpdateAverageSpeedReferenceValue(SpeedReferenceValue);
        }

        private void FrequencyMotor_IsOpenUpdate(object sender, IsOpenFrequencyMotorEventArgs e)
        {
            FrequencyMotorIsOpen = e.IsOpen;
        }

        private void FrequencyCounter_IsOpenUpdate(object sender, IsOpenFrequencyCounterEventArgs e)
        {
            FrequencyCounterIsOpen = e.IsOpen;
        }

        #endregion

        #region deserilize / serilize

        private void Serialization()
        {
            try
            {
                var serializer = new Serializer();

                lock (_locker)
                {
                    using (var file = File.Open(PathUserSettings, FileMode.Create))
                    {
                        using (var writer = new StreamWriter(file))
                        {
                            serializer.Serialize(writer, _userSettings);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private UserSettings Deserialization()
        {
            var deserializer = new Deserializer();

            using (var file = File.Open(PathUserSettings, FileMode.OpenOrCreate, FileAccess.Read))
            {
                using (var reader = new StreamReader(file))
                {
                    try
                    {
                        var userSettings = deserializer.Deserialize<UserSettings>(reader);

                        return userSettings;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
        }

        #endregion
    }

    public class SpeedPoint
    {
        /// <summary>
        /// Тестируемая скорость
        /// </summary>
        public decimal Speed { get; set; }

        /// <summary>
        /// Примерная частота вращения трубы, для достижения этой скорости
        /// </summary>
        public int SetFrequency { get; set; }

        /// <summary>
        /// Максимальный шаг при корректировке частоты, для достижения установленной скорости 
        /// </summary>
        public int MaxStep
        {
            get => _maxStep;
            set
            {
                if (value < 10 || value > 100)
                {
                    MessageBox.Show("Выберети значение в диапазоне от 10 до 100 Гц");

                    MaxStep = _maxStep;
                    return;
                }

                //MaxStep = value;
                _maxStep = value;
            }
        }

        [XmlIgnore] private int _maxStep = 10;

        /// <summary>
        /// Номер в списке
        /// </summary>
        public int Id { get; set; }

        public decimal MaxEdge { get; set; }

        public decimal MinEdge { get; set; }
    }

    public enum TypeTest
    {
        Dvs01 = 1,
        Dvs02 = 2
    }
}