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
        /// Снимаемое значение скорости с эталона
        /// </summary>
        public decimal ReferenceSpeedValue { get; set; }

        public decimal DeviceSpeedValue1 { get; set; }
        public decimal DeviceSpeedValue2 { get; set; }
        public decimal DeviceSpeedValue3 { get; set; }
        public decimal DeviceSpeedValue4 { get; set; }
        public decimal DeviceSpeedValue5 { get; set; }

        //public ObservableCollection<decimal> ValueCollection { get; set; }
        //    = new ObservableCollection<decimal>();

        //public void AddValueInCollection(decimal addValue)
        //{
        //    Application.Current.Dispatcher?.Invoke(() => ValueCollection.Add(addValue));
        //}

        //public decimal CollectionCount => ValueCollection.Count;
    }
}