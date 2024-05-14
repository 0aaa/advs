using OfficeOpenXml;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
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
    internal class MainWindowVm : BaseVm.BaseVm
    {
        private const string USER_SETTINGS_PATH = "UserSettings.txt";
        private const string ID_FRE_COUNTER_DEVICE = "43-85/6";
        private readonly UserSettings _userSettings;
        private readonly object _locker;
        private CancellationTokenSource _ctsTask;
        private MeasurementsData _measurementsData;
		// Все свойства что ниже, должны сохранятся пре перезапуске.
		private TypeTest _typeTest;
        private string _pathSave;
        /// <summary>Эталонное значение скорости с частотной трубы</summary>
        private decimal _speedReferenceValue;
        private decimal _averageSpeedReferenceValue;
		/// <summary>Флаг показывающий было ли экстренное завершение поверки</summary>
		private bool _isEmergencyStop;
        /// <summary>Флаг для включения в колекцию среднего значения эталона, всех его значений за время теста скоростной точки после прохождения корректировки.</summary>
        private bool _acceptCorrectionReference;
        #region RealyCommand
        public RelayCommand[] ReviseCommands { get; }// Start, Stop, SaveSpeeds, VisibilitySetFrequency, Reset, ClosePortFrequencyCounter, ClosePortFrequencyMotor.
		#region Команды смены страницы
		public RelayCommand[] PaginationCommands { get; }// MainWindow, Settings, Checkpoints, Debug.
		#endregion
		#endregion
		#region Properties
		public TypeTestDescription[] TypeTestDescriptionArr { get; }
        public SettingsModel SettingsModel { get; }
        /// <summary>Значения скорости на которых нужно считать значения датчика.</summary>
        public ObservableCollection<SpeedPoint> SpeedPointsList { get; }
        public ObservableCollection<DsvValue01> CollectionDvsValue01 { get; }
        public ObservableCollection<DvsValue02> CollectionDvsValue02 { get; }
        public ObservableCollection<DvsValue03> CollectionDvsValue03 { get; }
        public ObservableCollection<string> PortsList { get; }
        public ObservableCollection<decimal> AverageSpeedReferenceCollection { get; }
        public string MainWindowHeader { get; }
        /// <summary>Время ожидания после установки значения частоты, что бы дать аэротрубе стабилизировать значение</summary>
        public int WaitSetFrequency { get; }
        public bool ChangeTypeTestOnValidation => !IsTestActive;
        /// <summary>Флаг для биндинга отображения таблицы разультатов для Dvs2</summary>
		public bool[] DvsModelContentVisibility { get; set; }
        /// <summary>Свойство для хранения условий поверки</summary>
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
        public UserControl FrameContent { get; set; }
        public SelectedPage SelectedPage { get; set; }
        public TypeTest TypeTest
        {
            get => _typeTest;
            set
            {
                _typeTest = value;
                OnPropertyChanged(nameof(TypeTest));
                _userSettings.TypeTest = value;
                Serialization();
				for (int i = 0; i < DvsModelContentVisibility.Length; i++)
				{
					DvsModelContentVisibility[i] = false;
				}
				DvsModelContentVisibility[(int)value - 1] = true;
				OnPropertyChanged(nameof(DvsModelContentVisibility));
            }
        }
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
        /// <summary>Свойство, для биндинга на интерфейс текущее действие внутри программы</summary>
        public string StatusCurrentAction { get; set; }
        /// <summary>Текст busyIndicator</summary>
        public string BusyContent { get; set; }
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
        /// <summary>Активность BusyIndicator</summary>
        public bool IsBusy { get; set; }
        /// <summary>Флаг показывающий активно ли тестирование.</summary>
        public bool IsTestActive { get; set; }
        public bool FrequencyCounterIsOpen { get; set; }
        public bool FrequencyMotorIsOpen { get; set; }
        public bool VisibilitySetFrequency { get; set; }
        /// <summary>Флаг для биндинга отображения таблицы разультатов для Dvs1</summary>
        #endregion

        public MainWindowVm()
        {
            FrequencyCounterDevice.Instance.IsOpenUpdate += (_, e) => FrequencyCounterIsOpen = e.IsOpen;
            FrequencyMotorDevice.Instance.IsOpenUpdate += (_, e) => FrequencyMotorIsOpen = e.IsOpen;
            FrequencyMotorDevice.Instance.UpdateReferenceValue += FrequencyMotor_UpdateReferenceValue;
            FrequencyMotorDevice.Instance.UpdateSetFrequency += FrequencyMotor_UpdateSetFrequency;
			SettingsModel = new SettingsModel();
			SpeedPointsList = new ObservableCollection<SpeedPoint> {
				new SpeedPoint { Id = 1, Speed = 0.7m, SetFrequency = 500, MaxStep = 10, MinEdge = 0m, MaxEdge = 3.007m }
				, new SpeedPoint { Id = 2, Speed = 5m, SetFrequency = 2765, MaxStep = 50, MinEdge = 3.320m, MaxEdge = 8.837m }
				, new SpeedPoint { Id = 3, Speed = 10m, SetFrequency = 5390, MaxStep = 50, MinEdge = 9.634m, MaxEdge = 15.595m }
				, new SpeedPoint { Id = 4, Speed = 15m, SetFrequency = 8130, MaxStep = 50, MinEdge = 15.935m, MaxEdge = 22.366m }
				, new SpeedPoint { Id = 5, Speed = 20m, SetFrequency = 10810, MaxStep = 80, MinEdge = 22.248m, MaxEdge = 29.124m }
				, new SpeedPoint { Id = 6, Speed = 25m, SetFrequency = 13570, MaxStep = 90, MinEdge = 28.549m, MaxEdge = 35.895m }
				, new SpeedPoint { Id = 7, Speed = 30m, SetFrequency = 16384, MaxStep = 100, MinEdge = 32.340m, MaxEdge = 39.948m }
			};
			DvsModelContentVisibility = new bool[3];
            SpeedPointsList.CollectionChanged += DefaultSpeedPoints_CollectionChanged;
			_locker = new object();
            _userSettings = Deserialization() ?? new UserSettings();
            SettingsModel.FilterChannels[0] = _userSettings.SettingsModel.FilterChannels[0];
            SettingsModel.FilterChannels[1] = _userSettings.SettingsModel.FilterChannels[1];
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
				{
                    SpeedPointsList.Add(speedPoint);
				}
            }
			for (int i = 0; i < DvsModelContentVisibility.Length; i++)
			{
				DvsModelContentVisibility[i] = false;
			}
			DvsModelContentVisibility[1] = true;// DVS-02.
			_typeTest = TypeTest.Dvs02;
			_pathSave = "";
			PortsList = new ObservableCollection<string>(SerialPort.GetPortNames());
			WaitSetFrequency = 5000;
			AverageSpeedReferenceCollection = new ObservableCollection<decimal>();
			MainWindowHeader = $"{Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title}  v{Assembly.GetExecutingAssembly().GetName().Version}";
			TypeTestDescriptionArr = new TypeTestDescription[] {
				new TypeTestDescription(TypeTest.Dvs01, "ДСВ - 01")
				, new TypeTestDescription(TypeTest.Dvs02, "ДВС - 02")
				, new TypeTestDescription(TypeTest.Dvs03, "ДВС - 03")
			};
			CollectionDvsValue01 = new ObservableCollection<DsvValue01>();
			CollectionDvsValue02 = new ObservableCollection<DvsValue02>();
			CollectionDvsValue03 = new ObservableCollection<DvsValue03>();
			ReviseCommands = new RelayCommand[] {
				 new RelayCommand(StartTest, () => !IsTestActive)// Start.
				, new RelayCommand(StopTest, () => IsTestActive)// Stop.
				, new RelayCommand(SaveSpeedPointsCollection)// SaveSpeeds.
				, new RelayCommand(() => VisibilitySetFrequency = !VisibilitySetFrequency)// VisibilitySetFrequency.
        #region Частотомер ЧЗ-85/6
				, new RelayCommand(FrequencyCounterDevice.Instance.RstCommand, FrequencyCounterDevice.Instance.IsOpen)// Reset.
				, new RelayCommand(() => FrequencyCounterDevice.Instance.ClosePort(), FrequencyCounterDevice.Instance.IsOpen)// ClosePortFrequencyCounter.
        #endregion
        #region Анемометр / Частотный двигатель
				, new RelayCommand(() => FrequencyMotorDevice.Instance.ClosePort(), FrequencyMotorDevice.Instance.IsOpen)// ClosePortFrequencyMotor.
        #endregion
			};
			PaginationCommands = new RelayCommand[] {
				new RelayCommand(ChangePageOnMainWindow, () => SelectedPage != SelectedPage.MainWindow && !IsTestActive)// MainWindow.
				, new RelayCommand(ChangePageOnSettings, () => SelectedPage != SelectedPage.Settings && !IsTestActive)// Settings.
				, new RelayCommand(ChangePageOnCheckPoints, () => SelectedPage != SelectedPage.Checkpoint && !IsTestActive)// Checkpoints.
				, new RelayCommand(ChangePageOnDebug, () => SelectedPage != SelectedPage.Debug && !IsTestActive)// Debug.
			};
			#region Код для теста
			//var dvsValue1 = new DvsValue02(5) {
			//	DeviceSpeedValue1 = new SpeedValue { IsVerified = true, IsСheckedNow = true, ResultValue = 4.32m }
			//	, DeviceSpeedValue2 = new SpeedValue()
			//	, DeviceSpeedValue3 = new SpeedValue()
			//	, DeviceSpeedValue4 = new SpeedValue()
			//	, DeviceSpeedValue5 = new SpeedValue()
			//	, ReferenceSpeedValue = 5
			//};
			//var dvsValue2 = new DvsValue02(10) {
			//	DeviceSpeedValue1 = new SpeedValue { IsVerified = false, IsСheckedNow = false, ResultValue = 14.32m }
			//	, ReferenceSpeedValue = 10
			//};
			//var dvsValue3 = new DvsValue02(15) {
			//	DeviceSpeedValue1 = new SpeedValue { IsVerified = false, IsСheckedNow = true, ResultValue = 24.32m }
			//	, ReferenceSpeedValue = 15
			//};
			//var dvsValue4 = new DvsValue02(20) {
			//	DeviceSpeedValue1 = new SpeedValue { IsVerified = false, IsСheckedNow = false, ResultValue = 14.32m }
			//	, ReferenceSpeedValue = 20
			//};
			//var dvsValue5 = new DvsValue02(25) {
			//	DeviceSpeedValue1 = new SpeedValue { IsVerified = false, IsСheckedNow = true, ResultValue = 24.32m }
			//	, ReferenceSpeedValue = 25
			//};
			//var dvsValue6 = new DvsValue02(30) {
			//	DeviceSpeedValue1 = new SpeedValue { IsVerified = false, IsСheckedNow = false, ResultValue = 14.32m }
			//	, ReferenceSpeedValue = 30
			//};
			//CollectionDvsValue02.Add(dvsValue1);
			//CollectionDvsValue02.Add(dvsValue2);
			//CollectionDvsValue02.Add(dvsValue3);
			//CollectionDvsValue02.Add(dvsValue4);
			//CollectionDvsValue02.Add(dvsValue5);
			//CollectionDvsValue02.Add(dvsValue6);
			#endregion
		}

        private void UpdateAverageSpeedReferenceValue(decimal newValue)
        {
            if (AverageSpeedReferenceCollection.Count > 5 && !_acceptCorrectionReference)
            {
                AverageSpeedReferenceCollection.RemoveAt(0);
            }
            AverageSpeedReferenceCollection.Add(newValue);
            _averageSpeedReferenceValue = Math.Round(AverageSpeedReferenceCollection.Average(), 2);
        }

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
            FrameContent = new SpeedPointsView(SpeedPointsList, ReviseCommands[2]);// SaveSpeeds.
			SelectedPage = SelectedPage.Checkpoint;
        }

        private void ChangePageOnDebug()
        {
            Task.Run(async () => await Task.Run(() =>
            {
                IsBusy = true;
                BusyContent = "Проверка подключения Аэродинамической трубы";
                if (!FrequencyMotorDevice.Instance.OpenPort(SettingsModel.ComPortFrequencyMotor))
                {
                    IsBusy = false;
                    BusyContent = string.Empty;
                    return;
                }
                IsBusy = false;
                BusyContent = string.Empty;
                Application.Current.Dispatcher?.Invoke(() => FrameContent = new DebugView());
                SelectedPage = SelectedPage.Debug;
            }));
        }
        #endregion

        private void SaveSpeedPointsCollection()
        {
            _userSettings.SpeedPointsList.Clear();
            foreach (var speedPoint in SpeedPointsList)
			{
                _userSettings.SpeedPointsList.Add(speedPoint);
			}
            Serialization();
        }

        private bool OpenPortFrequencyCounterDevice()
        {
            FrequencyCounterDevice.Instance.OpenPort(SettingsModel.ComPortFrequencyCounter, FrequencyCounterDevice.Instance.GateTimeToMSec(SettingsModel.GateTime));
			if (!FrequencyCounterIsOpen)
			{
				return false;
			}
            if (!FrequencyCounterDevice.Instance.GetModelVersion().Contains(ID_FRE_COUNTER_DEVICE))
            {
                FrequencyCounterDevice.Instance.ClosePort();
                MessageBox.Show($"{SettingsModel.ComPortFrequencyCounter} не является частотомером", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            OnOffFilter(1, SettingsModel.FilterChannels[0]);
            OnOffFilter(2, SettingsModel.FilterChannels[1]);
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
                    SettingsModel.FilterChannels[0] = isOn;
                    break;
                case 2:
                    FrequencyCounterDevice.Instance.SwitchFilter(2, isOn);
                    SettingsModel.FilterChannels[1] = isOn;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        #endregion
        #endregion

        #region Revise methods
        private void StopTest()
        {
            BusyContent = "Тестирование прервано пользователем \r\n Ожидание завершения процесса";
            IsBusy = true;
            IsTestActive = false;
            _ctsTask.Cancel();
        }

        private void EmergencyStop()
        {
            BusyContent = "Аварийная остановка";
            IsBusy = true;
            IsTestActive = false;
            _isEmergencyStop = true;
            _ctsTask.Cancel();
        }

        private void StopTestClosePort()
        {
            IsTestActive = false;
            IsBusy = false;
            BusyContent = string.Empty;
            if (FrequencyCounterIsOpen)
			{
                FrequencyCounterDevice.Instance.ClosePort();
			}
            if (FrequencyMotorIsOpen)
			{
                FrequencyMotorDevice.Instance.ClosePort();
			}
        }

        private void StartTest()
        {
            IsTestActive = true;
            if (!OpenMeasurementsData())
            {
                IsTestActive = false;
                return;
            }
            Task.Run(async () => await Task.Run(() =>
            {
                IsBusy = true;
                BusyContent = "Проверка подключенных устройств и их настройка";
                if (!OpenPortFrequencyCounterDevice() || !FrequencyMotorDevice.Instance.OpenPort(SettingsModel.ComPortFrequencyMotor))
                {
                    StopTestClosePort();
                    return;
                }
                IsBusy = false;
                BusyContent = string.Empty;
                _ctsTask = new CancellationTokenSource();
                FrequencyCounterDevice.Instance.StopTest = EmergencyStop;
                try
                {
					StatusCurrentAction = "Запуск тестирования";
                    switch (TypeTest)
                    {
                        case TypeTest.Dvs01:
                            LoadDefaultValueCollectionDvs1Value();
                            StartTestDvs01();
                            break;
                        case TypeTest.Dvs02:
                            LoadDefaultValueCollectionDvs2Value();
                            StartTestDvs02();
                            break;
						case TypeTest.Dvs03:
							LoadDefaultValueCollectionDvs3Value();
							StartTestDvs03();
							break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
					StatusCurrentAction = "Поверка завершена ";
                }
                catch (Exception e)
                {
                    GlobalLog.Log.Debug(e, e.Message);
					MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    FrequencyMotorDevice.Instance.SetFrequency(0, 0);
                    switch (TypeTest)
                    {
                        case TypeTest.Dvs01:
                            ResultToXlsxDvs1();
                            break;
                        case TypeTest.Dvs02:
                            ResultToXlsxDvs2();
                            break;
						case TypeTest.Dvs03:
							ResultToXlsxDvs3();
							break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    StopTestClosePort();
                    if (_isEmergencyStop)
                    {
                        MessageBox.Show("Произошло аварийное завершение поверки", "Внимание", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                    }
                    else
                    {
                        MessageBox.Show("Поверка завершена", "Внимание", MessageBoxButton.OK, MessageBoxImage.Asterisk, MessageBoxResult.OK);
                    }
                }
            }));
        }

        private bool OpenMeasurementsData()
        {
            var setMeasurementsData = new SetMeasurementsData(new SetMeasurementsDataVm(this));
            setMeasurementsData.ShowDialog();
            var isContinue = setMeasurementsData.ViewModel.IsContinue;
            setMeasurementsData.Close();
            if (!isContinue)
            {
                MessageBox.Show("Отменено пользователем", "Внимание", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                return false;
            }
            Serialization();
            if (string.IsNullOrEmpty(PathSave))
            {
                MessageBox.Show("Не указан путь сохранения", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                return false;
            }
            return isContinue;
        }

        /// <summary>Метод для очистки от старых значений  CollectionDvsValue02 и заполнением пустых значений. Для ДВС2</summary>
        private void LoadDefaultValueCollectionDvs2Value()
        {
            Application.Current.Dispatcher?.Invoke(() =>
            {
                CollectionDvsValue02?.Clear();
                foreach (var point in SpeedPointsList)
				{
                    CollectionDvsValue02.Add(new DvsValue02(point.Speed));
				}
            });
        }

        /// <summary>Метод для очистки от старых значений  CollectionDvsValue01 и заполнением пустых значений. Для ДСВ1</summary>
        private void LoadDefaultValueCollectionDvs1Value()
        {
            Application.Current.Dispatcher?.Invoke(() =>
            {
                CollectionDvsValue01?.Clear();
                for (int i = 1; i < SpeedPointsList.Count - 1; i++)// Первую точку (0.7) скипаю и последнюю (30).
				{
                    CollectionDvsValue01.Add(new DsvValue01(SpeedPointsList[i].Speed));
                }
            });
        }

		private void LoadDefaultValueCollectionDvs3Value()
		{
			Application.Current.Dispatcher?.Invoke(() =>
			{
				CollectionDvsValue03?.Clear();
				for (int i = 1; i < SpeedPointsList.Count - 1; i++)// Первую точку (0.7) скипаю и последнюю (30).
				{
					CollectionDvsValue03.Add(new DvsValue03(SpeedPointsList[i].Speed));
				}
			});
		}

        private void StartTestDvs01()
        {
            var timeOutCounter = FrequencyCounterDevice.Instance.GateTimeToMSec(SettingsModel.GateTime);
			decimal? average;
			int id = 0;
			if (_ctsTask.Token.IsCancellationRequested)
			{
				return;
			}
			for (int j = 0; j < CollectionDvsValue01[id].DeviceSpeedValues.Length; j++)
			{
				for (int i = 1; i < SpeedPointsList.Count - 1; i++)// Первую точку (0.7) скипаю и последнюю (30). Снятие 1-ого значения.
				{
					id = SpeedPointsList[i].Id - 2;// Исправляем смещение из-за скипа 1-ой позиции в SpeedPointsList и в разнице нумерации в SpeedPointsList. Выходит -2.
					CollectionDvsValue01[id].DeviceSpeedValues[j].IsСheckedNow = true;
					Preparation(i);// Метод разгона трубы.
					if (_ctsTask.Token.IsCancellationRequested)
					{
						return;
					}
					StatusCurrentAction = $"Точка {SpeedPointsList[i].Speed} : Снятие значения 1";
					CollectionDvsValue01[id].DeviceSpeedValues[j].ResultValue = FrequencyCounterDevice.Instance.GetCurrentHzValue(SpeedPointsList[i], j > 0 ? timeOutCounter : 7000, _ctsTask);// Время запроса для точки 0.7 больше из-за маленькой скорости прокрутки датчика.
					CollectionDvsValue01[id].DeviceSpeedValues[j].IsVerified = true;
					Thread.Sleep(50);
					if (_ctsTask.Token.IsCancellationRequested)
					{
						return;
					}
					CollectionDvsValue01[id].ReferenceSpeedValues[j] = _averageSpeedReferenceValue;
				}
            }
            foreach (var dvsValue01 in CollectionDvsValue01)
            {
                average = (dvsValue01.ReferenceSpeedValues[0] + dvsValue01.ReferenceSpeedValues[1] + dvsValue01.ReferenceSpeedValues[2]) / dvsValue01.ReferenceSpeedValues.Length;
				if (average != null)
				{
					dvsValue01.ReferenceSpeedValueMain = Math.Round((decimal)average, 2);
				}
            }
        }

        private void Preparation(int i)
        {
            StatusCurrentAction = $"Точка {SpeedPointsList[i].Speed}";
            _acceptCorrectionReference = false;
            FrequencyMotorDevice.Instance.SetFrequency(SpeedPointsList[i].SetFrequency, SpeedPointsList[i].Speed);
            Thread.Sleep(WaitSetFrequency);// Время ожидания для стабилизации трубы.
			Application.Current.Dispatcher?.Invoke(AverageSpeedReferenceCollection.Clear);
            StatusCurrentAction = $"Точка {SpeedPointsList[i].Speed} : Корректировка скорости";
            FrequencyMotorDevice.Instance.CorrectionSpeedMotor(ref _averageSpeedReferenceValue, SpeedPointsList[i], ref _ctsTask);
            _acceptCorrectionReference = true;
            Thread.Sleep(100);
        }

        private void StartTestDvs02()
        {
            var timeOutCounter = FrequencyCounterDevice.Instance.GateTimeToMSec(SettingsModel.GateTime);
			if (_ctsTask.Token.IsCancellationRequested)
			{
				return;
			}
			int id;
            foreach (var point in SpeedPointsList)
            {
                id = point.Id - 1;// Так как номеровка идет с 1, а коллекция с 0.
                StatusCurrentAction = $"Точка {point.Speed}";
                _acceptCorrectionReference = false;
                FrequencyMotorDevice.Instance.SetFrequency(point.SetFrequency, point.Speed);
                Thread.Sleep(WaitSetFrequency);// Время ожидания для стабилизации трубы.
				Application.Current.Dispatcher?.Invoke(AverageSpeedReferenceCollection.Clear);
                StatusCurrentAction = $"Точка {point.Speed} : Корректировка скорости";
                if (point.Speed == 30)
				{
                    Thread.Sleep(15000);
				}
                else// Для скоростной точки 30, отключаю коррекцию скорости, так как труба не может разогнаться до 30 м/с. А где-то до 27-29 м/с.
				{
                    FrequencyMotorDevice.Instance.CorrectionSpeedMotor(ref _averageSpeedReferenceValue, point, ref _ctsTask);
				}
				if (_ctsTask.Token.IsCancellationRequested)
				{
					return;
				}
                _acceptCorrectionReference = true;
                Thread.Sleep(100);
                if (point.Speed == 0.7m)// На данной скорости, датчик вращается очень медленно. А значение поступает на частотомер, в момент полного оборота датчика.
				{
                    timeOutCounter = 5000;
				}
				for (int i = 0; i < CollectionDvsValue02[id].DeviceSpeedValues.Length; i++)
				{
					CollectionDvsValue02[id].DeviceSpeedValues[i].IsСheckedNow = true;
					StatusCurrentAction = $"Точка {point.Speed} : Снятие значения {i + 1}";
					if (_ctsTask.Token.IsCancellationRequested)
					{
						return;
					}
					CollectionDvsValue02[id].DeviceSpeedValues[i].ResultValue = FrequencyCounterDevice.Instance.GetCurrentHzValue(point, timeOutCounter, _ctsTask);
					CollectionDvsValue02[id].DeviceSpeedValues[i].IsVerified = true;
					Thread.Sleep(50);
					if (_ctsTask.Token.IsCancellationRequested)
					{
						return;
					}
				}
                BusyContent = string.Empty;
                IsBusy = false;
                CollectionDvsValue02[id].ReferenceSpeedValue = _averageSpeedReferenceValue;
            }
        }

		private void StartTestDvs03() { }

        private void ResultToXlsxDvs2()
        {
            var pathExampleXlsxFile = @"Resources\Dvs2.xlsx";
            while (true)
            {
                if (File.Exists(pathExampleXlsxFile))
				{
                    break;
				}
                if (MessageBoxResult.OK == MessageBox.Show("Отсутствует файл образец Dvs2.xlsx. Пожалуйста, поместите файл и повторите попытку (ОК). Или нажмите отмена для пропуска создания .xlsx", "Ошибка", MessageBoxButton.OKCancel))
				{
                    continue;// Если было нажато "ОК".
				}
                return;// Если была нажата "отмена".
			}
            using (var package = new ExcelPackage(new FileInfo(pathExampleXlsxFile)))
            {
                var ws = package.Workbook.Worksheets.First();
				#region Заполнение значений
				const int xlsxSeed = 14;
                for (int i = 0; i < 7; i++)
                {
                    AddValueInCell(ws.Cells[i + xlsxSeed, 12], CollectionDvsValue02[i].ReferenceSpeedValue);
                    AddValueInCell(ws.Cells[i + xlsxSeed, 13], CollectionDvsValue02[i].DeviceSpeedValues[0].ResultValue);
                    AddValueInCell(ws.Cells[i + xlsxSeed, 14], CollectionDvsValue02[i].DeviceSpeedValues[1].ResultValue);
                    AddValueInCell(ws.Cells[i + xlsxSeed, 15], CollectionDvsValue02[i].DeviceSpeedValues[2].ResultValue);
                    AddValueInCell(ws.Cells[i + xlsxSeed, 16], CollectionDvsValue02[i].DeviceSpeedValues[3].ResultValue);
                    AddValueInCell(ws.Cells[i + xlsxSeed, 17], CollectionDvsValue02[i].DeviceSpeedValues[4].ResultValue);
                }
                //Условия поверки
                ws.Cells[42, 16].Value = MeasurementsData.Verifier;
                ws.Cells[42, 20].Value = DateTime.Now.ToString("dd.MM.yyyy");
                ws.Cells[25, 5].Value = MeasurementsData.Temperature;
                ws.Cells[26, 5].Value = MeasurementsData.Humidity;
                ws.Cells[27, 5].Value = MeasurementsData.Pressure;
                ws.Cells[16, 6].Value = MeasurementsData.DeviceId;
                //ws.Cells[5, 4].Value = "Протокол ДВС-02 №00212522 от 10.01.2021";
                #endregion
                var path = $"Протокол ДВС-02 № {MeasurementsData.DeviceId} от {DateTime.Now:dd.MM.yyyy}.xlsx";
                var fullPath = Path.Combine(PathSave, path);
                var attemptSave = 1;
                while (true)
                {
                    if (File.Exists(fullPath))
                    {
                        path = $"Протокол ДВС-02 № {MeasurementsData.DeviceId} от {DateTime.Now:dd.MM.yyyy}({attemptSave}).xlsx";
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
				{
                    break;
				}
                if (MessageBoxResult.OK == MessageBox.Show("Отсутствует файл образец Dvs1.xlsx. Пожалуйста, поместите файл и повторите попытку (ОК). Или нажмите отмена для пропуска создания .xlsx", "Ошибка", MessageBoxButton.OKCancel))
				{
                    continue;// Если было нажато "ОК".
				}
                return;// Если была нажата "отмена".
			}
            using (var package = new ExcelPackage(new FileInfo(pathExampleXlsxFile)))
            {
                var ws = package.Workbook.Worksheets.First();
				#region Заполнение значений
				const int xlsxSeed = 14;
                for (int i = 0; i < 5; i++)
                {
                    AddValueInCell(ws.Cells[i + xlsxSeed, 16], CollectionDvsValue01[i].ReferenceSpeedValueMain);
                    AddValueInCell(ws.Cells[i + xlsxSeed, 17], CollectionDvsValue01[i].DeviceSpeedValues[0].ResultValue);
                    AddValueInCell(ws.Cells[i + xlsxSeed, 18], CollectionDvsValue01[i].DeviceSpeedValues[1].ResultValue);
                    AddValueInCell(ws.Cells[i + xlsxSeed, 19], CollectionDvsValue01[i].DeviceSpeedValues[2].ResultValue);
                }
                //Условия поверки
                ws.Cells[44, 16].Value = MeasurementsData.Verifier;
                ws.Cells[44, 20].Value = DateTime.Now.ToString("dd.MM.yyyy");
                ws.Cells[25, 5].Value = MeasurementsData.Temperature;
                ws.Cells[26, 5].Value = MeasurementsData.Humidity;
                ws.Cells[27, 5].Value = MeasurementsData.Pressure;
                ws.Cells[16, 6].Value = MeasurementsData.DeviceId;
                #endregion
                var path = $"Протокол ДСВ-01 № {MeasurementsData.DeviceId} от {DateTime.Now:dd.MM.yyyy}.xlsx";
                var fullPath = Path.Combine(PathSave, path);
                var attemptSave = 1;
                while (true)
                {
                    if (File.Exists(fullPath))
                    {
                        path = $"Протокол ДСВ-01 № {MeasurementsData.DeviceId} от {DateTime.Now:dd.MM.yyyy}({attemptSave}).xlsx";
                        fullPath = Path.Combine(PathSave, path);
                        attemptSave++;
                        continue;
                    }
                    break;
                }
                package.SaveAs(new FileInfo(fullPath));
            }
        }

		private void ResultToXlsxDvs3() { }

        /// <summary>Метод для добавления значения в ячейку excel и ее обработка</summary>
        private void AddValueInCell(ExcelRange excelRange, decimal? value)
        {
            if (value != null)
			{
                excelRange.Value = value;
			}
            excelRange.Style.Numberformat.Format = "#,###0.000";
        }
        #endregion

        private void DefaultSpeedPoints_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            for (int i = 0; i < SpeedPointsList.Count; i++)
			{
                SpeedPointsList[i].Id = i + 1;
			}
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
            SpeedReferenceValue = (decimal)e.ReferenceValue;
            UpdateAverageSpeedReferenceValue(SpeedReferenceValue);
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
                    using (var file = File.Open(USER_SETTINGS_PATH, FileMode.Create))
                    {
                        using (var writer = new StreamWriter(file))
                        {
                            serializer.Serialize(writer, _userSettings);
                        }
                    }
                }
            }
            catch (Exception e)
            {
				// ignored
				MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private UserSettings Deserialization()
        {
            var deserializer = new Deserializer();
            using (var file = File.Open(USER_SETTINGS_PATH, FileMode.OpenOrCreate, FileAccess.Read))
            {
                using (var reader = new StreamReader(file))
                {
                    try
                    {
                        return deserializer.Deserialize<UserSettings>(reader);
                    }
                    catch (Exception e)
                    {
						MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }
                }
            }
        }
        #endregion
    }

    public class SpeedPoint
    {
        [XmlIgnore] private int _maxStep = 10;
        /// <summary>Тестируемая скорость</summary>
        public decimal Speed { get; set; }
        public decimal MaxEdge { get; set; }
        public decimal MinEdge { get; set; }
        /// <summary>Примерная частота вращения трубы, для достижения этой скорости</summary>
        public int SetFrequency { get; set; }
        /// <summary>Максимальный шаг при корректировке частоты, для достижения установленной скорости</summary>
        public int MaxStep
        {
            get => _maxStep;
            set
            {
                if (value < 10 || value > 100)
                {
                    MessageBox.Show("Выберете значение в диапазоне от 10 до 100 Гц");
                    MaxStep = _maxStep;
                    return;
                }
                //MaxStep = value;
                _maxStep = value;
            }
        }
        /// <summary>Номер в списке</summary>
        public int Id { get; set; }
    }

    public class TypeTestDescription
    {
        public TypeTest TypeTest { get; set; }
        public string Description { get; set; }

        public TypeTestDescription(TypeTest typeTest, string description)
        {
            TypeTest = typeTest;
            Description = description;
        }
    }

    public enum TypeTest
    {
        Dvs01 = 1,
        Dvs02 = 2,
		Dvs03 = 3
    }

    public enum SelectedPage
    {
        MainWindow = 0,
        Settings = 1,
        Checkpoint = 2,
        Debug = 3
    }
}