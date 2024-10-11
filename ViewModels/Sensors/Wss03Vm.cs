using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using ADVS.Models;
using ADVS.ViewModels.Services;
using System.Linq;
using ADVS.Models.Evaluations;
using ADVS.ViewModels.Base;

namespace ADVS.ViewModels.Sensors
{
    internal partial class Wss03Vm(Settings s) : BaseVm
	{
		private const int LAT = 5000;// Время ожидания после установки значения частоты, чтобы дать аэротрубе стабилизировать значение.
		private readonly Settings _s = s;
		private string _stat;
		public ObservableCollection<Wss03Eval> Evals { get; } = [];
		public string Stat
		{
			get => _stat;
			set
			{
				_stat = value;
				OnPropertyChanged(nameof(Stat));
			}
		}

		private static decimal GetS()
		{
			Cymometer.Inst.Write("M");
			try
			{
				var m = Cymometer.Inst.Read().Split()[5];
				return Convert.ToDecimal(m, System.Globalization.CultureInfo.InvariantCulture);
			}
			catch (TimeoutException)
			{
				Cymometer.Inst.Write("OPEN 1");
				Cymometer.Inst.Read();
				return GetS();
			}
        }

		private bool CheckCond()
		{
			var t = Convert.ToInt32(_s.Conditions.T);
			var h = Convert.ToInt32(_s.Conditions.H);
			var p = Convert.ToInt32(_s.Conditions.P);
			if (t >= 15 && t <= 25 && h >= 30 && h <= 80 && p >= 84 && p <= 106)
			{
				return true;
			}
			MessageBox.Show("Wrong measurement conditions", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			return false;
		}

		private bool CheckVer()
		{
			Cymometer.Inst.Write("VER", 500);
			Stat = Cymometer.Inst.Read().Replace("\r\n", "");
			var v = Stat.Split()[^1].TrimStart('V').Split('.');
			if ((v[0] == "1" && v[1] == "0") || v[0] == "0")
			{
				MessageBox.Show($"Wrong {Stat}. v1.1.0 or higher required.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}
			return true;
		}

		private bool CheckSnum()
		{
			Cymometer.Inst.Write("SNUM");
			Stat = Cymometer.Inst.Read().Replace("\r\n", "");
			var n = Stat.Split()[^1];
			if (IsSnum(n))
			{
				_s.Conditions.Snum = n;
				return true;
			}
			MessageBox.Show($"Wrong serial number {Stat}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			return false;
		}

        private static bool IsSnum(string n)
            => n.Length == 10 && n[0] == '0' && n[1] == '0' && n[2] == '9' && n[3] == '8' && n.All(char.IsDigit)
                && (n[4] - 48) * 10 + (n[5] - 48) <= DateTime.Now.Year - 2000;

		private void SetAveraging()
		{
			Cymometer.Inst.Write("INTV 1", 500);
			Stat = Cymometer.Inst.Read();
		}

		private void InitSs()// Метод для очистки от старых значений SensorValues с заполнением пустыми значениями. Для WSS-03.
		{
			Application.Current.Dispatcher?.Invoke(() =>
			{
				var ss = new decimal[] { .7m, 2.5m, 4.9m, 10, 20, 30 };
				Checkpoint c;
				Evals?.Clear();
				for (int i = 0; i < _s.Checkpoints.Count; i++)
				{
					c = _s.Checkpoints.ElementAtOrDefault(i);
                    if (ss.Contains(c.S))
                    {
						Evals.Add(new Wss03Eval(c.S, c.F));
                    }
				}
			});
		}

		private void Prepare(int i, ref bool acceptRef, List<decimal> avgRefs, ref decimal avgRef, CancellationTokenSource t)
		{
			acceptRef = false;
			Stat = $"Точка {Evals[i].S}: Задача частоты {Evals[i].F}";
			Tube.Inst.SetF(Evals[i].F, Evals[i].S);
			Thread.Sleep(LAT);// Время ожидания для стабилизации трубы.
			Application.Current.Dispatcher?.Invoke(avgRefs.Clear);
			if (Evals[i].S == 30)
			{
				Stat = $"Точка {Evals[i].S}: Корректировка невозможна в данной точке";
				Thread.Sleep(15000);
			}
			else// Для скоростной точки 30 отключаю коррекцию скорости, тк. труба не может разогнаться до 30 м/с. А где-то до 27 - 29 м/с.
			{
				Stat = $"Точка {Evals[i].S}: Корректировка скорости";
				Tube.Inst.AdjustS(ref avgRef, _s.Checkpoints.FirstOrDefault(c => c.S == Evals[i].S), t);
			}
			if (t.Token.IsCancellationRequested)
			{
				return;
			}
			acceptRef = true;
			Thread.Sleep(100);
		}

		private void WriteXlsx()
		{
			const string SAMPLE_PATH = "Resources\\Wss3.xlsx";
            while (!File.Exists(SAMPLE_PATH))
			{
				if (1 != (int)MessageBox.Show("Отсутствует файл-шаблон протокола Wss3.xlsx. Пожалуйста, поместите файл-шаблон в папку \"Resources\" и повторите попытку (ОК). Или нажмите \"Отмена\" для пропуска создания .xlsx-протокола", "Файл-шаблон не найден", MessageBoxButton.OKCancel))
				{
					return;
				}
			}
			var p = new ExcelPackage(new FileInfo(SAMPLE_PATH));
			ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
			var ws = p.Workbook.Worksheets[0];
			#region Заполнение значений.
			const int XLSX_SEED = 18;
			for (int i = 0; i < Evals.Count; i++)
			{
				MainWindowVm.AddToCell(ws.Cells[i + XLSX_SEED, 15], Evals[i].Ref);
				for (int j = 0; j < Evals[i].Ss.Length; j++)
				{
					MainWindowVm.AddToCell(ws.Cells[i + XLSX_SEED, j + 16], Evals[i].Ss[j].V);
				}
			}
			ws.Cells[7, 8].Value = DateTime.Now.ToLongDateString();
			ws.Cells[42, 20].Value = DateTime.Now.ToString("dd.MM.yyyy");
			ws.Cells[45, 19].Value = DateTime.Now.ToLongDateString();
			ws.Cells[25, 5].Value = _s.Conditions.T;
			ws.Cells[26, 5].Value = _s.Conditions.H;
			ws.Cells[27, 5].Value = _s.Conditions.P;
			ws.Cells[16, 6].Value = _s.Conditions.Snum;
			ws.Cells[16, 10].Value = (_s.Conditions.Snum[4] - 48) * 10 + _s.Conditions.Snum[5] - 48 + 2000;
			ws.Cells[37, 19].Value = _s.Conditions.Snum;
			//ws.Cells[5, 4].Value = "Протокол ДВС-03 №00212522 от 10.01.2021";
			#endregion
			string path = $"Протокол ДВС-03 № {_s.Conditions.Snum} от {DateTime.Now:dd.MM.yyyy}.xlsx";
			string fullP = Path.Combine(_s.Path, path);
			int att = 1;
			while (File.Exists(fullP))
			{
				path = $"Протокол ДВС-03 № {_s.Conditions.Snum} от {DateTime.Now:dd.MM.yyyy}({att}).xlsx";
				fullP = Path.Combine(_s.Path, path);
				att++;
			}
			p.SaveAs(fullP);
		}

		public void Revise(ref bool acceptRef, List<decimal> avgRefs, ref decimal avgRef, CancellationTokenSource t)
		{
            if (!CheckCond() || !CheckVer() || !CheckSnum())
            {
				return;
            }
			SetAveraging();
			InitSs();
			for (int i = 0; i < Evals.Count; i++)
			{
				Prepare(i, ref acceptRef, avgRefs, ref avgRef, t);
				Evals[i].Ss[0].IsСheckedNow = true;
				Stat = $"Точка {Evals[i].S}: Снятие значения";
				Thread.Sleep(60000);// 1 min. interval for WSS-03.
				Evals[i].Ss[0].V = GetS();
				Evals[i].Ss[0].IsVerified = true;
				if (t.Token.IsCancellationRequested)
				{
					return;
				}
				OnPropertyChanged("IsBusy");// "IsBusy" take only false.
				Evals[i].Ref = avgRef;
			}
			WriteXlsx();
		}
    }
}