using Android.App;
using Android.Gms.Tasks;
using Xamarin.Google.Android.Play.Core.AppUpdate;
using Xamarin.Google.Android.Play.Core.AppUpdate.Install.Model;

namespace CinemaShelf
{
    // Asks Google Play whether a newer version is live and, if so, lets Play show its own
    // update prompt. Only works for builds installed from the Play Store.
    internal static class AppUpdatePrompt
    {
        private const int RequestCode = 1001;
        private static bool _checked;

        public static void CheckOnce(Activity activity)
        {
            if (_checked)
                return;
            _checked = true;

            try
            {
                var manager = AppUpdateManagerFactory.Create(activity);
                manager.GetAppUpdateInfo().AddOnSuccessListener(new UpdateInfoListener(manager, activity));
            }
            catch { /* no Play services, sideloaded build - nothing to offer */ }
        }

        private sealed class UpdateInfoListener : Java.Lang.Object, IOnSuccessListener
        {
            private readonly IAppUpdateManager _manager;
            private readonly Activity _activity;

            public UpdateInfoListener(IAppUpdateManager manager, Activity activity)
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

                if (!info.IsUpdateTypeAllowed(AppUpdateType.Immediate))
                    return;

                try
                {
                    _manager.StartUpdateFlowForResult(
                        info,
                        _activity,
                        AppUpdateOptions.NewBuilder(AppUpdateType.Immediate).Build(),
                        RequestCode);
                }
                catch { /* user dismissed or Play could not start the flow */ }
            }
        }
    }
}
