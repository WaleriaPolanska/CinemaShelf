using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace CinemaShelf
{
	public partial class App 
	{
		public App()
		{
			InitializeComponent();
		}

		protected override Window CreateWindow(IActivationState? activationState)
		{
			return new Window(new AppShell());
		}
	}
}