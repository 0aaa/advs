using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using VerificationAirVelocitySensor.Models;
using VerificationAirVelocitySensor.Models.Classes;
using VerificationAirVelocitySensor.ViewModels.Services;

namespace VerificationAirVelocitySensor.ViewModels.Sensors
{
	internal class Wss03Vm(Settings s) : Base.BaseVm
	{
		private const int LAT = 5000;// Время ожидания после установки значения частоты, чтобы дать аэротрубе стабилизировать значение.
		private readonly Settings _s = s;
		private readonly NumberFormatInfo _f = new() { NumberDecimalSeparator = "." };
        //private readonly Checkpoint[] _cs = [// To confirm with MD.
        //    new Checkpoint { Id = 1, S = .7m, F = 445, MaxStep = 10, Min = 0m, Max = 3.007m }
        //    , new Checkpoint { Id = 2, S = 2.5m, F = 2605, MaxStep = 20, Min = 3.320m, Max = 8.837m }
        //    , new Checkpoint { Id = 3, S = 4.5m, F = 5650, MaxStep = 20, Min = 9.634m, Max = 15.595m }
        //    , new Checkpoint { Id = 4, S = 10m, F = 7750, MaxStep = 20, Min = 15.935m, Max = 22.366m }
        //    , new Checkpoint { Id = 5, S = 20m, F = 10600, MaxStep = 30, Min = 22.248m, Max = 29.124m }
        //    , new Checkpoint { Id = 7, S = 30m, F = 16384, MaxStep = 30, Min = 32.340m, Max = 39.948m }
        //];
        private readonly Checkpoint[] _cs = [s.Checkpoints[0], s.Checkpoints[1]];// Debug.
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

		private decimal GetS()
		{
			Cymometer.Inst.Write("M");
			try
			{
				var m = Cymometer.Inst.Read().Split()[5];
				return Convert.ToDecimal(m, _f);
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

		private void SetAveraging()
		{
			Cymometer.Inst.Write("INTV 1", 500);
			Stat = Cymometer.Inst.Read();
		}

		private void InitSps()// Метод для очистки от старых значений SensorValues с заполнением пустыми значениями. Для WSS-03.
		{
			Application.Current.Dispatcher?.Invoke(() =>
			{
				Measurements?.Clear();
				for (int i = 0; i < _cs.Length; i++)
				{
					Measurements.Add(new Wss03Measur(_cs[i].S));
				}
			});
		}

		private void Prepare(int cpI, ref bool canAdjust, List<decimal> avgSrefs, ref decimal avgSref, CancellationTokenSource t)
		{
			canAdjust = false;
			Stat = $"Точка {_cs[cpI].S}: Задача частоты {_cs[cpI].F}";
			Tube.Inst.SetF(_cs[cpI].F, _cs[cpI].S);
			Thread.Sleep(LAT);// Время ожидания для стабилизации трубы.
			Application.Current.Dispatcher?.Invoke(avgSrefs.Clear);
			if (_cs[cpI].S == 30)
			{
				Stat = $"Точка {_cs[cpI].S}: Корректировка невозможна в данной точке";
				Thread.Sleep(15000);
			}
			else// Для скоростной точки 30 отключаю коррекцию скорости, тк. труба не может разогнаться до 30 м/с. А где-то до 27 - 29 м/с.
			{
				Stat = $"Точка {_cs[cpI].S}: Корректировка скорости";
				Tube.Inst.AdjustS(ref avgSref, _cs[cpI], t);
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
			const string SAMPLE_PATH = @"Resources\Wss3.xlsx";
			while (!File.Exists(SAMPLE_PATH))
			{
				if (1 != (int)MessageBox.Show("Отсутствует файл-шаблон протокола Wss3.xlsx. Пожалуйста, поместите файл-шаблон в папку \"Resources\" и повторите попытку (ОК). Или нажмите \"Отмена\" для пропуска создания .xlsx-протокола", "Файл-шаблон не найден", MessageBoxButton.OKCancel))
				{
					return;
				}
			}
			var p = new ExcelPackage(new FileInfo(SAMPLE_PATH));
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
			p.SaveAs(new FileInfo(fullPath));
		}

		public void Revise(ref bool canAdjust, List<decimal> avgSrefs, ref decimal avgSref, CancellationTokenSource t)
		{
            if (!CheckCond() || !CheckVer())
            {
				return;
            }
			SetAveraging();
			InitSps();
			for (int i = 0; i < _cs.Length; i++)
			{
				Prepare(i, ref canAdjust, avgSrefs, ref avgSref, t);
				Measurements[i].Ss[0].IsСheckedNow = true;
				Stat = $"Точка {_cs[i].S}: Снятие значения";
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
	}
}