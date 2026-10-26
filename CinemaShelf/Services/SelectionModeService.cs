using System;

namespace CinemaShelf.Services
{
    public static class SelectionModeService
    {
        public static bool IsSelectionMode { get; private set; } = false;
        public static bool IsSelectAll { get; set; } = false;
        public static event Action<bool> SelectionModeChanged;

        public static void SetSelectionMode(bool enabled)
        {
            if (IsSelectionMode != enabled)
            {
                IsSelectionMode = enabled;
                if (!enabled) IsSelectAll = false;
                SelectionModeChanged?.Invoke(enabled);
            }
        }
    }
}
