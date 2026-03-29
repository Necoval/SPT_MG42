using System.Threading.Tasks;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Mods.Interfaces;

namespace HololiveCards.Mod.Load;

[Injectable(TypePriority = OnLoadOrder.PreSptModLoader + 5)]
public sealed class Bootstrap : IOnLoad
{
    private readonly ISptLogger _logger;

    public Bootstrap(ISptLogger logger)
    {
        _logger = logger;
    }

    public Task OnLoad()
    {
        _logger.Info("[HololiveCards] DLL bootstrap loaded.");
        return Task.CompletedTask;
    }
}
