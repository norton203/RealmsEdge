
using Microsoft.Extensions.Logging;
using RealmsEdge.Maui.Services;
using RealmsEdge.Shared.Interfaces;
using RealmsEdge.Shared.Models.World;
using RealmsEdge.Shared.Services;
using MudBlazor.Services;

namespace RealmsEdge
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddMudServices();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif
            // Singletons first
            builder.Services.AddSingleton<DiceService>();
            builder.Services.AddSingleton(
                WorldMap.CreateStarterWorld());

            // Scoped services in dependency order
            builder.Services.AddScoped<ISoundService, SoundService>();
            builder.Services.AddScoped<CharacterValidationService>();
            builder.Services.AddScoped<CharacterService>();
            builder.Services.AddScoped<PartyService>();
            builder.Services.AddScoped<WorldService>();
            builder.Services.AddScoped<EncounterService>();
            builder.Services.AddScoped<NavigationService>();
            builder.Services.AddScoped<CombatService>();
            builder.Services.AddScoped<QuestService>();
            builder.Services.AddScoped<GameStateManager>();
            return builder.Build();
        }
    }
}
