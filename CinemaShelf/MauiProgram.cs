using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
#if !MACCATALYST
using Plugin.MauiMtAdmob;
#endif

namespace CinemaShelf;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if !MACCATALYST
        builder.UseMauiMTAdmob();
#endif

#if ANDROID
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<AdBanner, AdBannerHandler>();
            handlers.AddHandler<NativeAdCard, NativeAdCardHandler>();
        });
#endif

        builder.Services.AddSingleton(_ =>
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "movies.db");
            return new DatabaseService(dbPath);
        });
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<GoldenListPage>();
        builder.Services.AddTransient<AddMoviePage>();
        builder.Services.AddTransient<NotepadPage>();

#if IOS
        IOSButtonHandler.Register();
#elif MACCATALYST
        MacCatalystButtonHandler.Register();
#endif

        return builder.Build();
    }
}
