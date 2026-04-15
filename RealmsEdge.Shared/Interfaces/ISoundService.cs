using RealmsEdge.Shared.Enums;

namespace RealmsEdge.Shared.Interfaces
{
    public interface ISoundService
    {
        Task PlayAsync(SoundEffect sound);
        Task StopAsync(SoundEffect sound);
        Task StopAllAsync();
        Task PreloadAsync(SoundEffect sound);
        Task SetMasterVolumeAsync(double volume);
        Task SetMutedAsync(bool muted);
        bool IsMuted { get; }
    }
}