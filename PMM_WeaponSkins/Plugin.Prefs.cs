using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace PMM_WeaponSkins;

/// <summary>MySQL for the player settings (config "Database"), used when UseWeaponSkinsDatabase is off or WeaponSkins' config cannot be read.</summary>
public sealed class DatabaseConfig
{
	[JsonPropertyName("Host")] public string Host { get; set; } = "";
	[JsonPropertyName("Port")] public uint Port { get; set; } = 3306;
	[JsonPropertyName("User")] public string User { get; set; } = "";
	[JsonPropertyName("Password")] public string Password { get; set; } = "";
	[JsonPropertyName("Name")] public string Name { get; set; } = "";
	[JsonPropertyName("SslMode")] public string SslMode { get; set; } = "Preferred";
	[JsonPropertyName("Table")] public string Table { get; set; } = "pmm_ws_players";
}

/// <summary>Player settings of the menu (scale, picker sorting), shared by servers on one MySQL database.</summary>
public partial class Plugin
{
	private PlayerStore? _store;

	private PlayerStore Store => _store ??= new PlayerStore(Logger, ConnectionString(), Config.Database.Table);

	/// <summary>The MySQL connection string, or null when no database is set.</summary>
	private string? ConnectionString()
	{
		var db = Config.Database;
		if (Config.UseWeaponSkinsDatabase)
		{
			var ws = WeaponSkinsDatabase();
			if (ws != null)
				db = ws;
			else
				Logger.LogWarning("UseWeaponSkinsDatabase: the database of WeaponSkins could not be read, using \"Database\".");
		}
		if (string.IsNullOrWhiteSpace(db.Host) || string.IsNullOrWhiteSpace(db.Name))
			return null;
		var builder = new MySqlConnectionStringBuilder
		{
			Server = db.Host.Trim(),
			Port = db.Port,
			UserID = db.User,
			Password = db.Password,
			Database = db.Name.Trim(),
			Pooling = true,
			MinimumPoolSize = 0,
		};
		if (Enum.TryParse<MySqlSslMode>(db.SslMode, true, out var ssl))
			builder.SslMode = ssl;
		return builder.ConnectionString;
	}

	/// <summary>configs/plugins/WeaponSkins/WeaponSkins.json, or null when it is not there.</summary>
	private string? WeaponSkinsConfigFile()
	{
		var dir = Path.GetFullPath(Path.Combine(ModuleDirectory, "..", "..", "configs", "plugins", "WeaponSkins"));
		return Directory.Exists(dir)
			? Directory.GetFiles(dir, "*.json").FirstOrDefault(f => string.Equals(Path.GetFileName(f), "WeaponSkins.json", StringComparison.OrdinalIgnoreCase))
			: null;
	}

	private static JsonDocument ParseConfig(string file) =>
		JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

	private string? _styleFile;
	private DateTime _styleStamp;
	private int _menuStyle = PanoramaMenuStyle;
	private int _loggedStyle = -1;

	/// <summary>"Menu_Style" of WeaponSkins that means a panorama menu (RRimmer-PanoramaMenu).</summary>
	private const int PanoramaMenuStyle = 3;

	/// <summary>
	/// "menu" -> "Menu_Style" of WeaponSkins (1 = built-in, 2 = NickFox MenuManager, 3 = RRimmer PanoramaMenu). Re-read when
	/// the file changes. When it cannot be read, 3 is assumed, so the menu keeps working.
	/// </summary>
	private int WeaponSkinsMenuStyle()
	{
		try
		{
			var file = WeaponSkinsConfigFile();
			if (file == null)
				return PanoramaMenuStyle;
			var stamp = File.GetLastWriteTimeUtc(file);
			if (file == _styleFile && stamp == _styleStamp)
				return _menuStyle;
			_styleFile = file;
			_styleStamp = stamp;
			using var doc = ParseConfig(file);
			_menuStyle = doc.RootElement.TryGetProperty("menu", out var menu) && menu.TryGetProperty("Menu_Style", out var style) &&
				style.TryGetInt32(out var value) ? value : PanoramaMenuStyle;
		}
		catch (Exception ex)
		{
			Logger.LogWarning("Menu_Style of WeaponSkins could not be read: {Error}", ex.Message);
			_menuStyle = PanoramaMenuStyle;
		}
		if (_menuStyle != _loggedStyle)
		{
			_loggedStyle = _menuStyle;
			if (_menuStyle == PanoramaMenuStyle)
				Logger.LogInformation("WeaponSkins Menu_Style is {Style}: the WeaponSkins commands open this panorama menu.", _menuStyle);
			else
				Logger.LogInformation("WeaponSkins Menu_Style is {Style}: the panorama menu is off, WeaponSkins opens its own menu. Set Menu_Style to 3 to use it.", _menuStyle);
		}
		return _menuStyle;
	}

	/// <summary>The "database" block of configs/plugins/WeaponSkins/WeaponSkins.json.</summary>
	private DatabaseConfig? WeaponSkinsDatabase()
	{
		try
		{
			var file = WeaponSkinsConfigFile();
			if (file == null)
				return null;
			using var doc = ParseConfig(file);
			if (!doc.RootElement.TryGetProperty("database", out var d))
				return null;
			string Str(string key) => d.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
			return new DatabaseConfig
			{
				Host = Str("host"),
				Port = d.TryGetProperty("port", out var port) && port.TryGetUInt32(out var p) ? p : 3306,
				User = Str("user"),
				Password = Str("password"),
				Name = Str("name"),
				SslMode = Str("ssl_mode") is { Length: > 0 } ssl ? ssl : "Preferred",
				Table = Config.Database.Table,
			};
		}
		catch (Exception ex)
		{
			Logger.LogWarning("WeaponSkins config could not be read: {Error}", ex.Message);
			return null;
		}
	}

	/// <summary>Loads the settings when a player joins, so the menu opens with them.</summary>
	[GameEventHandler]
	public HookResult OnPlayerFullConnectPrefs(EventPlayerConnectFull @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player != null && player.IsValid && !player.IsBot && player.SteamID != 0)
			Store.Load(player.SteamID);
		return HookResult.Continue;
	}

	/// <summary>Puts the saved sorting into the menu state (on open; again once MySQL answers).</summary>
	private void ApplyPrefs(CCSPlayerController player, Ui ui)
	{
		var steamId = player.SteamID;
		var cached = Store.Get(steamId);
		if (cached != null)
		{
			ui.StSort = Math.Clamp(cached.StSort, 0, LayoutIds.PickSorts - 1);
			ui.KcSort = Math.Clamp(cached.KcSort, 0, LayoutIds.PickSorts - 1);
			return;
		}
		var slot = player.Slot;
		Store.Load(steamId, prefs =>
		{
			var current = CounterStrikeSharp.API.Utilities.GetPlayerFromSlot(slot);
			if (current == null || !current.IsValid || current.SteamID != steamId)
				return;
			var u = _ui[slot];
			u.StSort = Math.Clamp(prefs.StSort, 0, LayoutIds.PickSorts - 1);
			u.KcSort = Math.Clamp(prefs.KcSort, 0, LayoutIds.PickSorts - 1);
			u.PickerKey = "";
			if (u.Open)
				Render(current);
		});
	}

	/// <summary>Saves the picker sorting after the player changed it.</summary>
	private void SaveSorts(CCSPlayerController player, Ui ui)
	{
		var prefs = Store.Get(player.SteamID) ?? new PlayerPrefs();
		prefs.StSort = ui.StSort;
		prefs.KcSort = ui.KcSort;
		Store.Save(player.SteamID, prefs);
	}
}
