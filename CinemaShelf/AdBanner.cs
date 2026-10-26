using Microsoft.Maui.Controls;

namespace CinemaShelf
{
    // Thin MAUI view backed by the native AdMob banner.
    // Android implementation: Platforms/Android/AdBannerHandler.cs
    public class AdBanner : View
    {
        public string AdUnitId { get; set; } = "";

        // Width the banner is requested at, in dp. 0 = full screen width.
        public int WidthDp { get; set; }
    }
}
