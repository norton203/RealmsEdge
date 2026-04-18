using RealmsEdge.Shared.Models.Characters;

namespace RealmsEdge.Shared.Interfaces
{
    /// <summary>
    /// Persistence contract for character storage.
    /// Lives in Shared so CharacterService can depend on it
    /// without referencing any MAUI-specific types.
    /// The concrete implementation (SQLite) lives in the MAUI project.
    /// </summary>
    public interface IDatabaseService
    {
        // =====================
        // Initialisation
        // =====================

        /// <summary>
        /// Creates the database and tables if they do not exist.
        /// Must be called once at startup before any other method.
        /// </summary>
        Task InitialiseAsync();

        // =====================
        // Characters
        // =====================

        Task<List<PlayerCharacter>> GetAllCharactersAsync();

        Task<PlayerCharacter?> GetCharacterAsync(Guid id);

        /// <summary>
        /// Insert or replace — handles both create and update.
        /// </summary>
        Task SaveCharacterAsync(PlayerCharacter character);

        Task DeleteCharacterAsync(Guid id);

        Task<bool> CharacterExistsAsync(Guid id);
    }
}
