namespace RealmsEdge.Shared.Interfaces
{
    /// <summary>
    /// Abstracts platform preference storage so
    /// GameStateManager (Shared) never touches
    /// MAUI-specific APIs directly.
    /// </summary>
    public interface ISettingsService
    {
        // =====================
        // Auto-Save
        // =====================

        bool AutoSaveEnabled { get; set; }

        // =====================
        // Sound
        // (wired up later — stub for now)
        // =====================

        bool SoundMuted { get; set; }
        float MasterVolume { get; set; }

        // =====================
        // Helpers
        // =====================

        void Reset();
    }
}