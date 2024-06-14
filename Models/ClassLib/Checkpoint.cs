using System.Windows;
using System.Xml.Serialization;

namespace VerificationAirVelocitySensor.Models.ClassLib
{
    internal class Checkpoint
    {
        [XmlIgnore] private int _maxStep = 10;
        public decimal Speed { get; set; }// Тестируемая скорость.
        public decimal MaxEdge { get; set; }
        public decimal MinEdge { get; set; }
        public int Frequency { get; set; }// Примерная частота вращения трубы для достижения этой скорости.
        public int MaxStep// Максимальный шаг при корректировке частоты для достижения установленной скорости.
        {
            get => _maxStep;
            set
            {
                if (value < 10 || value > 100)
                {
                    MessageBox.Show("Выберете значение в диапазоне от 10 до 100 Гц");
                    MaxStep = _maxStep;
                    return;
                }
                _maxStep = value;
            }
        }
        public int Id { get; set; }// Номер в списке.
    }
}