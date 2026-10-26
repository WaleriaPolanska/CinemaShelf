using Android.Content;
using Android.Gms.Ads;
using Android.Gms.Ads.NativeAd;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Microsoft.Maui;
using Microsoft.Maui.Handlers;

namespace CinemaShelf
{
    public class NativeAdCardHandler : ViewHandler<NativeAdCard, NativeAdView>
    {
        public static readonly IPropertyMapper<NativeAdCard, NativeAdCardHandler> Mapper =
            new PropertyMapper<NativeAdCard, NativeAdCardHandler>(ViewMapper);

        public NativeAdCardHandler() : base(Mapper) { }

        protected override NativeAdView CreatePlatformView()
        {
            var context = Context;
            var density = context.Resources.DisplayMetrics.Density;
            int Dp(int value) => (int)(value * density);

            var headline = new TextView(context)
            {
                TextSize = 15f
            };
            headline.SetTextColor(Color.ParseColor("#FFFFFF"));
            headline.SetTypeface(null, TypefaceStyle.Bold);
            headline.SetMaxLines(2);

            var body = new TextView(context) { TextSize = 12f };
            body.SetTextColor(Color.ParseColor("#C9A0FF"));
            body.SetMaxLines(2);

            var sponsored = new TextView(context) { TextSize = 10f, Text = "Ad" };
            sponsored.SetTextColor(Color.ParseColor("#0D0B1A"));
            sponsored.SetBackgroundColor(Color.ParseColor("#FFD700"));
            sponsored.SetPadding(Dp(4), 0, Dp(4), 0);

            var media = new MediaView(context);
            // Google flags media views smaller than roughly 120dp as an implementation problem.
            var mediaParams = new LinearLayout.LayoutParams(Dp(120), Dp(120));
            mediaParams.SetMargins(0, 0, Dp(10), 0);
            media.LayoutParameters = mediaParams;

            var callToAction = new Button(context)
            {
                TextSize = 12f,
                Text = "Open"
            };
            callToAction.SetTextColor(Color.ParseColor("#FFFFFF"));
            callToAction.SetBackgroundColor(Color.ParseColor("#6C3CE1"));
            callToAction.SetPadding(Dp(12), 0, Dp(12), 0);

            var texts = new LinearLayout(context) { Orientation = Orientation.Vertical };
            texts.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
            var badgeRow = new LinearLayout(context) { Orientation = Orientation.Horizontal };
            badgeRow.AddView(sponsored);
            texts.AddView(badgeRow);
            texts.AddView(headline);
            texts.AddView(body);

            var row = new LinearLayout(context) { Orientation = Orientation.Horizontal };
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8));
            row.SetBackgroundColor(Color.ParseColor("#130A22"));
            row.AddView(media);
            row.AddView(texts);
            row.AddView(callToAction);

            var adView = new NativeAdView(context);
            adView.AddView(row);
            adView.HeadlineView = headline;
            adView.BodyView = body;
            adView.MediaView = media;
            adView.CallToActionView = callToAction;

            var adUnitId = VirtualView.AdUnitId;
            ConsentManager.RunWhenAllowed(() =>
            {
                var loader = new AdLoader.Builder(context, adUnitId)
                    .ForNativeAd(new LoadedListener(adView, headline, body, callToAction))
                    .Build();
                loader.LoadAd(new AdRequest.Builder().Build());
            });

            return adView;
        }

        private sealed class LoadedListener : Java.Lang.Object, NativeAd.IOnNativeAdLoadedListener
        {
            private readonly NativeAdView _adView;
            private readonly TextView _headline;
            private readonly TextView _body;
            private readonly Button _callToAction;

            public LoadedListener(NativeAdView adView, TextView headline, TextView body,
                Button callToAction)
            {
                _adView = adView;
                _headline = headline;
                _body = body;
                _callToAction = callToAction;
            }

            public void OnNativeAdLoaded(NativeAd nativeAd)
            {
                _headline.Text = nativeAd.Headline ?? "";
                _body.Text = nativeAd.Body ?? "";
                _body.Visibility = string.IsNullOrEmpty(nativeAd.Body) ? ViewStates.Gone : ViewStates.Visible;

                var callToAction = nativeAd.CallToAction;
                _callToAction.Text = string.IsNullOrEmpty(callToAction) ? "Open" : callToAction;

                _adView.SetNativeAd(nativeAd);
            }
        }
    }
}
