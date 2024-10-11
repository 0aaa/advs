using ADVS.Models;
using ADVS.Models.Evaluations;
using ADVS.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace ADVS.ViewModels.Services
{
    internal partial class Settings : BaseVm
	{
		private const string FILENAME = "UserSettings.txt";
		private string _m;
		public ObservableCollection<Checkpoint> Checkpoints { get; private set; }
		public Devices Devices { get; private set; }
		public Conditions Conditions { get; private set; }
		public string M
		{
			get => _m;
			set
			{
				_m = value;
				OnPropertyChanged(nameof(M));
			}
		}
		public string Path { get; set; }

		public Settings()
		{
			Devices = new Devices();
			Conditions = new Conditions();
			if (Checkpoints == null || Checkpoints.Count == 0)// Значения скорости, на которых нужно считать значения датчика.
			{
				Checkpoints = [
					new Checkpoint { Id = 1, S = 0.7m, F = 500, Step = 10, Min = 0m, Max = 3.007m }
					, new Checkpoint { Id = 2, S = 2.5m, F = 1383, Step = 10, Min = -99, Max = 99 }
					, new Checkpoint { Id = 3, S = 4.9m, F = 2654, Step = 20, Min = -99, Max = 99 }
					, new Checkpoint { Id = 4, S = 5m, F = 2765, Step = 50, Min = 3.320m, Max = 8.837m }
					, new Checkpoint { Id = 5, S = 10m, F = 5390, Step = 50, Min = 9.634m, Max = 15.595m }
					, new Checkpoint { Id = 6, S = 15m, F = 8130, Step = 50, Min = 15.935m, Max = 22.366m }
					, new Checkpoint { Id = 7, S = 20m, F = 10810, Step = 80, Min = 22.248m, Max = 29.124m }
					, new Checkpoint { Id = 8, S = 25m, F = 13570, Step = 90, Min = 28.549m, Max = 35.895m }
					, new Checkpoint { Id = 9, S = 30m, F = 16384, Step = 100, Min = 32.340m, Max = 39.948m }
				];
			}
			Checkpoints.CollectionChanged += (_, _) =>
			{
				for (int i = 0; i < Checkpoints.Count; i++)
				{
					Checkpoints[i].Id = i + 1;
				}
			};
		}

		#region Deserialization/serialization.
		public void Serialize()
		{
			try
			{
				using var f = File.Open(FILENAME, FileMode.Create);
				using var sw = new StreamWriter(f);
				new Serializer().Serialize(sw, this);
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		public static Settings Deserialize()
		{
			using var f = File.Open(FILENAME, FileMode.OpenOrCreate, FileAccess.Read);
			using var sr = new StreamReader(f);
			try
			{
				return new Deserializer().Deserialize<Settings>(sr) ?? new Settings();
			}
			catch (YamlException)
			{
				MessageBox.Show($"Broken settings file. Please remove file {FILENAME} from the app directories.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return new Settings();
			}
		}
		#endregion

		public bool CheckCond()
		{
			var t = Convert.ToInt32(Conditions.T);
			var h = Convert.ToInt32(Conditions.H);
			var p = Convert.ToInt32(Conditions.P);
			var n = Conditions.Snum;
			var r = false;
			switch (_m)
			{
				case "ДСВ-01":
					r = t >= 15 && t <= 25 && h >= 20 && h <= 90 && p >= 95 && p <= 105;
					break;
				case "ДВС-02":
					r = t >= 5 && t <= 35 && h <= 75;
					break;
				case "WSS-03":
					return true;
			}
			if (r && Path != "" && (n.Length == 10 || n.Length == 6))
			{
				return true;
			}
			MessageBox.Show("Wrong measurement conditions or saving path or snum length", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			return false;
		}
	}
}