# HololiveCards DLL Mod (SPT 4.0.13)

This mod now runs as a **C# DLL server mod** (no JavaScript entrypoint required).

## What is implemented

- Loads all item configs from:
  - `config/cards/*.json`
  - `config/packs/*.json`
- Clones base templates and creates custom items.
- Adds item locales and handbook entries.
- Adds sold items to trader assort (including Prapor booster + card binder).
- Applies ragfair/fence blacklist behavior.
- Injects item probabilities into static loot containers.
- Registers loot-box content into `Loot.randomLootContainers`.
- Keeps compatibility filter patching for container grid filters.

## Build requirements

- .NET SDK 9+ (10.x also works)
- `SPT_DIR` should point to your **SPT root folder** (the folder that contains `Aki_Data`).

Example:

```powershell
$env:SPT_DIR = "C:\Tarkov\SPT"
dotnet build csharp/HololiveCards.Mod/HololiveCards.Mod.csproj -c Release
```

## Why you saw missing assembly errors before

Your previous build attempted to resolve references from the wrong location.
SPT assemblies are under:

- `C:\Tarkov\SPT\Aki_Data\Server\*.dll`

The updated `.csproj` now resolves references from:

- `$(SPT_DIR)\Aki_Data\Server\...`

(and has a fallback relative path for builds started from inside the mod folder).

## Install into SPT

After build, copy these into your mod folder:

- `csharp/HololiveCards.Mod/bin/Release/net9.0/HololiveCards.Mod.dll`
- `config/`
- `bundles/`
- `bundles.json`

Final layout should look like:

```text
SPT\user\mods\hololiveCards\HololiveCards.Mod.dll
SPT\user\mods\hololiveCards\config\...
SPT\user\mods\hololiveCards\bundles\...
SPT\user\mods\hololiveCards\bundles.json
```
