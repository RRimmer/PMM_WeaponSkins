using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PMM_WeaponSkins;

/// <summary>
/// Read-only snapshot of a player's loadout for other plugins (PMM_MainMenu, INVENTORY tab), called by reflection:
/// <c>PMM_WeaponSkins.PmmWsHome.Snapshot(ulong steamId, int team, int[] defs)</c>. Changes nothing.
/// Pictures are class names of pmm_ws_art.css ("" = no picture), rarity 0-7.
/// </summary>
public static class PmmWsHome
{
	public const int Version = 1;

	internal static Plugin? Instance;

	/// <summary>
	/// JSON: {"team":3,"slots":{"agents":{"a","n","r"},"gloves":…,"knife":…,"zeus":…,"music":…,"pins":…},
	/// "w":{"61":{"a","n","r","wn"}}} — "n" is the skin name ("" without a skin), "wn" the weapon name. Null when not ready.
	/// </summary>
	public static string? Snapshot(ulong steamId, int team, int[] defs)
	{
		try
		{
			return Instance?.HomeSnapshot(steamId, team, defs);
		}
		catch (Exception ex)
		{
			Instance?.Logger.LogError("PmmWsHome.Snapshot failed: {Error}", ex.Message);
			return null;
		}
	}
}

public partial class Plugin
{
	private static readonly string[] SnapshotSlots = { "agents", "gloves", "knife", "zeus", "music", "pins" };

	internal string? HomeSnapshot(ulong steamId, int team, int[] defs)
	{
		if (!_bridge.Found || !EnsureCatalog())
			return null;
		var json = _bridge.LoadoutJson(steamId);
		LoadoutDto? loadout = null;
		if (!string.IsNullOrEmpty(json))
			loadout = JsonSerializer.Deserialize<LoadoutDto>(json);
		team = team == 2 ? 2 : 3;
		var ui = new Ui { Team = team, Loadout = loadout };

		static string Pic(string art) => art.StartsWith("ph-", StringComparison.Ordinal) || art == "ag-none" ? "" : art;

		var slots = new Dictionary<string, object>();
		foreach (var sec in SnapshotSlots)
		{
			var (art, name, rarity) = Equipped(ui, sec);
			slots[sec] = new { a = Pic(art), n = name, r = rarity };
		}

		var weapons = new Dictionary<string, object>();
		foreach (var def in defs)
		{
			// a one-team weapon (USP-S, Glock, M4...) shows that team's skin, the rest the asked team's
			var side = Weapons.ByDef.TryGetValue(def, out var info) && info.Team != 0 ? loadout?.For(info.Team) : ui.Side;
			var (art, rarity) = WeaponArt(side, def);
			var paint = side?.Paint(def) ?? 0;
			var skin = paint > 0 ? _catalog.Paint(def, paint)?.Name ?? "" : "";
			weapons[def.ToString()] = new { a = Pic(art), n = skin, r = rarity, wn = _catalog.Name(def) };
		}

		return JsonSerializer.Serialize(new { team, slots, w = weapons });
	}
}
