using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VerificationAirVelocitySensor.Styles.Converters
{
    internal class BooleanConverter<T>(T trueValue, T falseValue) : IValueConverter
    {
		public T True { get; set; } = trueValue;
		public T False { get; set; } = falseValue;

		public virtual object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? True : False;
        }

        public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is T && (bool)value;
        }
    }

    internal sealed class BooleanToVisibilityConverter : BooleanConverter<Visibility>
    {
        public BooleanToVisibilityConverter() : base(Visibility.Visible, Visibility.Collapsed) { }
    }

    [ValueConversion(typeof(bool), typeof(bool))]
    internal class InverseBooleanConverter : IValueConverter
    {
        #region IValueConverter Members
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(bool))
			{
                throw new InvalidOperationException("The target must be a boolean");
			}
            return value != null && !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
        #endregion
    }
}