using Android.App;
using Android.Content.PM;
using Android.OS;
using Microsoft.Maui;

namespace CinemaShelf;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                           ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private static bool _consentRequested;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#0D0B1A"));

        if (!_consentRequested)
        {
            _consentRequested = true;
            // Ads stay dormant until this settles — see ConsentManager.
            try { ConsentManager.Gather(this); }
            catch { /* ignore */ }
            ConsentManager.RunWhenAllowed(InterstitialAds.Preload);
            InAppUpdate.CheckOnLaunch(this);
        }

        // Offers the Play Store update prompt when a newer version is live.
        AppUpdatePrompt.CheckOnce(this);
    }

    protected override void OnResume()
    {
        base.OnResume();
        InterstitialAds.Preload();
    }
}
