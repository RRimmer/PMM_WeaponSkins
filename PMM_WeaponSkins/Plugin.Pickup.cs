using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Logging;

namespace PMM_WeaponSkins;

/// <summary>
/// WeaponSkins paints a weapon once, when it is created for its buyer, so a weapon picked up from the ground or from
/// another player keeps the previous owner's skin. Through WeaponSkinsApi.ApplyToWeapon:
/// - "RepaintPickedUp": every picked-up weapon gets the new holder's own skin (or none, when they have no skin for it);
/// - "RepaintPickedUpDefault": only weapons without any skin (default ones lying around) get it; skinned weapons
///   of other players keep their skin.
/// </summary>
public partial class Plugin
{
	private const ulong SteamId64Base = 76561197960265728UL;
	private bool _pickupWarned;
	/// <summary>Weapon entity handle -> SteamID whose skin this plugin put on it.</summary>
	private readonly Dictionary<uint, ulong> _repainted = new();

	[GameEventHandler]
	public HookResult OnItemPickupRepaint(EventItemPickup @event, GameEventInfo info)
	{
		if (!Config.RepaintPickedUp && !Config.RepaintPickedUpDefault)
			return HookResult.Continue;
		var player = @event.Userid;
		if (player == null || !player.IsValid || player.IsBot || player.SteamID == 0)
			return HookResult.Continue;

		var item = (@event.Item ?? "").Trim();
		if (item.Length == 0 || item.Contains("knife", StringComparison.OrdinalIgnoreCase) || item.Contains("bayonet", StringComparison.OrdinalIgnoreCase))
			return HookResult.Continue;

		var slot = player.Slot;
		var steamId = player.SteamID;
		// a moment later: the weapon is attached to the pawn, and a bought one already has WeaponSkins' paint
		AddTimer(0.15f, () =>
		{
			var current = Utilities.GetPlayerFromSlot(slot);
			if (current == null || !current.IsValid || current.SteamID != steamId)
				return;
			RepaintPickedUp(current);
		});
		return HookResult.Continue;
	}

	/// <summary>Every weapon the player carries that someone else bought (or the map spawned) gets the player's skin.</summary>
	private void RepaintPickedUp(CCSPlayerController player)
	{
		if (!_bridge.CanApplyToWeapon)
		{
			if (!_pickupWarned)
			{
				_pickupWarned = true;
				Logger.LogWarning("RepaintPickedUp / RepaintPickedUpDefault is on, but this WeaponSkins build has no WeaponSkinsApi.ApplyToWeapon. Update WeaponSkins.");
			}
			return;
		}

		var weapons = player.PlayerPawn.Value?.WeaponServices?.MyWeapons;
		if (weapons == null)
			return;
		var side = PickupSide(player);
		var account = (uint)(player.SteamID - SteamId64Base);
		foreach (var handle in weapons)
		{
			var weapon = handle.Value;
			if (weapon == null || !weapon.IsValid || !weapon.DesignerName.StartsWith("weapon_", StringComparison.Ordinal) ||
				weapon.DesignerName.Contains("knife", StringComparison.Ordinal) || weapon.DesignerName.Contains("bayonet", StringComparison.Ordinal) ||
				weapon.DesignerName is "weapon_c4" or "weapon_healthshot" || weapon.DesignerName.EndsWith("grenade", StringComparison.Ordinal) ||
				weapon.DesignerName is "weapon_flashbang" or "weapon_smokegrenade" or "weapon_molotov" or "weapon_decoy")
				continue;
			var raw = weapon.EntityHandle.Raw;
			// repainted for this player already
			if (_repainted.TryGetValue(raw, out var who) && who == player.SteamID)
				continue;
			var painted = HasSkin(weapon);
			var def = (int)weapon.AttributeManager.Item.ItemDefinitionIndex;
			var wanted = side?.Paint(def) ?? -1;
			// nothing to change: no skin on it and none set up for it
			if (!painted && wanted == 0)
				continue;
			// bought by this player and already wearing their skin. A weapon given to the player (an admin "give" that
			// fell to the floor, a map spawn) has the player as its original owner too, but no skin: that one is painted.
			if (painted && weapon.OriginalOwnerXuidLow == account && (wanted < 0 || weapon.FallbackPaintKit == wanted))
				continue;
			// "default only": a weapon that carries any skin is left as it is
			if (!Config.RepaintPickedUp && painted)
				continue;
			var error = _bridge.ApplyToWeapon(player, raw);
			if (error == null)
			{
				if (_repainted.Count > 1024)
					_repainted.Clear();
				_repainted[raw] = player.SteamID;
			}
			Dbg(player, $"repaint picked up {weapon.DesignerName} (def {def}, skin {weapon.FallbackPaintKit}, wanted {wanted}): {error ?? "ok"}");
		}
	}

	/// <summary>The player's loadout for their current side, or null when WeaponSkins has not loaded it.</summary>
	private TeamDto? PickupSide(CCSPlayerController player)
	{
		try
		{
			var json = _bridge.LoadoutJson(player.SteamID);
			if (string.IsNullOrEmpty(json))
				return null;
			return JsonSerializer.Deserialize<LoadoutDto>(json)?.For(player.TeamNum == 3 ? 3 : 2);
		}
		catch (Exception)
		{
			return null;
		}
	}

	/// <summary>True when the weapon is painted: a fallback paint kit (WeaponSkins and similar) or a paint kit attribute.</summary>
	private static bool HasSkin(CBasePlayerWeapon weapon)
	{
		if (weapon.FallbackPaintKit > 0)
			return true;
		try
		{
			var item = weapon.AttributeManager.Item;
			return HasPaintAttribute(item.NetworkedDynamicAttributes) || HasPaintAttribute(item.AttributeList);
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>Attribute 6 = "set item texture prefab" (paint kit).</summary>
	private static bool HasPaintAttribute(CAttributeList list)
	{
		foreach (var attribute in list.Attributes)
		{
			if (attribute.AttributeDefinitionIndex == 6 && attribute.Value > 0f)
				return true;
		}
		return false;
	}
}
