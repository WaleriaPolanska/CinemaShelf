using Android.App;
using Android.Gms.Tasks;
using Xamarin.Google.Android.Play.Core.AppUpdate;
using Xamarin.Google.Android.Play.Core.AppUpdate.Install;
using Xamarin.Google.Android.Play.Core.AppUpdate.Install.Model;

namespace CinemaShelf
{
    // Offers the Play update dialog on launch. Flexible flow: the download runs in the
    // background, and the user is asked to restart once it is ready.
    internal static class InAppUpdate
    {
        private const int RequestCode = 4711;
        private static bool _asked;

        public static void CheckOnLaunch(Activity activity)
        {
            if (_asked)
                return;
            _asked = true;

            try
            {
                var manager = AppUpdateManagerFactory.Create(activity);
                manager.GetAppUpdateInfo().AddOnSuccessListener(new InfoListener(manager, activity));
            }
            catch { /* no Play Services, sideloaded build, no network */ }
        }

        private sealed class InfoListener : Java.Lang.Object, IOnSuccessListener
        {
            private readonly IAppUpdateManager _manager;
            private readonly Activity _activity;

            public InfoListener(IAppUpdateManager manager, Activity activity)
            {
                _manager = manager;
                _activity = activity;
            }

            public void OnSuccess(Java.Lang.Object result)
            {
                if (result is not AppUpdateInfo info)
                    return;

                if (info.UpdateAvailability() != UpdateAvailability.UpdateAvailable)
                    return;
                if (!info.IsUpdateTypeAllowed(AppUpdateType.Flexible))
                    return;

                _manager.RegisterListener(new InstallListener(_manager, _activity));
                _manager.StartUpdateFlowForResult(
                    info,
                    _activity,
                    AppUpdateOptions.NewBuilder(AppUpdateType.Flexible).Build(),
                    RequestCode);
            }
        }

        // The flexible flow downloads quietly; installing still needs a restart, so ask.
        private sealed class InstallListener : Java.Lang.Object, IInstallStateUpdatedListener
        {
            private readonly IAppUpdateManager _manager;
            private readonly Activity _activity;

            public InstallListener(IAppUpdateManager manager, Activity activity)
            {
                _manager = manager;
                _activity = activity;
            }

            public void OnStateUpdate(InstallState installState)
            {
                if (installState == null)
                    return;

                if (installState.InstallStatus() != InstallStatus.Downloaded)
                    return;

                _manager.UnregisterListener(this);

                _activity.RunOnUiThread(() =>
                {
                    new AndroidX.AppCompat.App.AlertDialog.Builder(_activity)
                        .SetTitle("Update ready")
                        .SetMessage("A new version of CinemaShelf has been downloaded.")
                        .SetPositiveButton("Restart", (s, e) => _manager.CompleteUpdate())
                        .SetNegativeButton("Later", (s, e) => { })
                        .Show();
                });
            }
        }
    }
}
