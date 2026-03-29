# HololiveCards DLL Port (WIP)

This repository originally shipped as a JavaScript server mod.

To support setups that rely on assembly discovery (`*.Mod.dll` under `user/mods/<mod>`), this folder adds a C# mod scaffold:

- `csharp/HololiveCards.Mod/HololiveCards.Mod.csproj`
- `csharp/HololiveCards.Mod/Mod/HololiveCardsMetadata.cs`
- `csharp/HololiveCards.Mod/Load/OnLoad.cs`

## Build

1. Set your SPT installation path:

   ```powershell
   $env:SPT_DIR="C:\Tarkov\SPT"
   ```

2. Build:

   ```powershell
   dotnet build csharp/HololiveCards.Mod/HololiveCards.Mod.csproj -c Release
   ```

3. Copy output `HololiveCards.Mod.dll` to:

   `SPT\user\mods\hololiveCards\`

Along with:

- `config/`
- `bundles/`
- `bundles.json`

## Current status

The DLL currently provides bootstrap loading/logging only.
The full functional port (items, trader offers, ragfair behavior, loot injection, profile migration) is pending migration from the JS implementation.
