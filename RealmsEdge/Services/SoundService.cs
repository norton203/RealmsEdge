using Microsoft.JSInterop;
using RealmsEdge.Shared.Enums;
using RealmsEdge.Shared.Interfaces;

namespace RealmsEdge.Maui.Services
{
    public class SoundService : ISoundService, IAsyncDisposable
    {
        private readonly IJSRuntime _js;
        private IJSObjectReference? _module;
        private bool _isMuted = false;

        public SoundService(IJSRuntime js)
        {
            _js = js;
        }

        // Lazy load the JS module on first use
        private async Task<IJSObjectReference> GetModuleAsync()
        {
            _module ??= await _js.InvokeAsync<IJSObjectReference>(
                "import", "./js/soundManager.js");
            return _module;
        }

        public async Task PlayAsync(SoundEffect sound)
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("play", sound.ToString());
        }

        public async Task StopAsync(SoundEffect sound)
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("stop", sound.ToString());
        }

        public async Task StopAllAsync()
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("stopAll");
        }

        public async Task PreloadAsync(SoundEffect sound)
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("preload", sound.ToString());
        }

        public async Task SetMasterVolumeAsync(double volume)
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("setMasterVolume", volume);
        }

        public async Task SetMutedAsync(bool muted)
        {
            _isMuted = muted;
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("setMuted", muted);
        }

        public bool IsMuted => _isMuted;

        public async ValueTask DisposeAsync()
        {
            if (_module != null)
                await _module.DisposeAsync();
        }
    }
}