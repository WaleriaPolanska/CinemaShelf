using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;

namespace CinemaShelf;

public class DatabaseService : IDisposable
{
    private readonly SQLiteConnection _db;

    public DatabaseService(string dbPath)
    {
        _db = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
        _db.CreateTable<Movie>();
        _db.CreateTable<Note>();
    }

    public void Dispose()
    {
        _db?.Close();
        _db?.Dispose();
    }

    public void RemoveDuplicates()
    {
        var movies = _db.Table<Movie>().ToList();
        var duplicates = movies
            .GroupBy(m => new { m.Title, m.Year })
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.OrderBy(m => m.Id).Skip(1))
            .ToList();
        foreach (var dup in duplicates)
            _db.Delete<Movie>(dup.Id);
    }

    public List<Movie> GetMovies()
    {
        return _db.Table<Movie>().Where(m => m.IsInMainList).ToList();
    }

    public void AddMovie(Movie movie)
    {
        _db.Insert(movie);
    }

    public bool MovieExists(string title, string year)
    {
        return _db.Table<Movie>().Any(m => m.Title == title && m.Year == year);
    }

    public void DeleteMovie(int id)
    {
        var movie = _db.Find<Movie>(id);
        if (movie == null) return;
        if (movie.IsGolden)
        {
            movie.IsInMainList = false;
            _db.Update(movie);
        }
        else
        {
            _db.Delete<Movie>(id);
        }
    }

    public void RemoveFromGolden(int id)
    {
        var movie = _db.Find<Movie>(id);
        if (movie == null) return;
        if (movie.IsInMainList)
        {
            movie.IsGolden = false;
            _db.Update(movie);
        }
        else
        {
            _db.Delete<Movie>(id);
        }
    }

    public List<Movie> GetGoldenMovies()
    {
        return _db.Table<Movie>().Where(m => m.IsGolden).ToList();
    }

    public void ToggleGolden(int id, bool isGolden)
    {
        var movie = _db.Find<Movie>(id);
        if (movie != null)
        {
            movie.IsGolden = isGolden;
            _db.Update(movie);
        }
    }

    public void ClearAllMovies()
    {
        _db.Execute("UPDATE Movie SET IsInMainList = 0 WHERE IsInMainList = 1 AND IsGolden = 1");
        _db.Execute("DELETE FROM Movie WHERE IsInMainList = 1 AND IsGolden = 0");
    }

    public void ClearAllGoldenMovies()
    {
        _db.Execute("UPDATE Movie SET IsGolden = 0 WHERE IsGolden = 1 AND IsInMainList = 1");
        _db.Execute("DELETE FROM Movie WHERE IsGolden = 1 AND IsInMainList = 0");
    }

    public void RestoreToMainList(int id)
    {
        var movie = _db.Find<Movie>(id);
        if (movie != null)
        {
            movie.IsInMainList = true;
            _db.Update(movie);
        }
    }

    public List<Movie> GetMoviesByIds(IEnumerable<int> ids)
    {
        if (ids == null || !ids.Any()) return new List<Movie>();
        var idList = ids.ToList();
        var placeholders = string.Join(",", idList);
        return _db.Query<Movie>($"SELECT * FROM Movie WHERE Id IN ({placeholders})");
    }

    public List<Note> GetNotes()
    {
        return _db.Table<Note>().OrderByDescending(n => n.DateAdded).ToList();
    }

    public void AddNote(Note note)
    {
        _db.Insert(note);
    }

    public void UpdateNote(Note note)
    {
        _db.Update(note);
    }

    public void DeleteNote(int id)
    {
        _db.Delete<Note>(id);
    }
}
