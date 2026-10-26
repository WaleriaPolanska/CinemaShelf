using CinemaShelf.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace CinemaShelf
{
    public partial class GoldenListPage : ContentPage
    {
        private readonly DatabaseService _db;
        // Newest first by default; the Sort menu still switches to any other order.
        private string _sortMode = "date_desc";
        private bool _selectMode = false;
        private string _typeFilter = "All";
        private List<Movie> _allMovies = new();
        public ObservableCollection<Movie> Movies { get; set; } = new();
        private bool _showSelection = false;
        private bool _firstSearchShown = false;
        private Action<bool> _selectionModeHandler;
        private CancellationTokenSource _searchCts;
        private int _pendingRemoveGoldenId = -1;

        private static readonly Color _gold = Color.FromArgb("#FFD700");
        private static readonly Color _goldDark = Color.FromArgb("#1A1400");
        private static readonly Color _red = Color.FromArgb("#FF4444");
        private static readonly Color _purpleDark = Color.FromArgb("#1A0A2E");

        public bool ShowSelection
        {
            get => _showSelection;
            set { _showSelection = value; OnPropertyChanged(nameof(ShowSelection)); }
        }

        public GoldenListPage(DatabaseService db)
        {
            InitializeComponent();
            _db = db;
            ShowSelection = SelectionModeService.IsSelectionMode;
            _selectionModeHandler = (enabled) => {
                ShowSelection = enabled;
                OkButton.IsVisible = Movies.Any(m => m.IsSelected);
                if (!enabled) foreach (var m in Movies) m.IsSelected = false;
            };
            SelectionModeService.SelectionModeChanged += _selectionModeHandler;
            BindingContext = this;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _selectMode = SelectionModeService.IsSelectionMode;
            ShowSelection = _selectMode;
            OkButton.IsVisible = _selectMode;
            SelectAllRow.IsVisible = _selectMode;
            if (!_selectMode) SelectAllCheckBox.IsChecked = false;
            else SelectAllCheckBox.IsChecked = SelectionModeService.IsSelectAll;
            if (_selectMode)
            {
                ShareButton.Text = "⇩ Share";
                ShareButton.BackgroundColor = _gold;
                ShareButton.TextColor = _goldDark;
            }
            else
            {
                ShareButton.Text = "⇧ Share";
                ShareButton.BackgroundColor = _goldDark;
                ShareButton.TextColor = _gold;
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
            _allMovies = await Task.Run(() => _db.GetGoldenMovies().ToList());
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var query = SearchBar?.Text?.Trim() ?? "";
            var filtered = _allMovies.AsEnumerable();
            if (!string.IsNullOrEmpty(query))
                filtered = filtered.Where(m => m.Title.Contains(query, StringComparison.OrdinalIgnoreCase));
            if (_typeFilter == "Movies")
                filtered = filtered.Where(m => m.ContentType != "TVSeries");
            else if (_typeFilter == "Series")
                filtered = filtered.Where(m => m.ContentType == "TVSeries");
            var all = _sortMode switch
            {
                "az_desc" => filtered.OrderByDescending(m => m.Title, StringComparer.OrdinalIgnoreCase).ToList(),
                "date_desc" => filtered.OrderByDescending(m => m.DateAdded).ToList(),
                "date_asc" => filtered.OrderBy(m => m.DateAdded).ToList(),
                _ => filtered.OrderBy(m => m.Title, StringComparer.OrdinalIgnoreCase).ToList()
            };
            Movies.Clear();
            foreach (var m in all)
            {
                m.IsSelected = MovieSelectionService.IsSelected(m.Id);
                Movies.Add(m);
            }
            EmptyLabel.IsVisible = Movies.Count == 0;
        }

        private void RefreshMovies() => LoadFromDb();

        private void ShowFirstSearchOverlay()
        {
            if (!_firstSearchShown)
            {
                FirstSearchOverlay.IsVisible = true;
                _firstSearchShown = true;
                Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(2), () => FirstSearchOverlay.IsVisible = false);
            }
        }

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            ShowFirstSearchOverlay();
            var hasText = !string.IsNullOrEmpty(e.NewTextValue);
            SearchBar.FontAttributes = hasText ? FontAttributes.Bold | FontAttributes.Italic : FontAttributes.None;
            SearchBar.TextColor = hasText ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#E0AAFF");
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

        private void OnSortClicked(object sender, EventArgs e)
        {
            FormatDropdown.IsVisible = false;
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
            SortByNameBg.Color = _goldDark;
            SortByNameLabel.TextColor = _gold;
            SortByDateBg.Color = _goldDark;
            SortByDateLabel.TextColor = _gold;

            if (_sortMode.StartsWith("az"))
            {
                SortByNameBg.Color = _gold;
                SortByNameLabel.TextColor = _goldDark;
                SortByNameLabel.Text = _sortMode == "az_asc" ? "A → Z" : "Z → A";
            }
            else if (_sortMode.StartsWith("date"))
            {
                SortByDateBg.Color = _gold;
                SortByDateLabel.TextColor = _goldDark;
                SortByDateLabel.Text = _sortMode == "date_desc" ? "New → Old" : "Old → New";
            }
        }

        private void UpdateSortButtonColors()
        {
            if (SortDropdown.IsVisible)
            {
                SortButton.BackgroundColor = _gold;
                SortButton.TextColor = _goldDark;
            }
            else
            {
                SortButton.BackgroundColor = _goldDark;
                SortButton.TextColor = _gold;
            }
        }

        private void OnFormatClicked(object sender, EventArgs e)
        {
            SortDropdown.IsVisible = false;
            SortButton.Text = "▼ Sort";
            UpdateSortButtonColors();
            FormatDropdown.IsVisible = !FormatDropdown.IsVisible;
            UpdateFormatButtonArrow();
            UpdateFormatButtonColors();
            if (FormatDropdown.IsVisible)
                UpdateFormatDropdownButtons();
        }

        private void OnFormatItemClicked(object sender, EventArgs e)
        {
            string param = null;
            if (e is TappedEventArgs te)
                param = te.Parameter as string;
            if (param == "Movies")
                _typeFilter = "Movies";
            else if (param == "Series")
                _typeFilter = "Series";
            else
                _typeFilter = "All";
            FormatDropdown.IsVisible = false;
            UpdateFormatButtonArrow();
            UpdateFormatButtonColors();
            ApplyFilters();
        }

        private void UpdateFormatDropdownButtons()
        {
            FormatAllBg.Color = _goldDark;
            FormatAllLabel.TextColor = _gold;
            FormatMoviesBg.Color = _goldDark;
            FormatMoviesLabel.TextColor = _gold;
            FormatSeriesBg.Color = _goldDark;
            FormatSeriesLabel.TextColor = _gold;

            BoxView activeBg;
            Label activeLabel;
            switch (_typeFilter)
            {
                case "Movies": activeBg = FormatMoviesBg; activeLabel = FormatMoviesLabel; break;
                case "Series": activeBg = FormatSeriesBg; activeLabel = FormatSeriesLabel; break;
                default: activeBg = FormatAllBg; activeLabel = FormatAllLabel; break;
            }
            activeBg.Color = _gold;
            activeLabel.TextColor = _goldDark;
        }

        private void UpdateFormatButtonArrow()
        {
            FormatButton.Text = FormatDropdown.IsVisible ? "▲ Format" : "▼ Format";
        }

        private void UpdateFormatButtonColors()
        {
            if (FormatDropdown.IsVisible)
            {
                FormatButton.BackgroundColor = _gold;
                FormatButton.TextColor = _goldDark;
            }
            else
            {
                FormatButton.BackgroundColor = _goldDark;
                FormatButton.TextColor = _gold;
            }
        }


        private void SetSelectMode(bool enabled)
        {
            _selectMode = enabled;
            SelectionModeService.SetSelectionMode(enabled);
            ShowSelection = enabled;
            foreach (var m in Movies) m.IsSelected = false;
            OkButton.IsVisible = false;
        }

        private void OnCheckBoxCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (sender is CheckBox cb && cb.BindingContext is Movie movie)
            {
                MovieSelectionService.SetSelected(movie.Id, true, cb.IsChecked);
                OkButton.IsVisible = MovieSelectionService.GetAllSelected(_db).Count > 0;
            }
        }

        private async void OnShareClicked(object sender, EventArgs e)
        {
            await ButtonAnimator.AnimateFast(sender);
            HideDropdowns();
            if (_selectMode)
            {
                MovieSelectionService.Clear();
                SetSelectMode(false);
                OkButton.IsVisible = false;
                SelectAllRow.IsVisible = false;
                SelectAllCheckBox.IsChecked = false;
                ShowSelection = false;
                ShareButton.Text = "⇧ Share";
                ShareButton.BackgroundColor = _goldDark;
                ShareButton.TextColor = _gold;
            }
            else
            {
                SetSelectMode(true);
                OkButton.IsVisible = true;
                SelectAllRow.IsVisible = true;
                ShareButton.Text = "⇩ Share";
                ShareButton.BackgroundColor = _gold;
                ShareButton.TextColor = _goldDark;
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
                MovieSelectionService.SetSelected(m.Id, true, !allSelected);
            }
            OkButton.IsVisible = MovieSelectionService.GetAllSelected(_db).Count > 0;
            ApplyFilters();
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await ButtonAnimator.Animate(sender);
            HideDropdowns();
            MovieSelectionService.Clear();
            SetSelectMode(false);
            OkButton.IsVisible = false;
            SelectAllRow.IsVisible = false;
            SelectAllCheckBox.IsChecked = false;
            ShowSelection = false;
            ShareButton.Text = "⇧ Share";
            ShareButton.BackgroundColor = _goldDark;
            ShareButton.TextColor = _gold;
        }

        private void ShowEmptyPopup()
        {
            EmptyPopup.IsVisible = true;
            Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(2), () => EmptyPopup.IsVisible = false);
        }

        private async void OnOkClicked(object sender, EventArgs e)
        {
            await ButtonAnimator.Animate(sender);
            HideDropdowns();
            if (Movies.Count == 0)
            {
                ShowEmptyPopup();
                return;
            }
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
                string prefix = m.IsGolden ? "\u2605" : "▷";
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
            OkButton.IsVisible = false;
            SelectAllRow.IsVisible = false;
            SelectAllCheckBox.IsChecked = false;
            ShowSelection = false;
            ShareButton.Text = "⇧ Share";
            ShareButton.BackgroundColor = _goldDark;
            ShareButton.TextColor = _gold;
            RefreshMovies();
        }

        private async void RemoveFromGolden_Clicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is int id)
            {
                await btn.ScaleTo(1.8, 150, Easing.SpringOut);
                await btn.ScaleTo(1.0, 150, Easing.SpringIn);

                var movie = Movies.FirstOrDefault(m => m.Id == id);
                _pendingRemoveGoldenId = id;
                DeleteGoldenPopupTitle.Text = $"Delete \"{movie?.Title}\"?";
                DeleteGoldenPopupSubtitle.Text = movie?.IsInMainList == true
                    ? "Movie will be removed only from Golden list and will stay on Home list."
                    : "This action cannot be undone.";
                await ShowDeleteGoldenPopupAsync();
            }
        }

        private async void OnDeleteGoldenConfirm(object sender, EventArgs e)
        {
            await HideDeleteGoldenPopupAsync();
            if (_pendingRemoveGoldenId < 0)
                return;

            _db.RemoveFromGolden(_pendingRemoveGoldenId);
            _pendingRemoveGoldenId = -1;
            RefreshMovies();
        }

        private async void OnDeleteGoldenCancel(object sender, EventArgs e)
        {
            await HideDeleteGoldenPopupAsync();
            _pendingRemoveGoldenId = -1;
        }

        private async Task ShowDeleteGoldenPopupAsync()
        {
            DeleteGoldenPopup.Opacity = 0;
            DeleteGoldenPopupContent.Opacity = 0;
            DeleteGoldenPopupContent.Scale = 0.92;
            DeleteGoldenPopup.IsVisible = true;

            await Task.WhenAll(
                DeleteGoldenPopup.FadeTo(1, 140, Easing.CubicOut),
                DeleteGoldenPopupContent.FadeTo(1, 160, Easing.CubicOut),
                DeleteGoldenPopupContent.ScaleTo(1, 180, Easing.SpringOut)
            );
        }

        private async Task HideDeleteGoldenPopupAsync()
        {
            if (!DeleteGoldenPopup.IsVisible)
                return;

            await Task.WhenAll(
                DeleteGoldenPopup.FadeTo(0, 120, Easing.CubicIn),
                DeleteGoldenPopupContent.FadeTo(0, 90, Easing.CubicIn),
                DeleteGoldenPopupContent.ScaleTo(0.94, 120, Easing.CubicIn)
            );

            DeleteGoldenPopup.IsVisible = false;
            DeleteGoldenPopup.Opacity = 1;
            DeleteGoldenPopupContent.Opacity = 1;
            DeleteGoldenPopupContent.Scale = 1;
        }

        private void OnSelectAllCheckChanged(object sender, CheckedChangedEventArgs e)
        {
            SelectionModeService.IsSelectAll = e.Value;
            foreach (var m in Movies)
            {
                m.IsSelected = e.Value;
                MovieSelectionService.SetSelected(m.Id, true, e.Value);
            }
            OkButton.IsVisible = MovieSelectionService.GetAllSelected(_db).Count > 0;
            ApplyFilters();
        }

        private async void OnHomeClicked(object sender, EventArgs e)
        {
            if (sender is View view)
            {
                await view.ScaleTo(0.92, 140, Easing.CubicOut);
                await view.ScaleTo(1.0, 140, Easing.CubicIn);
            }
            SortDropdown.IsVisible = false;
            FormatDropdown.IsVisible = false;
            SearchBar.Text = string.Empty;
            await Shell.Current.GoToAsync("..");
        }

        private async void RestoreToMain_Clicked(object sender, EventArgs e)
        {
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

            if (id < 0 || animTarget == null)
                return;

            await animTarget.ScaleTo(1.8, 150, Easing.SpringOut);
            await animTarget.ScaleTo(1.0, 150, Easing.SpringIn);

            _db.RestoreToMainList(id);
            RefreshMovies();
        }

        private async void OnMovieTapped(object sender, TappedEventArgs e)
        {
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
            _db.ClearAllGoldenMovies();
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
            SortDropdown.IsVisible = false;
            FormatDropdown.IsVisible = false;
            SearchBar.Text = string.Empty;
            SearchBar.Unfocus();
        }
    }
}
