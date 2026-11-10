using System.Globalization;
using System.Text.Json;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace PMM_WeaponSkins;

/// <summary>Navigation, card lists and everything that saves through WeaponSkins.</summary>
public partial class Plugin
{
	internal static readonly string[] Secs = { "gloves", "knife", "zeus", "music", "pins" };
	private static readonly StringComparer NameOrder = StringComparer.OrdinalIgnoreCase;

	private static int Bit(int team) => team == 2 ? 1 : 2;

	private static string TeamName(int team) => Lang.T(team == 2 ? "team.t" : "team.ct");

	private static string RarityName(int rarity) => rarity is >= 1 and <= 7 ? Lang.T("r." + rarity) : "";

	private static string AgentRarityName(int rarity) => rarity is >= 3 and <= 6 ? Lang.T("ra." + rarity) : "";

	/// <summary>Picture of the default agent / default gloves of a side, straight from the CS2 files (pmm_ws.base.css).</summary>
	internal static string DefaultAgentArt(int team) => team == 2 ? "dag-2" : "dag-3";
	internal static string DefaultGlovesArt(int team) => team == 2 ? "dgl-2" : "dgl-3";

	/// <summary>Sets Eq / EqCT / EqT of a card from a test on each side's loadout.</summary>
	private static Card Mark(Card card, LoadoutDto? loadout, Func<TeamDto, bool> equipped)
	{
		card.EqCT = loadout != null && equipped(loadout.CT);
		card.EqT = loadout != null && equipped(loadout.T);
		card.Eq = card.EqCT || card.EqT;
		return card;
	}

	/// <summary>Equipped on the side being edited only (agents, "no skin" cards).</summary>
	private static Card MarkOwn(Card card, int team, bool equipped)
	{
		card.Eq = equipped;
		card.EqCT = equipped && team == 3;
		card.EqT = equipped && team == 2;
		return card;
	}

	// ------------------------------------------------------------------ views

	private void GoHome(Ui ui)
	{
		ui.View = ViewKind.Home;
		ui.Set = null;
		ui.Dd = false;
		ui.GloveDef = 0;
		ui.KnifeDef = 0;
		ui.WeaponDef = 0;
	}

	private void GoAgents(Ui ui)
	{
		ui.View = ViewKind.Agents;
		ui.Set = null;
		ui.Dd = false;
	}

	private void GoSec(Ui ui, string sec)
	{
		ui.View = ViewKind.Equip;
		ui.Sec = sec;
		ui.Set = null;
		ui.Dd = false;
		ui.GloveDef = 0;
		ui.KnifeDef = 0;
	}

	private void GoCat(Ui ui, int cat)
	{
		ui.View = ViewKind.Weapons;
		ui.Cat = Math.Clamp(cat, 0, Weapons.Cats.Length - 1);
		ui.Set = null;
		ui.Dd = false;
		ui.WeaponDef = 0;
	}

	private void GoBack(Ui ui)
	{
		ui.Dd = false;
		if (ui.View == ViewKind.Equip)
		{
			ui.GloveDef = 0;
			ui.KnifeDef = 0;
		}
		else if (ui.View == ViewKind.Weapons)
		{
			ui.WeaponDef = 0;
		}
	}

	// ------------------------------------------------------------------ card lists

	private void BuildList(CCSPlayerController player, Ui ui)
	{
		var side = ui.Side;
		var cards = new List<Card>();
		Card? lead = null;
		var key = "";
		var crumbs = new List<string>();
		var sortable = false;
		ui.Chips = false;
		ui.ChipList.Clear();

		switch (ui.View)
		{
			case ViewKind.Agents:
			{
				var list = ui.Team == 3 ? _catalog.AgentsCT : _catalog.AgentsT;
				var factions = ui.Team == 3 ? _catalog.FactionsCT : _catalog.FactionsT;
				ui.Chips = true;
				ui.ChipList.Add(("", Lang.T("all")));
				foreach (var faction in factions.Take(LayoutIds.Chips - 1))
					ui.ChipList.Add(faction);
				if (ui.Fac.Length > 0 && !ui.ChipList.Any(c => c.Key == ui.Fac))
					ui.Fac = "";

				var current = side?.Agent;
				var team = ui.Team;
				foreach (var agent in list)
				{
					if (ui.Fac.Length > 0 && agent.Group != ui.Fac)
						continue;
					var model = agent.Key;
					var faction = factions.FirstOrDefault(f => f.Key == agent.Group).Name ?? "";
					cards.Add(MarkOwn(new Card
					{
						Art = agent.Art,
						Ph = agent.Art.Length == 0 ? "ph-agent" : "",
						Name = agent.Name,
						Sub = AgentRarityName(agent.Rarity).Length > 0 ? faction + " - " + AgentRarityName(agent.Rarity) : faction,
						Rarity = agent.Rarity,
						Click = p => SetAgent(p, model)
					}, team, string.Equals(current, model, StringComparison.OrdinalIgnoreCase)));
				}
				lead = MarkOwn(new Card
				{
					Art = DefaultAgentArt(team),
					Name = Lang.T("def.agent"),
					Sub = Lang.T("def.agent.s"),
					Def = true,
					Click = p => SetAgent(p, null)
				}, team, string.IsNullOrEmpty(current));
				key = "ag" + ui.Team + ui.Fac;
				crumbs.Add(Lang.T("sec.agents"));
				sortable = true;
				break;
			}

			case ViewKind.Equip when ui.Sec == "gloves" && ui.GloveDef <= 0:
			{
				cards.Add(MarkOwn(new Card
				{
					Art = DefaultGlovesArt(ui.Team),
					Name = Lang.T("def.gloves"),
					Sub = Lang.T("def.gloves.s"),
					Def = true,
					Click = p => SetGlovesDefault(p)
				}, ui.Team, (side?.GloveDef ?? 0) <= 0));
				foreach (var type in _catalog.GloveTypes.Where(g => ItemShown(g.Id)))
				{
					var def = type.Id;
					cards.Add(Mark(new Card
					{
						Art = type.Art,
						Name = type.Name,
						Sub = Lang.F("n.skins", type.Group),
						Rarity = 6,
						Click = p => { var u = UiOf(p); if (u != null) u.GloveDef = def; }
					}, ui.Loadout, t => t.GloveDef == def));
				}
				key = "gt";
				crumbs.Add(Lang.T("sec.gloves"));
				break;
			}

			case ViewKind.Equip when ui.Sec == "gloves":
			{
				var def = ui.GloveDef;
				foreach (var paint in _catalog.PaintsOf(def))
				{
					var id = paint.Id;
					cards.Add(Mark(SkinCard(paint, p => OpenSettings(p, DraftKind.Glove, def, id, 0)), ui.Loadout, t => t.GloveDef == def && t.GlovePaint == id));
				}
				key = "gl" + def;
				crumbs.Add(Lang.T("sec.gloves"));
				crumbs.Add(_catalog.GloveTypes.FirstOrDefault(g => g.Id == def)?.Name ?? _catalog.Name(def));
				sortable = true;
				break;
			}

			case ViewKind.Equip when ui.Sec == "knife" && ui.KnifeDef <= 0:
			{
				var knife = side?.Knife ?? 0;
				var defaultKnife = Weapons.DefaultKnife(ui.Team);
				cards.Add(MarkOwn(new Card
				{
					Art = _catalog.BaseArt(defaultKnife, _items),
					Name = Lang.T("def.knife"),
					Sub = Lang.T("no.skin.s"),
					Def = true,
					Click = p => SetKnife(p, 0)
				}, ui.Team, knife <= 0));
				foreach (var item in _catalog.Knives.Where(k => ItemShown(k.Id)))
				{
					var def = item.Id;
					var count = _catalog.PaintsOf(def).Count;
					cards.Add(Mark(new Card
					{
						Art = item.Art,
						Name = item.Name,
						Sub = count > 0 ? Lang.F("n.skins", count) : Lang.T("no.skins"),
						Rarity = 6,
						Click = p => { var u = UiOf(p); if (u != null) u.KnifeDef = def; }
					}, ui.Loadout, t => t.Knife == def));
				}
				key = "kn";
				crumbs.Add(Lang.T("sec.knife"));
				break;
			}

			case ViewKind.Equip when ui.Sec == "knife":
			{
				var def = ui.KnifeDef;
				var equipped = side != null && side.Knife == def ? side.Paint(def) : -1;
				foreach (var paint in _catalog.PaintsOf(def))
				{
					var id = paint.Id;
					cards.Add(Mark(SkinCard(paint, p => OpenSettings(p, DraftKind.Knife, def, id, 0)), ui.Loadout, t => t.Knife == def && t.Paint(def) == id));
				}
				lead = MarkOwn(new Card
				{
					Art = _catalog.BaseArt(def, _items),
					Name = Lang.T("vanilla"),
					Sub = Lang.T("vanilla.s"),
					Def = true,
					Click = p => SaveVanilla(p, def)
				}, ui.Team, equipped == 0);
				key = "kn" + def;
				crumbs.Add(Lang.T("sec.knife"));
				crumbs.Add(_catalog.Name(def));
				sortable = true;
				break;
			}

			case ViewKind.Equip when ui.Sec == "zeus":
			{
				var def = Weapons.Zeus;
				var equipped = side?.Paint(def) ?? 0;
				foreach (var paint in _catalog.PaintsOf(def))
				{
					var id = paint.Id;
					cards.Add(Mark(SkinCard(paint, p => OpenSettings(p, DraftKind.Zeus, def, id, 0)), ui.Loadout, t => t.Paint(def) == id));
				}
				lead = MarkOwn(new Card
				{
					Art = _catalog.BaseArt(def, _items),
					Name = Lang.T("no.skin"),
					Sub = Lang.T("no.skin.s"),
					Def = true,
					Click = p => SaveVanilla(p, def)
				}, ui.Team, equipped <= 0);
				key = "zs";
				crumbs.Add(Lang.T("sec.zeus"));
				sortable = true;
				break;
			}

			case ViewKind.Equip when ui.Sec == "music":
			{
				var current = ui.Loadout?.Music ?? 0;
				foreach (var kit in _catalog.Music)
				{
					var id = kit.Id;
					cards.Add(new Card
					{
						Art = kit.Art,
						Ph = kit.Art.Length == 0 ? "ph-music" : "",
						Name = kit.Name,
						Sub = RarityName(kit.Rarity),
						Rarity = kit.Rarity,
						Eq = current == id,
						Click = p => SetMusic(p, id)
					});
				}
				lead = new Card
				{
					Ph = "ph-music",
					Name = Lang.T("no.music"),
					Sub = Lang.T("no.music.s"),
					Def = true,
					Eq = current <= 0,
					Click = p => SetMusic(p, 0)
				};
				key = "mk";
				crumbs.Add(Lang.T("sec.music"));
				sortable = true;
				break;
			}

			case ViewKind.Equip:
			{
				var current = ui.Loadout?.Pin ?? 0;
				foreach (var pin in _catalog.Pins)
				{
					var id = pin.Id;
					cards.Add(new Card
					{
						Art = pin.Art,
						Ph = pin.Art.Length == 0 ? "ph-pin" : "",
						Name = pin.Name,
						Sub = pin.Group.Length > 0 ? pin.Group : RarityName(pin.Rarity),
						Rarity = pin.Rarity,
						Eq = current == id,
						Click = p => SetPin(p, id)
					});
				}
				lead = new Card
				{
					Ph = "ph-pin",
					Name = Lang.T("no.pin"),
					Sub = Lang.T("no.pin.s"),
					Def = true,
					Eq = current <= 0,
					Click = p => SetPin(p, 0)
				};
				key = "pn";
				crumbs.Add(Lang.T("sec.pins"));
				sortable = true;
				break;
			}

			case ViewKind.Weapons when ui.WeaponDef <= 0:
			{
				var cat = Weapons.Cats[ui.Cat];
				foreach (var weapon in CatWeapons(cat))
				{
					var def = weapon.Def;
					var paint = side?.Paint(def) ?? 0;
					var item = paint > 0 ? _catalog.Paint(def, paint) : null;
					cards.Add(new Card
					{
						Art = item != null && item.Art.Length > 0 ? item.Art : _catalog.BaseArt(def, _items),
						Name = _catalog.Name(def),
						Sub = item?.Name ?? Lang.T("no.skin"),
						Rarity = item?.Rarity ?? 0,
						Dim = !Weapons.UsableBy(weapon, ui.Team),
						MarkCT = weapon.Team != 2,
						MarkT = weapon.Team != 3,
						Click = p => { var u = UiOf(p); if (u != null) u.WeaponDef = def; }
					});
				}
				key = "wl" + ui.Cat;
				crumbs.Add(Lang.T("cat." + cat));
				break;
			}

			case ViewKind.Weapons:
			{
				var def = ui.WeaponDef;
				var equipped = side?.Paint(def) ?? 0;
				foreach (var paint in _catalog.PaintsOf(def))
				{
					var id = paint.Id;
					cards.Add(Mark(SkinCard(paint, p => OpenSettings(p, DraftKind.Weapon, def, id, 0)), ui.Loadout, t => t.Paint(def) == id));
				}
				lead = MarkOwn(new Card
				{
					Art = _catalog.BaseArt(def, _items),
					Name = Lang.T("no.skin"),
					Sub = Lang.T("no.skin.s"),
					Def = true,
					Click = p => SaveVanilla(p, def)
				}, ui.Team, equipped <= 0);
				key = "ws" + def;
				crumbs.Add(Lang.T("cat." + Weapons.Cats[ui.Cat]));
				crumbs.Add(_catalog.Name(def));
				sortable = true;
				break;
			}
		}

		if (sortable)
		{
			var sorted = Sorted(cards, ui.Sort.GetValueOrDefault(key));
			// the equipped item goes first; the "no skin" card goes last unless that is what is equipped
			var arranged = sorted.Where(c => c.Eq).ToList();
			if (lead != null && lead.Eq)
				arranged.Insert(0, lead);
			arranged.AddRange(sorted.Where(c => !c.Eq));
			if (lead != null && !lead.Eq)
				arranged.Add(lead);
			cards = arranged;
		}
		else if (lead != null)
		{
			cards.Insert(0, lead);
		}

		ui.Cards = cards;
		ui.ListKey = key;
		ui.Crumbs = string.Join("  /  ", crumbs);
		ui.Back = crumbs.Count > 1;
		ui.Sortable = sortable;
	}

	private static Card SkinCard(Item paint, Action<CCSPlayerController> click) => new()
	{
		Art = paint.Art,
		Name = paint.Name,
		Sub = RarityName(paint.Rarity),
		Rarity = paint.Rarity,
		Click = click
	};

	private static List<Card> Sorted(List<Card> cards, int mode)
	{
		IEnumerable<Card> query = mode switch
		{
			1 => cards.OrderBy(c => c.Rarity).ThenBy(c => c.Name, NameOrder),
			2 => cards.OrderBy(c => c.Name, NameOrder),
			3 => cards.OrderByDescending(c => c.Name, NameOrder),
			_ => cards.OrderByDescending(c => c.Rarity).ThenBy(c => c.Name, NameOrder)
		};
		return query.ToList();
	}

	// ------------------------------------------------------------------ quick saves

	private void AfterSave(CCSPlayerController player, string? error, string label, string value)
	{
		var ui = UiOf(player);
		if (ui == null)
			return;
		if (error != null)
		{
			ShowToast(player, ErrorText(error), true);
			return;
		}
		ReloadLoadout(player, ui);
		ShowToast(player, Lang.F("toast.set", label, value), false);
	}

	private void SetAgent(CCSPlayerController player, string? model)
	{
		var ui = UiOf(player);
		if (ui == null)
			return;
		var error = _bridge.SaveAgent(player, Bit(ui.Team), model);
		var name = model != null && _catalog.AgentByModel.TryGetValue(model, out var agent) ? agent.Name : Lang.T("def.agent");
		AfterSave(player, error, Lang.T("sec.agents"), name);
		if (error == null)
			GoHome(ui);
	}

	private void SetGlovesDefault(CCSPlayerController player)
	{
		var ui = UiOf(player);
		if (ui == null)
			return;
		AfterSave(player, _bridge.SaveGloves(player, Bit(ui.Team), 0, 0, 0f, 0), Lang.T("sec.gloves"), Lang.T("def.gloves.n"));
	}

	private void SetKnife(CCSPlayerController player, int def)
	{
		var ui = UiOf(player);
		if (ui == null)
			return;
		AfterSave(player, _bridge.SaveKnife(player, Bit(ui.Team), def), Lang.T("sec.knife"), def > 0 ? _catalog.Name(def) : Lang.T("def.knife"));
	}

	/// <summary>Removes the skin (paint 0). For a knife this also selects that knife.</summary>
	private void SaveVanilla(CCSPlayerController player, int def)
	{
		var ui = UiOf(player);
		if (ui == null)
			return;
		var json = JsonSerializer.Serialize(new { p = 0, w = 0.000001f, s = 0, st = -1 });
		AfterSave(player, _bridge.SaveWeapon(player, Bit(ui.Team), def, json), _catalog.Name(def),
			Weapons.IsKnife(def) ? Lang.T("vanilla") : Lang.T("no.skin"));
	}

	private void SetMusic(CCSPlayerController player, int id)
	{
		var name = id > 0 ? _catalog.Music.FirstOrDefault(m => m.Id == id)?.Name ?? "#" + id : Lang.T("none");
		AfterSave(player, _bridge.SaveMusic(player, id), Lang.T("sec.music"), name);
	}

	private void SetPin(CCSPlayerController player, int id)
	{
		var name = id > 0 ? _catalog.Pins.FirstOrDefault(m => m.Id == id)?.Name ?? "#" + id : Lang.T("none");
		AfterSave(player, _bridge.SavePin(player, id), Lang.T("sec.pins"), name);
	}

	private static string ErrorText(string error)
	{
		if (error.StartsWith("agent_cooldown:", StringComparison.Ordinal))
			return Lang.F("err.agent_cooldown", error["agent_cooldown:".Length..]);
		return error switch
		{
			"cooldown" or "link_required" or "loadout_not_ready" or "data_not_loaded" or "not_ready" or "invalid" or "api" => Lang.T("err." + error),
			_ => Lang.F("err.unknown", error)
		};
	}

	// ------------------------------------------------------------------ settings

	private void OpenSettings(CCSPlayerController player, DraftKind kind, int def, int paint, int tab)
	{
		var ui = UiOf(player);
		var item = _catalog.Paint(def, paint);
		if (ui == null || item == null)
			return;

		var side = ui.Side;
		var other = ui.Loadout?.For(ui.Team == 2 ? 3 : 2);
		// the side being edited first; a skin set up only for the other side starts from that side's settings
		var saved = side?.SavedEntry(def, paint) ?? other?.SavedEntry(def, paint);
		var knife = kind == DraftKind.Knife;
		var min = Math.Max(item.Min, knife ? 0.01f : 0.0001f);
		var max = Math.Max(item.Max, min);
		var draft = new Draft
		{
			Kind = kind,
			Def = def,
			Paint = paint,
			Art = item.Art,
			Rarity = item.Rarity,
			SkinName = item.Name,
			TypeName = kind == DraftKind.Glove
				? _catalog.GloveTypes.FirstOrDefault(g => g.Id == def)?.Name ?? _catalog.Name(def)
				: _catalog.Name(def),
			Min = min,
			Max = max,
			Wear = Math.Clamp(saved?.Wear ?? min, min, max),
			Seed = Math.Clamp(saved?.Seed ?? 0, 0, 1000),
			Tag = saved?.Tag is { Length: > 0 } tag ? tag : null,
			St = saved != null && saved.StatTrak >= 0,
			StCount = Math.Max(saved?.StatTrak ?? 0, 0),
			Tab = tab,
			WearDefault = min,
			StickersAllowed = kind == DraftKind.Weapon && _bridge.StickersEnabled && _bridge.StickersAllowed(player)
		};
		ApplyFeatureLocks(player, draft);

		if (kind == DraftKind.Glove && side != null && side.GloveDef == def && side.GlovePaint == paint)
		{
			draft.Wear = Math.Clamp(side.GloveWear > 0f ? side.GloveWear : draft.Wear, min, max);
			draft.Seed = Math.Clamp(side.GloveSeed, 0, 1000);
		}

		// "Team": the side the player is setting up (the menu's side), or both when this skin is on both sides already
		bool On(TeamDto? t) => t != null && kind switch
		{
			DraftKind.Glove => t.GloveDef == def && t.GlovePaint == paint,
			DraftKind.Knife => t.Knife == def && t.Paint(def) == paint,
			_ => t.Paint(def) == paint,
		};
		draft.Side = On(side) && On(other) ? 1 : ui.Team == 2 ? 2 : 0;

		// what the player may not change shows (and is saved) as the default
		if (draft.LkWear)
			draft.Wear = draft.WearDefault;
		if (draft.LkSeed)
			draft.Seed = 0;
		if (draft.LkSt)
			draft.St = false;
		if (draft.LkTag)
			draft.Tag = null;

		if (saved?.Stickers != null)
		{
			foreach (var s in saved.Stickers)
			{
				if (s.Slot is >= 0 and <= Draft.MaxSlot && s.Id > 0 && !draft.SlotLocked(s.Slot))
					draft.Stickers[s.Slot] = new StickerDraft
					{
						Id = s.Id, X = s.X, Y = s.Y, Rot = s.Rotation, Wear = s.Wear,
						Schema = s.Slot >= 4 && s.Schema < 0 ? DefaultSchema(def, s.Slot) : s.Schema,
					};
			}
		}
		if (saved?.Charm is { Id: > 0 } charm && !draft.LkKc)
			draft.Charm = LoadCharm(def, charm.Id, charm.X, charm.Y, charm.Z, charm.Pattern);

		if (!draft.HasDecor || (draft.Tab == 1 && draft.LkStk) || (draft.Tab == 2 && draft.LkKc))
			draft.Tab = 0;
		// the first slot the player may use
		draft.Slot = Math.Clamp(Math.Min(draft.Slot, draft.StkLimit - 1), 0, Draft.MaxSlot);
		ui.Set = draft;
		ui.Dd = false;
		ui.StPage = 0;
	}

	private void ApplySettings(CCSPlayerController player, Ui ui)
	{
		var d = ui.Set;
		if (d == null)
			return;

		var teams = d.Side == 0 ? 2 : d.Side == 2 ? 1 : 3;
		// settings the player may not use are saved with their defaults (config "Features", "StickerSlots")
		var wear = d.LkWear ? d.WearDefault : d.Wear;
		var seed = d.LkSeed ? 0 : d.Seed;
		string? error;
		if (d.Kind == DraftKind.Glove)
		{
			error = _bridge.SaveGloves(player, teams, d.Def, d.Paint, wear, seed);
		}
		else
		{
			var stickers = new List<object>();
			for (var i = 0; i < d.Stickers.Length; i++)
			{
				if (d.Stickers[i] is { } s && d.HasDecor && !d.SlotLocked(i))
					stickers.Add(new { sl = i, i = s.Id, w = s.Wear, sc = 1f, r = s.Rot, x = s.X, y = s.Y, sh = i >= 4 ? Math.Max(s.Schema, 0) : s.Schema }); // CS2 ignores sticker scale
			}
			object? charm = d.HasDecor && d.Charm != null && !d.LkKc
				? new { i = d.Charm.Id, p = d.Charm.Pattern, x = d.Charm.X, y = d.Charm.Y, z = d.Charm.Z }
				: null;
			var st = !d.HasStatTrak || !d.St || d.LkSt ? -1 : d.StEdited ? d.StCount : -2;
			var json = JsonSerializer.Serialize(new
			{
				p = d.Paint,
				w = wear,
				s = seed,
				tag = d.LkTag ? null : d.Tag,
				st,
				stk = stickers,
				ch = charm
			});
			error = _bridge.SaveWeapon(player, teams, d.Def, json);
		}

		if (error != null)
		{
			ShowToast(player, ErrorText(error), true);
			return;
		}

		ReloadLoadout(player, ui);
		var sideText = Lang.T(d.Side == 0 ? "side.ct" : d.Side == 2 ? "side.t" : "side.both").ToLowerInvariant();
		ShowToast(player, Lang.F("toast.applied", d.FullName, sideText), false);
		ui.Set = null;
	}

	// ------------------------------------------------------------------ sticker / charm picker

	private void BuildPicker(Ui ui)
	{
		var d = ui.Set;
		if (d == null)
			return;
		var charms = d.Tab == 2;
		var key = charms
			? $"k#{_catalog.Stamp}#{ui.KcSort}#{ui.KcQuery}"
			: $"s#{_catalog.Stamp}#{ui.StSort}#{ui.StRar}#{ui.StSec}#{ui.StQuery}";
		if (key == ui.PickerKey)
			return;
		ui.PickerKey = key;
		if (charms)
		{
			var query = ui.KcQuery;
			ui.Picker = Sorted(_catalog.Charms.Where(c => query.Length == 0 || c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)), ui.KcSort);
			return;
		}
		var rarity = ui.StRar <= 0 ? 0 : ui.StRar + 2;
		var search = ui.StQuery;
		var sec = ui.StSec;
		// A search from "regular" or "all" looks through every sticker; inside a tournament it stays in that tournament.
		var anySec = sec == "*" || (search.Length > 0 && sec.Length == 0);
		ui.Picker = Sorted(_catalog.Stickers.Where(s =>
			(rarity == 0 || s.Rarity == rarity) &&
			(anySec || s.Event == sec) &&
			(search.Length == 0 || s.Name.Contains(search, StringComparison.OrdinalIgnoreCase))), ui.StSort);
	}

	/// <summary>Picker sorting, see PSORTS in tools/gen_layout.py. Items arrive newest first.</summary>
	private static List<Item> Sorted(IEnumerable<Item> items, int mode) => (mode switch
	{
		1 => items.OrderByDescending(i => i.Ord),
		2 => items.OrderByDescending(i => i.Rarity).ThenBy(i => i.Ord),
		3 => items.OrderBy(i => i.Rarity).ThenBy(i => i.Ord),
		4 => items.OrderBy(i => i.SortName, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.Ord),
		5 => items.OrderByDescending(i => i.SortName, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.Ord),
		_ => items.OrderBy(i => i.Ord),
	}).ToList();

	/// <summary>Section of the sticker picker by its row in the dropdown: 0 regular, 1 all, then tournaments.</summary>
	private string StickerSection(int row) => row switch
	{
		0 => "",
		1 => "*",
		_ => row - 2 < _catalog.StickerEvents.Count ? _catalog.StickerEvents[row - 2].Name : "",
	};

	private void Pick(Ui ui, int index)
	{
		var d = ui.Set;
		if (d == null || !d.HasDecor || index < 0 || index >= ui.Picker.Count)
			return;
		var item = ui.Picker[index];
		if (d.Tab == 2)
		{
			if (d.Charm != null)
				d.Charm.Id = item.Id;
			else
				d.Charm = LoadCharm(d.Def, item.Id, 0f, 0f, 0f, 0);
			d.EditCharm = false;
			return;
		}
		var slot = Math.Clamp(d.Slot, 0, Draft.MaxSlot);
		if (d.Stickers[slot] is { } current)
			current.Id = item.Id;
		else
			d.Stickers[slot] = new StickerDraft { Id = item.Id, Schema = DefaultSchema(d.Def, slot) };
		d.EditSticker = false;
	}

	// ------------------------------------------------------------------ preview geometry

	// Stickers: real geometry from the CS2 models, see StickerGeo. X / Y are the game's own
	// "sticker slot N offset x/y": a shift from the slot's default spot, x towards the stock, y downwards.
	private const float StickerStep = 0.01f;

	/// <summary>True when the preview shows a skin render, false for the plain weapon render.</summary>
	private static bool SkinArt(Draft d) => d.Paint > 0;

	/// <summary>
	/// The model sticker spot a sticker's X / Y are measured from. Without a schema slots 0..3 use their own spot;
	/// a schema (always present on the fifth sticker, slot 4) names another one. Mirrors WeaponApplier, which writes
	/// "sticker slot N schema" only when the schema is not 0 or the slot is 4.
	/// </summary>
	private static int Spot(Draft d, int slot)
	{
		var schema = d.Stickers[Math.Clamp(slot, 0, Draft.MaxSlot)]?.Schema ?? -1;
		return schema > 0 || slot >= 4 ? Math.Max(schema, 0) : slot;
	}

	/// <summary>Schema of a new sticker: the fifth one takes the model's fifth spot, or slot 1's spot when there is none.</summary>
	private static int DefaultSchema(int def, int slot) => slot < 4 ? -1 : (StickerGeo.For(def)?.Spots ?? 0) >= 5 ? 4 : 0;

	/// <summary>Allowed X / Y range of a slot: the bounding box of the weapon's sticker surface.</summary>
	private static (float MinX, float MaxX, float MinY, float MaxY) StickerRange(Draft d, int slot)
	{
		var geo = StickerGeo.For(d.Def);
		if (geo == null)
			return (-0.6f, 0.6f, -0.4f, 0.4f);
		var spot = Spot(d, slot);
		return (Floor2(geo.MinU - geo.SlotX(spot)), Ceil2(geo.MaxU - geo.SlotX(spot)),
			Floor2(geo.MinV - geo.SlotY(spot)), Ceil2(geo.MaxV - geo.SlotY(spot)));
	}

	/// <summary>Whether a sticker shifted by (x, y) from its spot still lies on the weapon. Unknown weapons allow anything.</summary>
	private static bool StickerFits(Draft d, int slot, float x, float y)
	{
		var geo = StickerGeo.For(d.Def);
		var spot = Spot(d, slot);
		return geo == null || geo.OnSurface(geo.SlotX(spot) + x, geo.SlotY(spot) + y);
	}

	private static bool MoveSticker(Draft d, StickerDraft s, int slot, float dx, float dy)
	{
		var x = Round2(s.X + dx);
		var y = Round2(s.Y + dy);
		if (!StickerFits(d, slot, x, y))
			return false;
		s.X = x;
		s.Y = y;
		return true;
	}

	// Charms: KeychainMarkup quads from the CS2 models, see StickerGeo. The keychain offset x / y / z attributes are
	// absolute model coordinates of the attachment point, which must lie on one of those quads.

	/// <summary>
	/// Builds a charm draft. (0, 0, 0) - what WeaponSkins' own menu saves - and points that are not on the weapon
	/// become Valve's default spot, so the charm never hangs in the air.
	/// </summary>
	private static CharmDraft LoadCharm(int def, int id, float x, float y, float z, int pattern)
	{
		var c = new CharmDraft { Id = id, Pattern = pattern, X = x, Y = y, Z = z };
		var geo = StickerGeo.For(def);
		if (geo == null || !geo.HasCharmGeo)
		{
			c.Default = x == 0f && y == 0f && z == 0f;
			return c;
		}
		var (dx, dy, dz) = geo.CharmDefault();
		var onWeapon = (x != 0f || y != 0f || z != 0f) && geo.CharmAt(x, z, out _);
		if (!onWeapon || (MathF.Abs(x - dx) < 0.01f && MathF.Abs(z - dz) < 0.01f))
		{
			c.X = dx; c.Y = dy; c.Z = dz;
			c.Default = true;
		}
		else
		{
			c.Default = false;
		}
		return c;
	}

	private static void ResetCharm(Draft d, CharmDraft c)
	{
		var geo = StickerGeo.For(d.Def);
		if (geo != null && geo.HasCharmGeo)
			(c.X, c.Y, c.Z) = geo.CharmDefault();
		else
			c.X = c.Y = c.Z = 0f;
		c.Default = true;
	}

	/// <summary>Puts the charm on the surface point (x along, z up). False when there is no charm surface there.</summary>
	private static bool SetCharmPoint(Draft d, CharmDraft c, float x, float z)
	{
		var geo = StickerGeo.For(d.Def);
		if (geo == null || !geo.HasCharmGeo || !geo.CharmAt(x, z, out var y))
			return false;
		c.X = MathF.Round(x * 1000f) / 1000f;
		c.Y = MathF.Round(y * 1000f) / 1000f;
		c.Z = MathF.Round(z * 1000f) / 1000f;
		c.Default = false;
		return true;
	}

	private static bool MoveCharm(Draft d, CharmDraft c, int dx, int dz)
	{
		var geo = StickerGeo.For(d.Def);
		if (geo == null || !geo.HasCharmGeo)
			return false;
		var step = geo.CharmStep;
		return SetCharmPoint(d, c, c.X + dx * step, c.Z + dz * step);
	}

	private static float Round2(float v) => MathF.Round(v * 100f) / 100f;
	private static float RoundHalf(float v) => MathF.Round(v * 2f) / 2f;

	private static readonly (float, float)[] CellProbes =
	{
		(0f, 0f), (-0.3f, 0f), (0.3f, 0f), (0f, -0.3f), (0f, 0.3f), (-0.3f, -0.3f), (0.3f, -0.3f), (-0.3f, 0.3f), (0.3f, 0.3f)
	};

	private static float Floor2(float v) => MathF.Floor(v * 100f) / 100f;
	private static float Ceil2(float v) => MathF.Ceiling(v * 100f) / 100f;

	/// <summary>Puts the sticker under a click on the preview. False when the click is off the weapon's sticker surface.</summary>
	private static bool PlaceSticker(Draft d, StickerDraft s, int slot, int col, int row)
	{
		var geo = StickerGeo.For(d.Def);
		if (geo == null)
			return false;
		var spot = Spot(d, slot);
		// A cell is 30 px; thin parts (a stock, a slide) may miss its centre, so try a few points inside it,
		// nearest to the centre first.
		foreach (var (ox, oy) in CellProbes)
		{
			var fx = (col + 0.5f + ox) / LayoutIds.ClickCols;
			var fy = (row + 0.5f + oy) / LayoutIds.ClickRows;
			if (!geo.Unmap(SkinArt(d), fx, fy, out var u, out var v))
				continue;
			var x = Round2(u - geo.SlotX(spot));
			var y = Round2(v - geo.SlotY(spot));
			if (!geo.OnSurface(geo.SlotX(spot) + x, geo.SlotY(spot) + y))
				continue;
			s.X = x;
			s.Y = y;
			return true;
		}
		return false;
	}

	/// <summary>Hangs the charm under a click on the preview. False when the click is not on a charm surface.</summary>
	private static bool PlaceCharm(Draft d, CharmDraft c, int col, int row)
	{
		var geo = StickerGeo.For(d.Def);
		if (geo == null || !geo.HasCharmGeo)
			return false;
		foreach (var (ox, oy) in CellProbes)
		{
			var fx = (col + 0.5f + ox) / LayoutIds.ClickCols;
			var fy = (row + 0.5f + oy) / LayoutIds.ClickRows;
			if (geo.UnmapCharm(SkinArt(d), fx, fy, out var x, out var z) && SetCharmPoint(d, c, x, z))
				return true;
		}
		return false;
	}

	private static string Fmt2(float v) => (v < 0f ? "-" : "+") + Lang.Num(Math.Abs(v), "0.00");
	private static string Fmt1(float v) => (v < 0f ? "-" : "+") + Lang.Num(Math.Abs(v), "0.0");

	private static int WearZone(float wear) => wear >= 0.45f ? 4 : wear >= 0.38f ? 3 : wear >= 0.15f ? 2 : wear >= 0.07f ? 1 : 0;

	private static readonly float[] WearZoneStart = { 0f, 0.07f, 0.15f, 0.38f, 0.45f };

}
