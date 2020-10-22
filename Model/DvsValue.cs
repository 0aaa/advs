using System.Collections.ObjectModel;
using System.Linq;
using VerificationAirVelocitySensor.ViewModel.BaseVm;

namespace VerificationAirVelocitySensor.Model
{
    public class DvsValue : BaseVm
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

        public ObservableCollection<decimal> ValueCollection { get; set; }
            = new ObservableCollection<decimal>();

        public void AddValueInCollection(decimal addValue)
        {
            ValueCollection.Add(addValue);

            AverageValue = ValueCollection.Sum() / ValueCollection.Count;
        }

        public decimal CollectionCount => ValueCollection.Count;
    }
}