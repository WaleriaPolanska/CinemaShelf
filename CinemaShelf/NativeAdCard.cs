using Microsoft.Maui.Controls;

namespace CinemaShelf
{
    // Marker item placed between movies in the list.
    public class NativeAdSlot
    {
    }

    // Thin MAUI view backed by a native AdMob "native advanced" ad.
    // Android implementation: Platforms/Android/NativeAdCardHandler.cs
    public class NativeAdCard : View
    {
        public string AdUnitId { get; set; } = "";
    }
}
