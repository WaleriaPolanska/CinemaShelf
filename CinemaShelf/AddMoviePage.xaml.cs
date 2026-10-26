using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace CinemaShelf
{
	[QueryProperty(nameof(OpenUrl), "openUrl")]
	public partial class AddMoviePage : ContentPage
	{
		private readonly DatabaseService _dbService;
		private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
		private string _currentUrl = "";
		private static bool _hintShown = false;
		private string _lastGoogleQuery = "";
		private string _lastTitleUrl = "";  // remembers the movie title page URL
		private bool _userSearched = false; // true after first search
		private string _openUrl;

		public string OpenUrl
		{
			get => _openUrl;
			set
			{
				_openUrl = Uri.UnescapeDataString(value ?? "");
				if (!string.IsNullOrWhiteSpace(_openUrl))
				{
					Dispatcher.Dispatch(() => BrowserWebView.Source = _openUrl);
				}
			}
		}

		public AddMoviePage(DatabaseService dbService)
		{
			InitializeComponent();
			_dbService = dbService;
			BrowserWebView.Navigating += OnWebViewNavigating;
			ShowHint();
		}

		private CancellationTokenSource _spinnerCts;
		private CancellationTokenSource _saveCts;

		protected override void OnDisappearing()
		{
			base.OnDisappearing();
			_spinnerCts?.Cancel();
			_spinnerCts?.Dispose();
			_spinnerCts = null;
			_saveCts?.Cancel();
			_saveCts?.Dispose();
			_saveCts = null;
		}

		private void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
		{
			var targetUrl = e.Url ?? "";

			// Don't show spinner until user has searched at least once
			if (!_userSearched)
			{
				// Detect first search via Google's search bar
				if (targetUrl.Contains("google.com/search") && _currentUrl.Contains("google.com"))
					_userSearched = true;
				else
					return;
			}

			// Leaving IMDb title page
			if (_currentUrl.Contains("imdb.com/title/") && !targetUrl.Contains("imdb.com/title/"))
			{
				// Going to Google or IMDb find — real exit, show spinner
				if (targetUrl.Contains("google.com") || targetUrl.Contains("imdb.com/find"))
				{
					_lastTitleUrl = "";
					ShowLoadingOverlay();
				}
				// Otherwise it's sub-content linked from the movie page — keep Save, no spinner
			}
			// Navigating TO IMDb title page from non-title
			else if (targetUrl.Contains("imdb.com/title/") && !_currentUrl.Contains("imdb.com/title/") && string.IsNullOrEmpty(_lastTitleUrl))
			{
				ShowLoadingOverlay();
			}
			// Navigating to Google search (user typed in Google's search bar)
			else if (targetUrl.Contains("google.com/search") && !_currentUrl.Contains("imdb.com/title/"))
			{
				ShowLoadingOverlay();
			}
		}

		private async void ShowLoadingOverlay()
		{
			_spinnerCts?.Cancel();
			_spinnerCts?.Dispose();
			_spinnerCts = new CancellationTokenSource();
			var token = _spinnerCts.Token;

			LoadingOverlay.IsVisible = true;
			LoadingOverlay.Opacity = 1;

			// Pulse animation on the star
			try
			{
				while (!token.IsCancellationRequested)
				{
					await LoadingStarLabel.ScaleTo(1.3, 400, Microsoft.Maui.Easing.SinInOut);
					if (token.IsCancellationRequested) break;
					await LoadingStarLabel.ScaleTo(0.8, 400, Microsoft.Maui.Easing.SinInOut);
				}
			}
			catch (TaskCanceledException) { }
		}

		private async void HideLoadingOverlay()
		{
			_spinnerCts?.Cancel();
			LoadingStarLabel.CancelAnimations();
			LoadingStarLabel.Scale = 1;
			await LoadingOverlay.FadeTo(0, 200);
			LoadingOverlay.IsVisible = false;
		}




		private async void OnSearchClicked(object sender, EventArgs e)
		{
			await ButtonAnimator.Animate(sender);
			DoSearch();
		}
		private void OnSearchCompleted(object sender, EventArgs e) => DoSearch();

		// The built-in clear button only shows while the entry has focus, and the field
		// keeps its text after a search, so the cross is drawn as a real button.
		private void OnSearchEntryTextChanged(object sender, TextChangedEventArgs e)
			=> ClearSearchButton.IsVisible = !string.IsNullOrEmpty(e.NewTextValue);

		private void OnClearSearchClicked(object sender, EventArgs e)
		{
			SearchEntry.Text = string.Empty;
			SearchEntry.Focus();
		}

		private void DoSearch()
		{
			var query = SearchEntry.Text;
			if (string.IsNullOrWhiteSpace(query))
				return;
			_userSearched = true;
			// new search — drop state from the previous movie, otherwise a slow/failed
			// query read on the results page leaves the old one in place
			_lastGoogleQuery = "";
			_lastTitleUrl = "";
			var url = "https://www.google.com/search?q=" + Uri.EscapeDataString(query) + "&hl=en&gl=US";
			ShowLoadingOverlay();
			BrowserWebView.Source = url;
			ActionBorder.IsVisible = true;
			ShowHint();
		}

		private async void ShowHint()
		{
			if (_hintShown) return;
			_hintShown = true;
			HintBorder.IsVisible = true;
			await HintBorder.FadeTo(1, 300);
		}

		private async void OnHintDismissed(object sender, EventArgs e)
		{
			await HintBorder.FadeTo(0, 300);
			HintBorder.IsVisible = false;
		}

		private void OnDuplicateDismissed(object sender, EventArgs e)
		{
			DuplicatePopup.IsVisible = false;
		}

		private void ShowDuplicatePopup(string icon, string text)
		{
			DuplicateLabel.Text = text;
			DuplicatePopup.IsVisible = true;
		}

		// Swiping in from the edge, like the system back button, steps back through the
		// browser instead of leaving the page - the nav arrows are gone.
		protected override bool OnBackButtonPressed()
		{
			if (BrowserWebView.CanGoBack)
			{
				BrowserWebView.GoBack();
				return true;
			}

			return base.OnBackButtonPressed();
		}

		private async void OnHomeClicked(object sender, EventArgs e)
		{
			await ButtonAnimator.Animate(sender);
#if ANDROID
			InterstitialAds.ShowOnHome();
#endif
			await Shell.Current.GoToAsync("..");
		}

		// Clicks the "reject"/"decline" control of a cookie banner and never the accepting
		// one. Both Google and IMDb show the same page content after a refusal.
		private const string DeclineConsentScript =
			"(function(){" +
			"var b=document.querySelector('#W0wltc,[data-testid*=reject],[data-testid*=decline],button[id*=reject],button[id*=Reject],button[aria-label*=Reject],button[aria-label*=Decline]');" +
			"if(b){b.click();return;}" +
			"var els=document.querySelectorAll('button,input[type=submit],span[role=button],a[role=button]');" +
			"for(var i=0;i<els.length;i++){" +
			"var t=((els[i].innerText||els[i].value||'')+'').toLowerCase().trim();" +
			"if(t.indexOf('reject')!==-1||t.indexOf('decline')!==-1||t.indexOf('do not consent')!==-1||t.indexOf('odrzu')!==-1){els[i].click();return;}}})()";

		private async void OnWebViewNavigated(object sender, WebNavigatedEventArgs e)
		{
			_currentUrl = e.Url ?? "";
			PageTitleLabel.Text = _currentUrl;

			// Update action button immediately (before any awaits)
			UpdateActionButton();

			// Force mobile viewport and prevent overflow
			await BrowserWebView.EvaluateJavaScriptAsync(
				"(function(){" +
				"var vp=document.querySelector('meta[name=viewport]');" +
				"if(vp){vp.content='width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no';}" +
				"else{var m=document.createElement('meta');m.name='viewport';m.content='width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no';document.head.appendChild(m);}" +
				"document.body.style.overflowX='hidden';document.body.style.maxWidth='100vw';" +
				"document.documentElement.style.overflowX='hidden';" +
				"})()");

			// Dismiss cookie consent dialogs by declining: the pages serve the same content
			// either way, and consenting to tracking is not ours to do on the user's behalf.
			await BrowserWebView.EvaluateJavaScriptAsync(DeclineConsentScript);

			// Consent dialogs render after the page load event, and IMDb builds its own out
			// of spans rather than buttons, so retry a moment later across every site.
			await Task.Delay(1200);
			await BrowserWebView.EvaluateJavaScriptAsync(DeclineConsentScript);
			// Save corrected Google search query when on Google
			if (_currentUrl.Contains("google.com/search"))
			{
				try
				{
					var gq = await BrowserWebView.EvaluateJavaScriptAsync(
						"(document.querySelector('input[name=q]') || document.querySelector('textarea[name=q]'))?.value || ''");
					gq = gq?.Trim('"') ?? "";
					if (!string.IsNullOrWhiteSpace(gq))
						_lastGoogleQuery = gq;
				}
				catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"WebView JS error: {ex.Message}"); }
			}

			// Show save button on IMDb title page, message on find page
			// (already handled by UpdateActionButton above)
		}

		private void UpdateActionButton()
		{
			if (_currentUrl.Contains("imdb.com/title/"))
			{
				_lastTitleUrl = _currentUrl;
				ActionIcon.Text = "✦";
				ActionIcon.TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#FFD700");
				ActionLabel.Text = "S A V E";
				ActionBorder.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#6C3CE1");
				ActionBorder.InputTransparent = false;
				HideLoadingOverlay();
			}
			else if (_currentUrl.Contains("imdb.com/find"))
			{
				_lastTitleUrl = "";
				ActionIcon.Text = "✦";
				ActionIcon.TextColor = Microsoft.Maui.Graphics.Colors.White;
				ActionLabel.Text = "Choose a movie/series";
				ActionBorder.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#333333");
				ActionBorder.InputTransparent = true;
				HideLoadingOverlay();
			}
			else if (_currentUrl.Contains("google.com"))
			{
				_lastTitleUrl = "";
				ActionIcon.Text = "✦";
				ActionIcon.TextColor = Microsoft.Maui.Graphics.Colors.White;
				ActionLabel.Text = "This movie/series";
				ActionBorder.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#6C3CE1");
				ActionBorder.InputTransparent = false;
				if (_currentUrl.Contains("google.com/search"))
				{
					ActionBorder.IsVisible = true;
					HideLoadingOverlay();
				}
				// If not on search results yet, keep spinner until we get to /search
			}
			else if (!string.IsNullOrEmpty(_lastTitleUrl))
			{
				ActionIcon.Text = "✦";
				ActionIcon.TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#FFD700");
				ActionLabel.Text = "S A V E";
				ActionBorder.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#6C3CE1");
				ActionBorder.InputTransparent = false;
			}
			else
			{
				ActionIcon.Text = "✦";
				ActionIcon.TextColor = Microsoft.Maui.Graphics.Colors.White;
				ActionLabel.Text = "This movie/series";
				ActionBorder.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#6C3CE1");
				ActionBorder.InputTransparent = false;
				HideLoadingOverlay();
			}
		}

		private async void OnActionButtonClicked(object sender, EventArgs e)
		{
			await ButtonAnimator.Animate(ActionBorder);
			if (_currentUrl.Contains("imdb.com/title/") || !string.IsNullOrEmpty(_lastTitleUrl))
				await DoSaveMovie(ActionBorder);
			else
				await DoFindMovie(ActionBorder);
		}

		// Step 1: User confirmed this is the movie — extract corrected name from Google, translate, search IMDb
		private async Task DoFindMovie(object sender)
		{
			try
			{
				if (_currentUrl.Contains("imdb.com/title/"))
					return;

				string movieName = "";

				// Use saved Google corrected query (captures typo corrections)
				if (!string.IsNullOrWhiteSpace(_lastGoogleQuery))
					movieName = _lastGoogleQuery;

				// Try live extraction from Google search input if still on Google
				if (string.IsNullOrWhiteSpace(movieName) && _currentUrl.Contains("google.com"))
				{
					try
					{
						var googleQuery = await BrowserWebView.EvaluateJavaScriptAsync(
							"(document.querySelector('input[name=q]') || document.querySelector('textarea[name=q]'))?.value || ''");
						googleQuery = googleQuery?.Trim('"') ?? "";
						if (!string.IsNullOrWhiteSpace(googleQuery))
							movieName = googleQuery;
					}
					catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Google JS error: {ex.Message}"); }
				}

				// Fallback to search bar text
				if (string.IsNullOrWhiteSpace(movieName))
					movieName = SearchEntry.Text?.Trim() ?? "";

				if (string.IsNullOrWhiteSpace(movieName))
				{
					ShowDuplicatePopup("", "Enter a movie name\nin the search bar.");
					return;
				}

				ShowToast("Searching IMDb...");

				// Translate to English for accurate IMDb results
				var translated = await TranslateToEnglish(movieName);
				if (!string.IsNullOrWhiteSpace(translated))
					movieName = translated;

				// Strip trailing "movie"/"film"/"series" only if one word before it
				var stripped = Regex.Replace(movieName, @"(?i)\s+(movie|film|series)\s*$", "").Trim();
				var wc = stripped.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
				if (wc == 1 && !string.IsNullOrWhiteSpace(stripped))
					movieName = stripped;

				// Navigate to IMDb search — user picks the right movie from list
				var imdbSearchUrl = $"https://www.imdb.com/find/?q={Uri.EscapeDataString(movieName)}&s=tt";
				BrowserWebView.Source = imdbSearchUrl;
			}
			catch (Exception ex)
			{
				ShowDuplicatePopup("", "App error");
			}
		}

		// Step 2: Save movie from current IMDb page
		private async Task DoSaveMovie(object sender)
		{
			_saveCts?.Cancel();
			_saveCts?.Dispose();
			_saveCts = new CancellationTokenSource();
			var token = _saveCts.Token;

			try
			{
				// If on sub-page, go back to main title page
				// If on sub-page, go back to main title page
				var isMainTitlePage = _currentUrl.Contains("imdb.com/title/") && 
					!_currentUrl.Contains("/fullcredits") && !_currentUrl.Contains("/mediaviewer") &&
					!_currentUrl.Contains("/videogallery") && !_currentUrl.Contains("/reviews") &&
					!_currentUrl.Contains("/episodes") && !_currentUrl.Contains("/trivia") &&
					!_currentUrl.Contains("/plotsummary") && !_currentUrl.Contains("/releaseinfo");

				if (!isMainTitlePage && !string.IsNullOrEmpty(_lastTitleUrl))
				{
					ShowLoadingOverlay();
					if (BrowserWebView.CanGoBack) BrowserWebView.GoBack();
					// Wait until back on title page
					for (int i = 0; i < 20; i++)
					{
						await Task.Delay(500);
						if (token.IsCancellationRequested) return;
						if (_currentUrl == _lastTitleUrl) break;
					}
					await Task.Delay(1000);
					HideLoadingOverlay();
					if (token.IsCancellationRequested) return;
					ShowToast("Saving...");
				}
				else if (!_currentUrl.Contains("imdb.com/title/"))
				{
					ShowDuplicatePopup("", "Navigate to an IMDb\nmovie page first.");
					return;
				}
				else
				{
					ShowToast("Saving...");
				}

				for (int attempt = 0; attempt < 4; attempt++)
				{
					if (token.IsCancellationRequested) return;
					await Task.Delay(1500);
					if (token.IsCancellationRequested) return;
					var saved = await TryScrapeAndSave();
					if (saved) return;
				}

				if (!token.IsCancellationRequested)
					ShowDuplicatePopup("", "App error");
			}
			catch (Exception ex)
			{
				ShowDuplicatePopup("", "App error");
			}
		}


		private async Task<bool> TryScrapeAndSave()
		{
			// Try JSON-LD first
			var data = await TryParseJsonLd();

			// If JSON-LD failed or incomplete, try JS scraping
			if (data == null || string.IsNullOrWhiteSpace(data.Title))
			{
				data = await TryJsScrape();
			}
			else
			{
				// Fill missing fields from JS
				var jsData = await TryJsScrape();
				if (jsData != null)
				{
					if (string.IsNullOrWhiteSpace(data.Poster)) data.Poster = jsData.Poster;
					if (string.IsNullOrWhiteSpace(data.Rating)) data.Rating = jsData.Rating;
					if (string.IsNullOrWhiteSpace(data.Description)) data.Description = jsData.Description;
					if (string.IsNullOrWhiteSpace(data.Year)) data.Year = jsData.Year;
					if (data.ContentType == "Movie" && jsData.ContentType != "Movie") data.ContentType = jsData.ContentType;
				}
			}

			if (data == null || string.IsNullOrWhiteSpace(data.Title))
				return false;

			// Pages hand back HTML entities: without this a title reads "Don&apos;t Say Good Luck".
			data.Title = DecodeHtml(data.Title);
			data.Description = DecodeHtml(data.Description);

			double.TryParse(data.Rating, System.Globalization.NumberStyles.Any,
				System.Globalization.CultureInfo.InvariantCulture, out var ratingNum);

			if (_dbService.MovieExists(data.Title, data.Year ?? ""))
			{
				DuplicateLabel.Text = $"\"{data.Title}\" ({data.Year})\nis already in your list!";
				DuplicatePopup.IsVisible = true;
				return true;
			}

			var movie = new Movie
			{
				Title = data.Title,
				Year = data.Year ?? "",
				Description = data.Description ?? "",
				PosterUrl = data.Poster ?? "",
				Rating = ratingNum,
				ImdbUrl = _currentUrl.Contains("imdb.com") ? _currentUrl : "",
				ContentType = data.ContentType ?? "Movie",
				Status = "WantToWatch"
			};
			_dbService.AddMovie(movie);
			Services.MovieAddedService.NotifyMovieAdded();
			ShowToast("Done ✓");
#if ANDROID
			// Let the confirmation be seen before a full-screen ad covers it.
			await Task.Delay(2300);
			InterstitialAds.NoteMovieAdded();
#endif
			return true;
		}

		private static string DecodeHtml(string value)
			=> string.IsNullOrEmpty(value) ? value : System.Net.WebUtility.HtmlDecode(value);

		private async Task<ImdbScrapedData> TryParseJsonLd()
		{
			try
			{
				var js = "(function(){ var el = document.querySelector(\"script[type='application/ld+json']\"); if (!el) return ''; return el.textContent; })()";
				var jsonText = await BrowserWebView.EvaluateJavaScriptAsync(js);
				if (string.IsNullOrWhiteSpace(jsonText)) return null;
				jsonText = jsonText.Trim('"');
				jsonText = Regex.Unescape(jsonText);
				
				using var doc = JsonDocument.Parse(jsonText);
				var root = doc.RootElement;

				var type = root.TryGetProperty("@type", out var t) ? t.GetString() : "";
				if (type != "Movie" && type != "TVSeries")
					return null;

				var data = new ImdbScrapedData();
				data.ContentType = type;
				data.Title = root.TryGetProperty("name", out var n) ? n.GetString() : "";
				
				if (root.TryGetProperty("datePublished", out var dp))
				{
					var dpStr = dp.GetString() ?? "";
					data.Year = dpStr.Length >= 4 ? dpStr.Substring(0, 4) : "";
				}

				data.Description = root.TryGetProperty("description", out var desc) ? desc.GetString() : "";
				data.Poster = root.TryGetProperty("image", out var img) ? img.GetString() : "";
				data.Rating = "";

				if (root.TryGetProperty("aggregateRating", out var ar) &&
					ar.TryGetProperty("ratingValue", out var rv))
				{
					data.Rating = rv.ToString();
				}

				return data;
			}
			catch { return null; }
		}

		private async Task<ImdbScrapedData> TryJsScrape()
		{
			try
			{
				var js = "(function() {" +
					"var d = {};" +
					"var h1 = document.querySelector('h1[data-testid=hero__pageTitle] span') || document.querySelector('h1 span');" +
					"d.title = h1 ? h1.innerText : '';" +
					"var typeEl = document.querySelector('meta[property=og:type]');" +
					"d.contentType = (typeEl && typeEl.content && typeEl.content.indexOf('video.tv_show') !== -1) ? 'TVSeries' : 'Movie';" +
					"var links = document.querySelectorAll('a[href*=releaseinfo]');" +
					"d.year = '';" +
					"for (var i = 0; i < links.length; i++) {" +
					"  var txt = links[i].innerText.trim();" +
					"  if (/^\\d{4}$/.test(txt)) { d.year = txt; break; }" +
					"}" +
					"if (!d.year) {" +
					"  var metas = document.querySelectorAll('li.ipc-inline-list__item a');" +
					"  for (var j = 0; j < metas.length; j++) {" +
					"    var mt = metas[j].innerText.trim();" +
					"    if (/^\\d{4}$/.test(mt)) { d.year = mt; break; }" +
					"  }" +
					"}" +
					"var ratingEl = document.querySelector('div[data-testid=hero-rating-bar__aggregate-rating__score] span');" +
					"d.rating = ratingEl ? ratingEl.innerText.trim() : '';" +
					"var descEl = document.querySelector('span[data-testid=plot-xl]') || document.querySelector('span[data-testid=plot-xs_to_m]');" +
					"d.description = descEl ? descEl.innerText.trim() : '';" +
					"var imgEl = document.querySelector('div[data-testid=hero-media__poster] img') || document.querySelector('img.ipc-image');" +
					"d.poster = imgEl ? imgEl.src : '';" +
					"return JSON.stringify(d);" +
					"})()";
				var result = await BrowserWebView.EvaluateJavaScriptAsync(js);
				if (string.IsNullOrWhiteSpace(result)) return null;
				result = result.Trim('"');
				result = Regex.Unescape(result);
				using var doc = JsonDocument.Parse(result);
				var root = doc.RootElement;
				return new ImdbScrapedData
				{
					Title = root.TryGetProperty("title", out var t) ? t.GetString() : "",
					Year = root.TryGetProperty("year", out var y) ? y.GetString() : "",
					Rating = root.TryGetProperty("rating", out var r) ? r.GetString() : "",
					Description = root.TryGetProperty("description", out var d) ? d.GetString() : "",
					Poster = root.TryGetProperty("poster", out var p) ? p.GetString() : "",
					ContentType = root.TryGetProperty("contentType", out var ct) ? ct.GetString() : "Movie"
				};
			}
			catch { return null; }
		}


		private async Task<string> TranslateToEnglish(string text)
		{
			try
			{
				var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=en&dt=t&q={Uri.EscapeDataString(text)}";
				var json = await _httpClient.GetStringAsync(url);
				using var doc = JsonDocument.Parse(json);
				var translated = doc.RootElement[0][0][0].GetString();
				return translated?.Trim();
			}
			catch { return null; }
		}

		private CancellationTokenSource _toastCts;

		// Each toast cancels the previous one: without this the tail of "Saving..."
		// hid the "Done" that had already replaced it.
		private async void ShowToast(string message)
		{
			_toastCts?.Cancel();
			_toastCts?.Dispose();
			_toastCts = new CancellationTokenSource();
			var token = _toastCts.Token;

			ToastLabel.Text = message;
			ToastBorder.IsVisible = true;
			await ToastBorder.FadeTo(1, 200);

			try { await Task.Delay(2000, token); }
			catch (TaskCanceledException) { return; }

			if (token.IsCancellationRequested) return;
			await ToastBorder.FadeTo(0, 300);
			if (token.IsCancellationRequested) return;
			ToastBorder.IsVisible = false;
		}
	}

	public class ImdbScrapedData
	{
		public string Title { get; set; }
		public string Year { get; set; }
		public string Description { get; set; }
		public string Rating { get; set; }
		public string Poster { get; set; }
		public string ContentType { get; set; } = "Movie";
	}
}
