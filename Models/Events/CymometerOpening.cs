namespace ADVS.Models.Events
{
    internal class CymometerOpening : System.EventArgs// Событие открытия или закрытия порта частотомера.
    {
        public bool IsOpen { get; set; }
    }
}