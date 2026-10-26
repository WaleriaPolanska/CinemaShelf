using System.Collections.Generic;

namespace CinemaShelf.Services
{
    public static class MovieSelectionService
    {
        private static readonly HashSet<int> _selectedIds = new();

        public static void SetSelected(int id, bool isGolden, bool selected)
        {
            if (selected)
                _selectedIds.Add(id);
            else
                _selectedIds.Remove(id);
        }

        public static bool IsSelected(int id) => _selectedIds.Contains(id);

        public static void Clear() => _selectedIds.Clear();

        public static List<Movie> GetAllSelected(DatabaseService db)
        {
            if (_selectedIds.Count == 0) return new List<Movie>();
            return db.GetMoviesByIds(_selectedIds);
        }
    }
}
