# CinemaShelf

Personal list of movies and TV series. The user finds a title in a built-in browser, the app extracts the data from the IMDb page and saves a card: title, year, rating, poster and description.

Android app built with **.NET MAUI** on **.NET 10**. There is no backend: everything is stored on the device in SQLite.

## Screens

| Screen | What it does |
|---|---|
| MainPage | movie list, search, sorting, filter by type, export and import |
| AddMoviePage | built-in browser to find and save a movie (IMDb page parsing) |
| GoldenListPage | favourites ("golden list") |
| NotepadPage | notes |

## Tech stack

C# · .NET 10 · .NET MAUI · SQLite (sqlite-net-pcl) · WebView + HTML parsing · Google Mobile Ads · Google User Messaging Platform (GDPR consent)

Target framework `net10.0-android36.0`, minSdk 23, targetSdk 36.

## Build

```bash
# debug build
dotnet build CinemaShelf/CinemaShelf.csproj -f net10.0-android36.0 -c Debug

# install on an emulator
adb install -r CinemaShelf/bin/Debug/net10.0-android36.0/com.cinemashelf.app-Signed.apk
```

## Notes

- Ad unit IDs in this repository are Google's public **test IDs**, so a build from source only shows test ads.
- No signing keys or credentials are stored in the repository; `.gitignore` excludes `*.keystore`, `*.jks` and `google-services.json`.
