using SPTarkov.Server.Core.Mods;
using SPTarkov.Server.Core.Models.Static;
using SemanticVersioning;

namespace HololiveCards.Mod.Mod;

public class HololiveCardsMetadata : AbstractModMetadata
{
    public override string Name => "HololiveCards";
    public override string Author => "knon + port";
    public override Version Version => Version.Parse("0.2.0");
    public override Version SPTVersion => Version.Parse("4.0.13");
    public override ModType Type => ModType.Server;
}
