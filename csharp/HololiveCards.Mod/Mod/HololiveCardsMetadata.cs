using SPTarkov.Server.Core.Models.Spt.Mod;
using SemanticVersioning;

namespace HololiveCards.Mod.Mod;

public class HololiveCardsMetadata : AbstractModMetadata
{
    public override string Name => "HololiveCards";
    public override string Author => "knon + port";
    public override SemanticVersioning.Version Version => SemanticVersioning.Version.Parse("0.2.1");
    public override SemanticVersioning.Version SPTVersion => SemanticVersioning.Version.Parse("4.0.13");
    public override ModType Type => ModType.Server;
}
