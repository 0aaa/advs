namespace ADVS.Models.Classes
{
    public class TubeOpening : System.EventArgs// Событие открытия или закрытия порта частотного двигателя.
    {
        public bool IsOpen { get; set; }
    }
}