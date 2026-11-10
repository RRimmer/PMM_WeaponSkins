using System.Reflection;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;

namespace PMM_WeaponSkins;

/// <summary>
/// Calls WeaponSkins.WeaponSkinsApi (added to WeaponSkins by Star for this menu) through reflection.
/// WeaponSkins lives in its own plugin load context, so only primitive types, strings and delegates of
/// shared types (CounterStrikeSharp, System) cross this boundary.
/// </summary>
internal sealed class Bridge
{
	private const string ApiType = "WeaponSkins.WeaponSkinsApi";
	private readonly ILogger _logger;
	private Type? _type;
	private bool _warned;

	private PropertyInfo? _isReady, _catalogStamp, _stickersEnabled, _hasExternal;
	private MethodInfo? _setExternal, _addListener, _removeListener, _checkPlayer, _stickersAllowed;
	private MethodInfo? _catalogJson, _loadoutJson, _saveWeapon, _saveKnife, _saveGloves, _saveAgent, _saveMusic, _savePin;
	private MethodInfo? _applyToWeapon;
	private FieldInfo? _apiVersion;

	public Bridge(ILogger logger)
	{
		_logger = logger;
	}

	public bool Found => Resolve() != null;

	public int ApiVersion => Resolve() != null && _apiVersion?.GetValue(null) is int v ? v : 0;

	private long _scanAt;

	private Type? Resolve()
	{
		var now = Environment.TickCount64;
		if (_type != null)
		{
			try
			{
				if (_isReady?.GetValue(null) is true)
					return _type;
			}
			catch (Exception)
			{
			}
			if (now - _scanAt < 3000)
				return _type;
		}
		else if (now - _scanAt < 3000 && _scanAt != 0)
		{
			return null;
		}

		_scanAt = now;
		Type? fallback = null;
		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			Type? type;
			try
			{
				if (assembly.IsDynamic)
					continue;
				type = assembly.GetType(ApiType, false);
			}
			catch (Exception)
			{
				continue;
			}
			var ready = type?.GetProperty("IsReady", BindingFlags.Public | BindingFlags.Static);
			if (type == null || ready == null)
				continue;

			bool alive;
			try
			{
				alive = ready.GetValue(null) is true;
			}
			catch (Exception)
			{
				alive = false;
			}
			fallback ??= type;
			if (!alive)
				continue;

			// A reloaded WeaponSkins leaves its old assembly behind: prefer the copy whose plugin is alive.
			fallback = type;
			break;
		}

		if (fallback != null && !ReferenceEquals(fallback, _type))
		{
			_type = fallback;
			Bind(fallback);
		}

		if (_type == null && !_warned)
		{
			_warned = true;
			_logger.LogWarning("WeaponSkins.WeaponSkinsApi was not found. Install the WeaponSkins build from the PMM_WeaponSkins release (WeaponSkins-PMM).");
		}
		return _type;
	}

	private void Bind(Type type)
	{
		const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
		_apiVersion = type.GetField("ApiVersion", flags);
		_isReady = type.GetProperty("IsReady", flags);
		_catalogStamp = type.GetProperty("CatalogStamp", flags);
		_stickersEnabled = type.GetProperty("StickersEnabled", flags);
		_hasExternal = type.GetProperty("HasExternalMenu", flags);
		_setExternal = type.GetMethod("SetExternalMenu", flags);
		_addListener = type.GetMethod("AddLoadoutListener", flags);
		_removeListener = type.GetMethod("RemoveLoadoutListener", flags);
		_checkPlayer = type.GetMethod("CheckPlayer", flags);
		_stickersAllowed = type.GetMethod("StickersAllowed", flags);
		_catalogJson = type.GetMethod("CatalogJson", flags);
		_loadoutJson = type.GetMethod("LoadoutJson", flags);
		_saveWeapon = type.GetMethod("SaveWeapon", flags);
		_saveKnife = type.GetMethod("SaveKnife", flags);
		_saveGloves = type.GetMethod("SaveGloves", flags);
		_saveAgent = type.GetMethod("SaveAgent", flags);
		_saveMusic = type.GetMethod("SaveMusic", flags);
		_savePin = type.GetMethod("SavePin", flags);
		_applyToWeapon = type.GetMethod("ApplyToWeapon", flags);
	}

	private static readonly object Failed = new();

	/// <summary>Invokes an API method; returns <see cref="Failed"/> when the call itself broke.</summary>
	private object? Call(MethodInfo? method, params object?[] args)
	{
		if (Resolve() == null || method == null)
			return Failed;
		try
		{
			return method.Invoke(null, args);
		}
		catch (TargetInvocationException ex)
		{
			_logger.LogError("WeaponSkinsApi.{Method} failed: {Error}", method.Name, ex.InnerException?.Message ?? ex.Message);
			return Failed;
		}
		catch (Exception ex)
		{
			_logger.LogError("WeaponSkinsApi.{Method} could not be called: {Error}", method.Name, ex.Message);
			_type = null;
			return Failed;
		}
	}

	private object? Get(PropertyInfo? property)
	{
		if (Resolve() == null || property == null)
			return null;
		try
		{
			return property.GetValue(null);
		}
		catch (Exception)
		{
			return null;
		}
	}

	public bool IsReady => Get(_isReady) is true;

	public int CatalogStamp => Get(_catalogStamp) is int stamp ? stamp : 0;

	public bool StickersEnabled => Get(_stickersEnabled) is true;

	public bool HasExternalMenu => Get(_hasExternal) is true;

	public void SetExternalMenu(Func<CCSPlayerController, string, bool>? handler) => Call(_setExternal, handler);

	public void AddLoadoutListener(Action<ulong> listener) => Call(_addListener, listener);

	public void RemoveLoadoutListener(Action<ulong> listener) => Call(_removeListener, listener);

	/// <summary>Null when the player may change skins, otherwise an error key.</summary>
	public string? CheckPlayer(CCSPlayerController player, bool reply) => Result(Call(_checkPlayer, player, reply));

	private static string? Result(object? value) => ReferenceEquals(value, Failed) ? "api" : value as string;

	public bool StickersAllowed(CCSPlayerController player) => Call(_stickersAllowed, player) is true;

	public string? CatalogJson() => Call(_catalogJson) as string;

	public string? LoadoutJson(ulong steamId) => Call(_loadoutJson, steamId) as string;

	public string? SaveWeapon(CCSPlayerController player, int teams, int def, string json) =>
		Result(Call(_saveWeapon, player, teams, def, json));

	/// <summary>False when this WeaponSkins build has no ApplyToWeapon (older than the "repaint picked up" change).</summary>
	public bool CanApplyToWeapon => Resolve() != null && _applyToWeapon != null;

	/// <summary>Puts the player's own skin on a weapon they hold (weapon.EntityHandle.Raw).</summary>
	public string? ApplyToWeapon(CCSPlayerController player, uint weaponHandle) =>
		Result(Call(_applyToWeapon, player, weaponHandle));

	public string? SaveKnife(CCSPlayerController player, int teams, int def) =>
		Result(Call(_saveKnife, player, teams, def));

	public string? SaveGloves(CCSPlayerController player, int teams, int def, int paint, float wear, int seed) =>
		Result(Call(_saveGloves, player, teams, def, paint, wear, seed));

	public string? SaveAgent(CCSPlayerController player, int teams, string? model) =>
		Result(Call(_saveAgent, player, teams, model));

	public string? SaveMusic(CCSPlayerController player, int kit) =>
		Result(Call(_saveMusic, player, kit));

	public string? SavePin(CCSPlayerController player, int pin) =>
		Result(Call(_savePin, player, pin));
}
