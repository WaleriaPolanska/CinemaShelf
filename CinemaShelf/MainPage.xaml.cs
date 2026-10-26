using CinemaShelf.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace CinemaShelf
{
    public partial class MainPage : ContentPage, INotifyPropertyChanged
    {
        private readonly DatabaseService _db;
        // Newest first by default; the Sort menu still switches to any other order.
        private string _sortMode = "date_desc";
        private bool _selectMode = false;
        private bool _showSelection = false;
        private string _formatFilter = "All";
        private List<Movie> _allMovies = new();
        private Action<bool> _selectionModeHandler;
        private CancellationTokenSource _searchCts;
        private int _pendingDeleteMovieId = -1;
        public ObservableCollection<Movie> Movies { get; set; } = new();

        // Cached colors to avoid repeated parsing
        private static readonly Color _purpleLight = Color.FromArgb("#E0AAFF");
        private static readonly Color _purpleDark = Color.FromArgb("#1A0A2E");
        private static readonly Color _red = Color.FromArgb("#FF4444");
        private static readonly Color _goldenDark = Color.FromArgb("#B8860B");
        private static readonly Color _goldenLight = Color.FromArgb("#FFD700");

        public bool ShowSelection
        {
            get => _showSelection;
            set { _showSelection = value; OnPropertyChanged(nameof(ShowSelection)); }
        }

        public MainPage(DatabaseService db)
        {
            InitializeComponent();
            _db = db;
            ShowSelection = SelectionModeService.IsSelectionMode;
            _selectionModeHandler = (enabled) => {
                ShowSelection = enabled;
                if (!enabled) foreach (var m in Movies) m.IsSelected = false;
            };
            SelectionModeService.SelectionModeChanged += _selectionModeHandler;
            MovieAddedService.MovieAdded += () =>
            {
                MainThread.BeginInvokeOnMainThread(() => RefreshMovies());
            };
            BindingContext = this;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _selectMode = SelectionModeService.IsSelectionMode;
            ShowSelection = _selectMode;
            SelectAllRow.IsVisible = _selectMode;
            if (!_selectMode) SelectAllCheckBox.IsChecked = false;
            else SelectAllCheckBox.IsChecked = SelectionModeService.IsSelectAll;
            if (_selectMode)
            {
                ShareButton.Text = "⇩ Share";
                ShareButton.BackgroundColor = _purpleLight;
                ShareButton.TextColor = _purpleDark;
            }
            else
            {
                ShareButton.Text = "⇧ Share";
                ShareButton.BackgroundColor = _purpleDark;
                ShareButton.TextColor = _purpleLight;
            }
            RefreshMovies();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = null;
        }

        private async void LoadFromDb()
        {
            _allMovies = await Task.Run(() => _db.GetMovies().ToList());
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var query = SearchEntry?.Text?.Trim() ?? "";
            var filtered = _allMovies.AsEnumerable();
            if (!string.IsNullOrEmpty(query))
                filtered = filtered.Where(m => m.Title.Contains(query, StringComparison.OrdinalIgnoreCase));
            if (_formatFilter == "Movies")
                filtered = filtered.Where(m => m.ContentType != "TVSeries");
            else if (_formatFilter == "Series")
                filtered = filtered.Where(m => m.ContentType == "TVSeries");
            var all = _sortMode switch
            {
                "az_desc" => filtered.OrderByDescending(m => m.Title, StringComparer.OrdinalIgnoreCase).ToList(),
                "date_desc" => filtered.OrderByDescending(m => m.DateAdded).ToList(),
                "date_asc" => filtered.OrderBy(m => m.DateAdded).ToList(),
                _ => filtered.OrderBy(m => m.Title, StringComparer.OrdinalIgnoreCase).ToList()
            };
            Movies.Clear();
            for (var i = 0; i < all.Count; i++)
            {
                var m = all[i];
                m.IsSelectionMode = _selectMode;
                m.IsSelected = _selectMode && MovieSelectionService.IsSelected(m.Id);
                // Ad card after the first movie, then after every fourth one.
                m.ShowAdAfter = i % 4 == 0;
                Movies.Add(m);
            }
            EmptyLabel.IsVisible = Movies.Count == 0;
        }

        private void RefreshMovies() => LoadFromDb();

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            HideDropdowns();
            var hasText = !string.IsNullOrEmpty(e.NewTextValue);
            SearchEntry.FontAttributes = hasText ? FontAttributes.Bold | FontAttributes.Italic : FontAttributes.None;
            SearchEntry.TextColor = hasText ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#E0AAFF");
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;
            try
            {
                await Task.Delay(250, token);
                if (!token.IsCancellationRequested)
                    ApplyFilters();
            }
            catch (TaskCanceledException) { }
        }

        private void HideDropdowns()
        {
            FormatDropdown.IsVisible = false;
            SettingsDropdown.IsVisible = false;
            SortDropdown.IsVisible = false;
            SortButton.Text = "▼ Sort";
            UpdateFormatButtonArrow();
            UpdateFormatButtonColors();
            UpdateSortButtonColors();
        }

        private async void OnSearchButtonClicked(object sender, EventArgs e)
        {
            await ButtonAnimator.Animate(sender);
            HideDropdowns();
        }

        private async void OnSortClicked(object sender, EventArgs e)
        {
            await ButtonAnimator.Animate(sender);
            FormatDropdown.IsVisible = false;
            SettingsDropdown.IsVisible = false;
            UpdateFormatButtonArrow();
            UpdateFormatButtonColors();
            SortDropdown.IsVisible = !SortDropdown.IsVisible;
            SortButton.Text = SortDropdown.IsVisible ? "▲ Sort" : "▼ Sort";
            UpdateSortButtonColors();
            if (SortDropdown.IsVisible)
                UpdateSortDropdownButtons();
        }

        private void OnSortByNameTapped(object sender, EventArgs e)
        {
            if (_sortMode.StartsWith("az"))
                _sortMode = _sortMode == "az_asc" ? "az_desc" : "az_asc";
            else
                _sortMode = "az_asc";
            UpdateSortDropdownButtons();
            ApplyFilters();
        }

        private void OnSortByDateTapped(object sender, EventArgs e)
        {
            if (_sortMode.StartsWith("date"))
                _sortMode = _sortMode == "date_desc" ? "date_asc" : "date_desc";
            else
                _sortMode = "date_desc";
            UpdateSortDropdownButtons();
            ApplyFilters();
        }

        private void UpdateSortDropdownButtons()
        {
            SortByNameBg.Color = _purpleDark;
            SortByNameLabel.TextColor = _purpleLight;
            SortByDateBg.Color = _purpleDark;
            SortByDateLabel.TextColor = _purpleLight;

            if (_sortMode.StartsWith("az"))
            {
                SortByNameBg.Color = _purpleLight;
                SortByNameLabel.TextColor = _purpleDark;
                SortByNameLabel.Text = _sortMode == "az_asc" ? "A → Z" : "Z → A";
            }
            else if (_sortMode.StartsWith("date"))
            {
                SortByDateBg.Color = _purpleLight;
                SortByDateLabel.TextColor = _purpleDark;
                SortByDateLabel.Text = _sortMode == "date_desc" ? "New → Old" : "Old → New";
            }
        }

        private void OnCheckBoxCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (sender is CheckBox cb && cb.BindingContext is Movie movie)
            {
                MovieSelectionService.SetSelected(movie.Id, false, cb.IsChecked);
            }
        }

        private void SetSelectMode(bool enabled)
        {
            _selectMode = enabled;
            SelectionModeService.SetSelectionMode(enabled);
            ShowSelection = enabled;
            foreach (var m in Movies) m.IsSelectionMode = enabled;
            if (!enabled) foreach (var m in Movies) m.IsSelected = false;
        }

        private async void OnShareClicked(object sender, EventArgs e)
        {
            await ButtonAnimator.AnimateFast(sender);
            HideDropdowns();
            if (_selectMode)
            {
                MovieSelectionService.Clear();
                SetSelectMode(false);
                SelectAllRow.IsVisible = false;
                SelectAllCheckBox.IsChecked = false;
                ShowSelection = false;
                ShareButton.Text = "⇧ Share";
                ShareButton.BackgroundColor = _purpleDark;
                ShareButton.TextColor = _purpleLight;
            }
            else
            {
                SetSelectMode(true);
                SelectAllRow.IsVisible = true;
                ShareButton.Text = "⇩ Share";
                ShareButton.BackgroundColor = _purpleLight;
                ShareButton.TextColor = _purpleDark;
            }
        }

        private async void OnSelectAllClicked(object sender, EventArgs e)
        {
            await ButtonAnimator.Animate(sender);
            HideDropdowns();
            bool allSelected = Movies.All(m => m.IsSelected);
            foreach (var m in Movies)
            {
                m.IsSelected = !allSelected;
                MovieSelectionService.SetSelected(m.Id, false, !allSelected);
            }
            ApplyFilters();
        }

        private async void OnFormatClicked(object sender, EventArgs e)
        {
            await ButtonAnimator.AnimateFast(sender);
            FormatDropdown.IsVisible = !FormatDropdown.IsVisible;
            SettingsDropdown.IsVisible = false;
            SortDropdown.IsVisible = false;
            SortButton.Text = "▼ Sort";
            UpdateFormatButtonArrow();
            UpdateFormatButtonColors();
            UpdateSortButtonColors();
            if (FormatDropdown.IsVisible)
                UpdateFormatDropdownButtons();
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            await ButtonAnimator.Animate(sender);
            SettingsDropdown.IsVisible = !SettingsDropdown.IsVisible;
#if ANDROID
            // Google requires this entry point only where a privacy form applies.
            PrivacyOptionsButton.IsVisible = ConsentManager.PrivacyOptionsRequired;
            PrivacySeparator.IsVisible = ConsentManager.PrivacyOptionsRequired;
#endif
            FormatDropdown.IsVisible = false;
            SortDropdown.IsVisible = false;
            SortButton.Text = "▼ Sort";
            UpdateFormatButtonArrow();
            UpdateFormatButtonColors();
            UpdateSortButtonColors();
        }

        private void OnPrivacyOptionsClicked(object sender, EventArgs e)
        {
            SettingsDropdown.IsVisible = false;
#if ANDROID
            if (Platform.CurrentActivity is Android.App.Activity activity)
                ConsentManager.ShowPrivacyOptions(activity);
#endif
        }

        private async void OnExportClicked(object sender, EventArgs e)
        {
            SettingsDropdown.IsVisible = false;
            var movies = _db.GetMovies();
            var golden = _db.GetGoldenMovies();
            var all = movies.Concat(golden).GroupBy(m => m.Id).Select(g => g.First()).ToList();
            var json = System.Text.Json.JsonSerializer.Serialize(all);
            var fileName = $"CinemaShelf_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, json);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Export CinemaShelf backup",
                File = new ShareFile(filePath)
            });
        }

        private async void OnImportClicked(object sender, EventArgs e)
        {
            SettingsDropdown.IsVisible = false;
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select CinemaShelf backup file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "public.json" } },
                        { DevicePlatform.Android, new[] { "application/json" } },
                        { DevicePlatform.MacCatalyst, new[] { "public.json" } },
                    })
                });
                if (result != null)
                {
                    using var stream = await result.OpenReadAsync();
                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync();
                    var movies = System.Text.Json.JsonSerializer.Deserialize<List<Movie>>(json);
                    if (movies != null)
                    {
                        int imported = 0;
                        int skipped = 0;
                        foreach (var m in movies)
                        {
                            if (_db.MovieExists(m.Title, m.Year))
                            {
                                skipped++;
                                continue;
                            }
                            m.Id = 0;
                            if (!m.IsInMainList && !m.IsGolden)
                                m.IsInMainList = true;
                            _db.AddMovie(m);
                            imported++;
                        }
                        var msg = $"Imported {imported} movies/series.";
                        if (skipped > 0) msg += $"\nSkipped {skipped} duplicates.";
                        ShowInfoPopup("✦", msg);
                        RefreshMovies();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowInfoPopup("⚠", $"Import failed:\n{ex.Message}");
            }
        }

        private void OnFormatItemClicked(object sender, EventArgs e)
        {
            string param = null;
            if (e is TappedEventArgs te)
                param = te.Parameter as string;
            if (param == "Movies")
                _formatFilter = "Movies";
            else if (param == "Series")
                _formatFilter = "Series";
            else
                _formatFilter = "All";
            FormatDropdown.IsVisible = false;
            UpdateFormatButtonArrow();
            UpdateFormatButtonColors();
            ApplyFilters();
        }

        private void UpdateFormatDropdownButtons()
        {
            FormatAllBg.Color = _purpleDark;
            FormatAllLabel.TextColor = _purpleLight;
            FormatMoviesBg.Color = _purpleDark;
            FormatMoviesLabel.TextColor = _purpleLight;
            FormatSeriesBg.Color = _purpleDark;
            FormatSeriesLabel.TextColor = _purpleLight;

            BoxView activeBg;
            Label activeLabel;
            switch (_formatFilter)
            {
                case "Movies": activeBg = FormatMoviesBg; activeLabel = FormatMoviesLabel; break;
                case "Series": activeBg = FormatSeriesBg; activeLabel = FormatSeriesLabel; break;
                default: activeBg = FormatAllBg; activeLabel = FormatAllLabel; break;
            }
            activeBg.Color = _purpleLight;
            activeLabel.TextColor = _purpleDark;
        }

        private void UpdateFormatButtonArrow()
        {
            FormatButton.Text = FormatDropdown.IsVisible ? "▲ Format" : "▼ Format";
        }

        private void UpdateFormatButtonColors()
        {
            if (FormatDropdown.IsVisible)
            {
                FormatButton.BackgroundColor = _purpleLight;
                FormatButton.TextColor = _purpleDark;
            }
            else
            {
                FormatButton.BackgroundColor = _purpleDark;
                FormatButton.TextColor = _purpleLight;
            }
        }

        private void UpdateSortButtonColors()
        {
            if (SortDropdown.IsVisible)
            {
                SortButton.BackgroundColor = _purpleLight;
                SortButton.TextColor = _purpleDark;
            }
            else
            {
                SortButton.BackgroundColor = _purpleDark;
                SortButton.TextColor = _purpleLight;
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await ButtonAnimator.Animate(sender);
            HideDropdowns();
            MovieSelectionService.Clear();
            SetSelectMode(false);
            SelectAllRow.IsVisible = false;
            SelectAllCheckBox.IsChecked = false;
            ShowSelection = false;
            ShareButton.Text = "⇧ Share";
            ShareButton.BackgroundColor = _purpleDark;
            ShareButton.TextColor = _purpleLight;
        }

        private async void OnOkClicked(object sender, EventArgs e)
        {
            await ButtonAnimator.Animate(sender);
            HideDropdowns();
            var selected = MovieSelectionService.GetAllSelected(_db);
            if (selected.Count == 0)
            {
                ShowInfoPopup("⇧", "Select movies to share first.");
                return;
            }
            var lines = selected.Select(m =>
            {
                string url = !string.IsNullOrWhiteSpace(m.ImdbUrl)
                    ? m.ImdbUrl
                    : $"https://www.imdb.com/find/?q={Uri.EscapeDataString(m.Title)}";
                string prefix = m.IsGolden ? "★" : "▷";
                return $"{prefix} {m.Title} ({m.Year}) ★ {m.Rating:F1}\n{url}";
            });
            var text = "■▷ C I N E M A  S H E L F\n\n" +
                string.Join("\n\n", lines) +
                "\n\nDownload the app: (coming soon to the App Store and Google Play)";
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Text = text,
                Title = "Share Movie List"
            });
            MovieSelectionService.Clear();
            SetSelectMode(false);
            SelectAllRow.IsVisible = false;
            SelectAllCheckBox.IsChecked = false;
            ShowSelection = false;
            ShareButton.Text = "⇧ Share";
            ShareButton.BackgroundColor = _purpleDark;
            ShareButton.TextColor = _purpleLight;
            RefreshMovies();
        }

        private async void AddMovie_Clicked(object sender, EventArgs e)
        {
            await ButtonAnimator.Animate(sender, 200);
            HideDropdowns();
            SearchEntry.Text = string.Empty;
#if ANDROID
            InterstitialAds.ShowOnAdd();
#endif
            await Shell.Current.GoToAsync(nameof(AddMoviePage));
        }

        private async void OnNotepadClicked(object sender, EventArgs e)
        {
            await ButtonAnimator.Animate(sender, 200);
            HideDropdowns();
            SearchEntry.Text = string.Empty;
            await Shell.Current.GoToAsync(nameof(NotepadPage));
        }

        private void OnSelectAllCheckChanged(object sender, CheckedChangedEventArgs e)
        {
            HideDropdowns();
            SelectionModeService.IsSelectAll = e.Value;
            foreach (var m in Movies)
            {
                m.IsSelected = e.Value;
                MovieSelectionService.SetSelected(m.Id, false, e.Value);
            }
            ApplyFilters();
        }

        private async void GoldenList_Clicked(object sender, EventArgs e)
        {
            await ButtonAnimator.AnimateFast(sender);
            HideDropdowns();
            SearchEntry.Text = string.Empty;
            await Shell.Current.GoToAsync(nameof(GoldenListPage));
        }

        private async void DeleteMovie_Clicked(object sender, EventArgs e)
        {
            HideDropdowns();
            if (sender is Button btn && btn.CommandParameter is int id)
            {
                await btn.ScaleTo(1.8, 150, Easing.SpringOut);
                await btn.ScaleTo(1.0, 150, Easing.SpringIn);
                var movie = Movies.FirstOrDefault(m => m.Id == id);
                _pendingDeleteMovieId = id;
                DeleteMoviePopupTitle.Text = $"Delete \"{movie?.Title}\"?";
                DeleteMoviePopupSubtitle.Text = movie?.IsGolden == true
                    ? "Movie will be removed only from Home list and will stay in Golden list."
                    : "This action cannot be undone.";
                await ShowDeleteMoviePopupAsync();
            }
        }

        private async void GoldenToggle_Clicked(object sender, EventArgs e)
        {
            HideDropdowns();
            int id = -1;
            VisualElement animTarget = null;
            if (sender is Button btn && btn.CommandParameter is int btnId)
            {
                id = btnId;
                animTarget = btn;
            }
            else if (sender is Border border && border.GestureRecognizers.Count > 0)
            {
                var tap = border.GestureRecognizers[0] as TapGestureRecognizer;
                if (tap?.CommandParameter is int borderId)
                {
                    id = borderId;
                    animTarget = border;
                }
            }
            if (id >= 0 && animTarget != null)
            {
                var movie = Movies.FirstOrDefault(m => m.Id == id);
                if (movie != null && !movie.IsGolden)
                {
                    // Trophy scale animation (original style)
                    await animTarget.ScaleTo(1.8, 150, Easing.SpringOut);
                    await animTarget.ScaleTo(1.0, 150, Easing.SpringIn);

                    _db.ToggleGolden(id, true);
                    RefreshMovies();

                    // Golden button quick flash
                    GoldenListButton.BackgroundColor = Color.FromArgb("#FFD700");
                    await Task.Delay(100);
                    GoldenListButton.BackgroundColor = Color.FromArgb("#B8860B");
                }
            }
        }

        private async void OnMovieTapped(object sender, TappedEventArgs e)
        {
            HideDropdowns();
            VisualElement border = null;
            if (sender is VisualElement ve && ve is Border)
            {
                border = ve;
            }
            else if (sender is VisualElement ve2)
            {
                // ищем Border среди родителей
                var parent = ve2.Parent;
                while (parent != null && border == null)
                {
                    if (parent is Border b) border = b;
                    parent = (parent as Element)?.Parent;
                }
            }
            if (border != null)
            {
                await border.ScaleTo(0.95, 160, Easing.CubicIn);
                await border.ScaleTo(1.0, 160, Easing.CubicOut);
            }
            if (e.Parameter is Movie movie)
            {
                string url;
                if (!string.IsNullOrWhiteSpace(movie.ImdbUrl))
                {
                    url = movie.ImdbUrl;
                }
                else
                {
                    url = $"https://www.imdb.com/find/?q={Uri.EscapeDataString(movie.Title)}";
                }
                await Shell.Current.GoToAsync($"{nameof(AddMoviePage)}?openUrl={Uri.EscapeDataString(url)}");
            }
        }


        private void ShowEmptyPopup()
        {
            EmptyPopup.IsVisible = true;
            Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(2), () => EmptyPopup.IsVisible = false);
        }

        private async void OnClearAllClicked(object sender, EventArgs e)
        {
            await ButtonAnimator.AnimateFast(sender);
            ClearAllButton.BackgroundColor = _red;
            ClearAllButton.TextColor = _purpleDark;
            HideDropdowns();
            if (Movies.Count == 0)
            {
                ClearAllButton.BackgroundColor = _purpleDark;
                ClearAllButton.TextColor = _red;
                ShowEmptyPopup();
                return;
            }
            await ShowClearAllPopupAsync();
        }

        private async void OnClearAllConfirm(object sender, EventArgs e)
        {
            await HideClearAllPopupAsync();
            ClearAllButton.BackgroundColor = _purpleDark;
            ClearAllButton.TextColor = _red;
            _db.ClearAllMovies();
            RefreshMovies();
        }

        private async void OnClearAllCancel(object sender, EventArgs e)
        {
            await HideClearAllPopupAsync();
            ClearAllButton.BackgroundColor = _purpleDark;
            ClearAllButton.TextColor = _red;
        }

        private async Task ShowClearAllPopupAsync()
        {
            ClearAllPopup.Opacity = 0;
            ClearAllPopupContent.Opacity = 0;
            ClearAllPopupContent.Scale = 0.92;
            ClearAllPopup.IsVisible = true;

            await Task.WhenAll(
                ClearAllPopup.FadeTo(1, 140, Easing.CubicOut),
                ClearAllPopupContent.FadeTo(1, 160, Easing.CubicOut),
                ClearAllPopupContent.ScaleTo(1, 180, Easing.SpringOut)
            );
        }

        private async Task HideClearAllPopupAsync()
        {
            if (!ClearAllPopup.IsVisible)
                return;

            await Task.WhenAll(
                ClearAllPopup.FadeTo(0, 120, Easing.CubicIn),
                ClearAllPopupContent.FadeTo(0, 90, Easing.CubicIn),
                ClearAllPopupContent.ScaleTo(0.94, 120, Easing.CubicIn)
            );

            ClearAllPopup.IsVisible = false;
            ClearAllPopup.Opacity = 1;
            ClearAllPopupContent.Opacity = 1;
            ClearAllPopupContent.Scale = 1;
        }

        private async void OnDeleteMovieConfirm(object sender, EventArgs e)
        {
            await HideDeleteMoviePopupAsync();
            if (_pendingDeleteMovieId < 0)
                return;

            _db.DeleteMovie(_pendingDeleteMovieId);
            _pendingDeleteMovieId = -1;
            RefreshMovies();
        }

        private async void OnDeleteMovieCancel(object sender, EventArgs e)
        {
            await HideDeleteMoviePopupAsync();
            _pendingDeleteMovieId = -1;
        }

        private async Task ShowDeleteMoviePopupAsync()
        {
            DeleteMoviePopup.Opacity = 0;
            DeleteMoviePopupContent.Opacity = 0;
            DeleteMoviePopupContent.Scale = 0.92;
            DeleteMoviePopup.IsVisible = true;

            await Task.WhenAll(
                DeleteMoviePopup.FadeTo(1, 140, Easing.CubicOut),
                DeleteMoviePopupContent.FadeTo(1, 160, Easing.CubicOut),
                DeleteMoviePopupContent.ScaleTo(1, 180, Easing.SpringOut)
            );
        }

        private async Task HideDeleteMoviePopupAsync()
        {
            if (!DeleteMoviePopup.IsVisible)
                return;

            await Task.WhenAll(
                DeleteMoviePopup.FadeTo(0, 120, Easing.CubicIn),
                DeleteMoviePopupContent.FadeTo(0, 90, Easing.CubicIn),
                DeleteMoviePopupContent.ScaleTo(0.94, 120, Easing.CubicIn)
            );

            DeleteMoviePopup.IsVisible = false;
            DeleteMoviePopup.Opacity = 1;
            DeleteMoviePopupContent.Opacity = 1;
            DeleteMoviePopupContent.Scale = 1;
        }

        private void ShowInfoPopup(string icon, string text)
        {
            InfoPopupText.Text = text;
            InfoPopup.IsVisible = true;
        }

        private void OnInfoPopupDismissed(object sender, EventArgs e)
        {
            InfoPopup.IsVisible = false;
        }

        private void OnBackgroundTapped(object sender, EventArgs e)
        {
            HideDropdowns();
            SearchEntry.Text = string.Empty;
            SearchEntry.Unfocus();
        }
    }
}

