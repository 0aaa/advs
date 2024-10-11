namespace ADVS.Styles.Converters
{
    internal class BooleanConverter<T>(T trueV, T falseV) : System.Windows.Data.IValueConverter
    {
		public T True { get; set; } = trueV;
		public T False { get; set; } = falseV;

		public virtual object Convert(object v, System.Type t, object p, System.Globalization.CultureInfo c)
        {
            return v is bool b && b ? True : False;
        }

        public virtual object ConvertBack(object v, System.Type t, object p, System.Globalization.CultureInfo c)
        {
            return v is T && (bool)v;
        }
    }

    internal sealed class BooleanToVisibilityConverter : BooleanConverter<System.Windows.Visibility>
    {
        public BooleanToVisibilityConverter() : base(System.Windows.Visibility.Visible, System.Windows.Visibility.Collapsed) { }
    }

    [System.Windows.Data.ValueConversion(typeof(bool), typeof(bool))]
    internal class InverseBooleanConverter : System.Windows.Data.IValueConverter
    {
        #region IValueConverter Members
        public object Convert(object v, System.Type t, object p, System.Globalization.CultureInfo c)
        {
            if (t != typeof(bool))
			{
                throw new System.InvalidOperationException("The target must be a boolean");
			}
            return v != null && !(bool)v;
        }

        public object ConvertBack(object v, System.Type t, object p, System.Globalization.CultureInfo c)
        {
            throw new System.NotSupportedException();
        }
        #endregion
    }
}