using System.Collections.ObjectModel;
using System.IO.Ports;
using VerificationAirVelocitySensor.Model.EnumLib;
using VerificationAirVelocitySensor.Models;
using VerificationAirVelocitySensor.Models.ClassLib;
using VerificationAirVelocitySensor.ViewModels.BaseVm;

namespace VerificationAirVelocitySensor.ViewModels
{
    internal class SettingsVm : BaseVm.BaseVm
    {
        #region Collections.
        public Lpf[] LpfArr { get; }
        public GateTime[] GateTimeArr { get; }
        public ObservableCollection<string> Ports { get; }
        #endregion
        public SettingsModel SettingsModel { get; }
        #region Relay command.
        public RelayCommand UpdatePorts { get; }
        #endregion

        public SettingsVm(SettingsModel settingsModel)
        {
            SettingsModel = settingsModel;
			GateTimeArr = [
				//new GateTimeDescription(GateTime.S1, "1 сек"),
				new GateTime(GateTimeSec.S4, "4 сек")
				, new GateTime(GateTimeSec.S7, "7 сек")
				, new GateTime(GateTimeSec.S10, "10 сек")
				, new GateTime(GateTimeSec.S100, "100 сек")
			];
			LpfArr = [
				new Lpf(LpfChannel.Channel1, "1-ый канал")
				, new Lpf(LpfChannel.Channel2, "2-ой канал")
			];
			Ports = new ObservableCollection<string>(SerialPort.GetPortNames());
			UpdatePorts = new RelayCommand(() => {
				var newPortList = new ObservableCollection<string>(SerialPort.GetPortNames());
				foreach (var port in newPortList)// Добавляю новые итемы из полученной коллекции.
				{
					if (!Ports.Contains(port))
					{
						Ports.Add(port);
					}
				}
				var deletePorts = new ObservableCollection<string>();
				foreach (var port in Ports)// Записываю старые итемы в коллекцию на удаление.
				{
					if (!newPortList.Contains(port))
					{
						deletePorts.Add(port);
					}
				}
				foreach (var port in deletePorts)// Удаляю лишние элементы.
				{
					Ports.Remove(port);
				}
			});
        }
    }
}