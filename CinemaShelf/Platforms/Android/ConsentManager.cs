using Android.App;
using Android.Gms.Ads;
using Xamarin.Google.UserMesssagingPlatform;

namespace CinemaShelf
{
    // Google requires a certified consent flow (UMP) before ads may be requested in the EEA/UK.
    // Nothing is loaded until this has run: the ad views wait for AdsAllowed.
    internal static class ConsentManager
    {
        private static bool _initialising;

        public static bool CanRequestAds { get; private set; }
        public static bool PrivacyOptionsRequired { get; private set; }
        public static event System.Action AdsAllowed;

        public static void Gather(Activity activity)
        {
            var builder = new ConsentRequestParameters.Builder()
                .SetTagForUnderAgeOfConsent(false);
#if DEBUG
            // Debug builds pretend to be in the EEA so the form can actually be seen;
            // the id is this machine's emulator, printed by UMP in logcat.
            builder = builder.SetConsentDebugSettings(new ConsentDebugSettings.Builder(activity)
                .SetDebugGeography(ConsentDebugSettings.DebugGeography.DebugGeographyEea)
                .AddTestDeviceHashedId("B3EEABB8EE11C2BE770B684D95219ECB")
                .Build());
#endif
            var parameters = builder.Build();

            var information = UserMessagingPlatform.GetConsentInformation(activity);

            information.RequestConsentInfoUpdate(activity, parameters,
                new UpdateSuccessListener(() =>
                    UserMessagingPlatform.LoadAndShowConsentFormIfRequired(activity,
                        new FormDismissedListener(_ => Settle(activity, information)))),
                new UpdateFailureListener(_ => Settle(activity, information)));
        }

        // Runs now if consent is already settled, otherwise once it is.
        public static void RunWhenAllowed(System.Action action)
        {
            if (CanRequestAds)
            {
                action();
                return;
            }

            void Once()
            {
                AdsAllowed -= Once;
                action();
            }

            AdsAllowed += Once;
        }

        // Entry point Google requires so the user can change their mind later.
        public static void ShowPrivacyOptions(Activity activity)
        {
            var information = UserMessagingPlatform.GetConsentInformation(activity);
            UserMessagingPlatform.ShowPrivacyOptionsForm(activity,
                new FormDismissedListener(_ => Settle(activity, information)));
        }

        private static void Settle(Activity activity, IConsentInformation information)
        {
            PrivacyOptionsRequired = information.PrivacyOptionsRequirementStatus ==
                ConsentInformationPrivacyOptionsRequirementStatus.Required;

            if (CanRequestAds || _initialising || !information.CanRequestAds())
                return;

            _initialising = true;
            // Loading an ad before the SDK reports itself ready comes back as no-fill.
            MobileAds.Initialize(activity, new InitialisationListener(() =>
            {
                CanRequestAds = true;
                AdsAllowed?.Invoke();
            }));
        }

        private sealed class InitialisationListener : Java.Lang.Object,
            Android.Gms.Ads.Initialization.IOnInitializationCompleteListener
        {
            private readonly System.Action _action;
            public InitialisationListener(System.Action action) => _action = action;

            public void OnInitializationComplete(Android.Gms.Ads.Initialization.IInitializationStatus status)
                => _action();
        }

        private sealed class UpdateSuccessListener : Java.Lang.Object,
            IConsentInformationOnConsentInfoUpdateSuccessListener
        {
            private readonly System.Action _action;
            public UpdateSuccessListener(System.Action action) => _action = action;
            public void OnConsentInfoUpdateSuccess() => _action();
        }

        private sealed class UpdateFailureListener : Java.Lang.Object,
            IConsentInformationOnConsentInfoUpdateFailureListener
        {
            private readonly System.Action<FormError> _action;
            public UpdateFailureListener(System.Action<FormError> action) => _action = action;
            public void OnConsentInfoUpdateFailure(FormError error) => _action(error);
        }

        private sealed class FormDismissedListener : Java.Lang.Object,
            IConsentFormOnConsentFormDismissedListener
        {
            private readonly System.Action<FormError> _action;
            public FormDismissedListener(System.Action<FormError> action) => _action = action;
            public void OnConsentFormDismissed(FormError error) => _action(error);
        }
    }
}
