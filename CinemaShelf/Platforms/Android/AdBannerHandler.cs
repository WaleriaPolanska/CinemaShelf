using Android.Gms.Ads;
using Microsoft.Maui;
using Microsoft.Maui.Handlers;

namespace CinemaShelf
{
    public class AdBannerHandler : ViewHandler<AdBanner, AdView>
    {
        public static readonly IPropertyMapper<AdBanner, AdBannerHandler> Mapper =
            new PropertyMapper<AdBanner, AdBannerHandler>(ViewMapper);

        public AdBannerHandler() : base(Mapper) { }

        protected override AdView CreatePlatformView()
        {
            var metrics = Context.Resources.DisplayMetrics;
            var screenDp = (int)(metrics.WidthPixels / metrics.Density);
            var widthDp = VirtualView.WidthDp > 0 ? VirtualView.WidthDp : screenDp;

            var adView = new AdView(Context)
            {
                AdSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSize(Context, widthDp),
                AdUnitId = VirtualView.AdUnitId
            };
            // Two banners ask the same ad unit at once, so one of them regularly comes
            // back empty. Retry a few times instead of leaving half the row blank.
            adView.AdListener = new RetryOnFailure(adView);
            ConsentManager.RunWhenAllowed(() => adView.LoadAd(new AdRequest.Builder().Build()));
            return adView;
        }

        private sealed class RetryOnFailure : AdListener
        {
            private const int MaxAttempts = 3;
            private readonly AdView _adView;
            private int _attempts;

            public RetryOnFailure(AdView adView) => _adView = adView;

            public override void OnAdFailedToLoad(LoadAdError error)
            {
                if (++_attempts > MaxAttempts)
                    return;

                new Android.OS.Handler(Android.OS.Looper.MainLooper).PostDelayed(
                    () => _adView.LoadAd(new AdRequest.Builder().Build()),
                    5000);
            }
        }

        protected override void DisconnectHandler(AdView platformView)
        {
            platformView.Destroy();
            base.DisconnectHandler(platformView);
        }
    }
}
