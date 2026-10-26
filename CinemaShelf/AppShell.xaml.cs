using Microsoft.Maui.Controls;

namespace CinemaShelf
{
	public partial class AppShell : Shell
	{
		public AppShell()
		{
			InitializeComponent();
			Routing.RegisterRoute(nameof(AddMoviePage), typeof(AddMoviePage));
			Routing.RegisterRoute(nameof(GoldenListPage), typeof(GoldenListPage));
			Routing.RegisterRoute(nameof(NotepadPage), typeof(NotepadPage));
		}
	}
}