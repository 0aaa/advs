using ADVS.Models.Classes;
using ADVS.ViewModels.Base;
using ADVS.ViewModels.Services;

namespace ADVS.ViewModels
{
    internal class DebugVm : BaseVm
    {
        public RelayCommand[] Rcs { get; }// StopFrequencyMotorCommand, SetSpeedFrequencyMotorCommand.
		public decimal Sref { get; private set; }
        public int F { get; set; }

        public DebugVm()
        {
			Rcs = [
				new RelayCommand(() => Tube.Inst.SetF(0, 0), Tube.Inst.IsOpen)
				, new RelayCommand(() => {
					Tube.Inst.RefUpd += Tube_UpdRef;
					Tube.Inst.SetF(F, 0);
				}, Tube.Inst.IsOpen)
			];
		}

        private void Tube_UpdRef(object _, RefUpd e)
        {
            Sref = (decimal)e.Ref;
        }

        public void Unload()
        {
            Tube.Inst.RefUpd -= Tube_UpdRef;
            Tube.Inst.SetF(0, 0);
            Tube.Inst.Close();
        }
    }
}