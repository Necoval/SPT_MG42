# HololiveCards DLL Mod (SPT 4.0.13)

This mod is now **DLL-only** (no JavaScript loader required).

## What is implemented

- Loads all item configs from:
  - `config/cards/*.json`
  - `config/packs/*.json`
- Clones base templates and creates custom items.
- Adds locales and handbook entries.
- Adds sold items to trader assort (booster + binder on Prapor via config).
- Applies ragfair/fence blacklist rules.
- Injects static loot probabilities.
- Registers loot-box reward pools in random loot containers.
- Applies compatibility filter fixes for missing container filters.

## Build

From mod root:

```powershell
dotnet build csharp/HololiveCards.Mod/HololiveCards.Mod.csproj -c Release
```

### Notes

- Build now uses NuGet package references for SPT 4.0.13 assemblies (`SPTarkov.Server.Core`, `SPTarkov.DI`, `SPTarkov.Common`), so you do not need to point `SPT_DIR` at local DLLs.
- The project explicitly compiles only:
  - `Load/Bootstrap.cs`
  - `Load/PostDbLoad.cs`
  - `Mod/HololiveCardsMetadata.cs`

  This prevents stale local files (for example an old `Load/OnLoad.cs`) from being compiled accidentally.

## Install into SPT

Copy these into `SPT\user\mods\hololiveCards\`:

- `csharp/HololiveCards.Mod/bin/Release/net9.0/HololiveCards.Mod.dll`
- `config/`
- `bundles/`
- `bundles.json`
