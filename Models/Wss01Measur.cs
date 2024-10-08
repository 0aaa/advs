namespace ADVS.Models
{
    internal class Wss01Measur : ViewModels.Base.BaseVm
    {
		private const int VALUES_CNT = 3;
        public Speed[] Ss { get; set; }
        public decimal S { get; }// Скорость потока воздуха на которой снимается значение.
        public decimal?[] RefSs { get; set; }// Снимаемое значение скорости с эталона.
        public decimal? RefS { get; set; }

        public Wss01Measur(decimal s)
        {
			Ss = new Speed[VALUES_CNT];
			RefSs = new decimal?[VALUES_CNT];
            for (int i = 0; i < Ss.Length; i++)
			{
				Ss[i] = new Speed();
            }
            S = s;
        }
    }
}