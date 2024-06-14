using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VerificationAirVelocitySensor.Models;
using VerificationAirVelocitySensor.Models.ClassLib;
using VerificationAirVelocitySensor.Views;
using VerificationAirVelocitySensor.ViewModels.BaseVm;
using VerificationAirVelocitySensor.ViewModels.Sensors;
using VerificationAirVelocitySensor.ViewModels.Services;
using VerificationAirVelocitySensor.Model.EnumLib;

namespace VerificationAirVelocitySensor.ViewModels
{
	internal class MainWindowVm : BaseVm.BaseVm
	{
		private const int T_OUT_SET_FREQ = 5000;// Время ожидания после установки значения частоты, чтобы дать аэротрубе стабилизировать значение.
		private List<decimal> _avgSpRefs;
		private CancellationTokenSource _ctsTask;
		// Все свойства что ниже, должны сохранятся при перезапуске.
		private UserControl _frame;
		private decimal _spRef;// Эталонное значение скорости с частотной трубы.
		private decimal _avgSpRef;
		private bool _canAdjust;// Флаг для включения в коллекцию среднего значения эталона, всех его значений за время теста скоростной точки после прохождения корректировки.
		private bool _isActiveRevision;// Флаг, показывающий активно ли тестирование.
		private bool _isBusy;
		#region Properties.
		public ObservableCollection<Dsv01Sp> WssValues01 { get; }
		public ObservableCollection<Dvs02Sp> WssValues02 { get; }
		public Sensor[] Sensors { get; }
		public RelayCommand[] RevisionCmds { get; }// Start, Stop, SaveSpeeds, VisibilitySetFrequency, Reset, ClosePortFrequencyCounter, ClosePortFrequencyMotor.
		public RelayCommand[] PaginationCmds { get; }// MainWindow, Settings, Checkpoints, Debug tab.
		public UserSettings UserSettings { get; }// Свойство для хранения условий поверки.
		public string Title { get; }
		public bool ChangeModelOnValidation => !_isActiveRevision;
		public UserControl Frame
		{
			get => _frame;
			private set
			{
				_frame = value;
				if (value != null)
				{
					SelectedPage = Enum.GetValues<SelectedPage>().FirstOrDefault(p => p.ToString().StartsWith(value.GetType().Name.Split("View")[0]));
				}
				else
				{
					SelectedPage = SelectedPage.Main;
				}
				UserSettings.Serialize();
			}
		}
		public SelectedPage SelectedPage { get; private set; }
		public string Status { get; private set; }// Свойство для биндинга на UI текущего действия внутри app.
		public string BusyContent { get; private set; }// Текст BusyIndicator.
		public decimal SpeedReference
		{
			get => _spRef;
			private set
			{
				_spRef = value;
				OnPropertyChanged(nameof(SpeedReference));
			}
		}
		public bool IsBusy// Активность BusyIndicator.
		{
			get => _isBusy;
			private set
			{
				_isBusy = value;
				if (!value)
				{
					BusyContent = "";
				}
			}
		}
		public bool VisibilitySetFrequency { get; private set; }
		public bool[] WssVisibility { get; private set; }// Флаг для биндинга отображения таблицы разультатов для WSS-0(n).
		#endregion
		private readonly Wss03Vm _wss03Vm;
		public Wss03 Wss03 { get; }

		public MainWindowVm()
		{
			_avgSpRefs = [];
			Tube.Instance.ReferenceUpdate += (_, e) => {
				SpeedReference = (decimal)e.ReferenceValue;
				if (_avgSpRefs.Count > 5 && !_canAdjust)
				{
					_avgSpRefs.RemoveAt(0);
				}
				_avgSpRefs.Add(SpeedReference);
				_avgSpRef = Math.Round(_avgSpRefs.Average(), 2);
			};
			WssVisibility = new bool[Enum.GetNames<SensorModel>().Length];
			UserSettings = UserSettings.Deserialize() ?? new UserSettings() { SensorModel = SensorModel.Dvs02 };
			UserSettings.PropertyChanged += (_, _) => {
				UserSettings.Serialize();
				for (int i = 0; i < WssVisibility.Length; i++)
				{
					WssVisibility[i] = false;
				}
				WssVisibility[(int)UserSettings.SensorModel - 1] = true;
				OnPropertyChanged(nameof(WssVisibility));
			};
			_wss03Vm = new Wss03Vm(UserSettings);
			_wss03Vm.PropertyChanged += (_, e) => {
				switch (e.PropertyName)
				{
					case "Status":
						Status = _wss03Vm.Status;
						break;
					case "IsBusy":
						IsBusy = false;// "IsBusy" take only false.
						break;
				}
			};
			WssVisibility[(int)UserSettings.SensorModel - 1] = true;
			Tube.Instance.SetFrequencyUpdate += (_, e) => UserSettings.SettingsModel.SetFrequencyTube = e.SetFrequency;
			Title = $"{Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title}  v{Assembly.GetExecutingAssembly().GetName().Version}";
			Sensors = [new Sensor(SensorModel.Dvs01, "ДСВ-01"), new Sensor(SensorModel.Dvs02, "ДВС-02"), new Sensor(SensorModel.Wss03, "ДВС-03")];
			WssValues01 = [];
			WssValues02 = [];
			Wss03 = new Wss03(_wss03Vm);
			RevisionCmds = [
				 new(Start, () => !_isActiveRevision)// Start.
				, new(() => {
					BusyContent = "Тестирование прервано пользователем\nОжидание завершения процесса";
					IsBusy = true;
					_isActiveRevision = false;
					_ctsTask.Cancel();
				}, () => _isActiveRevision)// Stop.
				, new(() => UserSettings.Serialize())// SaveSpeeds.
				, new(() => VisibilitySetFrequency = !VisibilitySetFrequency)// VisibilitySetFrequency.
				#region Частотомер ЧЗ-85/6.
				, new RelayCommand(Cymometer.Instance.Reset, Cymometer.Instance.IsOpen)// Reset.
				, new RelayCommand(Cymometer.Instance.Close, Cymometer.Instance.IsOpen)// ClosePortFrequencyCounter.
				#endregion
				#region Анемометр / Частотный двигатель.
				, new RelayCommand(Tube.Instance.Close, Tube.Instance.IsOpen)// ClosePortFrequencyMotor.
				#endregion
			];
			PaginationCmds = [
				new(() => { Frame = null; SelectedPage = SelectedPage.Main; }, () => SelectedPage != SelectedPage.Main && !_isActiveRevision)// MainWindow.
				, new(() => Frame = new SettingsView(new SettingsVm(UserSettings.SettingsModel)), () => SelectedPage != SelectedPage.Settings && !_isActiveRevision)// Settings.
				, new(() => Frame = new CheckpointsView(UserSettings.Checkpoints, RevisionCmds[2]), () => SelectedPage != SelectedPage.Checkpoints && !_isActiveRevision)// Checkpoints.
				, new(ChangePageOnDebug, () => SelectedPage != SelectedPage.Debug && !_isActiveRevision)// Debug tab.
			];
			#region Debug.
			//CollectionDvsValue02.Add(new DvsValue02(5) {
			//	DeviceSpeedValue1 = new SpeedValue { IsVerified = true, IsСheckedNow = true, ResultValue = 4.32m }
			//	, DeviceSpeedValue2 = new SpeedValue()
			//	, DeviceSpeedValue3 = new SpeedValue()
			//	, DeviceSpeedValue4 = new SpeedValue()
			//	, DeviceSpeedValue5 = new SpeedValue()
			//	, ReferenceSpeedValue = 5
			//});
			//CollectionDvsValue02.Add(new DvsValue02(10) { DeviceSpeedValue1 = new SpeedValue { ResultValue = 14.32m }, ReferenceSpeedValue = 10 });
			//CollectionDvsValue02.Add(new DvsValue02(15) { DeviceSpeedValue1 = new SpeedValue { IsСheckedNow = true, ResultValue = 24.32m }, ReferenceSpeedValue = 15 });
			//CollectionDvsValue02.Add(new DvsValue02(20) { DeviceSpeedValue1 = new SpeedValue { ResultValue = 14.32m }, ReferenceSpeedValue = 20 });
			//CollectionDvsValue02.Add(new DvsValue02(25) { DeviceSpeedValue1 = new SpeedValue { IsСheckedNow = true, ResultValue = 24.32m }, ReferenceSpeedValue = 25 });
			//CollectionDvsValue02.Add(new DvsValue02(30) { DeviceSpeedValue1 = new SpeedValue { ResultValue = 14.32m }, ReferenceSpeedValue = 30 });
			#endregion
		}

		#region Relay command methods.
		#region Методы смены страниц.
		private async void ChangePageOnDebug()
		{
			await Task.Run(() =>
			{
				IsBusy = true;
				BusyContent = "Проверка подключения аэродинамической трубы";
				if (!Tube.Instance.Open(UserSettings.SettingsModel.TubePort))
				{
					IsBusy = false;
					return;
				}
				IsBusy = false;
				Application.Current.Dispatcher?.Invoke(() => Frame = new DebugView());
			});
		}
		#endregion

		private bool OpenCymometer()
		{
			if (!Cymometer.Instance.Open(UserSettings.SettingsModel.CymometerPort, (int)UserSettings.SettingsModel.GateTime * 1000))
			{
				return false;
			}
			switch (Cymometer.Instance.GetModel())
			{
				case "counter":
					OnOffFilter(0);
					OnOffFilter(1);
					Cymometer.Instance.SetGateTime(UserSettings.SettingsModel.GateTime);
					break;
				case "WSS":
					break;
				default:
					Cymometer.Instance.Close();
					MessageBox.Show($"{UserSettings.SettingsModel.CymometerPort} не является ни частотомером, ни ДВС-03.", "Нераспознанное устройство", MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
			}
			return true;
		}

		#region Частотомер ЧЗ-85/6.
		private void OnOffFilter(int chIndex)
		{
			if (chIndex < 0 || 1 < chIndex)
			{
				throw new ArgumentOutOfRangeException(nameof(chIndex));
			}
			Cymometer.Instance.SwitchFilter(chIndex + 1, UserSettings.SettingsModel.FilterChannels[chIndex]);
		}
		#endregion
		#endregion

		#region Revision methods.
		private void EmergencyStop()
		{
			BusyContent = "Аварийная остановка";
			IsBusy = true;
			_isActiveRevision = false;
			_ctsTask.Cancel();
			MessageBox.Show("Произошло аварийное завершение поверки", "Внимание", MessageBoxButton.OK, MessageBoxImage.Error);
		}

		private void Stop()
		{
			_isActiveRevision = false;
			IsBusy = false;
			Cymometer.Instance.Close();
			Tube.Instance.Close();
		}

		private async void Start()
		{
			_isActiveRevision = true;
			if (!OpenConditions())
			{
				_isActiveRevision = false;
				return;
			}
			await Task.Run(() =>
			{
				IsBusy = true;
				BusyContent = "Проверка подключенных устройств и их настройка";
				if (!OpenCymometer() || !Tube.Instance.Open(UserSettings.SettingsModel.TubePort))
				{
					Stop();
					return;
				}
				IsBusy = false;
				_ctsTask = new CancellationTokenSource();
				Cymometer.Instance.Stop = EmergencyStop;
				try
				{
					Status = "Запуск тестирования";
					switch (UserSettings.SensorModel)
					{
						case SensorModel.Dvs01:
							Init01Sps();
							StartWss01();
							break;
						case SensorModel.Dvs02:
							Init02Sps();
							StartWss02();
							break;
						case SensorModel.Wss03:
							_wss03Vm.Revise(ref _canAdjust, ref _avgSpRefs, ref _avgSpRef, ref _ctsTask);
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
					Status = "Поверка завершена";
				}
				catch (Exception e)
				{
					GlobalLog.Log.Debug(e, e.Message);
					MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				finally
				{
					Tube.Instance.SetFreq(0, 0);
					switch (UserSettings.SensorModel)
					{
						case SensorModel.Dvs01:
							WriteXlsxWss01();
							break;
						case SensorModel.Dvs02:
							WriteXlsxWss02();
							break;
					}
					Stop();
					MessageBox.Show("Поверка завершена", "Завершено", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
			});
		}

		private bool OpenConditions()
		{
			var c = new SetMeasurementsData(new SetMeasurementsDataVm(UserSettings));
			c.ShowDialog();
			bool isContinue = c.ViewModel.IsContinue;
			c.Close();
			if (!isContinue)
			{
				MessageBox.Show("Отменено пользователем", "Внимание", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}
			UserSettings.Serialize();
			if (string.IsNullOrEmpty(UserSettings.SavePath))
			{
				MessageBox.Show("Не указан путь сохранения", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}
			return isContinue;
		}

		private void Init01Sps()// Метод для очистки от старых значений WssValues01 с заполнением пустыми значениями. Для WSS-01.
		{
			Application.Current.Dispatcher?.Invoke(() =>
			{
				WssValues01?.Clear();
				for (int i = 1; i < UserSettings.Checkpoints.Count - 1; i++)// Первую точку (0.7) скипаю и последнюю (30).
				{
					WssValues01.Add(new Dsv01Sp(UserSettings.Checkpoints[i].Speed));
				}
			});
		}

		private void Init02Sps()// Метод для очистки от старых значений WssValues02 с заполнением пустыми значениями. Для WSS-02.
		{
			Application.Current.Dispatcher?.Invoke(() =>
			{
				WssValues02?.Clear();
				for (int i = 0; i < UserSettings.Checkpoints.Count; i++)
				{
					WssValues02.Add(new Dvs02Sp(UserSettings.Checkpoints[i].Speed));
				}
			});
		}

		private void StartWss01()
		{
			int tOutCounter = (int)UserSettings.SettingsModel.GateTime * 1000;
			decimal? avg;
			int id = 0;
			for (int i = 0; i < WssValues01[id].DeviceSpeedValues.Length; i++)
			{
				for (int j = 1; j < UserSettings.Checkpoints.Count - 1; j++)// Первую точку (0.7) скипаю и последнюю (30). Снятие 1-ого значения.
				{
					id = UserSettings.Checkpoints[j].Id - 2;// Исправляем смещение из-за скипа 1-ой позиции в SpeedPointsList и в разнице нумерации в SpeedPointsList. Выходит -2.
					WssValues01[id].DeviceSpeedValues[i].IsСheckedNow = true;
					Prepare(j);// Метод разгона трубы.
					Status = $"Точка {UserSettings.Checkpoints[j].Speed}: Снятие значения 1";
					WssValues01[id].DeviceSpeedValues[i].ResultValue = Cymometer.Instance.GetCurrentHz(UserSettings.Checkpoints[j], i > 0 ? tOutCounter : 7000, _ctsTask);// Время запроса для точки 0.7 больше из-за маленькой скорости прокрутки датчика.
					WssValues01[id].DeviceSpeedValues[i].IsVerified = true;
					Thread.Sleep(50);
					if (_ctsTask.Token.IsCancellationRequested)
					{
						return;
					}
					WssValues01[id].ReferenceSpeedValues[i] = _avgSpRef;
				}
			}
			for (int i = 0; i < WssValues01.Count; i++)
			{
				avg = WssValues01[i].ReferenceSpeedValues.Average();
				if (avg != null)
				{
					WssValues01[i].ReferenceSpeedValueMain = Math.Round((decimal)avg, 2);
				}
			}
		}

		private void StartWss02()
		{
			int tOutCounter = (int)UserSettings.SettingsModel.GateTime * 1000;
			for (int i = 0; i < UserSettings.Checkpoints.Count; i++)
			{
				Prepare(i);
				if (UserSettings.Checkpoints[i].Speed == 0.7m)// На данной скорости, датчик вращается очень медленно. А значение поступает на частотомер в момент полного оборота датчика.
				{
					tOutCounter = 5000;
				}
				for (int j = 0; j < WssValues02[i].DeviceSpeeds.Length; j++)
				{
					WssValues02[i].DeviceSpeeds[j].IsСheckedNow = true;
					Status = $"Точка {UserSettings.Checkpoints[i].Speed}: Снятие значения {j + 1}";
					WssValues02[i].DeviceSpeeds[j].ResultValue = Cymometer.Instance.GetCurrentHz(UserSettings.Checkpoints[i], tOutCounter, _ctsTask);
					WssValues02[i].DeviceSpeeds[j].IsVerified = true;
					Thread.Sleep(50);
					if (_ctsTask.Token.IsCancellationRequested)
					{
						return;
					}
				}
				IsBusy = false;
				WssValues02[i].ReferenceSpeed = _avgSpRef;
			}
		}

		private void Prepare(int pntInd)
		{
			Status = $"Точка {UserSettings.Checkpoints[pntInd].Speed}";
			_canAdjust = false;
			Tube.Instance.SetFreq(UserSettings.Checkpoints[pntInd].Frequency, UserSettings.Checkpoints[pntInd].Speed);
			Thread.Sleep(T_OUT_SET_FREQ);// Время ожидания для стабилизации трубы.
			Application.Current.Dispatcher?.Invoke(_avgSpRefs.Clear);
			Status = $"Точка {UserSettings.Checkpoints[pntInd].Speed}: Корректировка скорости";
			if (UserSettings.Checkpoints[pntInd].Speed == 30)
			{
				Thread.Sleep(15000);
			}
			else// Для скоростной точки 30 отключаю коррекцию скорости, тк. труба не может разогнаться до 30 м/с. А где-то до 27-29 м/с.
			{
				Tube.Instance.AdjustSp(ref _avgSpRef, UserSettings.Checkpoints[pntInd], ref _ctsTask);
			}
			if (_ctsTask.Token.IsCancellationRequested)
			{
				return;
			}
			_canAdjust = true;
			Thread.Sleep(100);
		}

		private void WriteXlsxWss01()
		{
			const string SAMPLE_PATH = @"Resources\Dsv1.xlsx";
			while (!File.Exists(SAMPLE_PATH))
			{
				if (1 != (int)MessageBox.Show("Отсутствует файл образец Dvs1.xlsx. Пожалуйста, поместите файл и повторите попытку (ОК). Или нажмите \"Отмена\" для пропуска создания .xlsx", "Ошибка", MessageBoxButton.OKCancel))
				{
					return;
				}
			}
			var p = new ExcelPackage(new FileInfo(SAMPLE_PATH));
			var ws = p.Workbook.Worksheets[0];
			const int XLSX_SEED = 14;
			for (int i = 0; i < WssValues01.Count; i++)
			{
				AddToCell(ws.Cells[i + XLSX_SEED, 16], WssValues01[i].ReferenceSpeedValueMain);
				for (int j = 0; j < WssValues01[i].DeviceSpeedValues.Length; j++)
				{
					AddToCell(ws.Cells[i + XLSX_SEED, j + 17], WssValues01[i].DeviceSpeedValues[j].ResultValue);
				}
			}
			// Условия поверки.
			ws.Cells[44, 16].Value = UserSettings.MeasurementsData.Verifier;
			ws.Cells[44, 20].Value = DateTime.Now.ToString("dd.MM.yyyy");
			ws.Cells[25, 5].Value = UserSettings.MeasurementsData.Temperature;
			ws.Cells[26, 5].Value = UserSettings.MeasurementsData.Humidity;
			ws.Cells[27, 5].Value = UserSettings.MeasurementsData.Pressure;
			ws.Cells[16, 6].Value = UserSettings.MeasurementsData.DeviceId;
			string path = $"Протокол ДСВ-01 № {UserSettings.MeasurementsData.DeviceId} от {DateTime.Now:dd.MM.yyyy}.xlsx";
			string fullPath = Path.Combine(UserSettings.SavePath, path);
			int attemptSave = 1;
			while (File.Exists(fullPath))
			{
				path = $"Протокол ДСВ-01 № {UserSettings.MeasurementsData.DeviceId} от {DateTime.Now:dd.MM.yyyy}({attemptSave}).xlsx";
				fullPath = Path.Combine(UserSettings.SavePath, path);
				attemptSave++;
			}
			p.SaveAs(new FileInfo(fullPath));
		}

		private void WriteXlsxWss02()
		{
			const string SAMPLE_PATH = @"Resources\Dvs2.xlsx";
			while (!File.Exists(SAMPLE_PATH))
			{
				if (1 != (int)MessageBox.Show("Отсутствует файл образец Dvs2.xlsx. Пожалуйста, поместите файл и повторите попытку (ОК). Или нажмите отмена для пропуска создания .xlsx", "Ошибка", MessageBoxButton.OKCancel))
				{
					return;
				}
			}
			var p = new ExcelPackage(new FileInfo(SAMPLE_PATH));
			var ws = p.Workbook.Worksheets[0];
			#region Заполнение значений.
			const int XLSX_SEED = 14;
			for (int i = 0; i < WssValues02.Count; i++)
			{
				AddToCell(ws.Cells[i + XLSX_SEED, 12], WssValues02[i].ReferenceSpeed);
				for (int j = 0; j < WssValues02[i].DeviceSpeeds.Length; j++)
				{
					AddToCell(ws.Cells[i + XLSX_SEED, j + 13], WssValues02[i].DeviceSpeeds[j].ResultValue);
				}
			}
			// Условия поверки.
			ws.Cells[42, 16].Value = UserSettings.MeasurementsData.Verifier;
			ws.Cells[42, 20].Value = DateTime.Now.ToString("dd.MM.yyyy");
			ws.Cells[25, 5].Value = UserSettings.MeasurementsData.Temperature;
			ws.Cells[26, 5].Value = UserSettings.MeasurementsData.Humidity;
			ws.Cells[27, 5].Value = UserSettings.MeasurementsData.Pressure;
			ws.Cells[16, 6].Value = UserSettings.MeasurementsData.DeviceId;
			//ws.Cells[5, 4].Value = "Протокол ДВС-02 №00212522 от 10.01.2021";
			#endregion
			string path = $"Протокол ДВС-02 № {UserSettings.MeasurementsData.DeviceId} от {DateTime.Now:dd.MM.yyyy}.xlsx";
			string fullPath = Path.Combine(UserSettings.SavePath, path);
			int attemptSave = 1;
			while (File.Exists(fullPath))
			{
				path = $"Протокол ДВС-02 № {UserSettings.MeasurementsData.DeviceId} от {DateTime.Now:dd.MM.yyyy}({attemptSave}).xlsx";
				fullPath = Path.Combine(UserSettings.SavePath, path);
				attemptSave++;
			}
			p.SaveAs(new FileInfo(fullPath));
		}

		public static void AddToCell(ExcelRange er, decimal? v)// Метод для добавления значения в ячейку excel и её обработка.
		{
			if (v != null)
			{
				er.Value = v;
			}
			er.Style.Numberformat.Format = "#,###0.000";
		}
		#endregion
	}
}