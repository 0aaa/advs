using System.Collections.ObjectModel;
using System.Windows;
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
        public decimal ReferenceSpeedValue { get; set; }

        public ObservableCollection<decimal> ValueCollection { get; set; }
            = new ObservableCollection<decimal>();

        public void AddValueInCollection(decimal addValue)
        {
            Application.Current.Dispatcher?.Invoke(() => ValueCollection.Add(addValue));
        }

        public decimal CollectionCount => ValueCollection.Count;
    }
}