using System;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace CinemaShelf
{
    public partial class SplashPage : ContentPage
    {
        public SplashPage()
        {
            InitializeComponent();
            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, EventArgs e)
        {
            Loaded -= OnPageLoaded;
            try
            {
                await Task.Delay(1200);
                await Shell.Current.GoToAsync("//MainPage");
            }
            catch
            {
                try { await Shell.Current.GoToAsync("//MainPage"); }
                catch { /* give up */ }
            }
        }
    }
}
