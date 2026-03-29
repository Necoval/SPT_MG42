using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Mods.Interfaces;

namespace HololiveCards.Mod.Load;

[Injectable]
public class OnLoad(ILogger<OnLoad> logger) : IOnLoad
{
    public void OnLoad()
    {
        logger.LogInformation("[HololiveCards] DLL bootstrap loaded.");
        logger.LogInformation("[HololiveCards] NOTE: Full item/trader/loot migration from JS to DLL is still in progress.");
    }
}
