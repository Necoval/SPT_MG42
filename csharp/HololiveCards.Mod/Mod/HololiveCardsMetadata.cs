using SemanticVersioning;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace HololiveCards.Mod.Mod;

public sealed record HololiveCardsMetadata : AbstractModMetadata
{
    public override string Name { get; init; } = "HololiveCards";

    public override string Author { get; init; } = "knon + port";

    public override string ModGuid { get; init; } = "com.knon.hololivecards";

    public override string License { get; init; } = "MIT";

    public override string? Url { get; init; } = "https://github.com/knon/hololiveCards";

    public override SemanticVersioning.Version Version { get; init; } = SemanticVersioning.Version.Parse("0.2.3");

    public override SemanticVersioning.Range SptVersion { get; init; } = SemanticVersioning.Range.Parse("4.0.13");

    public override bool? IsBundleMod { get; init; } = false;

    public override List<string> Contributors { get; init; } = [];

    public override Dictionary<string, SemanticVersioning.Range> ModDependencies { get; init; } = new();

    public override List<string> Incompatibilities { get; init; } = [];
}
