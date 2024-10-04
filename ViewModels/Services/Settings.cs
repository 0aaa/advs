using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using VerificationAirVelocitySensor.Models.Enums;
using VerificationAirVelocitySensor.Models;
using VerificationAirVelocitySensor.Models.Classes;
using YamlDotNet.Serialization;

namespace VerificationAirVelocitySensor.ViewModels.Services
{
	internal class Settings : Base.BaseVm// Пользовательские настройки.
	{
		private const string PATH = "UserSettings.txt";
		private Model _m;
		public ObservableCollection<Checkpoint> Checkpoints { get; private set; }
		public Devices Devices { get; private set; }
		public Conditions Conditions { get; private set; }
		public Model M
		{
			get => _m;
			set
			{
				_m = value;
				OnPropertyChanged(nameof(M));
			}
		}
		public string SavePath { get; set; }// Путь сохранения результата.

		public Settings()
		{
			Devices = new Devices();
			Conditions = new Conditions();
			if (Checkpoints == null || Checkpoints.Count == 0)// Значения скорости, на которых нужно считать значения датчика.
			{
				Checkpoints = [
					new Checkpoint { Id = 1, S = 0.7m, F = 500, MaxStep = 10, Min = 0m, Max = 3.007m }
					, new Checkpoint { Id = 2, S = 5m, F = 2765, MaxStep = 50, Min = 3.320m, Max = 8.837m }
					, new Checkpoint { Id = 3, S = 10m, F = 5390, MaxStep = 50, Min = 9.634m, Max = 15.595m }
					, new Checkpoint { Id = 4, S = 15m, F = 8130, MaxStep = 50, Min = 15.935m, Max = 22.366m }
					, new Checkpoint { Id = 5, S = 20m, F = 10810, MaxStep = 80, Min = 22.248m, Max = 29.124m }
					, new Checkpoint { Id = 6, S = 25m, F = 13570, MaxStep = 90, Min = 28.549m, Max = 35.895m }
					, new Checkpoint { Id = 7, S = 30m, F = 16384, MaxStep = 100, Min = 32.340m, Max = 39.948m }
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
				var s = new Serializer();
				using var f = File.Open(PATH, FileMode.Create);
				using var sw = new StreamWriter(f);
				s.Serialize(sw, this);
			}
			catch (Exception e)
			{
				// ignored
				MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		public static Settings Deserialize()
		{
			var d = new Deserializer();
			using var f = File.Open(PATH, FileMode.OpenOrCreate, FileAccess.Read);
			using var sr = new StreamReader(f);
			try
			{
				var s = d.Deserialize<Settings>(sr);
				s.Conditions ??= new Conditions();
				return s;
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return null;
			}
		}
		#endregion
	}
}