using RealmsEdge.Shared.Interfaces;

namespace RealmsEdge.Maui.Services
{
    /// <summary>
    /// Persists user preferences via MAUI's Preferences API.
    /// Survives app restarts. Stored in platform-native
    /// key-value storage (NSUserDefaults / SharedPreferences).
    /// </summary>
    public class SettingsService : ISettingsService
    {
        // =====================
        // Keys
        // =====================

        private const string KeyAutoSave = "setting_autosave";
        private const string KeySoundMuted = "setting_muted";
        private const string KeyMasterVolume = "setting_volume";

        // =====================
        // Auto-Save
        // =====================

        public bool AutoSaveEnabled
        {
            get => Preferences.Default.Get(KeyAutoSave, true);
            set => Preferences.Default.Set(KeyAutoSave, value);
        }

        // =====================
        // Sound
        // =====================

        public bool SoundMuted
        {
            get => Preferences.Default.Get(KeySoundMuted, false);
            set => Preferences.Default.Set(KeySoundMuted, value);
        }

        public float MasterVolume
        {
            get => Preferences.Default.Get(KeyMasterVolume, 0.8f);
            set => Preferences.Default.Set(
                KeyMasterVolume,
                Math.Clamp(value, 0f, 1f));
        }

        // =====================
        // Reset
        // =====================

        public void Reset()
        {
            Preferences.Default.Remove(KeyAutoSave);
            Preferences.Default.Remove(KeySoundMuted);
            Preferences.Default.Remove(KeyMasterVolume);
        }
    }
}