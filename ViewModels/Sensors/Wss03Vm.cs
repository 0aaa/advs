using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using ADVS.Models;
using ADVS.Models.Classes;
using ADVS.ViewModels.Services;
using System.Linq;

namespace ADVS.ViewModels.Sensors
{
	internal class Wss03Vm(Settings s) : Base.BaseVm
	{
		private const int LAT = 5000;// Время ожидания после установки значения частоты, чтобы дать аэротрубе стабилизировать значение.
		private readonly Settings _s = s;
		private string _stat;
		public ObservableCollection<Wss03Measur> Measurements { get; } = [];
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
			if (t >= 15 && t <= 25 && h >= 30 && h <= 80 && p >= 84 && p <= 106 && IsSnum(_s.Conditions.Snum))
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

		private void SetAveraging()
		{
			Cymometer.Inst.Write("INTV 1", 500);
			Stat = Cymometer.Inst.Read();
		}

		private void InitSs()// Метод для очистки от старых значений SensorValues с заполнением пустыми значениями. Для WSS-03.
		{
			Application.Current.Dispatcher?.Invoke(() =>
			{
				//var ss = new decimal[] { .7m, 2.5m, 4.8m, 10, 20, 30 };
				var ss = new decimal[] { .7m, 2.5m, 4.8m, 10, 20, 30 };// Debug.
				Checkpoint currC;
				Measurements?.Clear();
				for (int i = 0; i < _s.Checkpoints.Count; i++)
				{
					currC = _s.Checkpoints.ElementAtOrDefault(i);
                    if (ss.Contains(currC.S))
                    {
						Measurements.Add(new Wss03Measur(currC.S, currC.F));
                    }
				}
			});
		}

		private void Prepare(int cI, ref bool canAdjust, List<decimal> avgSrefs, ref decimal avgSref, CancellationTokenSource t)
		{
			canAdjust = false;
			Stat = $"Точка {Measurements[cI].S}: Задача частоты {Measurements[cI].F}";
			Tube.Inst.SetF(Measurements[cI].F, Measurements[cI].S);
			Thread.Sleep(LAT);// Время ожидания для стабилизации трубы.
			Application.Current.Dispatcher?.Invoke(avgSrefs.Clear);
			if (Measurements[cI].S == 30)
			{
				Stat = $"Точка {Measurements[cI].S}: Корректировка невозможна в данной точке";
				Thread.Sleep(15000);
			}
			else// Для скоростной точки 30 отключаю коррекцию скорости, тк. труба не может разогнаться до 30 м/с. А где-то до 27 - 29 м/с.
			{
				Stat = $"Точка {Measurements[cI].S}: Корректировка скорости";
				Tube.Inst.AdjustS(ref avgSref, _s.Checkpoints.FirstOrDefault(c => c.S == Measurements[cI].S), t);
			}
			if (t.Token.IsCancellationRequested)
			{
				return;
			}
			canAdjust = true;
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
			for (int i = 0; i < Measurements.Count; i++)
			{
				MainWindowVm.AddToCell(ws.Cells[i + XLSX_SEED, 15], Measurements[i].RefS);
				for (int j = 0; j < Measurements[i].Ss.Length; j++)
				{
					MainWindowVm.AddToCell(ws.Cells[i + XLSX_SEED, j + 16], Measurements[i].Ss[j].V);
				}
			}
			// Условия поверки.
			ws.Cells[42, 16].Value = _s.Conditions.Verifier;
			ws.Cells[42, 20].Value = DateTime.Now.ToString("dd.MM.yyyy");
			ws.Cells[16, 10].Value = (_s.Conditions.Snum[4] - 48) * 10 + _s.Conditions.Snum[5] - 48 + 2000;
			ws.Cells[25, 5].Value = _s.Conditions.T;
			ws.Cells[26, 5].Value = _s.Conditions.H;
			ws.Cells[27, 5].Value = _s.Conditions.P;
			ws.Cells[16, 6].Value = _s.Conditions.Snum;
			ws.Cells[37, 19].Value = _s.Conditions.Snum;
			//ws.Cells[5, 4].Value = "Протокол ДВС-03 №00212522 от 10.01.2021";
			#endregion
			string path = $"Протокол ДВС-03 № {_s.Conditions.Snum} от {DateTime.Now:dd.MM.yyyy}.xlsx";
			string fullPath = Path.Combine(_s.SavePath, path);
			int attempt = 1;
			while (File.Exists(fullPath))
			{
				path = $"Протокол ДВС-03 № {_s.Conditions.Snum} от {DateTime.Now:dd.MM.yyyy}({attempt}).xlsx";
				fullPath = Path.Combine(_s.SavePath, path);
				attempt++;
			}
			p.SaveAs(fullPath);
		}

		public void Revise(ref bool canAdjust, List<decimal> avgSrefs, ref decimal avgSref, CancellationTokenSource t)
		{
            if (!CheckCond() || !CheckVer())
            {
				return;
            }
			SetAveraging();
			InitSs();
			for (int i = 0; i < Measurements.Count; i++)
			{
				Prepare(i, ref canAdjust, avgSrefs, ref avgSref, t);
				Measurements[i].Ss[0].IsСheckedNow = true;
				Stat = $"Точка {Measurements[i].S}: Снятие значения";
				Thread.Sleep(60000);// 1 min. interval for WSS-03.
				Measurements[i].Ss[0].V = GetS();
				Measurements[i].Ss[0].IsVerified = true;
				if (t.Token.IsCancellationRequested)
				{
					return;
				}
				OnPropertyChanged("IsBusy");// "IsBusy" take only false.
				Measurements[i].RefS = avgSref;
			}
			WriteXlsx();
		}

        public static bool IsSnum(string str)
            => str.Length == 10 && str[0] == '0' && str[1] == '0' && str[2] == '9' && str[3] == '8' && str.All(char.IsDigit)
                && (str[4] - 48) * 10 + (str[5] - 48) <= DateTime.Now.Year - 2000;
    }
}