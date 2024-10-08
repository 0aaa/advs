namespace ADVS.Models.Classes
{
    internal class GateTime(Enums.Secs s, string d)// Класс для создания коллекции доступных enum GateTime для биндинга на UI.
    {
        public Enums.Secs Sec { get; } = s;
        public string Descr { get; } = d;
    }
}