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
using ADVS.Models;
using ADVS.Views;
using ADVS.ViewModels.Base;
using ADVS.ViewModels.Sensors;
using ADVS.ViewModels.Services;
using ADVS.Models.Evaluations;
using ADVS.Models.Events;
using System.ComponentModel;

namespace ADVS.ViewModels
{
    internal partial class MainWindowVm : BaseVm
	{
		private const int LAT = 5000;// Время ожидания после установки значения частоты, чтобы дать аэротрубе стабилизировать значение.
		private readonly List<decimal> _avgRefs;
		private readonly Wss03Vm _wss03Vm;
		private CancellationTokenSource _t;
		// Все свойства что ниже, должны сохранятся при перезапуске.
		private UserControl _frame;
		private decimal _ref;// Эталонное значение скорости с частотной трубы.
		private decimal _avgRef;
		private bool _acceptRef;// Флаг для включения в коллекцию среднего значения эталона, всех его значений за время теста скоростной точки после прохождения корректировки.
		#region Properties.
		public ObservableCollection<Wss01Eval> Wss01Evals { get; }
		public ObservableCollection<Wss02Eval> Wss02Evals { get; }
		public RelayCommand[] RevisionRcs { get; }// Start, Stop, SaveSpeeds, VisibilitySetFrequency, Reset, ClosePortFrequencyCounter, ClosePortFrequencyMotor.
		public RelayCommand[] PaginationRcs { get; }
		public string[] Sensors { get; }
		public Settings Settings { get; }
		public string Title { get; }
		public bool IsBusy => !string.IsNullOrEmpty(BusyContent);
		public bool[] WssVisibility { get; private set; }
		public UserControl Frame
		{
			get => _frame;
			private set
			{
				_frame = value;
				Settings.Serialize();
			}
		}
		public string Stat { get; private set; }// Свойство для биндинга на UI текущего действия внутри app.
		public string BusyContent { get; private set; }
		public decimal Ref
		{
			get => _ref;
			private set
			{
				_ref = value;
				OnPropertyChanged(nameof(Ref));
			}
		}
		public bool IsReady { get; private set; }
		#endregion
		public Wss03 Wss03 { get; }

		public MainWindowVm()
		{
			_avgRefs = [];
            Tube.Inst.RefUpd += HandleRef;
			Sensors = ["ДСВ-01", "ДВС-02"];
			WssVisibility = new bool[Sensors.Length + 1];// + 1 for WSS-03.
			Settings = Settings.Deserialize();
            Settings.PropertyChanged += HandleSettings;
			_wss03Vm = new Wss03Vm(Settings);
            _wss03Vm.PropertyChanged += HandleStat;
			WssVisibility[Array.IndexOf(Sensors, Settings.M) >= 0 ? Array.IndexOf(Sensors, Settings.M) : ^1] = true;
			Title = $"{Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title}  v{Assembly.GetExecutingAssembly().GetName().Version}";
			Wss01Evals = [];
			Wss02Evals = [];
			Wss03 = new Wss03(_wss03Vm);
			RevisionRcs = [
				 new(Start, () => IsReady)
				, new(() => {
					BusyContent = "Тестирование прервано пользователем. Ожидание завершения процесса";
					IsReady = true;
					_t.Cancel();
				}, () => !IsReady)// Stop.
				, new(() => Settings.Serialize())
				#region Частотомер ЧЗ-85/6.
				, new RelayCommand(Cymometer.Inst.Reset, Cymometer.Inst.IsOpen)
				, new RelayCommand(Cymometer.Inst.Close, Cymometer.Inst.IsOpen)
				#endregion
				#region Анемометр / Частотный двигатель.
				, new RelayCommand(Tube.Inst.Close, Tube.Inst.IsOpen)
				#endregion
			];
            PaginationRcs = [
				new(() => Frame = null)
				, new(() => Frame = new SettingsView(new DeviceSettingsVm(Settings.Devices)), () => IsReady)
				, new(() => Frame = new CheckpointsView(Settings.Checkpoints, RevisionRcs[2]), () => IsReady)
			];
			IsReady = true;
		}

        private void HandleSettings(object _, PropertyChangedEventArgs e)
        {
            Settings.Serialize();
			SetVisibility();
			OnPropertyChanged(nameof(WssVisibility));
        }

		private void SetVisibility()
		{
            for (int i = 0; i < WssVisibility.Length; i++)
            {
                WssVisibility[i] = false;
            }
			WssVisibility[Array.IndexOf(Sensors, Settings.M) >= 0 ? Array.IndexOf(Sensors, Settings.M) : ^1] = true;
		}

        private void HandleStat(object _, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Stat":
                    Stat = _wss03Vm.Stat;
                    break;
                case "IsBusy":
					BusyContent = "";// "IsBusy" take only false.
                    break;
            }
        }

        private void HandleRef(object _, RefUpd e)
        {
            Ref = (decimal)e.Ref;
            if (_avgRefs.Count > 5 && !_acceptRef)
            {
                _avgRefs.RemoveAt(0);
            }
            _avgRefs.Add(Ref);
            _avgRef = Math.Round(_avgRefs.Average(), 2);
        }

        #region Частотомер ЧЗ-85/6.
        private bool OpenCymometer()
		{
			if (!Cymometer.Inst.Open(Settings.Devices.Cymometer, (int)Settings.Devices.Sec * 1000))
			{
				return false;
			}
			switch (Cymometer.Inst.GetModel())
			{
				case "counter":
					Cymometer.Inst.SetUp(Settings.Devices.Sec);
					break;
				case "WSS":
					Settings.M = "WSS-03";
					SetVisibility();
					break;
				default:
					Cymometer.Inst.Close();
					MessageBox.Show($"{Settings.Devices.Cymometer} не является ни частотомером, ни ДВС-03.", "Нераспознанное устройство", MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
			}
			return true;
		}
		#endregion

		#region Revision methods.
		private async void Start()
		{
			IsReady = false;
			if (!OpenConditions())
			{
				IsReady = true;
				return;
			}
			await Task.Run(() =>
			{
				BusyContent = "Проверка подключённых устройств и их настройка";
				if (!OpenCymometer() || !Tube.Inst.Open(Settings.Devices.Tube))
				{
					Stop();
					return;
				}
				BusyContent = "";
				_t = new CancellationTokenSource();
				Cymometer.Inst.Stop = EmergencyStop;
				try
				{
					Stat = "Запуск тестирования";
					switch (Settings.M)
					{
						case "ДСВ-01":
							Init01();
							Start01();
							break;
						case "ДВС-02":
							Init02();
							Start02();
							break;
						case "WSS-03":
							_wss03Vm.Revise(ref _acceptRef, _avgRefs, ref _avgRef, _t);
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
					Stat = "Поверка завершена";
				}
				catch (Exception e)
				{
					MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				finally
				{
					Tube.Inst.SetF(0, 0);
					switch (Settings.M)
					{
						case "ДСВ-01":
							WriteXlsx01();
							break;
						case "ДВС-02":
							WriteXlsx02();
							break;
					}
					Stop();
					MessageBox.Show("Поверка завершена", "Завершено", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
			});
		}

		private void Stop()
		{
			IsReady = true;
			BusyContent = "";
			Cymometer.Inst.Close();
			Tube.Inst.Close();
		}

		private void EmergencyStop()
		{
			BusyContent = "Аварийная остановка";
			IsReady = true;
			_t.Cancel();
			MessageBox.Show("Произошло аварийное завершение поверки", "Внимание", MessageBoxButton.OK, MessageBoxImage.Error);
		}

		private bool OpenConditions()
		{
			var c = new Views.Conditions(new ConditionsVm(Settings));
			c.ShowDialog();
			bool isContinue = c.ViewModel.IsContinue;
			c.Close();
			if (!isContinue)
			{
				return false;
			}
			Settings.Serialize();
			return Settings.CheckCond();
		}

		private void Init01()
		{
			Application.Current.Dispatcher?.Invoke(() =>
			{
				var ss = new decimal[] { 5, 10, 15, 20, 25 };
				Wss01Evals?.Clear();
                for (int i = 0; i < ss.Length; i++)
                {
					Wss01Evals.Add(new Wss01Eval(ss[i]));
                }
            });
		}

		private void Init02()
		{
			Application.Current.Dispatcher?.Invoke(() =>
			{
				var ss = new decimal[] { .7m, 5, 10, 15, 20, 25, 30 };
				Wss02Evals?.Clear();
                for (int i = 0; i < ss.Length; i++)
                {
					Wss02Evals.Add(new Wss02Eval(ss[i]));
                }
            });
		}

		private void Start01()
		{
			int lat = (int)Settings.Devices.Sec * 1000;
			Checkpoint c;
			decimal? avg;
			for (int i = 0; i < Wss01Evals[0].Ss.Length; i++)// Beware. Only 1st Checkpoint taken. Speeds quantity for each Checkpoint can be different.
			{
				for (int j = 0; j < Wss01Evals.Count; j++)
				{
					Wss01Evals[j].Ss[i].IsСheckedNow = true;
					c = Settings.Checkpoints.FirstOrDefault(cp => cp.S == Wss01Evals[j].S);
					Prepare(c);
					Stat = $"Точка {c.S}: Снятие значения {i + 1}";
					Wss01Evals[j].Ss[i].V = Cymometer.Inst.GetHz(c, i > 0 ? lat : 7000, _t);// Время запроса для точки 0.7 больше из-за маленькой скорости прокрутки датчика.
					Wss01Evals[j].Ss[i].IsVerified = true;
					Thread.Sleep(50);
					if (_t.Token.IsCancellationRequested)
					{
						return;
					}
					Wss01Evals[j].Refs[i] = _avgRef;
				}
			}
			for (int i = 0; i < Wss01Evals.Count; i++)
			{
				avg = Wss01Evals[i].Refs.Average();
				if (avg != null)
				{
					Wss01Evals[i].Ref = Math.Round((decimal)avg, 2);
				}
			}
		}

		private void Start02()
		{
            int lat = (int)Settings.Devices.Sec * 1000;
			Checkpoint c;
			for (int i = 0; i < Wss02Evals.Count; i++)
			{
				c = Settings.Checkpoints.FirstOrDefault(cp => cp.S == Wss02Evals[i].S);
				Prepare(c);
				if (c.S == .7m)// На данной скорости, датчик вращается очень медленно. А значение поступает на частотомер в момент полного оборота датчика.
				{
					lat = 5000;
				}
				for (int j = 0; j < Wss02Evals[i].Ss.Length; j++)
				{
					Wss02Evals[i].Ss[j].IsСheckedNow = true;
					Stat = $"Точка {c.S}: Снятие значения {j + 1}";
					Wss02Evals[i].Ss[j].V = Cymometer.Inst.GetHz(c, lat, _t);
					Wss02Evals[i].Ss[j].IsVerified = true;
					Thread.Sleep(50);
					if (_t.Token.IsCancellationRequested)
					{
						return;
					}
				}
				BusyContent = "";
				Wss02Evals[i].Ref = _avgRef;
			}
		}

		private void Prepare(Checkpoint c)
		{
			Stat = $"Точка {c.S}";
			_acceptRef = false;
			Tube.Inst.SetF(c.F, c.S);
			Thread.Sleep(LAT);
			Application.Current.Dispatcher?.Invoke(_avgRefs.Clear);
			Stat = $"Точка {c.S}: Корректировка скорости";
			if (c.S == 30)
			{
				Thread.Sleep(15000);
			}
			else// Для скоростной точки 30 отключаю коррекцию скорости, т.к. труба не может разогнаться до 30 м/с. А где-то до 27-29 м/с.
			{
				Tube.Inst.AdjustS(ref _avgRef, c, _t);
			}
			if (_t.Token.IsCancellationRequested)
			{
				return;
			}
			_acceptRef = true;
			Thread.Sleep(100);
		}

		private void WriteXlsx01()
		{
			const string SAMPLE_PATH = @"Resources\Wss1.xlsx";
			while (!File.Exists(SAMPLE_PATH))
			{
				if (1 != (int)MessageBox.Show("Отсутствует файл образец Wss1.xlsx. Пожалуйста, поместите файл и повторите попытку (ОК). Или нажмите \"Отмена\" для пропуска создания .xlsx", "Ошибка", MessageBoxButton.OKCancel))
				{
					return;
				}
			}
			var ep = new ExcelPackage(new FileInfo(SAMPLE_PATH));
			ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
			var ws = ep.Workbook.Worksheets[0];
			const int XLSX_SEED = 14;
			for (int i = 0; i < Wss01Evals.Count; i++)
			{
				AddToCell(ws.Cells[i + XLSX_SEED, 16], Wss01Evals[i].Ref);
				for (int j = 0; j < Wss01Evals[i].Ss.Length; j++)
				{
					AddToCell(ws.Cells[i + XLSX_SEED, j + 17], Wss01Evals[i].Ss[j].V);
				}
			}
			ws.Cells[7, 8].Value = DateTime.Now.ToLongDateString();
			ws.Cells[49, 7].Value = DateTime.Now.ToLongDateString();
			ws.Cells[44, 20].Value = DateTime.Now.ToString("dd.MM.yyyy");
			ws.Cells[47, 19].Value = DateTime.Now.ToLongDateString();
			ws.Cells[25, 5].Value = Settings.Conditions.T;
			ws.Cells[26, 5].Value = Settings.Conditions.H;
			ws.Cells[27, 5].Value = Settings.Conditions.P;
			ws.Cells[16, 6].Value = Settings.Conditions.Snum;
			ws.Cells[16, 10].Value = (Settings.Conditions.Snum[4] - 48) * 10 + (Settings.Conditions.Snum[5] - 48) + 2000;
			string p = $"{Settings.Path}\\Протокол ДСВ-01 № {Settings.Conditions.Snum} от {DateTime.Now:dd.MM.yyyy}.xlsx";
			int att = 0;
			while (File.Exists(p))
			{
				p = $"{Settings.Path}\\Протокол ДСВ-01 № {Settings.Conditions.Snum} от {DateTime.Now:dd.MM.yyyy}({att++}).xlsx";
			}
			ep.SaveAs(p);
		}

		private void WriteXlsx02()
		{
			const string SAMPLE_PATH = @"Resources\Wss2.xlsx";
			while (!File.Exists(SAMPLE_PATH))
			{
				if (1 != (int)MessageBox.Show("Отсутствует файл образец Wss2.xlsx. Пожалуйста, поместите файл и повторите попытку (ОК). Или нажмите отмена для пропуска создания .xlsx", "Ошибка", MessageBoxButton.OKCancel))
				{
					return;
				}
			}
			var ep = new ExcelPackage(new FileInfo(SAMPLE_PATH));
			ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
			var ws = ep.Workbook.Worksheets[0];
			#region Заполнение значений.
			const int XLSX_SEED = 14;
			for (int i = 0; i < Wss02Evals.Count; i++)
			{
				AddToCell(ws.Cells[i + XLSX_SEED, 12], Wss02Evals[i].Ref);
				for (int j = 0; j < Wss02Evals[i].Ss.Length; j++)
				{
					AddToCell(ws.Cells[i + XLSX_SEED, j + 13], Wss02Evals[i].Ss[j].V);
				}
			}
			ws.Cells[7, 8].Value = DateTime.Now.ToLongDateString();
			ws.Cells[42, 20].Value = DateTime.Now.ToString("dd.MM.yyyy");
			ws.Cells[45, 19].Value = DateTime.Now.ToLongDateString();
			ws.Cells[25, 5].Value = Settings.Conditions.T;
			ws.Cells[26, 5].Value = Settings.Conditions.H;
			ws.Cells[27, 5].Value = Settings.Conditions.P;
			ws.Cells[16, 6].Value = Settings.Conditions.Snum;
			ws.Cells[16, 10].Value = (Settings.Conditions.Snum[4] - 48) * 10 + (Settings.Conditions.Snum[5] - 48) + 2000;
			#endregion
			string p = $"{Settings.Path}\\Протокол ДВС-02 № {Settings.Conditions.Snum} от {DateTime.Now:dd.MM.yyyy}.xlsx";
			int att = 0;
			while (File.Exists(p))
			{
				p = $"{Settings.Path}\\Протокол ДВС-02 № {Settings.Conditions.Snum} от {DateTime.Now:dd.MM.yyyy}({att++}).xlsx";
			}
			ep.SaveAs(p);
		}

		public static void AddToCell(ExcelRange er, decimal? v)
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