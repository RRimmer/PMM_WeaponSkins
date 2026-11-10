using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using static CounterStrikeSharp.API.Core.Listeners;

namespace PMM_WeaponSkins;

public class PluginConfig : BasePluginConfig
{
	/// <summary>ru, en or tr. Item names always come from WeaponSkins (api.language in its config).</summary>
	[JsonPropertyName("Language")] public string Language { get; set; } = "ru";

	/// <summary>scroll = mouse wheel (default), pages = pages 1, 2, 3.</summary>
	[JsonPropertyName("ListMode")] public string ListMode { get; set; } = "scroll";

	/// <summary>both = on-screen keyboard with a "Write in chat" button (default), keyboard, chat.</summary>
	[JsonPropertyName("InputMode")] public string InputMode { get; set; } = "both";

	/// <summary>Extra commands that open the menu. WeaponSkins' own !ws, !knife, !gloves... already open it.</summary>
	[JsonPropertyName("Commands")] public List<string> Commands { get; set; } = new();

	[JsonPropertyName("OpenDelay")] public float OpenDelay { get; set; } = 0.1f;
	[JsonPropertyName("InputDelay")] public float InputDelay { get; set; } = 0.15f;

	/// <summary>Seconds a chat prompt waits for the answer.</summary>
	[JsonPropertyName("ChatTimeout")] public int ChatTimeout { get; set; } = 30;

	[JsonPropertyName("Debug")] public bool Debug { get; set; } = false;

	/// <summary>Default menu scale, %: 100 for 16:9, 90 for 16:10, 75 for 4:3, 70 for 5:4. Players change theirs in the menu or with !wsscale.</summary>
	[JsonPropertyName("Scale")] public int Scale { get; set; } = 100;

	/// <summary>Weapons picked up from the ground or another player get the new holder's own skin. Off by default.</summary>
	[JsonPropertyName("RepaintPickedUp")] public bool RepaintPickedUp { get; set; } = false;

	/// <summary>Only picked-up weapons without any skin (default ones lying around) get the new holder's skin. Off by default.</summary>
	[JsonPropertyName("RepaintPickedUpDefault")] public bool RepaintPickedUpDefault { get; set; } = false;

	/// <summary>Store the player settings in the MySQL database of WeaponSkins (read from its config). On by default.</summary>
	[JsonPropertyName("UseWeaponSkinsDatabase")] public bool UseWeaponSkinsDatabase { get; set; } = true;

	/// <summary>Another MySQL for the player settings, used when UseWeaponSkinsDatabase is false.</summary>
	[JsonPropertyName("Database")] public DatabaseConfig Database { get; set; } = new();

	/// <summary>Who may open each section (VIP / admin). Empty = everyone.</summary>
	[JsonPropertyName("Access")] public AccessConfig Access { get; set; } = new();

	/// <summary>Who may use StatTrak, name tags, stickers, charms, wear and pattern. Empty = everyone.</summary>
	[JsonPropertyName("Features")] public FeaturesConfig Features { get; set; } = new();

	/// <summary>Sticker slots per rule ("*" = everyone, "vip:Gold", "@css/flag"...): the player gets the most of the rules they match.</summary>
	[JsonPropertyName("StickerSlots")] public Dictionary<string, int> StickerSlots { get; set; } = new();

	/// <summary>Menu colour and glow for each side (palette names or #rrggbb).</summary>
	[JsonPropertyName("Theme")] public ThemeConfig Theme { get; set; } = new();

	/// <summary>Which sections the menu shows.</summary>
	[JsonPropertyName("Sections")] public SectionsConfig Sections { get; set; } = new();

	/// <summary>Weapons, knives and glove types not shown inside their sections (class names or definition indexes).</summary>
	[JsonPropertyName("HiddenWeapons")] public List<string> HiddenWeapons { get; set; } = new();
}

public partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
{
	private const string Root = "pw-root";
	private const string ChatPanel = "pw-chat";
	private const int MaxSlots = 64;
	private const long ClickGapMs = 70;

	public override string ModuleName => "PMM_WeaponSkins";
	public override string ModuleVersion => "0.1.1";
	public override string ModuleAuthor => "RRimmer";
	public override string ModuleDescription => "Panorama inventory menu for WeaponSkins by Star";

	public PluginConfig Config { get; set; } = new();

	private Bridge _bridge = null!;
	private readonly ItemTable _items = new();
	private readonly Catalog _catalog = new();
	private readonly Ui[] _ui = new Ui[MaxSlots];
	private CCSCustomHudLayout? _layout;
	private readonly Dictionary<string, bool>?[] _sentClass = new Dictionary<string, bool>?[MaxSlots];
	private readonly Dictionary<string, string>?[] _sentVar = new Dictionary<string, string>?[MaxSlots];
	private readonly Dictionary<string, string>?[] _exclusive = new Dictionary<string, string>?[MaxSlots];
	private readonly HashSet<string> _interned = new(StringComparer.Ordinal);
	private Func<CCSPlayerController, string, bool>? _menuHandler;
	private Action<ulong>? _loadoutHandler;
	private bool _rebuildQueued;
	private bool _rebuildSoon;
	/// <summary>The previous layout entity during a rebuild, still on screen until the new one is drawn.</summary>
	private CCSCustomHudLayout? _retiring;
	/// <summary>Interned class names after which the entity is replaced when nobody has the menu open.</summary>
	private const int SoftInternLimit = 880;
	/// <summary>... and right away even with the menu open (one picker page adds up to 64 + a few).</summary>
	private const int HardInternLimit = 940;

	private bool Pages => string.Equals(Config.ListMode, "pages", StringComparison.OrdinalIgnoreCase);

	public void OnConfigParsed(PluginConfig config)
	{
		config.Language = (config.Language ?? "ru").Trim().ToLowerInvariant();
		config.ListMode = string.Equals(config.ListMode, "pages", StringComparison.OrdinalIgnoreCase) ? "pages" : "scroll";
		config.InputMode = (config.InputMode ?? "both").Trim().ToLowerInvariant() switch
		{
			"keyboard" or "kb" => "keyboard",
			"chat" => "chat",
			_ => "both"
		};
		config.OpenDelay = Math.Clamp(config.OpenDelay, 0f, 2f);
		config.InputDelay = Math.Clamp(config.InputDelay, 0f, 2f);
		config.ChatTimeout = Math.Clamp(config.ChatTimeout, 5, 300);
		config.Scale = NearestScale(config.Scale);
		ParseVisibility(config);
		ParseTheme(config);
		config.Database ??= new DatabaseConfig();
		config.Commands ??= new List<string>();
		Config = config;
		Lang.Set(config.Language);
	}

	public override void Load(bool hotReload)
	{
		_bridge = new Bridge(Logger);
		PmmWsHome.Instance = this;
		for (var i = 0; i < MaxSlots; i++)
			_ui[i] = new Ui();
		_items.Load(ModuleDirectory, Logger);
		Lang.Load(ModuleDirectory, Logger);

		RegisterListener<OnCustomHudClicked>(OnClick);
		RegisterListener<OnMapStart>(_ => OnMapStarted());
		RegisterEventHandler<EventPlayerDisconnect>(OnDisconnect);
		AddCommandListener("say", OnSay, HookMode.Pre);
		AddCommandListener("say_team", OnSay, HookMode.Pre);
		foreach (var command in Config.Commands)
		{
			var name = command.Trim().TrimStart('!', '/');
			if (name.StartsWith("css_", StringComparison.OrdinalIgnoreCase))
				name = name[4..];
			if (name.Length > 0)
				AddCommand("css_" + name, "Opens the PMM_WeaponSkins inventory", OnCommand);
		}
		AddTimer(1.0f, Watchdog, TimerFlags.REPEAT);
		if (hotReload)
			Register();
	}

	public override void OnAllPluginsLoaded(bool hotReload) => Register();

	public override void Unload(bool hotReload)
	{
		PmmWsHome.Instance = null;
		try
		{
			if (_bridge.Found)
			{
				_bridge.SetExternalMenu(null);
				if (_loadoutHandler != null)
					_bridge.RemoveLoadoutListener(_loadoutHandler);
			}
		}
		catch (Exception)
		{
		}

		for (var i = 0; i < MaxSlots; i++)
		{
			if (_ui[i].Open && Utilities.GetPlayerFromSlot(i) is { IsValid: true } player)
				Hide(player);
		}
		var layout = _layout;
		_layout = null;
		if (layout != null && layout.IsValid)
			layout.Remove();
	}

	private void Register()
	{
		if (!_bridge.Found)
			return;
		_menuHandler ??= (player, section) =>
		{
			try
			{
				// WeaponSkins "Menu_Style" other than 3 (RRimmer-PanoramaMenu): let WeaponSkins open the menu it chose
				if (WeaponSkinsMenuStyle() != PanoramaMenuStyle)
					return false;
				return OpenMenu(player, section);
			}
			catch (Exception ex)
			{
				Logger.LogError("Opening the menu failed: {Error}", ex);
				return false;
			}
		};
		_loadoutHandler ??= OnLoadoutReady;
		_bridge.SetExternalMenu(_menuHandler);
		_bridge.RemoveLoadoutListener(_loadoutHandler);
		_bridge.AddLoadoutListener(_loadoutHandler);
		Logger.LogInformation("PMM_WeaponSkins is the WeaponSkins menu now (WeaponSkinsApi v{Version}).", _bridge.ApiVersion);
	}

	private void Watchdog()
	{
		if (_bridge.Found && _bridge.IsReady && !_bridge.HasExternalMenu)
			Register();

		var anyOpen = false;
		for (var i = 0; i < MaxSlots; i++)
			anyOpen |= _ui[i].Open;

		if (_rebuildQueued && !anyOpen)
		{
			Rebuild();
			return;
		}

		if (anyOpen && (_layout == null || !_layout.IsValid))
		{
			Dbg(null, "layout entity is gone (round restart?), rebuilding it");
			Rebuild();
		}
	}

	private void OnMapStarted()
	{
		_layout = null;
		_retiring = null;
		_interned.Clear();
		_rebuildQueued = false;
		_rebuildSoon = false;
		for (var i = 0; i < MaxSlots; i++)
		{
			_ui[i] = new Ui();
			ForgetSent(i);
		}
	}

	private HookResult OnDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player != null && player.Slot is >= 0 and < MaxSlots)
		{
			if (player.IsValid && _ui[player.Slot].Open)
				Hide(player);
			_ui[player.Slot] = new Ui();
			ForgetSent(player.Slot);
		}
		return HookResult.Continue;
	}

	private void OnCommand(CCSPlayerController? player, CommandInfo info)
	{
		if (player == null || !Valid(player))
			return;
		if (!_bridge.Found)
		{
			Chat(player, Lang.T("err.api"));
			return;
		}
		if (WeaponSkinsMenuStyle() != PanoramaMenuStyle)
		{
			Chat(player, Lang.T("err.style"));
			return;
		}
		if (!OpenMenu(player, "skins"))
			Chat(player, Lang.T("err.not_ready"));
	}

	private void OnLoadoutReady(ulong steamId)
	{
		for (var i = 0; i < MaxSlots; i++)
		{
			var ui = _ui[i];
			if (!ui.Open)
				continue;
			var player = Utilities.GetPlayerFromSlot(i);
			if (player == null || !player.IsValid || player.SteamID != steamId)
				continue;
			ReloadLoadout(player, ui);
			Render(player);
		}
	}

	// ------------------------------------------------------------------ open / close

	private static bool Valid(CCSPlayerController? player) =>
		player != null && player.IsValid && !player.IsBot && player.Slot is >= 0 and < MaxSlots;

	private Ui? UiOf(CCSPlayerController player) => Valid(player) ? _ui[player.Slot] : null;

	/// <summary>Called by WeaponSkins for !ws, !knife, !gloves, !agents, !music, !pin and !stickers.</summary>
	private bool OpenMenu(CCSPlayerController player, string section)
	{
		if (!Valid(player))
			return false;

		var error = _bridge.CheckPlayer(player, false);
		if (error == "api")
			return false;
		if (error != null)
		{
			Chat(player, ErrorText(error));
			return true;
		}
		if (!EnsureCatalog())
		{
			Chat(player, Lang.T("err.data_not_loaded"));
			return true;
		}
		if (!EnsureLayout())
		{
			Logger.LogError("custom_hud_layout could not be created; falling back to the WeaponSkins menu.");
			return false;
		}

		var ui = _ui[player.Slot];
		if (!ui.Open)
			ui.Reset();
		ApplyPrefs(player, ui);
		ui.Team = player.Team == CsTeam.Terrorist ? 2 : 3;
		ui.Prompt = null;
		ui.Set = null;
		ui.Dd = false;
		ui.PickDd = 0;
		ReloadLoadout(player, ui);

		var lockedOnOpen = false;
		switch (section)
		{
			// a hidden or locked section opens the home screen (and not WeaponSkins' own menu)
			case "knife" or "gloves" or "music" or "pins" when SecShown(section) && SecOpen(player, section):
				GoSec(ui, section);
				break;
			case "agents" when SecShown("agents") && SecOpen(player, "agents"):
				GoAgents(ui);
				break;
			case "knife" or "gloves" or "music" or "pins" or "agents" when SecShown(section):
				GoHome(ui);
				lockedOnOpen = true;
				break;
			case "stickers":
				OpenHeldStickers(player, ui);
				break;
			default:
				GoHome(ui);
				break;
		}

		ui.Open = true;
		ui.Token++;
		Dbg(player, $"open section={section} team={ui.Team} view={ui.View}");
		Reveal(player, ui);
		if (lockedOnOpen)
			Later(Config.OpenDelay + 0.05f, () => { if (player.IsValid && ui.Open) ShowToast(player, Lang.T("toast.locked"), true); });
		return true;
	}

	/// <summary>!stickers: the held weapon's skin settings on the sticker tab, or its skin list when it has no skin.</summary>
	private void OpenHeldStickers(CCSPlayerController player, Ui ui)
	{
		var weapon = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;
		var def = weapon != null && weapon.IsValid ? (int)weapon.AttributeManager.Item.ItemDefinitionIndex : 0;
		if (!Weapons.ByDef.TryGetValue(def, out var info) || !ItemShown(def) || !CatShown(Array.IndexOf(Weapons.Cats, info.Cat)) ||
			!CatOpen(player, Array.IndexOf(Weapons.Cats, info.Cat)))
		{
			GoHome(ui);
			return;
		}
		GoCat(ui, Array.IndexOf(Weapons.Cats, info.Cat));
		ui.WeaponDef = def;
		var paint = ui.Side?.Paint(def) ?? 0;
		if (paint > 0)
			OpenSettings(player, DraftKind.Weapon, def, paint, 1);
	}

	private void Reveal(CCSPlayerController player, Ui ui)
	{
		var token = ui.Token;
		Later(Config.OpenDelay, () =>
		{
			if (!player.IsValid || ui.Token != token || !ui.Open)
				return;
			Render(player);
			SetClass(player, Root, "pw-hidden", false);
			ui.Shown = true;
			Later(Config.InputDelay, () =>
			{
				if (player.IsValid && ui.Token == token && ui.Open && ui.Prompt is not { Chat: true })
					Capture(player, true);
			});
		});
	}

	private void Hide(CCSPlayerController player)
	{
		var ui = UiOf(player);
		if (ui != null)
		{
			ui.Token++;
			ui.Open = false;
			ui.Shown = false;
			ui.Prompt = null;
			ui.Set = null;
			ui.Dd = false;
			ui.PickDd = 0;
		}
		if (_layout != null && _layout.IsValid && player.IsValid)
		{
			Capture(player, false);
			SetClass(player, Root, "pw-hidden", true);
			SetClass(player, ChatPanel, "pw-hidden", true);
		}
		if (_retiring != null && _retiring.IsValid && player.IsValid)
		{
			_retiring.SetInputCaptureEnabled(player, false);
			_retiring.SetHasClassForPlayer(player, Root, "pw-hidden", true);
		}
	}

	private void Capture(CCSPlayerController player, bool on)
	{
		var layout = _layout;
		if (layout != null && layout.IsValid && player.IsValid)
			layout.SetInputCaptureEnabled(player, on);
	}

	// ------------------------------------------------------------------ data

	private bool EnsureCatalog()
	{
		if (!_bridge.IsReady)
			return _catalog.Loaded;
		var stamp = _bridge.CatalogStamp;
		if (_catalog.Loaded && _catalog.Stamp == stamp)
			return true;
		var json = _bridge.CatalogJson();
		if (string.IsNullOrEmpty(json))
			return _catalog.Loaded;
		try
		{
			var dto = JsonSerializer.Deserialize<CatalogDto>(json);
			if (dto == null)
				return _catalog.Loaded;
			_catalog.Fill(dto, _items);
			Dbg(null, $"catalogue v{dto.Stamp}: paints={_catalog.Paints.Count} knives={_catalog.Knives.Count} gloves={_catalog.GloveTypes.Count} agents={_catalog.AgentsT.Count}+{_catalog.AgentsCT.Count} music={_catalog.Music.Count} pins={_catalog.Pins.Count} stickers={_catalog.Stickers.Count} charms={_catalog.Charms.Count}");
			return true;
		}
		catch (Exception ex)
		{
			Logger.LogError("The WeaponSkins catalogue could not be read: {Error}", ex.Message);
			return _catalog.Loaded;
		}
	}

	private void ReloadLoadout(CCSPlayerController player, Ui ui)
	{
		var json = _bridge.LoadoutJson(player.SteamID);
		if (string.IsNullOrEmpty(json))
			return;
		try
		{
			ui.Loadout = JsonSerializer.Deserialize<LoadoutDto>(json);
		}
		catch (Exception ex)
		{
			Logger.LogError("Loadout of {Player} could not be read: {Error}", player.PlayerName, ex.Message);
		}
	}

	// ------------------------------------------------------------------ layout entity and what was sent to it

	private bool EnsureLayout()
	{
		var layout = _layout;
		if (layout != null && layout.IsValid)
			return true;
		var created = Utilities.CreateEntityByName<CCSCustomHudLayout>("custom_hud_layout");
		if (created == null || created.Handle == IntPtr.Zero)
			return false;
		created.StrLayout = LayoutIds.Path;
		created.DispatchSpawn();
		if (!created.IsValid)
			return false;
		_layout = created;
		_interned.Clear();
		_rebuildQueued = false;
		for (var i = 0; i < MaxSlots; i++)
			ForgetSent(i);
		return true;
	}

	/// <summary>
	/// A custom_hud_layout interns at most 1024 class names. Pictures are classes, so a long browsing
	/// session can run out; a fresh entity starts the table again.
	///
	/// While someone has the menu open the old entity stays on screen (and keeps the mouse captured) until
	/// the new one has been drawn and has the capture too, then it is removed. Without that overlap the menu
	/// blinks and the cursor jumps to the centre of the screen.
	/// </summary>
	private void Rebuild()
	{
		_rebuildSoon = false;
		var old = _layout;
		_layout = null;
		var keepOld = old != null && old.IsValid && _ui.Any(u => u.Open);
		if (!keepOld && old != null && old.IsValid)
			old.Remove();
		if (!EnsureLayout())
		{
			if (keepOld)
				old!.Remove();
			return;
		}
		for (var i = 0; i < MaxSlots; i++)
		{
			var ui = _ui[i];
			if (!ui.Open)
				continue;
			var player = Utilities.GetPlayerFromSlot(i);
			if (player == null || !player.IsValid)
			{
				_ui[i] = new Ui();
				continue;
			}
			ui.Token++;
			Reveal(player, ui);
		}
		if (keepOld)
		{
			if (_retiring != null && _retiring.IsValid)
				_retiring.Remove();
			var prev = old!;
			_retiring = prev;
			var handle = prev.Handle;
			AddTimer(Config.OpenDelay + Config.InputDelay + 0.35f, () =>
			{
				if (_retiring == prev)
					_retiring = null;
				if (prev.IsValid && prev.Handle == handle && (_layout == null || _layout.Handle != handle))
					prev.Remove();
			});
		}
	}

	private void ForgetSent(int slot)
	{
		_sentClass[slot] = null;
		_sentVar[slot] = null;
		_exclusive[slot] = null;
	}

	/// <summary>Classes authored on in pmm_ws.xml: their "off" must reach a client that never got an "on".</summary>
	private static readonly HashSet<string> AuthoredOn = new(StringComparer.Ordinal) { "pw-hidden", "team-ct", "v-home" };

	private void SetClass(CCSPlayerController player, string panel, string cls, bool on)
	{
		var layout = _layout;
		if (layout == null || !layout.IsValid || !player.IsValid)
			return;
		var sent = _sentClass[player.Slot] ??= new Dictionary<string, bool>(StringComparer.Ordinal);
		var key = panel + "\u0001" + cls;
		if (sent.TryGetValue(key, out var current))
		{
			if (current == on)
				return;
		}
		else if (!on && !AuthoredOn.Contains(cls))
		{
			sent[key] = false;
			return;
		}
		sent[key] = on;
		if (on && _interned.Add(cls))
		{
			if (_interned.Count > SoftInternLimit)
				_rebuildQueued = true;
			// close to the 1024 cap with the menu open: switch to a fresh entity on the next frame, before
			// the next page of pictures would overflow the table
			if (_interned.Count > HardInternLimit && !_rebuildSoon)
			{
				_rebuildSoon = true;
				Server.NextFrame(() =>
				{
					if (_rebuildSoon)
						Rebuild();
				});
			}
		}
		layout.SetHasClassForPlayer(player, panel, cls, on);
	}

	/// <summary>Keeps exactly one class of a group (a picture, a rarity, a position step) on a panel.</summary>
	private void Exclusive(CCSPlayerController player, string panel, string group, string? cls)
	{
		var map = _exclusive[player.Slot] ??= new Dictionary<string, string>(StringComparer.Ordinal);
		var key = panel + "\u0001" + group;
		map.TryGetValue(key, out var previous);
		cls ??= "";
		if (previous == cls)
			return;
		if (!string.IsNullOrEmpty(previous))
			SetClass(player, panel, previous, false);
		if (cls.Length > 0)
			SetClass(player, panel, cls, true);
		map[key] = cls;
	}

	private void SetVar(CCSPlayerController player, string name, string value) => SetVarOn(player, Root, name, value);

	private void SetVarOn(CCSPlayerController player, string panel, string name, string value)
	{
		var layout = _layout;
		if (layout == null || !layout.IsValid || !player.IsValid)
			return;
		var sent = _sentVar[player.Slot] ??= new Dictionary<string, string>(StringComparer.Ordinal);
		var key = panel + "\u0001" + name;
		if (sent.TryGetValue(key, out var current) && current == value)
			return;
		sent[key] = value;
		layout.SetDialogVariableStringForPlayer(player, panel, name, value);
	}

	// ------------------------------------------------------------------ helpers

	private void Later(float seconds, Action callback)
	{
		if (seconds <= 0f)
			Server.NextFrame(callback);
		else
			AddTimer(seconds, callback);
	}

	private void ShowToast(CCSPlayerController player, string text, bool error)
	{
		var ui = UiOf(player);
		if (ui == null)
			return;
		var token = ++ui.ToastToken;
		ui.Toast = text;
		ui.ToastErr = error;
		SetVar(player, "toast", text);
		SetClass(player, Root, "toast-err", error);
		SetClass(player, Root, "toast-on", true);
		Later(2.6f, () =>
		{
			if (player.IsValid && ui.ToastToken == token)
				SetClass(player, Root, "toast-on", false);
		});
	}

	private static void Chat(CCSPlayerController player, string text)
	{
		if (player.IsValid)
			player.PrintToChat($" {ChatColors.Gold}[Skins]{ChatColors.Default} {text}");
	}

	private void Dbg(CCSPlayerController? player, string text)
	{
		if (!Config.Debug)
			return;
		Logger.LogInformation("[PMM-WS] {Player}: {Text}", player?.PlayerName ?? "-", text);
	}
}
