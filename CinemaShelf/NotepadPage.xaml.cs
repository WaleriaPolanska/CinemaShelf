using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace CinemaShelf;

public partial class NotepadPage : ContentPage
{
    private readonly DatabaseService _db;
    public ObservableCollection<Note> Notes { get; set; } = new();

    public NotepadPage(DatabaseService db)
    {
        InitializeComponent();
        _db = db;
        NotesCollection.ItemsSource = Notes;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadNotes();
    }

    private async void LoadNotes()
    {
        var notes = await Task.Run(() => _db.GetNotes());
        Notes.Clear();
        foreach (var n in notes)
            Notes.Add(n);
        EmptyLabel.IsVisible = Notes.Count == 0;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await ButtonAnimator.Animate(sender);
        await Shell.Current.GoToAsync("..");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        await ButtonAnimator.Animate(sender);
        var text = NoteEditor.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        _db.AddNote(new Note { Text = text, DateAdded = DateTime.Now });
        NoteEditor.Text = "";
        LoadNotes();
    }

    private int _pendingDeleteNoteId;

    private async void OnDeleteNoteClicked(object sender, EventArgs e)
    {
        await ButtonAnimator.AnimateFast(sender);
        if (sender is Button btn && btn.CommandParameter is int id)
        {
            _pendingDeleteNoteId = id;
            DeleteNotePopup.IsVisible = true;
        }
    }

    private async void OnDeleteNoteConfirm(object sender, EventArgs e)
    {
        await ButtonAnimator.Animate(sender);
        _db.DeleteNote(_pendingDeleteNoteId);
        DeleteNotePopup.IsVisible = false;
        LoadNotes();
    }

    private async void OnDeleteNoteCancel(object sender, EventArgs e)
    {
        await ButtonAnimator.Animate(sender);
        DeleteNotePopup.IsVisible = false;
    }
}

