namespace ADVS.Models
{
    internal partial class Wss02Eval : ViewModels.Base.BaseVm
    {
		private const int VALUES_CNT = 5;
        public decimal S { get; }// Скорость потока воздуха, на которой снимается значение.
        public decimal? Ref { get; set; }// Снимаемое значение скорости с эталона.
        public Speed[] Ss { get; set; }

        public Wss02Eval(decimal s)
        {
			Ss = new Speed[VALUES_CNT];
            for (int i = 0; i < Ss.Length; i++)
			{
				Ss[i] = new Speed();
            }
            S = s;
        }
    }
}