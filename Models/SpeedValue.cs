﻿using VerificationAirVelocitySensor.ViewModels.BaseVm;

namespace VerificationAirVelocitySensor.Models
{
    internal class SpeedValue : BaseVm
    {
        public decimal? ResultValue { get; set; }// Итоговое полученное значение тестирования WSS.
        public bool IsСheckedNow { get; set; }// Показывает, проверяется ли сейчас это значение, для его подсветки.
        public bool IsVerified { get; set; }// Флаг для биндинга на UI, чтобы подсветить ячейку по результату проверки валидности. Необходим, чтобы не подсвечивать ячейки, которые ещё не проверялись либо не будут проверяться.
    }
}