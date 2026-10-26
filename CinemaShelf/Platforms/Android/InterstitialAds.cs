using Android.Gms.Ads;
using Android.Gms.Ads.Interstitial;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace CinemaShelf
{
    // Full-screen ads. Two placements, each with its own AdMob unit:
    //   Movies — after the first saved movie and every fifth one after that
    //   Home   — when leaving the browser page, no more than once every few minutes
    internal static class InterstitialAds
    {
#if DEBUG
        private const string MovieAdUnitId = "ca-app-pub-3940256099942544/1033173712";
        private const string HomeAdUnitId = "ca-app-pub-3940256099942544/1033173712";
#else
        private const string MovieAdUnitId = "ca-app-pub-3940256099942544/1033173712";
        private const string HomeAdUnitId = "ca-app-pub-3940256099942544/1033173712";
#endif
        private const string CountKey = "movies_added_total";

        private const string HomeShownAtKey = "home_ad_shown_at_count";
        private const string AddCountKey = "add_page_opened_total";

        private static readonly Slot Movies = new Slot(MovieAdUnitId);
        private static readonly Slot Home = new Slot(HomeAdUnitId);

        public static void Preload()
        {
            Movies.Preload();
            Home.Preload();
        }

        // Counts a saved movie and shows the ad when this one is due.
        public static void NoteMovieAdded()
        {
            var count = Preferences.Get(CountKey, 0) + 1;
            Preferences.Set(CountKey, count);

            if (count == 1 || count % 5 == 0)
                Movies.Show();
        }

        // Every fourth movie starting from the second one: 2, 6, 10, 14...
        // Tied to the count rather than to the tap, so leaving the page again on the
        // same count does not show it twice.
        // Third time the user opens the add-movie page, then every fourth: 3, 7, 11...
        public static void ShowOnAdd()
        {
            var count = Preferences.Get(AddCountKey, 0) + 1;
            Preferences.Set(AddCountKey, count);

            // Second press, then every fourth: 2, 6, 10, 14...
            if (count >= 2 && (count - 2) % 4 == 0)
                Home.Show();
        }

        public static void ShowOnHome()
        {
            var count = Preferences.Get(CountKey, 0);

            if (count < 2 || (count - 2) % 4 != 0)
                return;

            if (Preferences.Get(HomeShownAtKey, 0) == count)
                return;

            if (Home.Show())
                Preferences.Set(HomeShownAtKey, count);
        }

        private sealed class Slot
        {
            private readonly string _adUnitId;
            private InterstitialAd _loaded;
            private bool _loading;

            public Slot(string adUnitId) => _adUnitId = adUnitId;

            public void Preload()
            {
                if (_adUnitId.Length == 0 || _loaded != null || _loading || !ConsentManager.CanRequestAds)
                    return;

                var activity = Platform.CurrentActivity;
                if (activity == null)
                    return;

                _loading = true;
                InterstitialAd.Load(activity, _adUnitId, new AdRequest.Builder().Build(), new LoadCallback(this));
            }

            public bool Show()
            {
                var activity = Platform.CurrentActivity;
                if (_loaded == null || activity == null)
                {
                    Preload();
                    return false;
                }

                _loaded.Show(activity);
                _loaded = null;
                Preload();
                return true;
            }

            private sealed class LoadCallback : InterstitialAdLoadCallback
            {
                private readonly Slot _slot;
                public LoadCallback(Slot slot) => _slot = slot;

                [Java.Interop.Export("onAdLoaded")]
                public void OnInterstitialLoaded(InterstitialAd ad)
                {
                    _slot._loading = false;
                    _slot._loaded = ad;
                }

                public override void OnAdFailedToLoad(LoadAdError error)
                {
                    _slot._loading = false;
                    _slot._loaded = null;
                }
            }
        }
    }
}
