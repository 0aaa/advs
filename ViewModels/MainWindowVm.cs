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
using VerificationAirVelocitySensor.Models.Classes;
using VerificationAirVelocitySensor.Views;
using VerificationAirVelocitySensor.ViewModels.Base;
using VerificationAirVelocitySensor.ViewModels.Sensors;
using VerificationAirVelocitySensor.ViewModels.Services;
using VerificationAirVelocitySensor.Models.Enums;

namespace VerificationAirVelocitySensor.ViewModels
{
	internal class MainWindowVm : BaseVm
	{
		private const int LAT = 5000;// Время ожидания после установки значения частоты, чтобы дать аэротрубе стабилизировать значение.
		private readonly List<decimal> _avgSrefs;
		private CancellationTokenSource _t;
		// Все свойства что ниже, должны сохранятся при перезапуске.
		private UserControl _frame;
		private decimal _sRef;// Эталонное значение скорости с частотной трубы.
		private decimal _avgSref;
		private bool _canAdjust;// Флаг для включения в коллекцию среднего значения эталона, всех его значений за время теста скоростной точки после прохождения корректировки.
		private bool _isActiveRevision;// Флаг, показывающий активно ли тестирование.
		private bool _isBusy;
		#region Properties.
		public ObservableCollection<Wss01Measur> Wss01Measurements { get; }
		public ObservableCollection<Wss02Measur> Wss02Measurements { get; }
		public Wss[] Sensors { get; }
		public RelayCommand[] RevisionRcs { get; }// Start, Stop, SaveSpeeds, VisibilitySetFrequency, Reset, ClosePortFrequencyCounter, ClosePortFrequencyMotor.
		public RelayCommand[] PaginationRcs { get; }// MainWindow, Settings, Checkpoints, Debug tab.
		public Settings Settings { get; }// Свойство для хранения условий поверки.
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
					CurrPage = Enum.GetValues<CurrPage>().FirstOrDefault(p => p.ToString().StartsWith(value.GetType().Name.Split("View")[0]));
				}
				else
				{
                    CurrPage = CurrPage.Main;
				}
				Settings.Serialize();
			}
		}
		public CurrPage CurrPage { get; private set; }
		public string Stat { get; private set; }// Свойство для биндинга на UI текущего действия внутри app.
		public string BusyContent { get; private set; }// Текст BusyIndicator.
		public decimal Sref
		{
			get => _sRef;
			private set
			{
				_sRef = value;
				OnPropertyChanged(nameof(Sref));
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
		public bool Fvisibility { get; private set; }
		public bool[] WssVisibility { get; private set; }// Флаг для биндинга отображения таблицы разультатов для WSS-0(n).
		#endregion
		private readonly Wss03Vm _wss03Vm;
		public Wss03 Wss03 { get; }

		public MainWindowVm()
		{
			_avgSrefs = [];
			Tube.Inst.RefUpd += (_, e) => {
				Sref = (decimal)e.Ref;
				if (_avgSrefs.Count > 5 && !_canAdjust)
				{
					_avgSrefs.RemoveAt(0);
				}
				_avgSrefs.Add(Sref);
				_avgSref = Math.Round(_avgSrefs.Average(), 2);
			};
			WssVisibility = new bool[Enum.GetNames<Model>().Length];
			Settings = Settings.Deserialize() ?? new Settings() { M = Model.Dvs02 };
			Settings.PropertyChanged += (_, _) => {
				Settings.Serialize();
				for (int i = 0; i < WssVisibility.Length; i++)
				{
					WssVisibility[i] = false;
				}
				WssVisibility[(int)Settings.M - 1] = true;
				OnPropertyChanged(nameof(WssVisibility));
			};
			_wss03Vm = new Wss03Vm(Settings);
			_wss03Vm.PropertyChanged += (_, e) => {
				switch (e.PropertyName)
				{
					case "Status":
						Stat = _wss03Vm.Stat;
						break;
					case "IsBusy":
						IsBusy = false;// "IsBusy" take only false.
						break;
				}
			};
			WssVisibility[(int)Settings.M - 1] = true;
			Tube.Inst.CurrFupd += (_, e) => Settings.Devices.F = e.F;
			Title = $"{Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title}  v{Assembly.GetExecutingAssembly().GetName().Version}";
			Sensors = [new Wss(Model.Dvs01, "ДСВ-01"), new Wss(Model.Dvs02, "ДВС-02"), new Wss(Model.Wss03, "ДВС-03")];
			Wss01Measurements = [];
			Wss02Measurements = [];
			Wss03 = new Wss03(_wss03Vm);
			RevisionRcs = [
				 new(Start, () => !_isActiveRevision)// Start.
				, new(() => {
					BusyContent = "Тестирование прервано пользователем\nОжидание завершения процесса";
					IsBusy = true;
					_isActiveRevision = false;
					_t.Cancel();
				}, () => _isActiveRevision)// Stop.
				, new(() => Settings.Serialize())// SaveSpeeds.
				, new(() => Fvisibility = !Fvisibility)// VisibilitySetFrequency.
				#region Частотомер ЧЗ-85/6.
				, new RelayCommand(Cymometer.Inst.Reset, Cymometer.Inst.IsOpen)// Reset.
				, new RelayCommand(Cymometer.Inst.Close, Cymometer.Inst.IsOpen)// ClosePortFrequencyCounter.
				#endregion
				#region Анемометр / Частотный двигатель.
				, new RelayCommand(Tube.Inst.Close, Tube.Inst.IsOpen)// ClosePortFrequencyMotor.
				#endregion
			];
            PaginationRcs = [
				new(() => { Frame = null; CurrPage = CurrPage.Main; }, () => CurrPage != CurrPage.Main && !_isActiveRevision)// MainWindow.
				, new(() => Frame = new SettingsView(new DeviceSettingsVm(Settings.Devices)), () => CurrPage != CurrPage.Settings && !_isActiveRevision)// Settings.
				, new(() => Frame = new CheckpointsView(Settings.Checkpoints, RevisionRcs[2]), () => CurrPage != CurrPage.Checkpoints && !_isActiveRevision)// Checkpoints.
				, new(ChangePageOnDebug, () => CurrPage != CurrPage.Debug && !_isActiveRevision)// Debug tab.
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
				if (!Tube.Inst.Open(Settings.Devices.Tube))
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
			if (!Cymometer.Inst.Open(Settings.Devices.Cymometer, (int)Settings.Devices.Sec * 1000))
			{
				return false;
			}
			switch (Cymometer.Inst.GetModel())
			{
				case "counter":
					OnOffFilter(0);
					OnOffFilter(1);
					Cymometer.Inst.SetGt(Settings.Devices.Sec);
					break;
				case "WSS":
					break;
				default:
					Cymometer.Inst.Close();
					MessageBox.Show($"{Settings.Devices.Cymometer} не является ни частотомером, ни ДВС-03.", "Нераспознанное устройство", MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
			}
			return true;
		}

		#region Частотомер ЧЗ-85/6.
		private void OnOffFilter(int chI)
		{
			if (chI < 0 || 1 < chI)
			{
				throw new ArgumentOutOfRangeException(nameof(chI));
			}
			Cymometer.Inst.SwitchFilter(chI + 1, Settings.Devices.FilterChs[chI]);
		}
		#endregion
		#endregion

		#region Revision methods.
		private void EmergencyStop()
		{
			BusyContent = "Аварийная остановка";
			IsBusy = true;
			_isActiveRevision = false;
			_t.Cancel();
			MessageBox.Show("Произошло аварийное завершение поверки", "Внимание", MessageBoxButton.OK, MessageBoxImage.Error);
		}

		private void Stop()
		{
			_isActiveRevision = false;
			IsBusy = false;
			Cymometer.Inst.Close();
			Tube.Inst.Close();
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
				if (!OpenCymometer() || !Tube.Inst.Open(Settings.Devices.Tube))
				{
					Stop();
					return;
				}
				IsBusy = false;
				_t = new CancellationTokenSource();
				Cymometer.Inst.Stop = EmergencyStop;
				try
				{
					Stat = "Запуск тестирования";
					switch (Settings.M)
					{
						case Model.Dvs01:
							Init01Sps();
							StartWss01();
							break;
						case Model.Dvs02:
							Init02Sps();
							StartWss02();
							break;
						case Model.Wss03:
							_wss03Vm.Revise(ref _canAdjust, _avgSrefs, ref _avgSref, _t);
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
					Stat = "Поверка завершена";
				}
				catch (Exception e)
				{
					GlobalLog.Log.Debug(e, e.Message);
					MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				finally
				{
					Tube.Inst.SetF(0, 0);
					switch (Settings.M)
					{
						case Model.Dvs01:
							WriteXlsxWss01();
							break;
						case Model.Dvs02:
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
			var c = new Views.Conditions(new ConditionsVm(Settings));
			c.ShowDialog();
			bool isContinue = c.ViewModel.IsContinue;
			c.Close();
			if (!isContinue)
			{
				MessageBox.Show("Отменено пользователем", "Внимание", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}
			Settings.Serialize();
			if (string.IsNullOrEmpty(Settings.SavePath))
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
				Wss01Measurements?.Clear();
				for (int i = 1; i < Settings.Checkpoints.Count - 1; i++)// Первую точку (0.7) скипаю и последнюю (30).
				{
					Wss01Measurements.Add(new Wss01Measur(Settings.Checkpoints[i].S));
				}
			});
		}

		private void Init02Sps()// Метод для очистки от старых значений WssValues02 с заполнением пустыми значениями. Для WSS-02.
		{
			Application.Current.Dispatcher?.Invoke(() =>
			{
				Wss02Measurements?.Clear();
				for (int i = 0; i < Settings.Checkpoints.Count; i++)
				{
					Wss02Measurements.Add(new Wss02Measur(Settings.Checkpoints[i].S));
				}
			});
		}

		private void StartWss01()
		{
			int latency = (int)Settings.Devices.Sec * 1000;
			decimal? avg;
			int id = 0;
			for (int i = 0; i < Wss01Measurements[id].Ss.Length; i++)
			{
				for (int j = 1; j < Settings.Checkpoints.Count - 1; j++)// Первую точку (0.7) скипаю и последнюю (30). Снятие 1-ого значения.
				{
					id = Settings.Checkpoints[j].Id - 2;// Исправляем смещение из-за скипа 1-ой позиции в SpeedPointsList и в разнице нумерации в SpeedPointsList. Выходит -2.
					Wss01Measurements[id].Ss[i].IsСheckedNow = true;
					Prepare(j);// Метод разгона трубы.
					Stat = $"Точка {Settings.Checkpoints[j].S}: Снятие значения 1";
					Wss01Measurements[id].Ss[i].V = Cymometer.Inst.GetCurrHz(Settings.Checkpoints[j], i > 0 ? latency : 7000, _t);// Время запроса для точки 0.7 больше из-за маленькой скорости прокрутки датчика.
					Wss01Measurements[id].Ss[i].IsVerified = true;
					Thread.Sleep(50);
					if (_t.Token.IsCancellationRequested)
					{
						return;
					}
					Wss01Measurements[id].RefSs[i] = _avgSref;
				}
			}
			for (int i = 0; i < Wss01Measurements.Count; i++)
			{
				avg = Wss01Measurements[i].RefSs.Average();
				if (avg != null)
				{
					Wss01Measurements[i].RefS = Math.Round((decimal)avg, 2);
				}
			}
		}

		private void StartWss02()
		{
			int latency = (int)Settings.Devices.Sec * 1000;
			for (int i = 0; i < Settings.Checkpoints.Count; i++)
			{
				Prepare(i);
				if (Settings.Checkpoints[i].S == 0.7m)// На данной скорости, датчик вращается очень медленно. А значение поступает на частотомер в момент полного оборота датчика.
				{
					latency = 5000;
				}
				for (int j = 0; j < Wss02Measurements[i].Ss.Length; j++)
				{
					Wss02Measurements[i].Ss[j].IsСheckedNow = true;
					Stat = $"Точка {Settings.Checkpoints[i].S}: Снятие значения {j + 1}";
					Wss02Measurements[i].Ss[j].V = Cymometer.Inst.GetCurrHz(Settings.Checkpoints[i], latency, _t);
					Wss02Measurements[i].Ss[j].IsVerified = true;
					Thread.Sleep(50);
					if (_t.Token.IsCancellationRequested)
					{
						return;
					}
				}
				IsBusy = false;
				Wss02Measurements[i].RefS = _avgSref;
			}
		}

		private void Prepare(int cpI)
		{
			Stat = $"Точка {Settings.Checkpoints[cpI].S}";
			_canAdjust = false;
			Tube.Inst.SetF(Settings.Checkpoints[cpI].F, Settings.Checkpoints[cpI].S);
			Thread.Sleep(LAT);// Время ожидания для стабилизации трубы.
			Application.Current.Dispatcher?.Invoke(_avgSrefs.Clear);
			Stat = $"Точка {Settings.Checkpoints[cpI].S}: Корректировка скорости";
			if (Settings.Checkpoints[cpI].S == 30)
			{
				Thread.Sleep(15000);
			}
			else// Для скоростной точки 30 отключаю коррекцию скорости, тк. труба не может разогнаться до 30 м/с. А где-то до 27-29 м/с.
			{
				Tube.Inst.AdjustS(ref _avgSref, Settings.Checkpoints[cpI], _t);
			}
			if (_t.Token.IsCancellationRequested)
			{
				return;
			}
			_canAdjust = true;
			Thread.Sleep(100);
		}

		private void WriteXlsxWss01()
		{
			const string SAMPLE_PATH = @"Resources\Wss1.xlsx";
			while (!File.Exists(SAMPLE_PATH))
			{
				if (1 != (int)MessageBox.Show("Отсутствует файл образец Wss1.xlsx. Пожалуйста, поместите файл и повторите попытку (ОК). Или нажмите \"Отмена\" для пропуска создания .xlsx", "Ошибка", MessageBoxButton.OKCancel))
				{
					return;
				}
			}
			var p = new ExcelPackage(new FileInfo(SAMPLE_PATH));
			var ws = p.Workbook.Worksheets[0];
			const int XLSX_SEED = 14;
			for (int i = 0; i < Wss01Measurements.Count; i++)
			{
				AddToCell(ws.Cells[i + XLSX_SEED, 16], Wss01Measurements[i].RefS);
				for (int j = 0; j < Wss01Measurements[i].Ss.Length; j++)
				{
					AddToCell(ws.Cells[i + XLSX_SEED, j + 17], Wss01Measurements[i].Ss[j].V);
				}
			}
			// Условия поверки.
			ws.Cells[44, 16].Value = Settings.Conditions.Verifier;
			ws.Cells[44, 20].Value = DateTime.Now.ToString("dd.MM.yyyy");
			ws.Cells[25, 5].Value = Settings.Conditions.T;
			ws.Cells[26, 5].Value = Settings.Conditions.H;
			ws.Cells[27, 5].Value = Settings.Conditions.P;
			ws.Cells[16, 6].Value = Settings.Conditions.Snum;
			string path = $"Протокол ДСВ-01 № {Settings.Conditions.Snum} от {DateTime.Now:dd.MM.yyyy}.xlsx";
			string fullPath = Path.Combine(Settings.SavePath, path);
			int attemptSave = 1;
			while (File.Exists(fullPath))
			{
				path = $"Протокол ДСВ-01 № {Settings.Conditions.Snum} от {DateTime.Now:dd.MM.yyyy}({attemptSave}).xlsx";
				fullPath = Path.Combine(Settings.SavePath, path);
				attemptSave++;
			}
			p.SaveAs(new FileInfo(fullPath));
		}

		private void WriteXlsxWss02()
		{
			const string SAMPLE_PATH = @"Resources\Wss2.xlsx";
			while (!File.Exists(SAMPLE_PATH))
			{
				if (1 != (int)MessageBox.Show("Отсутствует файл образец Wss2.xlsx. Пожалуйста, поместите файл и повторите попытку (ОК). Или нажмите отмена для пропуска создания .xlsx", "Ошибка", MessageBoxButton.OKCancel))
				{
					return;
				}
			}
			var p = new ExcelPackage(new FileInfo(SAMPLE_PATH));
			var ws = p.Workbook.Worksheets[0];
			#region Заполнение значений.
			const int XLSX_SEED = 14;
			for (int i = 0; i < Wss02Measurements.Count; i++)
			{
				AddToCell(ws.Cells[i + XLSX_SEED, 12], Wss02Measurements[i].RefS);
				for (int j = 0; j < Wss02Measurements[i].Ss.Length; j++)
				{
					AddToCell(ws.Cells[i + XLSX_SEED, j + 13], Wss02Measurements[i].Ss[j].V);
				}
			}
			// Условия поверки.
			ws.Cells[42, 16].Value = Settings.Conditions.Verifier;
			ws.Cells[42, 20].Value = DateTime.Now.ToString("dd.MM.yyyy");
			ws.Cells[25, 5].Value = Settings.Conditions.T;
			ws.Cells[26, 5].Value = Settings.Conditions.H;
			ws.Cells[27, 5].Value = Settings.Conditions.P;
			ws.Cells[16, 6].Value = Settings.Conditions.Snum;
			//ws.Cells[5, 4].Value = "Протокол ДВС-02 №00212522 от 10.01.2021";
			#endregion
			string path = $"Протокол ДВС-02 № {Settings.Conditions.Snum} от {DateTime.Now:dd.MM.yyyy}.xlsx";
			string fullPath = Path.Combine(Settings.SavePath, path);
			int attemptSave = 1;
			while (File.Exists(fullPath))
			{
				path = $"Протокол ДВС-02 № {Settings.Conditions.Snum} от {DateTime.Now:dd.MM.yyyy}({attemptSave}).xlsx";
				fullPath = Path.Combine(Settings.SavePath, path);
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