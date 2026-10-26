using System;

namespace CinemaShelf.Services
{
    public static class MovieAddedService
    {
        public static event Action MovieAdded;

        public static void NotifyMovieAdded() => MovieAdded?.Invoke();
    }
}
