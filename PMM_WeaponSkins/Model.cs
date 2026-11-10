using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace PMM_WeaponSkins;

// ------------------------------------------------------------------ JSON from WeaponSkinsApi

internal sealed class CatalogDto
{
	[JsonPropertyName("stamp")] public int Stamp { get; set; }
	[JsonPropertyName("names")] public Dictionary<string, string> Names { get; set; } = new();
	[JsonPropertyName("weapons")] public List<WeaponDto> Weapons { get; set; } = new();
	[JsonPropertyName("paints")] public Dictionary<string, List<PaintDto>> Paints { get; set; } = new();
	[JsonPropertyName("knives")] public List<DefDto> Knives { get; set; } = new();
	[JsonPropertyName("gloves")] public List<DefDto> Gloves { get; set; } = new();
	[JsonPropertyName("agents")] public List<AgentDto> Agents { get; set; } = new();
	[JsonPropertyName("music")] public List<IdDto> Music { get; set; } = new();
	[JsonPropertyName("pins")] public List<IdDto> Pins { get; set; } = new();
	[JsonPropertyName("stickers")] public List<IdDto> Stickers { get; set; } = new();
	[JsonPropertyName("charms")] public List<IdDto> Charms { get; set; } = new();
}

internal sealed class WeaponDto
{
	[JsonPropertyName("d")] public int Def { get; set; }
	[JsonPropertyName("n")] public string Name { get; set; } = "";
	[JsonPropertyName("c")] public string Category { get; set; } = "";
}

internal sealed class PaintDto
{
	[JsonPropertyName("p")] public int Paint { get; set; }
	[JsonPropertyName("n")] public string Name { get; set; } = "";
	[JsonPropertyName("mn")] public float Min { get; set; }
	[JsonPropertyName("mx")] public float Max { get; set; } = 1f;
}

internal sealed class DefDto
{
	[JsonPropertyName("d")] public int Def { get; set; }
	[JsonPropertyName("n")] public string Name { get; set; } = "";
}

internal sealed class AgentDto
{
	[JsonPropertyName("m")] public string Model { get; set; } = "";
	[JsonPropertyName("n")] public string Name { get; set; } = "";
	[JsonPropertyName("t")] public int Team { get; set; }
	[JsonPropertyName("f")] public string Faction { get; set; } = "";
}

internal sealed class IdDto
{
	[JsonPropertyName("i")] public int Id { get; set; }
	[JsonPropertyName("n")] public string Name { get; set; } = "";
	[JsonPropertyName("g")] public string? Group { get; set; }
	/// <summary>Stickers: type (Autograph, Team, Event, Other), effect and tournament name.</summary>
	[JsonPropertyName("k")] public string? Kind { get; set; }
	[JsonPropertyName("e")] public string? Effect { get; set; }
	[JsonPropertyName("v")] public string? Event { get; set; }
}

internal sealed class LoadoutDto
{
	[JsonPropertyName("music")] public int Music { get; set; }
	[JsonPropertyName("pin")] public int Pin { get; set; }
	[JsonPropertyName("t")] public TeamDto T { get; set; } = new();
	[JsonPropertyName("ct")] public TeamDto CT { get; set; } = new();

	public TeamDto For(int team) => team == 3 ? CT : T;
}

internal sealed class TeamDto
{
	[JsonPropertyName("knife")] public int Knife { get; set; }
	[JsonPropertyName("gd")] public int GloveDef { get; set; }
	[JsonPropertyName("gp")] public int GlovePaint { get; set; }
	[JsonPropertyName("gw")] public float GloveWear { get; set; }
	[JsonPropertyName("gs")] public int GloveSeed { get; set; }
	[JsonPropertyName("agent")] public string? Agent { get; set; }
	[JsonPropertyName("w")] public Dictionary<string, EntryDto> Weapons { get; set; } = new();
	[JsonPropertyName("saved")] public Dictionary<string, Dictionary<string, EntryDto>> Saved { get; set; } = new();

	public EntryDto? Equipped(int def) =>
		Weapons.TryGetValue(def.ToString(CultureInfo.InvariantCulture), out var entry) ? entry : null;

	/// <summary>The paint equipped on a weapon, 0 when it has no skin.</summary>
	public int Paint(int def) => Equipped(def)?.Paint ?? 0;

	public EntryDto? SavedEntry(int def, int paint) =>
		Saved.TryGetValue(def.ToString(CultureInfo.InvariantCulture), out var byPaint) &&
		byPaint.TryGetValue(paint.ToString(CultureInfo.InvariantCulture), out var entry) ? entry : null;
}

internal sealed class EntryDto
{
	[JsonPropertyName("p")] public int Paint { get; set; }
	[JsonPropertyName("w")] public float Wear { get; set; }
	[JsonPropertyName("s")] public int Seed { get; set; }
	[JsonPropertyName("tag")] public string? Tag { get; set; }
	[JsonPropertyName("st")] public int StatTrak { get; set; } = -1;
	[JsonPropertyName("stk")] public List<StickerDto>? Stickers { get; set; }
	[JsonPropertyName("ch")] public CharmDto? Charm { get; set; }
}

internal sealed class StickerDto
{
	[JsonPropertyName("sl")] public int Slot { get; set; }
	[JsonPropertyName("i")] public int Id { get; set; }
	[JsonPropertyName("w")] public float Wear { get; set; }
	[JsonPropertyName("sc")] public float Scale { get; set; } = 1f;
	[JsonPropertyName("r")] public float Rotation { get; set; }
	[JsonPropertyName("x")] public float X { get; set; }
	[JsonPropertyName("y")] public float Y { get; set; }
	[JsonPropertyName("sh")] public int Schema { get; set; } = -1;
}

internal sealed class CharmDto
{
	[JsonPropertyName("i")] public int Id { get; set; }
	[JsonPropertyName("p")] public int Pattern { get; set; }
	[JsonPropertyName("x")] public float X { get; set; }
	[JsonPropertyName("y")] public float Y { get; set; }
	[JsonPropertyName("z")] public float Z { get; set; }
}

// ------------------------------------------------------------------ menu model

/// <summary>One picture card: a skin, an agent, a music kit, a sticker...</summary>
internal sealed class Item
{
	public int Id;
	public string Key = "";
	public string Name = "";
	public string Art = "";
	public int Rarity;
	public float Min;
	public float Max = 1f;
	public string Group = "";
	/// <summary>Stickers: tournament name, empty for regular capsule stickers.</summary>
	public string Event = "";
	/// <summary>Position in the catalogue, 0 = newest.</summary>
	public int Ord;
	/// <summary>Name without the "Sticker | " / "Charm | " prefix, for name sorting.</summary>
	public string SortName = "";
}

internal sealed record WeaponInfo(int Def, string Cat, int Team, string FallbackName);

internal static class Weapons
{
	public static readonly string[] Cats = { "pistol", "smg", "shotgun", "rifle", "sniper", "mg" };
	public const int Zeus = 31;

	// team: 0 both, 2 T only, 3 CT only
	public static readonly WeaponInfo[] All =
	{
		new(4, "pistol", 2, "Glock-18"), new(61, "pistol", 3, "USP-S"), new(32, "pistol", 3, "P2000"),
		new(36, "pistol", 0, "P250"), new(2, "pistol", 0, "Dual Berettas"), new(3, "pistol", 3, "Five-SeveN"),
		new(30, "pistol", 2, "Tec-9"), new(63, "pistol", 0, "CZ75-Auto"), new(1, "pistol", 0, "Desert Eagle"),
		new(64, "pistol", 0, "R8 Revolver"),
		new(17, "smg", 2, "MAC-10"), new(34, "smg", 3, "MP9"), new(33, "smg", 0, "MP7"), new(23, "smg", 0, "MP5-SD"),
		new(24, "smg", 0, "UMP-45"), new(19, "smg", 0, "P90"), new(26, "smg", 0, "PP-Bizon"),
		new(35, "shotgun", 0, "Nova"), new(25, "shotgun", 0, "XM1014"), new(29, "shotgun", 2, "Sawed-Off"), new(27, "shotgun", 3, "MAG-7"),
		new(13, "rifle", 2, "Galil AR"), new(10, "rifle", 3, "FAMAS"), new(7, "rifle", 2, "AK-47"), new(16, "rifle", 3, "M4A4"),
		new(60, "rifle", 3, "M4A1-S"), new(39, "rifle", 2, "SG 553"), new(8, "rifle", 3, "AUG"),
		new(40, "sniper", 0, "SSG 08"), new(9, "sniper", 0, "AWP"), new(11, "sniper", 2, "G3SG1"), new(38, "sniper", 3, "SCAR-20"),
		new(14, "mg", 0, "M249"), new(28, "mg", 0, "Negev"),
	};

	public static readonly Dictionary<int, WeaponInfo> ByDef = All.ToDictionary(w => w.Def);

	public static bool UsableBy(WeaponInfo w, int team) => w.Team == 0 || w.Team == team;

	public static bool IsKnife(int def) => def is 42 or 59 or 500 or 503 or (>= 505 and <= 526);

	public static int DefaultKnife(int team) => team == 2 ? 59 : 42;
}

/// <summary>Rarity and base-picture table generated from the WeaponSkins data (pmm_ws_items.json).</summary>
internal sealed class ItemTable
{
	private sealed class Dto
	{
		[JsonPropertyName("sk")] public Dictionary<string, int> Skins { get; set; } = new();
		[JsonPropertyName("ag")] public Dictionary<string, int> Agents { get; set; } = new();
		[JsonPropertyName("mk")] public Dictionary<string, int> Music { get; set; } = new();
		[JsonPropertyName("pn")] public Dictionary<string, int> Pins { get; set; } = new();
		[JsonPropertyName("st")] public Dictionary<string, int> Stickers { get; set; } = new();
		[JsonPropertyName("kc")] public Dictionary<string, int> Charms { get; set; } = new();
		[JsonPropertyName("bw")] public List<int> BaseWeapons { get; set; } = new();
	}

	private Dto _dto = new();
	private HashSet<int> _base = new();

	public void Load(string directory, ILogger logger)
	{
		var path = Path.Combine(directory, "pmm_ws_items.json");
		try
		{
			_dto = JsonSerializer.Deserialize<Dto>(File.ReadAllText(path)) ?? new Dto();
			_base = _dto.BaseWeapons.ToHashSet();
		}
		catch (Exception ex)
		{
			logger.LogWarning("pmm_ws_items.json could not be read ({Error}). Cards show without pictures and rarity.", ex.Message);
		}
	}

	public int Skin(int def, int paint) => _dto.Skins.TryGetValue($"{def}:{paint}", out var r) ? r : 0;
	public bool HasSkinArt(int def, int paint) => _dto.Skins.ContainsKey($"{def}:{paint}");
	public int Agent(string key) => _dto.Agents.TryGetValue(key, out var r) ? r : 0;
	public bool HasAgentArt(string key) => _dto.Agents.ContainsKey(key);
	public int Music(int id) => _dto.Music.TryGetValue(id.ToString(CultureInfo.InvariantCulture), out var r) ? r : 0;
	public int Pin(int id) => _dto.Pins.TryGetValue(id.ToString(CultureInfo.InvariantCulture), out var r) ? r : 0;
	public int Sticker(int id) => _dto.Stickers.TryGetValue(id.ToString(CultureInfo.InvariantCulture), out var r) ? r : 0;
	public int Charm(int id) => _dto.Charms.TryGetValue(id.ToString(CultureInfo.InvariantCulture), out var r) ? r : 0;
	public bool HasBase(int def) => _base.Contains(def);
	public bool HasMusicArt(int id) => _dto.Music.ContainsKey(id.ToString(CultureInfo.InvariantCulture));
	public bool HasPinArt(int id) => _dto.Pins.ContainsKey(id.ToString(CultureInfo.InvariantCulture));
	public bool HasStickerArt(int id) => _dto.Stickers.ContainsKey(id.ToString(CultureInfo.InvariantCulture));
	public bool HasCharmArt(int id) => _dto.Charms.ContainsKey(id.ToString(CultureInfo.InvariantCulture));
}

/// <summary>The WeaponSkins catalogue turned into card lists.</summary>
internal sealed class Catalog
{
	public int Stamp = -1;
	public readonly Dictionary<int, string> Names = new();
	public readonly Dictionary<int, List<Item>> Paints = new();
	public readonly List<Item> Knives = new();
	public readonly List<Item> GloveTypes = new();
	public readonly List<Item> AgentsT = new();
	public readonly List<Item> AgentsCT = new();
	public readonly List<(string Key, string Name)> FactionsT = new();
	public readonly List<(string Key, string Name)> FactionsCT = new();
	public readonly List<Item> Music = new();
	public readonly List<Item> Pins = new();
	public readonly List<Item> Stickers = new();
	public readonly List<Item> Charms = new();
	public readonly Dictionary<int, Item> StickerById = new();
	/// <summary>Tournaments with stickers, newest first.</summary>
	public readonly List<(string Name, int Count)> StickerEvents = new();
	/// <summary>Stickers that belong to no tournament (capsules, operations, collections).</summary>
	public int RegularStickers;

	/// <summary>"Sticker | s1mple (Gold) | Cologne 2026" -> "s1mple (Gold) | Cologne 2026".</summary>
	private static string SortKey(string name)
	{
		var bar = name.IndexOf('|');
		return (bar >= 0 ? name[(bar + 1)..] : name).Trim();
	}
	public readonly Dictionary<int, Item> CharmById = new();
	public readonly Dictionary<string, Item> AgentByModel = new(StringComparer.OrdinalIgnoreCase);

	public bool Loaded => Stamp >= 0;

	public static string AgentKey(string model)
	{
		var name = model.Replace('\\', '/');
		var slash = name.LastIndexOf('/');
		if (slash >= 0)
			name = name[(slash + 1)..];
		return name.EndsWith(".vmdl", StringComparison.OrdinalIgnoreCase) ? name[..^5] : name;
	}

	public static string SkinArt(int def, int paint) => $"sk-{def}-{paint}";

	public string Name(int def)
	{
		if (Names.TryGetValue(def, out var name) && name.Length > 0)
			return name;
		return Weapons.ByDef.TryGetValue(def, out var info) ? info.FallbackName : "#" + def;
	}

	public List<Item> PaintsOf(int def) => Paints.TryGetValue(def, out var list) ? list : new List<Item>();

	public Item? Paint(int def, int paint) => Paints.TryGetValue(def, out var list) ? list.Find(p => p.Id == paint) : null;

	public string BaseArt(int def, ItemTable table) => table.HasBase(def) ? "bw-" + def : "";

	public void Fill(CatalogDto dto, ItemTable table)
	{
		Names.Clear();
		Paints.Clear();
		Knives.Clear();
		GloveTypes.Clear();
		AgentsT.Clear();
		AgentsCT.Clear();
		FactionsT.Clear();
		FactionsCT.Clear();
		Music.Clear();
		Pins.Clear();
		Stickers.Clear();
		Charms.Clear();
		StickerById.Clear();
		CharmById.Clear();
		AgentByModel.Clear();

		foreach (var (key, name) in dto.Names)
		{
			if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var def))
				Names[def] = name;
		}
		foreach (var weapon in dto.Weapons)
			Names.TryAdd(weapon.Def, weapon.Name);

		foreach (var (key, list) in dto.Paints)
		{
			if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var def))
				continue;
			var items = new List<Item>(list.Count);
			foreach (var p in list)
			{
				if (p.Paint <= 0)
					continue;
				items.Add(new Item
				{
					Id = p.Paint,
					Name = p.Name,
					Art = table.HasSkinArt(def, p.Paint) ? SkinArt(def, p.Paint) : "",
					Rarity = table.Skin(def, p.Paint),
					Min = p.Min,
					Max = p.Max <= 0f ? 1f : p.Max
				});
			}
			Paints[def] = items;
		}

		foreach (var knife in dto.Knives)
		{
			Knives.Add(new Item
			{
				Id = knife.Def,
				Name = knife.Name,
				Art = BaseArt(knife.Def, table),
				Rarity = 6
			});
		}

		foreach (var glove in dto.Gloves)
		{
			var paints = PaintsOf(glove.Def);
			GloveTypes.Add(new Item
			{
				Id = glove.Def,
				Name = glove.Name,
				Art = paints.Count > 0 ? paints[0].Art : BaseArt(glove.Def, table),
				Rarity = 6,
				Group = paints.Count.ToString(CultureInfo.InvariantCulture)
			});
		}

		foreach (var agent in dto.Agents)
		{
			var key = AgentKey(agent.Model);
			var item = new Item
			{
				Key = agent.Model,
				Name = agent.Name,
				Art = table.HasAgentArt(key) ? "ag-" + key : "",
				Rarity = table.Agent(key),
				Group = FactionKey(agent.Model)
			};
			(agent.Team == 3 ? AgentsCT : AgentsT).Add(item);
			AgentByModel[agent.Model] = item;
			var factions = agent.Team == 3 ? FactionsCT : FactionsT;
			var index = factions.FindIndex(f => f.Key == item.Group);
			var label = string.IsNullOrWhiteSpace(agent.Faction) ? item.Group : agent.Faction;
			if (index < 0)
				factions.Add((item.Group, label));
			else if (label.Length < factions[index].Name.Length)
				factions[index] = (item.Group, label);
		}

		foreach (var kit in dto.Music)
			Music.Add(new Item { Id = kit.Id, Name = kit.Name, Art = table.HasMusicArt(kit.Id) ? "mk-" + kit.Id : "", Rarity = table.Music(kit.Id) });
		foreach (var pin in dto.Pins)
			Pins.Add(new Item { Id = pin.Id, Name = pin.Name, Art = table.HasPinArt(pin.Id) ? "pn-" + pin.Id : "", Rarity = table.Pin(pin.Id), Group = pin.Group ?? "" });
		foreach (var sticker in dto.Stickers)
		{
			var item = new Item
			{
				Id = sticker.Id, Name = sticker.Name, Art = table.HasStickerArt(sticker.Id) ? "st-" + sticker.Id : "",
				Rarity = table.Sticker(sticker.Id), Event = (sticker.Event ?? "").Trim(), SortName = SortKey(sticker.Name),
			};
			Stickers.Add(item);
			StickerById[item.Id] = item;
		}
		foreach (var charm in dto.Charms)
		{
			var item = new Item { Id = charm.Id, Name = charm.Name, Art = table.HasCharmArt(charm.Id) ? "kc-" + charm.Id : "", Rarity = table.Charm(charm.Id), SortName = SortKey(charm.Name) };
			Charms.Add(item);
			CharmById[item.Id] = item;
		}

		// newest first: the catalogue lists stickers and charms oldest first
		Stickers.Reverse();
		Charms.Reverse();
		for (var i = 0; i < Stickers.Count; i++)
			Stickers[i].Ord = i;
		for (var i = 0; i < Charms.Count; i++)
			Charms[i].Ord = i;

		// tournaments, newest first (order of their newest sticker), with sticker counts
		var events = new Dictionary<string, int>(StringComparer.Ordinal);
		StickerEvents.Clear();
		RegularStickers = 0;
		foreach (var item in Stickers)
		{
			if (item.Event.Length == 0)
			{
				RegularStickers++;
				continue;
			}
			if (events.TryGetValue(item.Event, out var n))
			{
				StickerEvents[n] = (item.Event, StickerEvents[n].Count + 1);
				continue;
			}
			events[item.Event] = StickerEvents.Count;
			StickerEvents.Add((item.Event, 1));
		}
		Stamp = dto.Stamp;
	}

	/// <summary>Agents are grouped by their model folder (tm_professional, ctm_st6...).</summary>
	private static string FactionKey(string model)
	{
		var parts = model.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
		return parts.Length >= 2 ? parts[^2] : model;
	}
}
