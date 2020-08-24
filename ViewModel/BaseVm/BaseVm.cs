﻿using System.ComponentModel;
using System.Runtime.CompilerServices;


namespace VerificationAirVelocitySensor.ViewModel.BaseVm
{
    public class BaseVm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
