using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using VerificationAirVelocitySensor.Models;
using VerificationAirVelocitySensor.ViewModels.Services;

namespace VerificationAirVelocitySensor.ViewModels.Sensors
{
	internal class Wss03Vm(UserSettings us) : BaseVm.BaseVm
	{
		private const int T_OUT_SET_FREQ = 5000;// Время ожидания после установки значения частоты, чтобы дать аэротрубе стабилизировать значение.
		private readonly UserSettings _settings = us;
		private string _status;
		public ObservableCollection<Wss03Sps> SensorValues { get; } = [];
		public string Status
		{
			get => _status;
			set
			{
				_status = value;
				OnPropertyChanged(nameof(Status));
			}
		}

		private static decimal GetSp()
		{
			Cymometer.Instance.Write("M");
			return Convert.ToDecimal(Cymometer.Instance.Read().Split()[2]);
		}

		private bool CheckCond()
		{
			var t = Convert.ToInt32(_settings.MeasurementsData.Temperature);
			var h = Convert.ToInt32(_settings.MeasurementsData.Humidity);
			var p = Convert.ToInt32(_settings.MeasurementsData.Pressure);
			if (t >= 15 && t <= 25 && h >= 30 && h <= 80 && p >= 84 && p <= 106)
			{
				return true;
			}
			Status = "Wrong measurement conditions";
			return false;
		}

		private bool CheckVer()
		{
			Cymometer.Instance.Write("VER", 500);
			Status = Cymometer.Instance.Read();
			var v = Status.Split()[^1].Split('.');
			if ((v[0] == "1" && v[1] == "0") || v[0] == "0")
			{
				Status = $"Wrong {Status}. v1.1.0 or higher required.";
				return false;
			}
			return true;
		}

		private void SetAveraging()
		{
			Cymometer.Instance.Write("INTV 1", 500);
			Status = Cymometer.Instance.Read();
		}

		private void InitSps()// Метод для очистки от старых значений SensorValues с заполнением пустыми значениями. Для WSS-03.
		{
			Application.Current.Dispatcher?.Invoke(() =>
			{
				SensorValues?.Clear();
				for (int i = 1; i < _settings.Checkpoints.Count - 1; i++)
				{
					SensorValues.Add(new Wss03Sps(_settings.Checkpoints[i].Speed));
				}
			});
		}

		private void Prepare(int pntInd, ref bool canAdjust, ref List<decimal> avgSpRefs, ref decimal avgSpRef, ref CancellationTokenSource cts)
		{
			canAdjust = false;
			Status = $"Точка {_settings.Checkpoints[pntInd].Speed}";
			Tube.Instance.SetFreq(_settings.Checkpoints[pntInd].Frequency, _settings.Checkpoints[pntInd].Speed);
			Thread.Sleep(T_OUT_SET_FREQ);// Время ожидания для стабилизации трубы.
			Application.Current.Dispatcher?.Invoke(avgSpRefs.Clear);
			Status = $"Точка {_settings.Checkpoints[pntInd].Speed}: Корректировка скорости";
			if (_settings.Checkpoints[pntInd].Speed == 30)
			{
				Thread.Sleep(15000);
			}
			else// Для скоростной точки 30 отключаю коррекцию скорости, тк. труба не может разогнаться до 30 м/с. А где-то до 27-29 м/с.
			{
				Tube.Instance.AdjustSp(ref avgSpRef, _settings.Checkpoints[pntInd], ref cts);
			}
			if (cts.Token.IsCancellationRequested)
			{
				return;
			}
			canAdjust = true;
			Thread.Sleep(100);
		}

		private void WriteXlsx()
		{
			const string SAMPLE_PATH = @"Resources\Dvs3.xlsx";
			while (!File.Exists(SAMPLE_PATH))
			{
				if (1 != (int)MessageBox.Show("Отсутствует файл-шаблон протокола Dvs3.xlsx. Пожалуйста, поместите файл-шаблон в папку \"Resources\" и повторите попытку (ОК). Или нажмите \"Отмена\" для пропуска создания .xlsx-протокола", "Файл-шаблон не найден", MessageBoxButton.OKCancel))
				{
					return;
				}
			}
			var p = new ExcelPackage(new FileInfo(SAMPLE_PATH));
			var ws = p.Workbook.Worksheets[0];
			#region Заполнение значений.
			const int XLSX_SEED = 14;
			for (int i = 0; i < SensorValues.Count; i++)
			{
				MainWindowVm.AddToCell(ws.Cells[i + XLSX_SEED, 12], SensorValues[i].ReferenceSpeed);
				for (int j = 0; j < SensorValues[i].DeviceSpeeds.Length; j++)
				{
					MainWindowVm.AddToCell(ws.Cells[i + XLSX_SEED, j + 13], SensorValues[i].DeviceSpeeds[j].ResultValue);
				}
			}
			// Условия поверки.
			ws.Cells[42, 16].Value = _settings.MeasurementsData.Verifier;
			ws.Cells[42, 20].Value = DateTime.Now.ToString("dd.MM.yyyy");
			ws.Cells[25, 5].Value = _settings.MeasurementsData.Temperature;
			ws.Cells[26, 5].Value = _settings.MeasurementsData.Humidity;
			ws.Cells[27, 5].Value = _settings.MeasurementsData.Pressure;
			ws.Cells[16, 6].Value = _settings.MeasurementsData.DeviceId;
			//ws.Cells[5, 4].Value = "Протокол ДВС-03 №00212522 от 10.01.2021";
			#endregion
			string path = $"Протокол ДВС-03 № {_settings.MeasurementsData.DeviceId} от {DateTime.Now:dd.MM.yyyy}.xlsx";
			string fullPath = Path.Combine(_settings.SavePath, path);
			int attemptSave = 1;
			while (File.Exists(fullPath))
			{
				path = $"Протокол ДВС-03 № {_settings.MeasurementsData.DeviceId} от {DateTime.Now:dd.MM.yyyy}({attemptSave}).xlsx";
				fullPath = Path.Combine(_settings.SavePath, path);
				attemptSave++;
			}
			p.SaveAs(new FileInfo(fullPath));
		}

		public void Revise(ref bool canAdjust, ref List<decimal> avgSpRefs, ref decimal avgSpRef, ref CancellationTokenSource cts)
		{
            if (!CheckCond() || !CheckVer())
            {
				return;
            }
			SetAveraging();
			InitSps();
			for (int i = 1; i < _settings.Checkpoints.Count - 1; i++)
			{
				Prepare(i, ref canAdjust, ref avgSpRefs, ref avgSpRef, ref cts);
				for (int j = 0; j < SensorValues[i].DeviceSpeeds.Length; j++)
				{
					SensorValues[i - 1].DeviceSpeeds[j].IsСheckedNow = true;
					Status = $"Точка {_settings.Checkpoints[i].Speed}: Снятие значения {j + 1}";
					SensorValues[i - 1].DeviceSpeeds[j].ResultValue = GetSp();
					SensorValues[i - 1].DeviceSpeeds[j].IsVerified = true;
					Thread.Sleep(50);
					if (cts.Token.IsCancellationRequested)
					{
						return;
					}
				}
				OnPropertyChanged("IsBusy");// "IsBusy" take only false.
				SensorValues[i].ReferenceSpeed = avgSpRef;
			}
			WriteXlsx();
		}
	}
}