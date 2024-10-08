namespace ADVS.Models.Classes
{
    internal class Wss(Enums.Model m, string d)
    {
        public Enums.Model Model { get; set; } = m;
        public string Descr { get; set; } = d;
    }
}