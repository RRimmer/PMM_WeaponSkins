using System.Globalization;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Logging;

namespace PMM_WeaponSkins;

/// <summary>Which menu sections are shown (config "Sections"). Hidden ones disappear from the home screen, the side list and the commands.</summary>
public sealed class SectionsConfig
{
	[JsonPropertyName("Agents")] public bool Agents { get; set; } = true;
	[JsonPropertyName("Gloves")] public bool Gloves { get; set; } = true;
	[JsonPropertyName("Knife")] public bool Knife { get; set; } = true;
	[JsonPropertyName("Zeus")] public bool Zeus { get; set; } = true;
	[JsonPropertyName("Music")] public bool Music { get; set; } = true;
	[JsonPropertyName("Pins")] public bool Pins { get; set; } = true;
	[JsonPropertyName("Pistols")] public bool Pistols { get; set; } = true;
	[JsonPropertyName("SMGs")] public bool Smgs { get; set; } = true;
	[JsonPropertyName("Shotguns")] public bool Shotguns { get; set; } = true;
	[JsonPropertyName("Rifles")] public bool Rifles { get; set; } = true;
	[JsonPropertyName("Snipers")] public bool Snipers { get; set; } = true;
	[JsonPropertyName("MachineGuns")] public bool MachineGuns { get; set; } = true;
}

/// <summary>
/// Who may open a section (config "Access"). An empty list = everyone. Otherwise the player needs one of the rules:
/// "vip" - any VIP, "vip:GROUP" - that VIP group, "@css/flag" - an admin flag, "#css/group" - an admin group.
/// A section the player may not open stays visible, with a lock.
/// </summary>
public sealed class AccessConfig
{
	[JsonPropertyName("Agents")] public List<string> Agents { get; set; } = new();
	[JsonPropertyName("Gloves")] public List<string> Gloves { get; set; } = new();
	[JsonPropertyName("Knife")] public List<string> Knife { get; set; } = new();
	[JsonPropertyName("Zeus")] public List<string> Zeus { get; set; } = new();
	[JsonPropertyName("Music")] public List<string> Music { get; set; } = new();
	[JsonPropertyName("Pins")] public List<string> Pins { get; set; } = new();
	[JsonPropertyName("Pistols")] public List<string> Pistols { get; set; } = new();
	[JsonPropertyName("SMGs")] public List<string> Smgs { get; set; } = new();
	[JsonPropertyName("Shotguns")] public List<string> Shotguns { get; set; } = new();
	[JsonPropertyName("Rifles")] public List<string> Rifles { get; set; } = new();
	[JsonPropertyName("Snipers")] public List<string> Snipers { get; set; } = new();
	[JsonPropertyName("MachineGuns")] public List<string> MachineGuns { get; set; } = new();
}

/// <summary>
/// Who may use each skin setting (config "Features"), same rules as "Access". A setting the player may not use stays
/// visible, greyed with a lock, and the skin is saved with its default (no StatTrak, no name tag, lowest wear, pattern 0,
/// no stickers / charm).
/// </summary>
public sealed class FeaturesConfig
{
	[JsonPropertyName("StatTrak")] public List<string> StatTrak { get; set; } = new();
	[JsonPropertyName("NameTag")] public List<string> NameTag { get; set; } = new();
	[JsonPropertyName("Stickers")] public List<string> Stickers { get; set; } = new();
	[JsonPropertyName("Charms")] public List<string> Charms { get; set; } = new();
	[JsonPropertyName("Wear")] public List<string> Wear { get; set; } = new();
	[JsonPropertyName("Pattern")] public List<string> Pattern { get; set; } = new();
}

/// <summary>Visible sections and the weapons, knives and gloves hidden inside them (config "Sections", "HiddenWeapons").</summary>
public partial class Plugin
{
	/// <summary>Class names for "HiddenWeapons", by definition index. The "weapon_" prefix may be left out in the config.</summary>
	private static readonly Dictionary<string, int> ItemClasses = new(StringComparer.OrdinalIgnoreCase)
	{
		// pistols
		["glock"] = 4, ["usp_silencer"] = 61, ["hkp2000"] = 32, ["p250"] = 36, ["elite"] = 2, ["fiveseven"] = 3,
		["tec9"] = 30, ["cz75a"] = 63, ["deagle"] = 1, ["revolver"] = 64,
		// SMGs
		["mac10"] = 17, ["mp9"] = 34, ["mp7"] = 33, ["mp5sd"] = 23, ["ump45"] = 24, ["p90"] = 19, ["bizon"] = 26,
		// shotguns
		["nova"] = 35, ["xm1014"] = 25, ["sawedoff"] = 29, ["mag7"] = 27,
		// rifles
		["galilar"] = 13, ["famas"] = 10, ["ak47"] = 7, ["m4a1"] = 16, ["m4a4"] = 16, ["m4a1_silencer"] = 60, ["sg556"] = 39, ["aug"] = 8,
		// snipers
		["ssg08"] = 40, ["awp"] = 9, ["g3sg1"] = 11, ["scar20"] = 38,
		// machine guns
		["m249"] = 14, ["negev"] = 28,
		// Zeus
		["taser"] = 31,
		// knives
		["bayonet"] = 500, ["knife_css"] = 503, ["knife_flip"] = 505, ["knife_gut"] = 506, ["knife_karambit"] = 507,
		["knife_m9_bayonet"] = 508, ["knife_tactical"] = 509, ["knife_falchion"] = 512, ["knife_survival_bowie"] = 514,
		["knife_butterfly"] = 515, ["knife_push"] = 516, ["knife_cord"] = 517, ["knife_canis"] = 518, ["knife_ursus"] = 519,
		["knife_gypsy_jackknife"] = 520, ["knife_outdoor"] = 521, ["knife_stiletto"] = 522, ["knife_widowmaker"] = 523,
		["knife_skeleton"] = 525, ["knife_kukri"] = 526,
		// gloves
		["studded_brokenfang_gloves"] = 4725, ["studded_bloodhound_gloves"] = 5027, ["sporty_gloves"] = 5030,
		["slick_gloves"] = 5031, ["leather_handwraps"] = 5032, ["motorcycle_gloves"] = 5033, ["specialist_gloves"] = 5034,
		["studded_hydra_gloves"] = 5035,
	};

	private HashSet<int> _hiddenItems = new();

	/// <summary>Reads "HiddenWeapons": class names (weapon_awp, awp, weapon_knife_karambit, sporty_gloves) or definition indexes.</summary>
	private void ParseVisibility(PluginConfig config)
	{
		config.Sections ??= new SectionsConfig();
		config.Access ??= new AccessConfig();
		config.Features ??= new FeaturesConfig();
		config.StickerSlots ??= new Dictionary<string, int>();
		config.HiddenWeapons ??= new List<string>();
		var hidden = new HashSet<int>();
		foreach (var raw in config.HiddenWeapons)
		{
			var name = (raw ?? "").Trim();
			if (name.StartsWith("weapon_", StringComparison.OrdinalIgnoreCase))
				name = name[7..];
			if (ItemClasses.TryGetValue(name, out var def) || int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out def))
				hidden.Add(def);
			else if (name.Length > 0)
			{
				try
				{
					Logger.LogWarning("HiddenWeapons: unknown item \"{Name}\"", raw);
				}
				catch
				{
					// the config can be parsed before the logger exists
				}
			}
		}
		_hiddenItems = hidden;
	}

	/// <summary>False for a weapon, knife or glove type listed in "HiddenWeapons".</summary>
	private bool ItemShown(int def) => !_hiddenItems.Contains(def);

	private bool SecShown(string sec) => sec switch
	{
		"agents" => Config.Sections.Agents,
		"gloves" => Config.Sections.Gloves,
		"knife" => Config.Sections.Knife,
		"zeus" => Config.Sections.Zeus && ItemShown(Weapons.Zeus),
		"music" => Config.Sections.Music,
		"pins" => Config.Sections.Pins,
		_ => true,
	};

	private bool CatShown(int cat) => (cat >= 0 && cat < Weapons.Cats.Length ? Weapons.Cats[cat] : "") switch
	{
		"pistol" => Config.Sections.Pistols,
		"smg" => Config.Sections.Smgs,
		"shotgun" => Config.Sections.Shotguns,
		"rifle" => Config.Sections.Rifles,
		"sniper" => Config.Sections.Snipers,
		"mg" => Config.Sections.MachineGuns,
		_ => false,
	};

	// ------------------------------------------------------------------ access (VIP / admin)

	private Vip? _vip;
	private Vip VipStatus => _vip ??= new Vip(Logger);

	private List<string>? SecRules(string sec) => sec switch
	{
		"agents" => Config.Access.Agents,
		"gloves" => Config.Access.Gloves,
		"knife" => Config.Access.Knife,
		"zeus" => Config.Access.Zeus,
		"music" => Config.Access.Music,
		"pins" => Config.Access.Pins,
		_ => null,
	};

	private List<string>? CatRules(int cat) => (cat >= 0 && cat < Weapons.Cats.Length ? Weapons.Cats[cat] : "") switch
	{
		"pistol" => Config.Access.Pistols,
		"smg" => Config.Access.Smgs,
		"shotgun" => Config.Access.Shotguns,
		"rifle" => Config.Access.Rifles,
		"sniper" => Config.Access.Snipers,
		"mg" => Config.Access.MachineGuns,
		_ => null,
	};

	/// <summary>True when the player may open the section (visible ones only; hidden ones are not shown at all).</summary>
	private bool SecOpen(CCSPlayerController player, string sec) => Allowed(player, SecRules(sec));

	private bool CatOpen(CCSPlayerController player, int cat) => Allowed(player, CatRules(cat));

	private bool Allowed(CCSPlayerController player, List<string>? rules)
	{
		if (rules == null || rules.Count == 0)
			return true;
		foreach (var raw in rules)
		{
			if (RuleMatches(player, raw))
				return true;
		}
		return false;
	}

	/// <summary>"*" everyone, "vip" any VIP, "vip:GROUP" a VIP group, "@css/flag" an admin flag, "#css/group" an admin group.</summary>
	private bool RuleMatches(CCSPlayerController player, string? raw)
	{
		var rule = (raw ?? "").Trim();
		if (rule.Length == 0)
			return false;
		try
		{
			if (rule == "*")
				return true;
			if (string.Equals(rule, "vip", StringComparison.OrdinalIgnoreCase))
				return VipStatus.IsVip(player);
			if (rule.StartsWith("vip:", StringComparison.OrdinalIgnoreCase))
				return VipStatus.IsVip(player) && string.Equals(VipStatus.Group(player), rule[4..].Trim(), StringComparison.OrdinalIgnoreCase);
			if (rule.StartsWith('@'))
				return AdminManager.PlayerHasPermissions(player, rule);
			if (rule.StartsWith('#'))
				return AdminManager.PlayerInGroup(player, rule);
		}
		catch (Exception ex)
		{
			Logger.LogWarning("Access rule \"{Rule}\" could not be checked: {Error}", rule, ex.Message);
		}
		return false;
	}

	// ------------------------------------------------------------------ skin settings (config "Features", "StickerSlots")

	/// <summary>Fills the locks of a new draft: which settings the player may change and how many sticker slots they get.</summary>
	private void ApplyFeatureLocks(CCSPlayerController player, Draft d)
	{
		var f = Config.Features;
		d.LkWear = !Allowed(player, f.Wear);
		d.LkSeed = !Allowed(player, f.Pattern);
		d.LkSt = !Allowed(player, f.StatTrak);
		d.LkTag = !Allowed(player, f.NameTag);
		d.LkKc = !Allowed(player, f.Charms);
		d.StkLimit = Allowed(player, f.Stickers) ? StickerSlotsOf(player) : 0;
	}

	/// <summary>
	/// "StickerSlots": the most slots among the rules the player matches; "*" is everyone. No matching rule (or an empty
	/// block) = all 5 slots.
	/// </summary>
	private int StickerSlotsOf(CCSPlayerController player)
	{
		var map = Config.StickerSlots;
		if (map == null || map.Count == 0)
			return LayoutIds.Stickers;
		var best = -1;
		foreach (var (rule, slots) in map)
		{
			if (slots > best && RuleMatches(player, rule))
				best = slots;
		}
		return Math.Clamp(best < 0 ? LayoutIds.Stickers : best, 0, LayoutIds.Stickers);
	}

	/// <summary>Weapons of a category that are not hidden.</summary>
	private IEnumerable<WeaponInfo> CatWeapons(string cat) => Weapons.All.Where(w => w.Cat == cat && ItemShown(w.Def));

	/// <summary>Equipment sections of the side list (gloves, knife, Zeus, music, pin) that are shown.</summary>
	private string[] ShownSecs => Secs.Where(SecShown).ToArray();

	/// <summary>Weapon categories that are shown.</summary>
	private int[] ShownCats => Enumerable.Range(0, Weapons.Cats.Length).Where(CatShown).ToArray();
}
