namespace ADVS.Models
{
    internal partial class Wss01Eval : ViewModels.Base.BaseVm
    {
		private const int VALUES_CNT = 3;
        public Speed[] Ss { get; set; }
        public decimal S { get; }// Скорость потока воздуха на которой снимается значение.
        public decimal?[] Refs { get; set; }// Снимаемое значение скорости с эталона.
        public decimal? Ref { get; set; }

        public Wss01Eval(decimal s)
        {
			Ss = new Speed[VALUES_CNT];
			Refs = new decimal?[VALUES_CNT];
            for (int i = 0; i < Ss.Length; i++)
			{
				Ss[i] = new Speed();
            }
            S = s;
        }
    }
}