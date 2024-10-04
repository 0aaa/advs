using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VerificationAirVelocitySensor.Styles.Converters
{
    internal class BooleanConverter<T>(T trueV, T falseV) : IValueConverter
    {
		public T True { get; set; } = trueV;
		public T False { get; set; } = falseV;

		public virtual object Convert(object v, Type t, object p, CultureInfo c)
        {
            return v is bool b && b ? True : False;
        }

        public virtual object ConvertBack(object v, Type t, object p, CultureInfo c)
        {
            return v is T && (bool)v;
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
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            if (t != typeof(bool))
			{
                throw new InvalidOperationException("The target must be a boolean");
			}
            return v != null && !(bool)v;
        }

        public object ConvertBack(object v, Type t, object p, CultureInfo c)
        {
            throw new NotSupportedException();
        }
        #endregion
    }
}