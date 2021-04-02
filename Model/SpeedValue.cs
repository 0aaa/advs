using VerificationAirVelocitySensor.ViewModel.BaseVm;

namespace VerificationAirVelocitySensor.Model
{
    public class SpeedValue : BaseVm
    {
        /// <summary>
        /// Итоговое полученное значение тестирования КС
        /// </summary>
        public decimal? ResultValue { get; set; }

        /// <summary>
        /// Показывает, проверяется ли сейчас это значение , для его подсветки
        /// </summary>
        public bool IsСheckedNow { get; set; }

        /// <summary>
        /// Флаг для биндинга на итерфейс, что бы подсветить ячейку по результату проверки валидности.
        /// Необходим, что бы не подсвечивать ячейки, которые еще не проверялись , либо не будут проверяться.
        /// </summary>
        public bool IsVerified { get; set; }
    }
}