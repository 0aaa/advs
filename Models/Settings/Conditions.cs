namespace ADVS.Models
{
    internal partial class Conditions : ViewModels.Base.BaseVm
    {
        public string Snum { get; set; }// Серийный номер.
        public string T { get; set; }// Температура.
        public string H { get; set; }// Влажность.
        public string P { get; set; }// Давление.
    }
}