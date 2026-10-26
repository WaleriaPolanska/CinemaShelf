using System;
using SQLite;

namespace CinemaShelf;

public class Note
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Text { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.Now;
}

