using System.Collections.ObjectModel;
using System.IO.Ports;
using ADVS.Models.Enums;
using ADVS.Models;
using ADVS.Models.Classes;
using ADVS.ViewModels.Base;

namespace ADVS.ViewModels
{
    internal partial class DeviceSettingsVm : BaseVm
    {
        #region Collections.
        public GateTime[] Gts { get; }
        public ObservableCollection<string> Ps { get; }
        #endregion
        public Devices Devices { get; }
        #region Relay command.
        public RelayCommand UpdatePs { get; }
        #endregion

        public DeviceSettingsVm(Devices s)
        {
            Devices = s;
            Gts = [
				//new GateTimeDescription(GateTime.S1, "1 s"),
				new GateTime(Secs.S4, "4 s")
				, new GateTime(Secs.S7, "7 s")
				, new GateTime(Secs.S10, "10 s")
				, new GateTime(Secs.S100, "100 s")
			];
			Ps = new ObservableCollection<string>(SerialPort.GetPortNames());
			UpdatePs = new RelayCommand(() => {
				var newPs = new ObservableCollection<string>(SerialPort.GetPortNames());
				foreach (var p in newPs)// Добавляю новые итемы из полученной коллекции.
				{
					if (!Ps.Contains(p))
					{
						Ps.Add(p);
					}
				}
				var deletePs = new ObservableCollection<string>();
				foreach (var p in Ps)// Записываю старые итемы в коллекцию на удаление.
				{
					if (!newPs.Contains(p))
					{
						deletePs.Add(p);
					}
				}
				foreach (var p in deletePs)// Удаляю лишние элементы.
				{
					Ps.Remove(p);
				}
			});
        }
    }
}