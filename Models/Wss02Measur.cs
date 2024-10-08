namespace ADVS.Models
{
    internal class Wss02Measur : ViewModels.Base.BaseVm
    {
		private const int VALUES_CNT = 5;
        public decimal S { get; }// Скорость потока воздуха, на которой снимается значение.
        public decimal? RefS { get; set; }// Снимаемое значение скорости с эталона.
        public Speed[] Ss { get; set; }

        public Wss02Measur(decimal s)
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