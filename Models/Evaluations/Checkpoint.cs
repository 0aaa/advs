namespace ADVS.Models.Evaluations
{
    internal class Checkpoint
    {
        [System.Xml.Serialization.XmlIgnore] private int _step = 10;
        public decimal S { get; set; }// Тестируемая скорость.
        public decimal Max { get; set; }
        public decimal Min { get; set; }
        public int F { get; set; }// Примерная частота вращения трубы для достижения этой скорости.
        public int Step// Максимальный шаг при корректировке частоты для достижения установленной скорости.
        {
            get => _step;
            set
            {
                if (value < 10 || value > 100)
                {
                    System.Windows.MessageBox.Show("Выберете значение в диапазоне от 10 до 100 Гц");
                    Step = _step;
                    return;
                }
                _step = value;
            }
        }
        public int Id { get; set; }
    }
}