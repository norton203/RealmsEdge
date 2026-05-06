using RealmsEdge.Shared.Models.Characters;
using RealmsEdge.Shared.Models.Session;

namespace RealmsEdge.Shared.Interfaces
{
    public interface IDatabaseService
    {
        // =====================
        // Initialisation
        // =====================

        Task InitialiseAsync();

        // =====================
        // Characters
        // =====================

        Task<List<PlayerCharacter>> GetAllCharactersAsync();
        Task<PlayerCharacter?> GetCharacterAsync(Guid id);
        Task SaveCharacterAsync(PlayerCharacter character);
        Task DeleteCharacterAsync(Guid id);
        Task<bool> CharacterExistsAsync(Guid id);

        // =====================
        // Save Slots
        // =====================

        /// <summary>
        /// Inserts or replaces the save in the given slot.
        /// Slot 0 is reserved for auto-save.
        /// </summary>
        Task SaveSessionAsync(SessionSaveData save);

        /// <summary>
        /// Returns null if the slot is empty.
        /// </summary>
        Task<SessionSaveData?> LoadSessionAsync(int slot);

        /// <summary>
        /// Returns all occupied slots ordered by slot number.
        /// </summary>
        Task<List<SessionSaveData>> GetAllSaveSlotsAsync();

        /// <summary>
        /// Clears a save slot. Safe to call on an empty slot.
        /// </summary>
        Task DeleteSaveAsync(int slot);
    }
}