using SemanticVersioning;
using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace HololiveCards.Mod.Mod;

public sealed record HololiveCardsMetadata : AbstractModMetadata
{
    public override string Name { get; init; } = "HololiveCards";

    public override string Author { get; init; } = "knon + port";

    public override string ModGuid { get; init; } = "com.knon.hololivecards";

    public override string License { get; init; } = "MIT";

    public override string? Url { get; init; } = "https://github.com/knon/hololiveCards";

    public override Version Version { get; init; } = new("0.2.2");

    public override Range SptVersion { get; init; } = new("4.0.13");

    public override bool? IsBundleMod { get; init; } = false;

    public override List<string>? Contributors { get; init; } = [];

    public override Dictionary<string, Range>? ModDependencies { get; init; } = new();

    public override List<string>? Incompatibilities { get; init; } = [];
}
