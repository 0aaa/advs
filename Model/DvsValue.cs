using System.Collections.ObjectModel;
using System.Linq;

namespace VerificationAirVelocitySensor.Model
{
    public class DvsValue
    {
        
        public DvsValue(decimal speedValue)
        {
            SpeedValue = speedValue;
        }

        /// <summary>
        /// Скорость потока воздуха на которой снимается значение
        /// </summary>
        public decimal SpeedValue { get; }

        /// <summary>
        /// TODO Что именно мы снимаем ? 
        /// Снимаемое значение частоты
        /// </summary>
        public decimal AverageValue { get; set; }

        private readonly ObservableCollection<decimal> _valueCollection = new ObservableCollection<decimal>();

        public void AddValueInCollection(decimal addValue)
        {
            _valueCollection.Add(addValue);

            AverageValue = _valueCollection.Sum() / _valueCollection.Count;
        }

        public decimal CollectionCount => _valueCollection.Count;
    }
}