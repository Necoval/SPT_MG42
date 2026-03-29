using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace HololiveCards.Mod.Load;

[Injectable(TypePriority = OnLoadOrder.Database + 50)]
public sealed class PostDbLoad : IOnLoad
{
    private const string ModName = "HololiveCards";
    private const string CardsDir = "config/cards";
    private const string PacksDir = "config/packs";
    private const string ModConfigPath = "config/mod_config.json";
    private const string ProbabilitiesPath = "config/probabilities.json";

    private readonly ISptLogger<PostDbLoad> _logger;
    private readonly DatabaseService _databaseService;

    private readonly Dictionary<string, string> _traderIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mechanic"] = "5a7c2eca46aef81a7ca2145d",
        ["skier"] = "58330581ace78e27b8b10cee",
        ["peacekeeper"] = "5935c25fb3acc3127c3d8cd9",
        ["therapist"] = "54cb57776803fa99248b456e",
        ["prapor"] = "54cb50c76803fa8b248b4571",
        ["jaeger"] = "5c0647fdd443bc2504c2d371",
        ["ragman"] = "5ac3b934156ae10c4430e83c"
    };

    private readonly Dictionary<string, string> _currencyIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["roubles"] = "5449016a4bdc2d6f028b456f",
        ["euros"] = "569668774bdc2da2298b4568",
        ["dollars"] = "5696686a4bdc2da3298b456a"
    };

    public PostDbLoad(ISptLogger<PostDbLoad> logger, DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
    }

    public Task OnLoad()
    {
        var modPath = AppContext.BaseDirectory;
        var modConfig = ReadJson(Path.Combine(modPath, ModConfigPath));
        var probabilities = ReadJson(Path.Combine(modPath, ProbabilitiesPath));
        var itemConfigs = LoadItemConfigs(modPath);

        if (itemConfigs.Count == 0)
        {
            _logger.Warning($"[{ModName}] No JSON item configs found under config/cards or config/packs");
            return Task.CompletedTask;
        }

        dynamic tables = _databaseService.GetTables();
        EnsureCompatFilters(tables);

        IDictionary<object, object?> templatesItems = GetDict(tables.templates.items);
        IList handbookItems = GetList(tables.templates.handbook.Items);
        List<IDictionary<string, object?>> allLocales = GetLocaleDicts(tables.locales.global);
        var ragfairBlacklist = ResolveNestedList(tables, "globals", "config", "RagFair", "dynamic", "blacklist", "custom");
        var fenceBlacklist = ResolveNestedList(tables, "traders", "579dc571d53a0658a154fbec", "base", "blacklist");

        var fallbackTrader = GetString(modConfig, "fallback_trader", "ragman");
        var forceEnableRagfair = GetBool(modConfig, "force_enable_ragfair_for_cards", true);
        var enableContainerSpawns = GetBool(modConfig, "enable_container_spawns", true);

        var created = 0;
        foreach (var config in itemConfigs)
        {
            try
            {
                var id = GetString(config, "id", string.Empty);
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var cloneItemId = GetString(config, "clone_item", string.Empty);
                if (!templatesItems.TryGetValue(cloneItemId, out object? cloneTemplate) || cloneTemplate is null)
                {
                    _logger.Warning($"[{ModName}] Clone template {cloneItemId} not found for {id}");
                    continue;
                }

                var clonedItem = CloneDictionary(cloneTemplate);
                ApplyItemProperties(clonedItem, config, forceEnableRagfair);
                templatesItems[id] = clonedItem;

                AddLocaleEntries(allLocales, config, id);
                AddHandbookEntry(handbookItems, config, id);
                AddTraderOffer(tables, config, id, fallbackTrader);
                if (enableContainerSpawns)
                {
                    AddToLootableContainers(tables, config, modConfig, probabilities, id);
                }

                AddToRandomLootContainers(tables, config, id);
                AddItemToTrophyStand(templatesItems, config, id);

                var canSellOnRagfair = forceEnableRagfair || GetBool(config, "can_sell_on_ragfair", false);
                if (!canSellOnRagfair)
                {
                    AddUnique(ragfairBlacklist, id);
                }
                AddUnique(fenceBlacklist, id);

                created++;
            }
            catch (Exception ex)
            {
                _logger.Warning($"[{ModName}] Failed to process item config: {ex.Message}");
            }
        }

        _logger.Info($"[{ModName}] Loaded {created} items from DLL module.");
        return Task.CompletedTask;
    }

    private static JsonElement ReadJson(string path)
    {
        if (!File.Exists(path))
        {
            return JsonDocument.Parse("{}").RootElement;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }

    private List<JsonElement> LoadItemConfigs(string modPath)
    {
        var result = new List<JsonElement>();
        foreach (var dir in new[] { CardsDir, PacksDir })
        {
            var abs = Path.Combine(modPath, dir);
            if (!Directory.Exists(abs))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(abs, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(file));
                    result.Add(doc.RootElement.Clone());
                }
                catch (Exception ex)
                {
                    _logger.Warning($"[{ModName}] Invalid JSON in {file}: {ex.Message}");
                }
            }
        }

        return result;
    }

    private static void EnsureCompatFilters(dynamic tables)
    {
        var templates = GetDict(tables.templates.items);
        foreach (var entry in templates.Values)
        {
            if (entry is not IDictionary<string, object?> item)
            {
                continue;
            }

            var parent = item.TryGetValue("_parent", out var p) ? p?.ToString() : string.Empty;
            var id = item.TryGetValue("_id", out var i) ? i?.ToString() : string.Empty;
            if ((parent != "5448e53e4bdc2d60728b4567" && parent != "5448bf274bdc2dfc2f8b456a") || id == "5c0a794586f77461c458f892")
            {
                continue;
            }

            var props = EnsureObject(item, "_props");
            var grids = EnsureList(props, "Grids");
            if (grids.Count == 0 || grids[0] is not IDictionary<string, object?> grid)
            {
                continue;
            }

            var gridProps = EnsureObject(grid, "_props");
            if (!gridProps.ContainsKey("filters"))
            {
                gridProps["filters"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["Filter"] = new List<object?> { "54009119af1c881c07000029" },
                        ["ExcludedFilter"] = new List<object?> { "" }
                    }
                };
            }
        }
    }

    private void ApplyItemProperties(IDictionary<string, object?> item, JsonElement config, bool forceEnableRagfair)
    {
        var id = GetString(config, "id", string.Empty);
        item["_id"] = id;
        item["_name"] = GetString(config, "item_name", id);
        item["_parent"] = GetString(config, "item_parent", item.TryGetValue("_parent", out var parent) ? parent?.ToString() ?? string.Empty : string.Empty);

        var props = EnsureObject(item, "_props");
        SetIfPresent(config, "item_name", v => { props["Name"] = v; });
        SetIfPresent(config, "item_short_name", v => { props["ShortName"] = v; });
        SetIfPresent(config, "item_description", v => { props["Description"] = v; });
        SetIfPresent(config, "stack_max_size", v => { props["StackMaxSize"] = v; });
        SetIfPresent(config, "item_sound", v => { props["ItemSound"] = v; });
        SetIfPresent(config, "weight", v => { props["Weight"] = v; });
        SetIfPresent(config, "color", v => { props["BackgroundColor"] = v; });
        SetIfPresent(config, "quest_item", v => { props["QuestItem"] = v; });
        SetIfPresent(config, "insurancedisabled", v => { props["InsuranceDisabled"] = v; });
        SetIfPresent(config, "availableforinsurance", v => { props["IsAlwaysAvailableForInsurance"] = v; });
        SetIfPresent(config, "isunremovable", v => { props["IsUnremovable"] = v; });
        SetIfPresent(config, "examinedbydefault", v => { props["ExaminedByDefault"] = v; });
        SetIfPresent(config, "discardingblock", v => { props["DiscardingBlock"] = v; });
        SetIfPresent(config, "isundiscardable", v => { props["IsUndiscardable"] = v; });
        SetIfPresent(config, "isungivable", v => { props["IsUngivable"] = v; });
        SetIfPresent(config, "discardlimit", v => { props["DiscardLimit"] = v; });

        if (config.TryGetProperty("ExternalSize", out var externalSize) && externalSize.ValueKind == JsonValueKind.Object)
        {
            SetIfPresent(externalSize, "width", v => { props["Width"] = v; });
            SetIfPresent(externalSize, "height", v => { props["Height"] = v; });
        }

        var canSellOnRagfair = forceEnableRagfair || GetBool(config, "can_sell_on_ragfair", false);
        props["CanSellOnRagfair"] = canSellOnRagfair;

        if (config.TryGetProperty("item_prefab_path", out var prefabPath))
        {
            var prefab = EnsureObject(props, "Prefab");
            prefab["path"] = prefabPath.GetString() ?? string.Empty;
        }

        if (config.TryGetProperty("gridStructure", out var grid) && grid.ValueKind == JsonValueKind.Array)
        {
            props["Grids"] = CreateGrid(config);
        }

        if (config.TryGetProperty("slotStructure", out var slots) && slots.ValueKind == JsonValueKind.Array)
        {
            props["Slots"] = JsonToObject(slots);
        }
    }

    private static object CreateGrid(JsonElement config)
    {
        if (!config.TryGetProperty("gridStructure", out var gridStructure) || gridStructure.ValueKind != JsonValueKind.Array)
        {
            return new List<object?>();
        }

        var grids = new List<object?>();
        var rowIndex = 0;
        foreach (var row in gridStructure.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array)
            {
                rowIndex++;
                continue;
            }

            var colIndex = 0;
            foreach (var cell in row.EnumerateArray())
            {
                var width = GetInt(cell, "width", 1);
                var height = GetInt(cell, "height", 1);
                var included = GetStringList(cell, "included_filter");
                var excluded = GetStringList(cell, "excluded_filter");
                grids.Add(new Dictionary<string, object?>
                {
                    ["_name"] = $"cell_{rowIndex}_{colIndex}",
                    ["_id"] = Guid.NewGuid().ToString("N"),
                    ["_parent"] = GetString(config, "id", string.Empty),
                    ["_props"] = new Dictionary<string, object?>
                    {
                        ["filters"] = new List<object?>
                        {
                            new Dictionary<string, object?>
                            {
                                ["Filter"] = included.Cast<object?>().ToList(),
                                ["ExcludedFilter"] = excluded.Cast<object?>().ToList()
                            }
                        },
                        ["cellsH"] = width,
                        ["cellsV"] = height,
                        ["minCount"] = 0,
                        ["maxCount"] = 0,
                        ["maxWeight"] = 0,
                        ["isSortingTable"] = false
                    }
                });
                colIndex++;
            }

            rowIndex++;
        }

        return grids;
    }

    private static void AddLocaleEntries(List<IDictionary<string, object?>> allLocales, JsonElement config, string id)
    {
        var name = GetString(config, "item_name", id);
        var shortName = GetString(config, "item_short_name", name);
        var description = GetString(config, "item_description", string.Empty);

        foreach (var locale in allLocales)
        {
            locale[$"{id} Name"] = name;
            locale[$"{id} ShortName"] = shortName;
            locale[$"{id} Description"] = description;
        }
    }

    private static void AddHandbookEntry(IList handbookItems, JsonElement config, string id)
    {
        handbookItems.Add(new Dictionary<string, object?>
        {
            ["Id"] = id,
            ["ParentId"] = GetString(config, "category_id", "5b47574386f77428ca22b2f1"),
            ["Price"] = GetInt(config, "price", 1)
        });
    }

    private void AddTraderOffer(dynamic tables, JsonElement config, string id, string fallbackTrader)
    {
        if (!GetBool(config, "sold", false))
        {
            return;
        }

        var traderName = GetString(config, "trader", fallbackTrader);
        var traderId = _traderIds.TryGetValue(traderName, out var mappedTraderId) ? mappedTraderId : traderName;
        var fallbackTraderId = _traderIds.TryGetValue(fallbackTrader, out var mappedFallbackTraderId) ? mappedFallbackTraderId : "54cb50c76803fa8b248b4571";

        IDictionary<object, object?> traders = GetDict(tables.traders);
        if (!traders.TryGetValue(traderId, out object? traderObj))
        {
            if (!traders.TryGetValue(fallbackTraderId, out traderObj))
            {
                _logger.Warning($"[{ModName}] Could not resolve trader for item {id}");
                return;
            }
        }

        var trader = EnsureDictionary(traderObj);
        var assort = EnsureObject(trader, "assort");
        var items = EnsureList(assort, "items");
        var barterScheme = EnsureObject(assort, "barter_scheme");
        var loyalLevelItems = EnsureObject(assort, "loyal_level_items");

        items.Add(new Dictionary<string, object?>
        {
            ["_id"] = id,
            ["_tpl"] = id,
            ["parentId"] = "hideout",
            ["slotId"] = "hideout",
            ["upd"] = new Dictionary<string, object?>
            {
                ["UnlimitedCount"] = GetBool(config, "unlimited_stock", true),
                ["StackObjectsCount"] = GetInt(config, "stock_amount", 999_999)
            }
        });

        var currency = GetString(config, "currency", "roubles");
        var currencyId = _currencyIds.TryGetValue(currency, out var mappedCurrency) ? mappedCurrency : currency;
        barterScheme[id] = new List<object?>
        {
            new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["count"] = GetInt(config, "price", 1),
                    ["_tpl"] = currencyId
                }
            }
        };

        loyalLevelItems[id] = GetInt(config, "trader_loyalty_level", 1);
    }

    private void AddToLootableContainers(dynamic tables, JsonElement config, JsonElement modConfig, JsonElement probabilities, string itemId)
    {
        if (!GetBool(config, "lootable", false))
        {
            return;
        }

        if (!config.TryGetProperty("loot_locations", out var lootLocations) || lootLocations.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        const string defaultContainerId = "5909d50c86f774659e6aaebe";
        var rarity = GetString(config, "rarity", "C");
        var rarityScale = GetDouble(modConfig, rarity, 0.1d);

        IDictionary<object, object?> locations = GetDict(tables.locations);
        foreach (var mapEntry in lootLocations.EnumerateObject())
        {
            if (!locations.TryGetValue(mapEntry.Name, out object? mapObj))
            {
                continue;
            }

            var map = EnsureDictionary(mapObj);
            var staticLoot = EnsureObject(map, "staticLoot");

            foreach (var containerNode in mapEntry.Value.EnumerateArray())
            {
                var configuredContainerId = containerNode.GetString() ?? string.Empty;
                var selectedContainerId = staticLoot.ContainsKey(configuredContainerId)
                    ? configuredContainerId
                    : (staticLoot.ContainsKey(defaultContainerId) ? defaultContainerId : string.Empty);

                if (string.IsNullOrWhiteSpace(selectedContainerId) || !staticLoot.TryGetValue(selectedContainerId, out var containerObj))
                {
                    continue;
                }

                var maxFound = ResolveMaxFound(probabilities, mapEntry.Name, selectedContainerId);
                if (maxFound <= 0)
                {
                    continue;
                }

                var container = EnsureDictionary(containerObj);
                var itemDistribution = EnsureList(container, "itemDistribution");
                itemDistribution.Add(new Dictionary<string, object?>
                {
                    ["tpl"] = itemId,
                    ["relativeProbability"] = Math.Max(1, (int)Math.Ceiling(maxFound * rarityScale))
                });
            }
        }
    }

    private static int ResolveMaxFound(JsonElement probabilities, string mapName, string containerId)
    {
        if (probabilities.ValueKind != JsonValueKind.Object ||
            !probabilities.TryGetProperty(mapName, out var map) ||
            map.ValueKind != JsonValueKind.Object ||
            !map.TryGetProperty(containerId, out var container) ||
            container.ValueKind != JsonValueKind.Object ||
            !container.TryGetProperty("max_found", out var maxFound))
        {
            return 0;
        }

        return maxFound.ValueKind == JsonValueKind.Number ? maxFound.GetInt32() : 0;
    }

    private static void AddToRandomLootContainers(dynamic tables, JsonElement config, string itemId)
    {
        if (!GetBool(config, "is_loot_box", false) || !config.TryGetProperty("lootContent", out var lootContent))
        {
            return;
        }

        var globals = EnsureDictionary(tables.globals);
        var configRoot = EnsureObject(globals, "config");
        var lootCfg = EnsureObject(configRoot, "Loot");
        var randomLootContainers = EnsureObject(lootCfg, "randomLootContainers");
        randomLootContainers[itemId] = JsonToObject(lootContent);
    }

    private static void AddItemToTrophyStand(IDictionary<object, object?> templatesItems, JsonElement config, string id)
    {
        if (!GetBool(config, "is_trophy", false))
        {
            return;
        }

        var trophyTemplateIds = new[]
        {
            "63dbd45917fff4dee40fe16e",
            "65424185a57eea37ed6562e9",
            "6542435ea57eea37ed6562f0"
        };

        foreach (var trophyTemplateId in trophyTemplateIds)
        {
            if (!templatesItems.TryGetValue(trophyTemplateId, out var templateObj))
            {
                continue;
            }

            var template = EnsureDictionary(templateObj);
            var props = EnsureObject(template, "_props");
            var slots = EnsureList(props, "Slots");
            foreach (var slotObj in slots.OfType<IDictionary<string, object?>>())
            {
                var slotName = slotObj.TryGetValue("_name", out var n) ? n?.ToString() : string.Empty;
                if (string.IsNullOrWhiteSpace(slotName) || !slotName.Contains("bigTrophies", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var slotProps = EnsureObject(slotObj, "_props");
                var filters = EnsureList(slotProps, "filters");
                foreach (var filterObj in filters.OfType<IDictionary<string, object?>>())
                {
                    var filterList = EnsureList(filterObj, "Filter");
                    AddUnique(filterList, id);
                }
            }
        }
    }

    private static List<IDictionary<string, object?>> GetLocaleDicts(dynamic globalLocales)
    {
        var result = new List<IDictionary<string, object?>>();
        if (globalLocales is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                if (entry.Value is IDictionary<string, object?> d)
                {
                    result.Add(d);
                }
                else if (entry.Value is IDictionary anyDict)
                {
                    result.Add(ToStringObjectDict(anyDict));
                }
            }
        }

        return result;
    }

    private static IDictionary<object, object?> GetDict(dynamic obj)
    {
        if (obj is IDictionary<object, object?> typed)
        {
            return typed;
        }

        if (obj is IDictionary raw)
        {
            var converted = new Dictionary<object, object?>();
            foreach (DictionaryEntry entry in raw)
            {
                converted[entry.Key] = entry.Value;
            }

            return converted;
        }

        return new Dictionary<object, object?>();
    }

    private static IList GetList(dynamic obj)
    {
        return obj as IList ?? new List<object?>();
    }

    private static IDictionary<string, object?> ResolveNestedObject(dynamic root, params string[] keys)
    {
        object? current = root;
        foreach (var key in keys)
        {
            if (current is IDictionary<object, object?> d1)
            {
                d1.TryGetValue(key, out current);
            }
            else if (current is IDictionary d2)
            {
                current = d2[key];
            }
            else
            {
                return new Dictionary<string, object?>();
            }
        }

        return EnsureDictionary(current);
    }

    private static IList ResolveNestedList(dynamic root, params string[] keys)
    {
        object? current = root;
        foreach (var key in keys)
        {
            if (current is IDictionary<object, object?> d1)
            {
                d1.TryGetValue(key, out current);
            }
            else if (current is IDictionary d2)
            {
                current = d2[key];
            }
            else
            {
                return new List<object?>();
            }
        }

        return current as IList ?? new List<object?>();
    }

    private static IDictionary<string, object?> EnsureDictionary(object? obj)
    {
        if (obj is IDictionary<string, object?> d)
        {
            return d;
        }

        if (obj is IDictionary raw)
        {
            return ToStringObjectDict(raw);
        }

        return new Dictionary<string, object?>();
    }

    private static IDictionary<string, object?> ToStringObjectDict(IDictionary source)
    {
        var dict = new Dictionary<string, object?>();
        foreach (DictionaryEntry entry in source)
        {
            var key = entry.Key?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(key))
            {
                dict[key] = entry.Value;
            }
        }

        return dict;
    }

    private static IDictionary<string, object?> EnsureObject(IDictionary<string, object?> parent, string key)
    {
        if (parent.TryGetValue(key, out var existing) && existing is IDictionary<string, object?> dict)
        {
            return dict;
        }

        if (parent.TryGetValue(key, out existing) && existing is IDictionary raw)
        {
            var converted = ToStringObjectDict(raw);
            parent[key] = converted;
            return converted;
        }

        var created = new Dictionary<string, object?>();
        parent[key] = created;
        return created;
    }

    private static IList EnsureList(IDictionary<string, object?> parent, string key)
    {
        if (parent.TryGetValue(key, out var existing) && existing is IList list)
        {
            return list;
        }

        var created = new List<object?>();
        parent[key] = created;
        return created;
    }

    private static object? JsonToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => JsonToObject(p.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonToObject).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static IDictionary<string, object?> CloneDictionary(object source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new Dictionary<string, object?>();
    }

    private static void AddUnique(IList list, string value)
    {
        foreach (var entry in list)
        {
            if (string.Equals(entry?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        list.Add(value);
    }

    private static string GetString(JsonElement element, string property, string fallback)
    {
        return element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String
            ? (prop.GetString() ?? fallback)
            : fallback;
    }

    private static int GetInt(JsonElement element, string property, int fallback)
    {
        return element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetInt32()
            : fallback;
    }

    private static double GetDouble(JsonElement element, string property, double fallback)
    {
        return element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetDouble()
            : fallback;
    }

    private static bool GetBool(JsonElement element, string property, bool fallback)
    {
        return element.TryGetProperty(property, out var prop) && (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
            ? prop.GetBoolean()
            : fallback;
    }

    private static List<string> GetStringList(JsonElement element, string property)
    {
        var result = new List<string>();
        if (!element.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }
        }

        return result;
    }

    private static void SetIfPresent(JsonElement element, string property, Action<object?> setter)
    {
        if (!element.TryGetProperty(property, out var prop))
        {
            return;
        }

        setter(JsonToObject(prop));
    }
}
