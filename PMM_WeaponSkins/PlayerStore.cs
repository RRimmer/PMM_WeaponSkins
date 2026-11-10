using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace PMM_WeaponSkins;

/// <summary>Per-player menu settings of PMM_WeaponSkins. Skins themselves are stored by WeaponSkins.</summary>
internal sealed class PlayerPrefs
{
	/// <summary>Menu scale, %; 0 = the config default.</summary>
	public int Scale;
	/// <summary>Sticker / charm picker sorting (see PSORTS in tools/gen_layout.py).</summary>
	public int StSort;
	public int KcSort;
}

/// <summary>
/// Keeps PlayerPrefs in MySQL, shared by every server on that database (by default the database of WeaponSkins, which
/// cannot run without MySQL anyway). Without a reachable database they live in memory until the plugin reloads.
/// Database work runs off the game thread; results come back with Server.NextFrame.
/// </summary>
internal sealed class PlayerStore
{
	private readonly ILogger _logger;
	private readonly string _table;
	private string? _connection;
	private readonly Dictionary<ulong, PlayerPrefs> _cache = new();
	private readonly HashSet<ulong> _loading = new();
	private readonly object _lock = new();
	private long _lastError;

	public bool UsesDatabase => _connection != null;

	public PlayerStore(ILogger logger, string? connection, string table)
	{
		_logger = logger;
		_table = Regex.IsMatch(table ?? "", "^[A-Za-z0-9_]{1,64}$") ? table! : "pmm_ws_players";
		_connection = connection;
		if (_connection == null)
		{
			_logger.LogWarning("No MySQL for player settings (UseWeaponSkinsDatabase / Database): they are kept only until the plugin reloads.");
			return;
		}
		var conn = _connection;
		Task.Run(async () =>
		{
			try
			{
				await using var db = new MySqlConnection(conn);
				await db.OpenAsync();
				await using var cmd = db.CreateCommand();
				cmd.CommandText = $"CREATE TABLE IF NOT EXISTS `{_table}` (" +
					"`steamid` BIGINT UNSIGNED NOT NULL PRIMARY KEY, " +
					"`scale` SMALLINT NOT NULL DEFAULT 0, " +
					"`st_sort` TINYINT NOT NULL DEFAULT 0, " +
					"`kc_sort` TINYINT NOT NULL DEFAULT 0, " +
					"`updated_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP)";
				await cmd.ExecuteNonQueryAsync();
				_logger.LogInformation("Player settings are stored in MySQL, table {Table}.", _table);
			}
			catch (Exception ex)
			{
				_logger.LogError("MySQL is not reachable ({Error}); player settings are kept in memory until the plugin reloads.", ex.Message);
				Server.NextFrame(() => _connection = null);
			}
		});
	}

	/// <summary>Cached settings, or null when they have not been loaded yet.</summary>
	public PlayerPrefs? Get(ulong steamId)
	{
		lock (_lock)
			return _cache.TryGetValue(steamId, out var p) ? p : null;
	}

	/// <summary>Loads a player's settings (from the cache, the file or MySQL) and calls back on the game thread.</summary>
	public void Load(ulong steamId, Action<PlayerPrefs>? loaded = null)
	{
		var cached = Get(steamId);
		if (cached != null || _connection == null)
		{
			var p = cached ?? Remember(steamId, new PlayerPrefs());
			loaded?.Invoke(p);
			return;
		}
		lock (_lock)
		{
			if (!_loading.Add(steamId))
				return;
		}
		var conn = _connection;
		Task.Run(async () =>
		{
			var prefs = new PlayerPrefs();
			try
			{
				await using var db = new MySqlConnection(conn);
				await db.OpenAsync();
				await using var cmd = db.CreateCommand();
				cmd.CommandText = $"SELECT `scale`, `st_sort`, `kc_sort` FROM `{_table}` WHERE `steamid` = @id";
				cmd.Parameters.AddWithValue("@id", steamId);
				await using var reader = await cmd.ExecuteReaderAsync();
				if (await reader.ReadAsync())
				{
					prefs.Scale = Convert.ToInt32(reader.GetValue(0));
					prefs.StSort = Convert.ToInt32(reader.GetValue(1));
					prefs.KcSort = Convert.ToInt32(reader.GetValue(2));
				}
			}
			catch (Exception ex)
			{
				LogError("load", ex);
			}
			Server.NextFrame(() =>
			{
				lock (_lock)
					_loading.Remove(steamId);
				var p = Get(steamId) ?? Remember(steamId, prefs);
				loaded?.Invoke(p);
			});
		});
	}

	public void Save(ulong steamId, PlayerPrefs prefs)
	{
		Remember(steamId, prefs);
		if (_connection == null)
			return;
		var conn = _connection;
		int scale = prefs.Scale, st = prefs.StSort, kc = prefs.KcSort;
		Task.Run(async () =>
		{
			try
			{
				await using var db = new MySqlConnection(conn);
				await db.OpenAsync();
				await using var cmd = db.CreateCommand();
				cmd.CommandText = $"INSERT INTO `{_table}` (`steamid`, `scale`, `st_sort`, `kc_sort`) VALUES (@id, @scale, @st, @kc) " +
					"ON DUPLICATE KEY UPDATE `scale` = VALUES(`scale`), `st_sort` = VALUES(`st_sort`), `kc_sort` = VALUES(`kc_sort`)";
				cmd.Parameters.AddWithValue("@id", steamId);
				cmd.Parameters.AddWithValue("@scale", scale);
				cmd.Parameters.AddWithValue("@st", st);
				cmd.Parameters.AddWithValue("@kc", kc);
				await cmd.ExecuteNonQueryAsync();
			}
			catch (Exception ex)
			{
				LogError("save", ex);
			}
		});
	}

	private PlayerPrefs Remember(ulong steamId, PlayerPrefs prefs)
	{
		lock (_lock)
			_cache[steamId] = prefs;
		return prefs;
	}

	private void LogError(string what, Exception ex)
	{
		var now = Environment.TickCount64;
		if (now - Interlocked.Read(ref _lastError) < 30000)
			return;
		Interlocked.Exchange(ref _lastError, now);
		_logger.LogError("MySQL {What} of player settings failed: {Error}", what, ex.Message);
	}
}
