using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace CinemaShelf;

public partial class AdBannerView : ContentView
{
    // Set from XAML to give this placement its own AdMob unit. Every placement gets a
    // full-width banner: half-width ones ask for a size (~176dp) that no standard demand
    // matches, so they stayed empty in production even though test ads always filled them.
    public string OverrideAdUnitId { get; set; } = "";

#if ANDROID
    private bool _bannerBuilt;
    private readonly string _adUnitId;
#endif

    public AdBannerView()
    {
        InitializeComponent();
        HeightRequest = 60;
        MinimumHeightRequest = 60;

#if ANDROID || IOS
#if ANDROID
#if DEBUG
        var adUnitId = "ca-app-pub-3940256099942544/6300978111";
#else
        var adUnitId = "ca-app-pub-3940256099942544/6300978111";
#endif
#elif IOS
#if DEBUG
        var adUnitId = "ca-app-pub-3940256099942544/2934735716";
#else
        var adUnitId = "ca-app-pub-3940256099942544/6300978111";
#endif
#endif
#if ANDROID
        _adUnitId = adUnitId;
#else
        Content = new Plugin.MauiMtAdmob.Controls.MTAdView
        {
            AdsId = adUnitId,
            HeightRequest = 60,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
#endif
#else
        Content = new BoxView { HeightRequest = 60, Color = Colors.Transparent };
#endif
    }

#if ANDROID
    // Built once the view has been measured: an adaptive banner has to be asked for at a
    // fixed width, and the usable width depends on each page's padding.
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (_bannerBuilt || width <= 0)
            return;
        _bannerBuilt = true;

#if DEBUG
        var adUnitId = _adUnitId;
#else
        var adUnitId = OverrideAdUnitId.Length > 0 ? OverrideAdUnitId : _adUnitId;
#endif

        Content = new AdBanner
        {
            AdUnitId = adUnitId,
            WidthDp = (int)width,
            HeightRequest = 60,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
    }
#endif
}
