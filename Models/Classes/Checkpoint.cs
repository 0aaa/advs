namespace VerificationAirVelocitySensor.Models.Classes
{
    internal class Checkpoint
    {
        [System.Xml.Serialization.XmlIgnore] private int _maxStep = 10;
        public decimal S { get; set; }// Тестируемая скорость.
        public decimal Max { get; set; }
        public decimal Min { get; set; }
        public int F { get; set; }// Примерная частота вращения трубы для достижения этой скорости.
        public int MaxStep// Максимальный шаг при корректировке частоты для достижения установленной скорости.
        {
            get => _maxStep;
            set
            {
                if (value < 10 || value > 100)
                {
                    System.Windows.MessageBox.Show("Выберете значение в диапазоне от 10 до 100 Гц");
                    MaxStep = _maxStep;
                    return;
                }
                _maxStep = value;
            }
        }
        public int Id { get; set; }// Номер в списке.
    }
}