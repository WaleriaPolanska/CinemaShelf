using Foundation;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.ApplicationModel;
using UIKit;
using AppTrackingTransparency;

namespace CinemaShelf;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        var result = base.FinishedLaunching(application, launchOptions);

        // Request ATT permission after a short delay to ensure UI is ready
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(1000);
            if (UIDevice.CurrentDevice.CheckSystemVersion(14, 5))
            {
                ATTrackingManager.RequestTrackingAuthorization(_ => { });
            }
        });

        return result;
    }
}
