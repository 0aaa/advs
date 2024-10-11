namespace ADVS.Models
{
	internal partial class Wss03Eval : ViewModels.Base.BaseVm
    {
		private const int VALUES_CNT = 1;
        public decimal S { get; }// Скорость потока воздуха, на которой снимается значение.
		public decimal? Ref { get; set; }// Снимаемое значение скорости с эталона.
		public Speed[] Ss { get; set; }
        public int F { get; set; }// Примерная частота вращения трубы для достижения этой скорости.

        public Wss03Eval(decimal s, int f)
		{
			Ss = new Speed[VALUES_CNT];
            for (int i = 0; i < Ss.Length; i++)
            {
				Ss[i] = new Speed();
            }
            S = s;
			F = f;
		}
	}
}