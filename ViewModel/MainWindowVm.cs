using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        public RelayCommand ResetCommand => new RelayCommand(FrequencyCounterDevice.Instance.RstCommand,
            FrequencyCounterDevice.Instance.IsOpen);


        public RelayCommand ClosePortFrequencyCounterCommand =>
            new RelayCommand(() => FrequencyCounterDevice.Instance.ClosePort(), FrequencyCounterDevice.Instance.IsOpen);

        #endregion

        #region Анемометр / Частотный двигатель

        public RelayCommand ClosePortFrequencyMotorCommand =>
            new RelayCommand(() => FrequencyMotorDevice.Instance.ClosePort(), FrequencyMotorDevice.Instance.IsOpen);

        #endregion


        public RelayCommand StartTestCommand => new RelayCommand(StartTest, StartTestValidation);

        public RelayCommand StopTestCommand => new RelayCommand(StopTest, StopTestValidation);

        public RelayCommand SaveSpeedsPointCommand => new RelayCommand(SaveSpeedPointsCollection);

        public RelayCommand ChangeVisibilitySetFrequencyCommand => new RelayCommand(o =>
        {
            VisibilitySetFrequency = !VisibilitySetFrequency;
        });

        #region Команды смены страницы

        public RelayCommand GoOnMainWindowCommand =>
            new RelayCommand(ChangePageOnMainWindow, o => SelectedPage != SelectedPage.MainWindow && !IsTestActive);

        public RelayCommand GoOnSettingsCommand =>
            new RelayCommand(ChangePageOnSettings, o => SelectedPage != SelectedPage.Settings && !IsTestActive);

        public RelayCommand GoOnCheckpointsCommand =>
            new RelayCommand(ChangePageOnCheckPoints, o => SelectedPage != SelectedPage.Checkpoint && !IsTestActive);

        public RelayCommand GoOnDebugCommand =>
            new RelayCommand(ChangePageOnDebug, o => SelectedPage != SelectedPage.Debug && !IsTestActive);

        #endregion

        #endregion

        #region Property

        public UserControl FrameContent { get; set; }
        public SelectedPage SelectedPage { get; set; }

        public SettingsModel SettingsModel { get; set; } = new SettingsModel();

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

        private string _pathSave = string.Empty;

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
        const string IdFreCounterDevice = "43-85/6";

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


        public ObservableCollection<string> PortsList { get; set; } =
            new ObservableCollection<string>(SerialPort.GetPortNames());

        public bool FrequencyCounterIsOpen { get; set; }
        public bool FrequencyMotorIsOpen { get; set; }

        /// <summary>
        /// Эталонное значение скорости с частотной трубы
        /// </summary>
        private decimal _speedReferenceValue;

        public decimal SpeedReferenceValue
        {
            get => _speedReferenceValue;
            set
            {
                _speedReferenceValue = value;
                OnPropertyChanged(nameof(SpeedReferenceValue));
            }
        }

        public int SetFrequencyMotor { get; set; }
        public bool VisibilitySetFrequency { get; set; }

        public ObservableCollection<decimal> AverageSpeedReferenceCollection { get; set; } =
            new ObservableCollection<decimal>();

        private decimal _averageSpeedReferenceValue;

        private void UpdateAverageSpeedReferenceValue(decimal newValue)
        {
            if (AverageSpeedReferenceCollection.Count > 5 && _acceptCorrectionReference == false)
            {
                AverageSpeedReferenceCollection.RemoveAt(0);
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

        #endregion

        #region RelayCommand Method

        #region методы смены страниц

        private void ChangePageOnMainWindow()
        {
            FrameContent = null;
            SelectedPage = SelectedPage.MainWindow;
        }

        private void ChangePageOnSettings()
        {
            FrameContent = new SettingsView(new SettingsVm(SettingsModel, UpdateSettingsAndSerialization));
            SelectedPage = SelectedPage.Settings;
        }

        private void ChangePageOnCheckPoints()
        {
            FrameContent = new SpeedPointsView(SpeedPointsList, SaveSpeedsPointCommand);
            SelectedPage = SelectedPage.Checkpoint;
        }

        private void ChangePageOnDebug()
        {
            Task.Run(async () => await Task.Run(() =>
            {
                IsBusy = true;
                BusyContent = "Проверка подключения Аэродинамической трубы";
                var validComMotor = FrequencyMotorDevice.Instance.OpenPort(SettingsModel.ComPortFrequencyMotor);

                if (validComMotor == false)
                {
                    IsBusy = false;
                    BusyContent = string.Empty;
                    return;
                }

                IsBusy = false;
                BusyContent = string.Empty;

                Application.Current.Dispatcher?.Invoke(() => { FrameContent = new DebugView(); });

                SelectedPage = SelectedPage.Debug;
            }));
        }

        #endregion

        private void SaveSpeedPointsCollection()
        {
            _userSettings.SpeedPointsList.Clear();

            foreach (var speedPoint in SpeedPointsList)
                _userSettings.SpeedPointsList.Add(speedPoint);

            Serialization();
        }


        private bool OpenPortFrequencyCounterDevice()
        {
            FrequencyCounterDevice.Instance.OpenPort(SettingsModel.ComPortFrequencyCounter , FrequencyCounterDevice.Instance.GateTimeToMSec(SettingsModel.GateTime));

            if (!FrequencyCounterIsOpen) return false;

            var answer = FrequencyCounterDevice.Instance.GetModelVersion();
            var validation = answer.Contains(IdFreCounterDevice);

            if (validation == false)
            {
                FrequencyCounterDevice.Instance.ClosePort();
                MessageBox.Show($"{SettingsModel.ComPortFrequencyCounter} не является частотомером",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            OnOffFilter(1, SettingsModel.FilterChannel1);
            OnOffFilter(2, SettingsModel.FilterChannel2);
            SetGateTime(SettingsModel.GateTime);


            return true;
        }

        #region Частотомер ЧЗ-85/6

        private void SetGateTime(GateTime gateTime)
        {
            FrequencyCounterDevice.Instance.SetGateTime(gateTime);
            SettingsModel.GateTime = gateTime;
        }

        private void OnOffFilter(int channel, bool isOn)
        {
            switch (channel)
            {
                case 1:
                    FrequencyCounterDevice.Instance.SwitchFilter(1, isOn);
                    SettingsModel.FilterChannel1 = isOn;
                    break;
                case 2:
                    FrequencyCounterDevice.Instance.SwitchFilter(2, isOn);
                    SettingsModel.FilterChannel2 = isOn;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        #endregion


        /// <summary>
        /// Значения скорости на которых нужно считать значения датчика.
        /// </summary>
        public ObservableCollection<SpeedPoint> SpeedPointsList { get; set; } =
            new ObservableCollection<SpeedPoint>
            {
                new SpeedPoint
                    {Id = 1, Speed = 0.7m, SetFrequency = 500, MaxStep = 10, MinEdge = 0m, MaxEdge = 3.007m},
                new SpeedPoint
                    {Id = 2, Speed = 5m, SetFrequency = 2765, MaxStep = 50, MinEdge = 3.320m, MaxEdge = 8.837m},
                new SpeedPoint
                    {Id = 3, Speed = 10m, SetFrequency = 5390, MaxStep = 50, MinEdge = 9.634m, MaxEdge = 15.595m},
                new SpeedPoint
                    {Id = 4, Speed = 15m, SetFrequency = 8130, MaxStep = 50, MinEdge = 15.935m, MaxEdge = 22.366m},
                new SpeedPoint
                    {Id = 5, Speed = 20m, SetFrequency = 10810, MaxStep = 80, MinEdge = 22.248m, MaxEdge = 29.124m},
                new SpeedPoint
                    {Id = 6, Speed = 25m, SetFrequency = 13570, MaxStep = 90, MinEdge = 28.549m, MaxEdge = 35.895m},
                new SpeedPoint
                    {Id = 7, Speed = 30m, SetFrequency = 16384, MaxStep = 100, MinEdge = 32.340m, MaxEdge = 39.948m}
            };


        public List<TypeTestDescription> TypeTestDescriptionList { get; set; } = new List<TypeTestDescription>
        {
            new TypeTestDescription(TypeTest.Dvs01, "ДСВ - 01"),
            new TypeTestDescription(TypeTest.Dvs02, "ДВС - 02")
        };

        /// <summary>
        /// Проверка запроса на отмену
        /// </summary>
        /// <param name="ctSource"></param>
        /// <returns></returns>
        private bool IsCancellationRequested(CancellationTokenSource ctSource) =>
            ctSource.Token.IsCancellationRequested;

        #region Test Method

        public ObservableCollection<DsvValue01> CollectionDvsValue01 { get; set; }
            = new ObservableCollection<DsvValue01>();

        public ObservableCollection<DvsValue02> CollectionDvsValue02 { get; set; }
            = new ObservableCollection<DvsValue02>();

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

        public bool ChangeTypeTestOnValidation => !IsTestActive;

        private void StopTestClosePort()
        {
            IsTestActive = false;
            IsBusy = false;
            BusyContent = string.Empty;

            if (FrequencyCounterIsOpen)
                FrequencyCounterDevice.Instance.ClosePort();

            if (FrequencyMotorIsOpen)
                FrequencyMotorDevice.Instance.ClosePort();
        }

        private void StartTest()
        {
            IsTestActive = true;

            if (OpenMeasurementsData() == false)
            {
                IsTestActive = false;
                return;
            }


            Task.Run(async () => await Task.Run(() =>
            {
                IsBusy = true;
                BusyContent = "Проверка подключенных устройств и их настройка";

                var validComCounter = OpenPortFrequencyCounterDevice();

                if (!validComCounter)
                {
                    StopTestClosePort();
                    return;
                }

                var validComMotor = FrequencyMotorDevice.Instance.OpenPort(SettingsModel.ComPortFrequencyMotor);
                if (!validComMotor)
                {
                    StopTestClosePort();
                    return;
                }


                IsBusy = false;
                BusyContent = string.Empty;

                _ctsTask = new CancellationTokenSource();

                switch (TypeTest)
                {
                    case TypeTest.Dvs01:
                        LoadDefaultValueCollectionDvs1Value();
                        try
                        {
                            StartTestDvs01(SettingsModel.GateTime);
                        }
                        catch (Exception e)
                        {
                            GlobalLog.Log.Debug(e, e.Message);
                        }
                        finally
                        {
                            FrequencyMotorDevice.Instance.SetFrequency(0, 0);

                            ResultToXlsxDvs1();

                            StopTestClosePort();

                            MessageBox.Show("Поверка завершена", "Внимание", MessageBoxButton.OK,
                                MessageBoxImage.Asterisk, MessageBoxResult.OK);
                        }


                        break;
                    case TypeTest.Dvs02:
                        LoadDefaultValueCollectionDvs2Value();
                        try
                        {
                            StartTestDvs02(SettingsModel.GateTime);
                        }
                        catch (Exception e)
                        {
                            GlobalLog.Log.Debug(e, e.Message);
                        }
                        finally
                        {
                            FrequencyMotorDevice.Instance.SetFrequency(0, 0);

                            ResultToXlsxDvs2();

                            StopTestClosePort();

                            MessageBox.Show("Поверка завершена", "Внимание", MessageBoxButton.OK,
                                MessageBoxImage.Asterisk, MessageBoxResult.OK);
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

            if (isContinue == false)
            {
                MessageBox.Show("Отменено пользователем", "Внимание", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            Serialization();

            if (string.IsNullOrEmpty(PathSave))
            {
                MessageBox.Show("Не указан путь сохранения", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }


            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            return isContinue;
        }

        /// <summary>
        /// Метод для очистки от старых значений  CollectionDvsValue02 и заполнением пустых значений. Для ДВС2
        /// </summary>
        public void LoadDefaultValueCollectionDvs2Value()
        {
            Application.Current.Dispatcher?.Invoke(() =>
            {
                CollectionDvsValue02?.Clear();

                CollectionDvsValue02 = new ObservableCollection<DvsValue02>();

                foreach (var point in SpeedPointsList)
                    CollectionDvsValue02.Add(new DvsValue02(point.Speed));
            });
        }


        /// <summary>
        /// Метод для очистки от старых значений  CollectionDvsValue01 и заполнением пустых значений. Для ДСВ1
        /// </summary>
        public void LoadDefaultValueCollectionDvs1Value()
        {
            Application.Current.Dispatcher?.Invoke(() =>
            {
                CollectionDvsValue01?.Clear();

                CollectionDvsValue01 = new ObservableCollection<DsvValue01>();

                //Первую точку (0.7) скипаю и последнюю (30) 
                for (var i = 1; i < SpeedPointsList.Count - 1; i++)
                {
                    CollectionDvsValue01.Add(new DsvValue01(SpeedPointsList[i].Speed));
                }
            });
        }

        private void StartTestDvs01(GateTime gateTime)
        {
            StatusCurrentAction = "Запуск тестирования";

            var timeOutCounter = FrequencyCounterDevice.Instance.GateTimeToMSec(gateTime);

            if (IsCancellationRequested(_ctsTask)) return;

            //Первую точку (0.7) скипаю и последнюю (30) 
            //Снятие 1-ого значения
            for (var i = 1; i < SpeedPointsList.Count - 1; i++)
            {
                //Исправляем смещение из-за скипа 1-ой позиции в SpeedPointsList и в разнице нумерации в SpeedPointsList . Выходит -2
                var id = SpeedPointsList[i].Id - 2;
                CollectionDvsValue01[id].DeviceSpeedValue1.IsСheckedNow = true;

                //Метод разгона трубы
                Preparation(i);

                if (IsCancellationRequested(_ctsTask)) return;

                StatusCurrentAction = $"Точка {SpeedPointsList[i].Speed} : Снятие значения 1";
                //Время запроса для точки 0.7 больше из-за маленькой скорости прокрутки датчика. 
                var timeOutCounterValue1 = 7000; 
                var value1 =
                    FrequencyCounterDevice.Instance.GetCurrentHzValue(SpeedPointsList[i], timeOutCounterValue1, _ctsTask);
                if (IsCancellationRequested(_ctsTask)) return;
                CollectionDvsValue01[id].DeviceSpeedValue1.ResultValue = value1;
                CollectionDvsValue01[id].DeviceSpeedValue1.IsVerified = true;
                Thread.Sleep(50);
                if (IsCancellationRequested(_ctsTask)) return;

                CollectionDvsValue01[id].ReferenceSpeedValue1 = _averageSpeedReferenceValue;
            }

            //Первую точку (0.7) скипаю и последнюю (30) 
            //Снятие 2-ого значения
            for (var i = 1; i < SpeedPointsList.Count - 1; i++)
            {
                //Исправляем смещение из-за скипа 1-ой позиции в SpeedPointsList и в разнице нумерации в SpeedPointsList . Выходит -2
                var id = SpeedPointsList[i].Id - 2;
                CollectionDvsValue01[id].DeviceSpeedValue2.IsСheckedNow = true;

                //Метод разгона трубы
                Preparation(i);

                if (IsCancellationRequested(_ctsTask)) return;

                StatusCurrentAction = $"Точка {SpeedPointsList[i].Speed} : Снятие значения 1";
                var value1 =
                    FrequencyCounterDevice.Instance.GetCurrentHzValue(SpeedPointsList[i], timeOutCounter, _ctsTask);
                if (IsCancellationRequested(_ctsTask)) return;
                CollectionDvsValue01[id].DeviceSpeedValue2.ResultValue = value1;
                CollectionDvsValue01[id].DeviceSpeedValue2.IsVerified = true;
                Thread.Sleep(50);
                if (IsCancellationRequested(_ctsTask)) return;

                CollectionDvsValue01[id].ReferenceSpeedValue2 = _averageSpeedReferenceValue;
            }

            //Первую точку (0.7) скипаю и последнюю (30) 
            //Снятие 3-его значения
            for (var i = 1; i < SpeedPointsList.Count - 1; i++)
            {
                //Исправляем смещение из-за скипа 1-ой позиции в SpeedPointsList и в разнице нумерации в SpeedPointsList . Выходит -2
                var id = SpeedPointsList[i].Id - 2;
                CollectionDvsValue01[id].DeviceSpeedValue3.IsСheckedNow = true;

                //Метод разгона трубы
                Preparation(i);

                if (IsCancellationRequested(_ctsTask)) return;


                StatusCurrentAction = $"Точка {SpeedPointsList[i].Speed} : Снятие значения 1";
                var value1 =
                    FrequencyCounterDevice.Instance.GetCurrentHzValue(SpeedPointsList[i], timeOutCounter, _ctsTask);
                if (IsCancellationRequested(_ctsTask)) return;
                CollectionDvsValue01[id].DeviceSpeedValue3.ResultValue = value1;
                CollectionDvsValue01[id].DeviceSpeedValue3.IsVerified = true;
                Thread.Sleep(50);
                if (IsCancellationRequested(_ctsTask)) return;

                CollectionDvsValue01[id].ReferenceSpeedValue3 = _averageSpeedReferenceValue;
            }

            foreach (var dvsValue01 in CollectionDvsValue01)
            {
                var average = (dvsValue01.ReferenceSpeedValue1 +
                               dvsValue01.ReferenceSpeedValue2 +
                               dvsValue01.ReferenceSpeedValue3) / 3;

                if (average != null) dvsValue01.ReferenceSpeedValueMain = Math.Round((decimal) average, 2);
            }


            StatusCurrentAction = $"Поверка завершена ";
        }

        private void Preparation(int i)
        {
            StatusCurrentAction = $"Точка {SpeedPointsList[i].Speed}";

            _acceptCorrectionReference = false;

            FrequencyMotorDevice.Instance.SetFrequency(SpeedPointsList[i].SetFrequency, SpeedPointsList[i].Speed);
            //Время ожидания для стабилизации трубы
            Thread.Sleep(WaitSetFrequency);

            Application.Current.Dispatcher?.Invoke(AverageSpeedReferenceCollection.Clear);

            StatusCurrentAction = $"Точка {SpeedPointsList[i].Speed} : Корректировка скорости";

            FrequencyMotorDevice.Instance.CorrectionSpeedMotor(ref _averageSpeedReferenceValue, SpeedPointsList[i],
                ref _ctsTask);

            _acceptCorrectionReference = true;

            Thread.Sleep(100);
        }

        private void StartTestDvs02(GateTime gateTime)
        {
            StatusCurrentAction = "Запуск тестирования";

            var timeOutCounter = FrequencyCounterDevice.Instance.GateTimeToMSec(gateTime);

            if (IsCancellationRequested(_ctsTask)) return;

            foreach (var point in SpeedPointsList)
            {
                //Так как номеровка идет с 1 , а коллекция с 0
                var id = point.Id - 1;
                CollectionDvsValue02[id].DeviceSpeedValue1.IsСheckedNow = true;


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
                    FrequencyMotorDevice.Instance.CorrectionSpeedMotor(ref _averageSpeedReferenceValue, point,
                        ref _ctsTask);

                if (IsCancellationRequested(_ctsTask)) return;

                _acceptCorrectionReference = true;

                Thread.Sleep(100);

                //На данной скорости, датчик вращается очень медленно. 
                //А значение поступает на частотомер, в момент полного оборота датчика.
                if (point.Speed == 0.7m)
                    timeOutCounter = 5000;

                StatusCurrentAction = $"Точка {point.Speed} : Снятие значения 1";
                var value1 = FrequencyCounterDevice.Instance.GetCurrentHzValue(point, timeOutCounter, _ctsTask);
                if (IsCancellationRequested(_ctsTask)) return;
                CollectionDvsValue02[id].DeviceSpeedValue1.ResultValue = value1;
                CollectionDvsValue02[id].DeviceSpeedValue1.IsVerified = true;
                Thread.Sleep(50);
                if (IsCancellationRequested(_ctsTask)) return;

                CollectionDvsValue02[id].DeviceSpeedValue2.IsСheckedNow = true;
                StatusCurrentAction = $"Точка {point.Speed} : Снятие значения 2";
                var value2 = FrequencyCounterDevice.Instance.GetCurrentHzValue(point, timeOutCounter, _ctsTask);
                if (IsCancellationRequested(_ctsTask)) return;
                CollectionDvsValue02[id].DeviceSpeedValue2.ResultValue = value2;
                CollectionDvsValue02[id].DeviceSpeedValue2.IsVerified = true;
                Thread.Sleep(50);
                if (IsCancellationRequested(_ctsTask)) return;

                CollectionDvsValue02[id].DeviceSpeedValue3.IsСheckedNow = true;
                StatusCurrentAction = $"Точка {point.Speed} : Снятие значения 3";
                var value3 = FrequencyCounterDevice.Instance.GetCurrentHzValue(point, timeOutCounter, _ctsTask);
                if (IsCancellationRequested(_ctsTask)) return;
                CollectionDvsValue02[id].DeviceSpeedValue3.ResultValue = value3;
                CollectionDvsValue02[id].DeviceSpeedValue3.IsVerified = true;
                Thread.Sleep(50);
                if (IsCancellationRequested(_ctsTask)) return;

                CollectionDvsValue02[id].DeviceSpeedValue4.IsСheckedNow = true;
                StatusCurrentAction = $"Точка {point.Speed} : Снятие значения 4";
                var value4 = FrequencyCounterDevice.Instance.GetCurrentHzValue(point, timeOutCounter, _ctsTask);
                if (IsCancellationRequested(_ctsTask)) return;
                CollectionDvsValue02[id].DeviceSpeedValue4.ResultValue = value4;
                CollectionDvsValue02[id].DeviceSpeedValue4.IsVerified = true;
                Thread.Sleep(50);
                if (IsCancellationRequested(_ctsTask)) return;

                CollectionDvsValue02[id].DeviceSpeedValue5.IsСheckedNow = true;
                StatusCurrentAction = $"Точка {point.Speed} : Снятие значения 5";
                var value5 = FrequencyCounterDevice.Instance.GetCurrentHzValue(point, timeOutCounter, _ctsTask);
                if (IsCancellationRequested(_ctsTask)) return;
                CollectionDvsValue02[id].DeviceSpeedValue5.ResultValue = value5;
                CollectionDvsValue02[id].DeviceSpeedValue5.IsVerified = true;
                Thread.Sleep(50);
                if (IsCancellationRequested(_ctsTask)) return;

                BusyContent = string.Empty;
                IsBusy = false;

                CollectionDvsValue02[id].ReferenceSpeedValue = _averageSpeedReferenceValue;
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
                    "Отсутствует файл образец Dvs2.xlsx  " +
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
                    AddValueInCell(ws.Cells[i + 12, 12], CollectionDvsValue02[i].ReferenceSpeedValue);
                    AddValueInCell(ws.Cells[i + 12, 13], CollectionDvsValue02[i].DeviceSpeedValue1.ResultValue);
                    AddValueInCell(ws.Cells[i + 12, 14], CollectionDvsValue02[i].DeviceSpeedValue2.ResultValue);
                    AddValueInCell(ws.Cells[i + 12, 15], CollectionDvsValue02[i].DeviceSpeedValue3.ResultValue);
                    AddValueInCell(ws.Cells[i + 12, 16], CollectionDvsValue02[i].DeviceSpeedValue4.ResultValue);
                    AddValueInCell(ws.Cells[i + 12, 17], CollectionDvsValue02[i].DeviceSpeedValue5.ResultValue);
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

                var path = $"Протокол ДВС-02 № {MeasurementsData.DeviceId} от {DateTime.Now:dd.MM.yyyy}.xlsx";

                var fullPath = Path.Combine(PathSave, path);
                var attemptSave = 1;

                while (true)
                {
                    if (File.Exists(fullPath))
                    {
                        path =
                            $"Протокол ДВС-02 № {MeasurementsData.DeviceId} от {DateTime.Now:dd.MM.yyyy}({attemptSave}).xlsx";
                        fullPath = Path.Combine(PathSave, path);
                        attemptSave++;
                        continue;
                    }

                    break;
                }

                package.SaveAs(new FileInfo(fullPath));
            }
        }

        private void ResultToXlsxDvs1()
        {
            var pathExampleXlsxFile = @"Resources\Dsv1.xlsx";
            while (true)
            {
                if (File.Exists(pathExampleXlsxFile))
                    break;

                var errorMessage =
                    "Отсутствует файл образец Dvs1.xlsx  " +
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

                for (var i = 0; i < 5; i++)
                {
                    AddValueInCell(ws.Cells[i + 12, 16], CollectionDvsValue01[i].ReferenceSpeedValueMain);
                    AddValueInCell(ws.Cells[i + 12, 17], CollectionDvsValue01[i].DeviceSpeedValue1.ResultValue);
                    AddValueInCell(ws.Cells[i + 12, 18], CollectionDvsValue01[i].DeviceSpeedValue2.ResultValue);
                    AddValueInCell(ws.Cells[i + 12, 19], CollectionDvsValue01[i].DeviceSpeedValue3.ResultValue);
                }


                //Условия поверки

                ws.Cells[47, 16].Value = MeasurementsData.Verifier;
                ws.Cells[47, 21].Value = $"{DateTime.Now:dd.MM.yyyy}";

                ws.Cells[23, 5].Value = MeasurementsData.Temperature;
                ws.Cells[24, 5].Value = MeasurementsData.Humidity;
                ws.Cells[25, 5].Value = MeasurementsData.Pressure;
                ws.Cells[14, 6].Value = MeasurementsData.DeviceId;

                #endregion

                var path = $"Протокол ДСВ-01 № {MeasurementsData.DeviceId} от {DateTime.Now:dd.MM.yyyy}.xlsx";

                var fullPath = Path.Combine(PathSave, path);
                var attemptSave = 1;

                while (true)
                {
                    if (File.Exists(fullPath))
                    {
                        path =
                            $"Протокол ДСВ-01 № {MeasurementsData.DeviceId} от {DateTime.Now:dd.MM.yyyy}({attemptSave}).xlsx";
                        fullPath = Path.Combine(PathSave, path);
                        attemptSave++;
                        continue;
                    }

                    break;
                }

                package.SaveAs(new FileInfo(fullPath));
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


            SettingsModel.FilterChannel1 = _userSettings.SettingsModel.FilterChannel1;
            SettingsModel.FilterChannel2 = _userSettings.SettingsModel.FilterChannel2;
            SettingsModel.FrequencyChannel = _userSettings.SettingsModel.FrequencyChannel;
            SettingsModel.GateTime = _userSettings.SettingsModel.GateTime;
            SettingsModel.ComPortFrequencyMotor = _userSettings.SettingsModel.ComPortFrequencyMotor;
            SettingsModel.ComPortFrequencyCounter = _userSettings.SettingsModel.ComPortFrequencyCounter;
            SettingsModel.SetFrequencyMotor = 0;
            MeasurementsData = _userSettings.MeasurementsData;
            PathSave = _userSettings.PathSave;
            TypeTest = _userSettings.TypeTest;

            if (_userSettings.SpeedPointsList != null && _userSettings.SpeedPointsList.Count != 0)
            {
                SpeedPointsList.Clear();
                foreach (var speedPoint in _userSettings.SpeedPointsList)
                    SpeedPointsList.Add(speedPoint);
            }

            #region Код для теста

            //var dvsValue1 = new DvsValue02(5)
            //{
            //    DeviceSpeedValue1 = new SpeedValue {IsVerified = true, IsСheckedNow = true, ResultValue = 4.32m},
            //    DeviceSpeedValue2 = new SpeedValue(),
            //    DeviceSpeedValue3 = new SpeedValue(),
            //    DeviceSpeedValue4 = new SpeedValue(),
            //    DeviceSpeedValue5 = new SpeedValue(),
            //    ReferenceSpeedValue = 5
            //};
            //var dvsValue2 = new DvsValue02(10)
            //{
            //    DeviceSpeedValue1 = new SpeedValue {IsVerified = false, IsСheckedNow = false, ResultValue = 14.32m},
            //    ReferenceSpeedValue = 10
            //};
            //var dvsValue3 = new DvsValue02(15)
            //{
            //    DeviceSpeedValue1 = new SpeedValue {IsVerified = false, IsСheckedNow = true, ResultValue = 24.32m},
            //    ReferenceSpeedValue = 15
            //};
            //var dvsValue4 = new DvsValue02(20)
            //{
            //    DeviceSpeedValue1 = new SpeedValue { IsVerified = false, IsСheckedNow = false, ResultValue = 14.32m },
            //    ReferenceSpeedValue = 20
            //};
            //var dvsValue5 = new DvsValue02(25)
            //{
            //    DeviceSpeedValue1 = new SpeedValue { IsVerified = false, IsСheckedNow = true, ResultValue = 24.32m },
            //    ReferenceSpeedValue = 25
            //};
            //var dvsValue6 = new DvsValue02(30)
            //{
            //    DeviceSpeedValue1 = new SpeedValue { IsVerified = false, IsСheckedNow = false, ResultValue = 14.32m },
            //    ReferenceSpeedValue = 30
            //};

            //CollectionDvsValue02.Add(dvsValue1);
            //CollectionDvsValue02.Add(dvsValue2);
            //CollectionDvsValue02.Add(dvsValue3);
            //CollectionDvsValue02.Add(dvsValue4);
            //CollectionDvsValue02.Add(dvsValue5);
            //CollectionDvsValue02.Add(dvsValue6);

            #endregion
        }

        private void DefaultSpeedPoints_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            for (var i = 0; i < SpeedPointsList.Count; i++)
                SpeedPointsList[i].Id = i + 1;
        }


        public void UpdateSettingsAndSerialization()
        {
            _userSettings.SettingsModel = SettingsModel;
            Serialization();
        }

        #region EventHandler Method

        private void FrequencyMotor_UpdateSetFrequency(object sender, UpdateSetFrequencyEventArgs e)
        {
            SettingsModel.SetFrequencyMotor = e.SetFrequency;
            SetFrequencyMotor = e.SetFrequency;
        }

        private void FrequencyMotor_UpdateReferenceValue(object sender, UpdateReferenceValueEventArgs e)
        {
            SpeedReferenceValue = (decimal) e.ReferenceValue;
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

    public class TypeTestDescription
    {
        public TypeTestDescription(TypeTest typeTest, string description)
        {
            TypeTest = typeTest;
            Description = description;
        }

        public TypeTest TypeTest { get; set; }
        public string Description { get; set; }
    }

    public enum TypeTest
    {
        Dvs01 = 1,
        Dvs02 = 2
    }

    public enum SelectedPage
    {
        MainWindow = 0,
        Settings = 1,
        Checkpoint = 2,
        Debug = 3
    }
}