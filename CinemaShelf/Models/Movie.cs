using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;
using SQLite;

namespace CinemaShelf;

public class Movie : INotifyPropertyChanged
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Title { get; set; }
    public string Year { get; set; }
    public string Description { get; set; }
    public string PosterUrl { get; set; }
    public double Rating { get; set; }
    public string Status { get; set; } // "Watched", "Watching", "WantToWatch"
    public string ImdbUrl { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.Now;
    public bool IsGolden { get; set; } = false;
    public bool IsInMainList { get; set; } = true;
    public string ContentType { get; set; } = "Movie"; // "Movie" or "TVSeries"

    [Ignore]
    public string ContentTypeIcon => ContentType == "TVSeries" ? "■" : "▷";
    [Ignore]
    public Color TrophyColor => IsGolden ? Colors.Gray : Colors.Gold;
    [Ignore]
    public bool TrophyEnabled => !IsGolden;
    [Ignore]
    public double TrophyOpacity => IsGolden ? 0.3 : 1.0;
    [Ignore]
    public bool CanRestoreToMain => !IsInMainList;
    [Ignore]
    public double RestoreOpacity => IsInMainList ? 0.0 : 1.0;

    // True for the movies an ad card follows in the list: the first one, then every fourth.
    private bool _showAdAfter;
    [Ignore]
    public bool ShowAdAfter
    {
        get => _showAdAfter;
        set { if (_showAdAfter != value) { _showAdAfter = value; OnPropertyChanged(); } }
    }

    private bool _isSelected;
    [Ignore]
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    private bool _isSelectionMode;
    [Ignore]
    public bool IsSelectionMode
    {
        get => _isSelectionMode;
        set
        {
            if (_isSelectionMode != value)
            {
                _isSelectionMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotSelectionMode));
                OnPropertyChanged(nameof(ShowTrophy));
            }
        }
    }

    [Ignore]
    public bool IsNotSelectionMode => !IsSelectionMode;
    [Ignore]
    public bool ShowTrophy => !IsSelectionMode && !IsGolden;
    [Ignore]
    public bool IsInBothLists => IsGolden && IsInMainList;

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
