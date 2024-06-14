using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using VerificationAirVelocitySensor.Model.EnumLib;
using VerificationAirVelocitySensor.Models;
using VerificationAirVelocitySensor.Models.ClassLib;
using YamlDotNet.Serialization;

namespace VerificationAirVelocitySensor.ViewModels.Services
{
	internal class UserSettings : BaseVm.BaseVm// Пользовательские настройки.
	{
		private const string USER_SETTINGS_PATH = "UserSettings.txt";
		private SensorModel _sensorModel;
		public ObservableCollection<Checkpoint> Checkpoints { get; private set; }
		public SettingsModel SettingsModel { get; private set; }
		public MeasurementsData MeasurementsData { get; private set; }
		public SensorModel SensorModel
		{
			get => _sensorModel;
			set
			{
				_sensorModel = value;
				OnPropertyChanged(nameof(SensorModel));
			}
		}
		public string SavePath { get; set; }// Путь сохранения результата.

		public UserSettings()
		{
			SettingsModel = new SettingsModel();
			if (Checkpoints == null || Checkpoints.Count == 0)// Значения скорости, на которых нужно считать значения датчика.
			{
				Checkpoints = [
					new Checkpoint { Id = 1, Speed = 0.7m, Frequency = 500, MaxStep = 10, MinEdge = 0m, MaxEdge = 3.007m }
					, new Checkpoint { Id = 2, Speed = 5m, Frequency = 2765, MaxStep = 50, MinEdge = 3.320m, MaxEdge = 8.837m }
					, new Checkpoint { Id = 3, Speed = 10m, Frequency = 5390, MaxStep = 50, MinEdge = 9.634m, MaxEdge = 15.595m }
					, new Checkpoint { Id = 4, Speed = 15m, Frequency = 8130, MaxStep = 50, MinEdge = 15.935m, MaxEdge = 22.366m }
					, new Checkpoint { Id = 5, Speed = 20m, Frequency = 10810, MaxStep = 80, MinEdge = 22.248m, MaxEdge = 29.124m }
					, new Checkpoint { Id = 6, Speed = 25m, Frequency = 13570, MaxStep = 90, MinEdge = 28.549m, MaxEdge = 35.895m }
					, new Checkpoint { Id = 7, Speed = 30m, Frequency = 16384, MaxStep = 100, MinEdge = 32.340m, MaxEdge = 39.948m }
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
				var serializer = new Serializer();
				using var file = File.Open(USER_SETTINGS_PATH, FileMode.Create);
				using var writer = new StreamWriter(file);
				serializer.Serialize(writer, this);
			}
			catch (Exception e)
			{
				// ignored
				MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		public static UserSettings Deserialize()
		{
			var deserializer = new Deserializer();
			using var file = File.Open(USER_SETTINGS_PATH, FileMode.OpenOrCreate, FileAccess.Read);
			using var reader = new StreamReader(file);
			try
			{
				var settings = deserializer.Deserialize<UserSettings>(reader);
				return settings;
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