using System.Windows.Controls;
using VerificationAirVelocitySensor.ViewModels.BaseVm;

namespace VerificationAirVelocitySensor.Views
{
	public partial class Wss03 : UserControl// Interaction logic for Wss03.xaml.
	{
		internal Wss03(BaseVm propertyChanged)
		{
			InitializeComponent();
			DataContext = propertyChanged;
		}
	}
}