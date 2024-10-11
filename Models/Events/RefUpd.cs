namespace ADVS.Models.Events
{
    public class RefUpd : System.EventArgs
    {
        public double Ref { get; set; }// Эталонное значение скорости на анемометре.
    }
}