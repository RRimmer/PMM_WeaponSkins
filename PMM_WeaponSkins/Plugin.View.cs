using System.Globalization;
using CounterStrikeSharp.API.Core;

namespace PMM_WeaponSkins;

/// <summary>Draws the state of one player into pmm_ws.xml: classes on panels and {s:...} variables.</summary>
public partial class Plugin
{
	private void RootClass(CCSPlayerController player, string cls, bool on) => SetClass(player, Root, cls, on);

	private static string R(int rarity) => rarity is >= 1 and <= 7 ? "r" + rarity : "";

	private static int Step(float fraction, int steps) => Math.Clamp((int)MathF.Round(fraction * steps), 0, steps);

	private void Render(CCSPlayerController player)
	{
		var ui = UiOf(player);
		if (ui == null || !ui.Open || _layout == null || !_layout.IsValid)
			return;

		var d = ui.Set;
		var prompt = ui.Prompt;
		var keyboard = prompt != null && !prompt.Chat;
		var editSticker = d != null && d.HasDecor && d.Tab == 1 && d.EditSticker && d.Stickers[Math.Clamp(d.Slot, 0, Draft.MaxSlot)] != null;
		var editCharm = d != null && d.HasDecor && d.Tab == 2 && d.EditCharm && d.Charm != null;
		var listView = ui.View is ViewKind.Agents or ViewKind.Equip or ViewKind.Weapons;

		RootClass(player, "team-ct", ui.Team == 3);
		RootClass(player, "team-t", ui.Team == 2);
		RenderTheme(player, ui);
		RootClass(player, "lang-en", Lang.LayoutEn);
		RootClass(player, "lang-tr", Lang.Tr);
		RenderScale(player);
		RootClass(player, "v-home", ui.View == ViewKind.Home);
		RootClass(player, "v-agents", ui.View == ViewKind.Agents);
		RootClass(player, "v-equip", ui.View == ViewKind.Equip);
		RootClass(player, "v-weapons", ui.View == ViewKind.Weapons);
		RootClass(player, "m-set", d != null && listView && ui.View != ViewKind.Agents);
		RootClass(player, "set-glove", d?.Kind == DraftKind.Glove);
		RootClass(player, "set-knife", d?.Kind is DraftKind.Knife or DraftKind.Zeus);
		RootClass(player, "set-weapon", d?.Kind == DraftKind.Weapon);
		RootClass(player, "nost", d != null && !d.HasDecor);
		RootClass(player, "tab-p", d != null && d.Tab == 0);
		RootClass(player, "tab-s", d != null && d.Tab == 1);
		RootClass(player, "tab-k", d != null && d.Tab == 2);
		RootClass(player, "ed-s", editSticker);
		RootClass(player, "ed-k", editCharm);
		RootClass(player, "dd", ui.Dd && d == null);

		SetVar(player, "subtitle", Subtitle(ui));

		switch (ui.View)
		{
			case ViewKind.Home:
				RenderHome(player, ui);
				break;
			case ViewKind.Agents:
				RenderHero(player, ui, "pw-mh-art");
				RenderList(player, ui);
				break;
			default:
				RenderNav(player, ui);
				if (d != null)
					RenderSettings(player, ui, d, editSticker, editCharm);
				else
					RenderList(player, ui);
				break;
		}

		RenderKeyboard(player, ui, keyboard ? prompt : null);
	}

	private string Subtitle(Ui ui)
	{
		var team = TeamName(ui.Team);
		return ui.View switch
		{
			ViewKind.Agents => team + " / " + Lang.T("sec.agents"),
			ViewKind.Equip => team + " / " + Lang.T("sec." + ui.Sec),
			ViewKind.Weapons => team + " / " + Lang.T("cat." + Weapons.Cats[ui.Cat]),
			_ => team
		};
	}

	// ------------------------------------------------------------------ equipped item of a section

	private (string Art, string Name, int Rarity) Equipped(Ui ui, string sec)
	{
		var side = ui.Side;
		switch (sec)
		{
			case "agents":
			{
				var model = side?.Agent;
				if (!string.IsNullOrEmpty(model) && _catalog.AgentByModel.TryGetValue(model, out var agent))
					return (agent.Art.Length > 0 ? agent.Art : "ph-agent", agent.Name, agent.Rarity);
				return (DefaultAgentArt(ui.Team), Lang.T("def.agent"), 0);
			}
			case "gloves":
			{
				if (side != null && side.GloveDef > 0)
				{
					var paint = _catalog.Paint(side.GloveDef, side.GlovePaint);
					var type = _catalog.GloveTypes.FirstOrDefault(g => g.Id == side.GloveDef)?.Name ?? _catalog.Name(side.GloveDef);
					if (paint != null)
						return (paint.Art.Length > 0 ? paint.Art : "ph-glove", type + " | " + paint.Name, paint.Rarity);
				}
				return (DefaultGlovesArt(ui.Team), Lang.T("def.gloves.n"), 0);
			}
			case "knife":
			{
				var def = side?.Knife ?? 0;
				if (def <= 0)
					return (_catalog.BaseArt(Weapons.DefaultKnife(ui.Team), _items), Lang.T("def.knife"), 0);
				var paint = side!.Paint(def);
				var item = paint > 0 ? _catalog.Paint(def, paint) : null;
				return item != null
					? (item.Art.Length > 0 ? item.Art : _catalog.BaseArt(def, _items), _catalog.Name(def) + " | " + item.Name, item.Rarity)
					: (_catalog.BaseArt(def, _items), _catalog.Name(def), 6);
			}
			case "zeus":
			{
				var paint = side?.Paint(Weapons.Zeus) ?? 0;
				var item = paint > 0 ? _catalog.Paint(Weapons.Zeus, paint) : null;
				return item != null
					? (item.Art, _catalog.Name(Weapons.Zeus) + " | " + item.Name, item.Rarity)
					: (_catalog.BaseArt(Weapons.Zeus, _items), _catalog.Name(Weapons.Zeus), 0);
			}
			case "music":
			{
				var id = ui.Loadout?.Music ?? 0;
				var kit = id > 0 ? _catalog.Music.FirstOrDefault(m => m.Id == id) : null;
				return kit != null ? (kit.Art.Length > 0 ? kit.Art : "ph-music", kit.Name, kit.Rarity) : ("ph-music", Lang.T("none"), 0);
			}
			default:
			{
				var id = ui.Loadout?.Pin ?? 0;
				var pin = id > 0 ? _catalog.Pins.FirstOrDefault(m => m.Id == id) : null;
				return pin != null ? (pin.Art.Length > 0 ? pin.Art : "ph-pin", pin.Name, pin.Rarity) : ("ph-pin", Lang.T("none"), 0);
			}
		}
	}

	private (string Art, int Rarity) WeaponArt(TeamDto? side, int def)
	{
		var paint = side?.Paint(def) ?? 0;
		var item = paint > 0 ? _catalog.Paint(def, paint) : null;
		return item != null && item.Art.Length > 0 ? (item.Art, item.Rarity) : (_catalog.BaseArt(def, _items), 0);
	}

	// ------------------------------------------------------------------ home

	private static readonly string[] HomeTiles = { "agents", "gloves", "knife", "zeus", "music", "pins" };

	private void RenderHome(CCSPlayerController player, Ui ui)
	{
		for (var i = 0; i < HomeTiles.Length; i++)
		{
			SetClass(player, $"pw-ht-{i}", "off", !SecShown(HomeTiles[i]));
			SetClass(player, $"pw-ht-{i}", "lock", !SecOpen(player, HomeTiles[i]));
			var (art, name, rarity) = Equipped(ui, HomeTiles[i]);
			Exclusive(player, $"pw-ht-art-{i}", "art", art);
			Exclusive(player, $"pw-ht-{i}", "r", R(rarity));
			SetVar(player, $"ht_n_{i}", name);
		}

		RenderHero(player, ui, "pw-hero-art");
		RootClass(player, "noag", !SecShown("agents"));
		SetClass(player, "pw-hero", "lock", !SecOpen(player, "agents"));

		var side = ui.Side;
		for (var r = 0; r < 2; r++)
			SetClass(player, $"pw-crow-{r}", "off", !CatShown(r * 3) && !CatShown(r * 3 + 1) && !CatShown(r * 3 + 2));
		for (var i = 0; i < Weapons.Cats.Length; i++)
		{
			var cat = Weapons.Cats[i];
			SetClass(player, $"pw-hc-{i}", "off", !CatShown(i));
			SetClass(player, $"pw-hc-{i}", "lock", !CatOpen(player, i));
			var usable = CatWeapons(cat).Where(w => Weapons.UsableBy(w, ui.Team)).ToList();
			var skinned = usable.Where(w => (side?.Paint(w.Def) ?? 0) > 0).ToList();
			var show = skinned.Concat(usable.Where(w => (side?.Paint(w.Def) ?? 0) <= 0)).Take(4).ToList();
			for (var j = 0; j < 4; j++)
			{
				var panel = $"pw-hc-{i}-{j}";
				if (j < show.Count)
				{
					var (art, rarity) = WeaponArt(side, show[j].Def);
					Exclusive(player, panel, "art", art);
					Exclusive(player, panel, "r", R(rarity));
					SetClass(player, panel, "off", false);
				}
				else
				{
					SetClass(player, panel, "off", true);
				}
			}
			SetVar(player, $"hc_c_{i}", Lang.F("skinned", skinned.Count, usable.Count));
		}

		var other = ui.Team == 2 ? 3 : 2;
		var otherModel = ui.Loadout?.For(other).Agent;
		var otherArt = !string.IsNullOrEmpty(otherModel) && _catalog.AgentByModel.TryGetValue(otherModel, out var otherAgent) && otherAgent.Art.Length > 0
			? otherAgent.Art
			: DefaultAgentArt(other);
		Exclusive(player, "pw-other-art", "art", otherArt);
		SetVar(player, "other_team", TeamName(other));
		SetVar(player, "other_sw", Lang.F("other.sw", TeamName(other)));
	}

	private void RenderHero(CCSPlayerController player, Ui ui, string panel)
	{
		var model = ui.Side?.Agent;
		Item? agent = null;
		if (!string.IsNullOrEmpty(model))
			_catalog.AgentByModel.TryGetValue(model, out agent);
		Exclusive(player, panel, "art", agent != null && agent.Art.Length > 0 ? agent.Art : DefaultAgentArt(ui.Team));
		SetVar(player, "team_name", TeamName(ui.Team));
		SetVar(player, "hero_n", agent?.Name ?? Lang.T("def.agent"));
		if (agent == null)
		{
			SetVar(player, "hero_f", Lang.T("def.agent.s"));
			return;
		}
		var factions = ui.Team == 3 ? _catalog.FactionsCT : _catalog.FactionsT;
		var faction = factions.FirstOrDefault(f => f.Key == agent.Group).Name ?? "";
		var rarity = AgentRarityName(agent.Rarity);
		SetVar(player, "hero_f", rarity.Length > 0 ? faction + " - " + rarity : faction);
	}

	// ------------------------------------------------------------------ left navigation

	private void RenderNav(CCSPlayerController player, Ui ui)
	{
		if (ui.View == ViewKind.Weapons)
		{
			var side = ui.Side;
			var cats = ShownCats;
			for (var i = 0; i < LayoutIds.Nav; i++)
			{
				if (i >= cats.Length)
				{
					SetClass(player, $"pw-nav-{i}", "off", true);
					continue;
				}
				var cat = Weapons.Cats[cats[i]];
				var usable = CatWeapons(cat).Where(w => Weapons.UsableBy(w, ui.Team)).ToList();
				var skinned = usable.Where(w => (side?.Paint(w.Def) ?? 0) > 0).ToList();
				var rep = skinned.FirstOrDefault() ?? usable.FirstOrDefault();
				var (art, rarity) = rep != null ? WeaponArt(side, rep.Def) : ("", 0);
				Exclusive(player, $"pw-nav-art-{i}", "art", art);
				Exclusive(player, $"pw-nav-{i}", "r", R(rarity));
				SetVar(player, $"nav_l_{i}", Lang.T("cat." + cat));
				SetVar(player, $"nav_s_{i}", Lang.F("skinned", skinned.Count, usable.Count));
				SetClass(player, $"pw-nav-{i}", "on", ui.Cat == cats[i]);
				SetClass(player, $"pw-nav-{i}", "lock", !CatOpen(player, cats[i]));
				SetClass(player, $"pw-nav-{i}", "off", false);
			}
			return;
		}

		var secs = ShownSecs;
		for (var i = 0; i < LayoutIds.Nav; i++)
		{
			if (i >= secs.Length)
			{
				SetClass(player, $"pw-nav-{i}", "off", true);
				continue;
			}
			var (art, name, rarity) = Equipped(ui, secs[i]);
			Exclusive(player, $"pw-nav-art-{i}", "art", art);
			Exclusive(player, $"pw-nav-{i}", "r", R(rarity));
			SetVar(player, $"nav_l_{i}", Lang.T("sec." + secs[i]));
			SetVar(player, $"nav_s_{i}", name);
			SetClass(player, $"pw-nav-{i}", "on", ui.Sec == secs[i]);
			SetClass(player, $"pw-nav-{i}", "lock", !SecOpen(player, secs[i]));
			SetClass(player, $"pw-nav-{i}", "off", false);
		}
	}

	// ------------------------------------------------------------------ right grid

	private void RenderList(CCSPlayerController player, Ui ui)
	{
		BuildList(player, ui);
		var mode = ui.Sort.GetValueOrDefault(ui.ListKey);
		SetVar(player, "crumbs", ui.Crumbs);
		SetVar(player, "count", Lang.F("count", ui.Cards.Count - ui.Cards.Count(c => c.Def)));
		SetVar(player, "sort_l", Lang.T("sort." + mode));
		RootClass(player, "noback", !ui.Back);
		RootClass(player, "sortable", ui.Sortable);
		RootClass(player, "chips", ui.Chips);
		for (var i = 0; i < 4; i++)
			SetClass(player, $"pw-sort-{i}", "on", mode == i);

		for (var i = 0; i < LayoutIds.Chips; i++)
		{
			var id = $"pw-chip-{i}";
			if (ui.Chips && i < ui.ChipList.Count)
			{
				SetVar(player, $"chip_{i}", ui.ChipList[i].Name);
				SetClass(player, id, "off", false);
				SetClass(player, id, "on", ui.ChipList[i].Key == ui.Fac);
			}
			else
			{
				SetClass(player, id, "off", true);
			}
		}

		var total = ui.Cards.Count;
		var per = Pages ? LayoutIds.GridCols * 3 : LayoutIds.GridCards;
		var pages = Math.Max(1, (total + per - 1) / per);
		var page = Math.Clamp(ui.Page.GetValueOrDefault(ui.ListKey), 0, pages - 1);
		ui.Page[ui.ListKey] = page;
		var start = page * per;
		var count = Math.Max(0, Math.Min(per, total - start));
		ui.CardStart = start;
		RootClass(player, "pages", pages > 1);
		SetVar(player, "page", Pages
			? Lang.F("page", page + 1, pages)
			: Lang.F("chunk", count > 0 ? start + 1 : 0, start + count, total));

		var rows = (count + LayoutIds.GridCols - 1) / LayoutIds.GridCols;
		for (var r = 0; r < LayoutIds.GridRows; r++)
			SetClass(player, $"pw-row-{r}", "off", r >= rows);

		for (var i = 0; i < LayoutIds.GridCards; i++)
		{
			var id = $"pw-c-{i}";
			if (i >= count)
			{
				if (i < rows * LayoutIds.GridCols)
					SetClass(player, id, "off", true);
				continue;
			}
			var card = ui.Cards[start + i];
			SetClass(player, id, "off", false);
			SetVar(player, $"cn_{i}", card.Name);
			SetVar(player, $"cs_{i}", card.Sub);
			Exclusive(player, $"pw-c-art-{i}", "art", card.Art.Length > 0 ? card.Art : card.Ph);
			Exclusive(player, id, "r", R(card.Rarity));
			SetClass(player, id, "eq", card.Eq);
			SetClass(player, id, "eq-ct", card.Eq && card.EqCT);
			SetClass(player, id, "eq-t", card.Eq && card.EqT);
			// badge and frame in the colour of the side it is equipped for (both sides: the side being edited)
			var eqAcc = card.EqCT && card.EqT ? (ui.Team == 3 ? _accCT : _accT) : card.EqCT ? _accCT : _accT;
			Exclusive(player, id, "eqa", card.Eq ? "eqa-" + eqAcc : null);
			SetClass(player, id, "def", card.Def);
			SetClass(player, id, "dim", card.Dim);
			SetClass(player, id, "tm-ct", card.MarkCT && ui.View == ViewKind.Weapons);
			SetClass(player, id, "tm-t", card.MarkT && ui.View == ViewKind.Weapons);
		}

		var shown = ui.ListKey + "#" + page + "#" + mode;
		if (shown != ui.ShownListKey)
		{
			ui.ShownListKey = shown;
			if (!Pages)
				ResetScroll(player, ui, "pw-grid");
		}
	}

	/// <summary>
	/// Panorama keeps the scroll offset of a panel. Switching overflow off for a moment (with the panel faded)
	/// brings a new list back to its top. Needs two ticks apart, so it runs on a short timer.
	/// </summary>
	private void ResetScroll(CCSPlayerController player, Ui ui, string panel)
	{
		SetClass(player, panel, "rs", true);
		var token = ui.Token;
		Later(0.3f, () =>
		{
			if (player.IsValid && ui.Token == token)
				SetClass(player, panel, "rs", false);
		});
	}

	// ------------------------------------------------------------------ skin settings

	private static string Pad6(int value) => Math.Clamp(value, 0, 999999).ToString("000000", CultureInfo.InvariantCulture);

	private void RenderSettings(CCSPlayerController player, Ui ui, Draft d, bool editSticker, bool editCharm)
	{
		var kind = d.Kind switch
		{
			DraftKind.Glove => "kind.glove",
			DraftKind.Knife => "kind.knife",
			DraftKind.Zeus => "kind.zeus",
			_ => "kind.weapon"
		};
		SetVar(player, "s_kind", Lang.T(kind));
		SetVar(player, "s_name", d.FullName);
		SetVar(player, "s_rar", RarityName(d.Rarity));
		Exclusive(player, "pw-s-rar", "rc", d.Rarity is >= 1 and <= 7 ? "rc" + d.Rarity : "");
		Exclusive(player, "pw-s-art", "art", d.Art);

		RootClass(player, "stled", d.HasStatTrak && d.St);
		SetVar(player, "s_led", "ST " + Pad6(d.StCount));
		RootClass(player, "hastag", d.Tag != null);
		SetVar(player, "s_tag", d.Tag != null ? "\"" + d.Tag + "\"" : "");
		var sideKey = d.Side == 0 ? "side.ct" : d.Side == 2 ? "side.t" : "side.both";
		SetVar(player, "s_side", Lang.T(sideKey));
		Exclusive(player, "pw-s-sideic", "side", d.Side == 0 ? "side-ct" : d.Side == 2 ? "side-t" : "side-both");

		var facts = Lang.F("facts", Lang.Num(d.Wear, "0.0000"), Lang.T("wear." + WearZone(d.Wear)), d.Seed);
		if (d.HasDecor)
			facts += Lang.F("facts.stk", d.UsedStickers, d.StkLimit) + (d.Charm != null ? Lang.T("facts.kc") : "");
		SetVar(player, "s_facts", facts);

		SetClass(player, "pw-tab-p", "on", d.Tab == 0);
		SetClass(player, "pw-tab-s", "on", d.Tab == 1);
		SetClass(player, "pw-tab-k", "on", d.Tab == 2);
		SetVar(player, "tab_s_c", d.UsedStickers + "/" + d.StkLimit);
		SetVar(player, "tab_k_c", d.Charm != null ? "1/1" : d.LkKc ? "0/0" : "0/1");
		SetClass(player, "pw-tab-s", "lock", d.LkStk);
		SetClass(player, "pw-tab-k", "lock", d.LkKc);
		for (var k = 0; k < LayoutIds.Stickers; k++)
			SetClass(player, $"pw-rs-{k}", "lock", d.SlotLocked(k));
		SetClass(player, "pw-rs-k", "lock", d.LkKc);

		RenderPreview(player, d, editSticker, editCharm);

		if (d.Tab == 0)
			RenderParams(player, d);
		else if (editSticker)
			RenderStickerEditor(player, d);
		else if (editCharm)
			RenderCharmEditor(player, d);
		else
			RenderPicker(player, ui, d);
	}

	private void RenderPreview(CCSPlayerController player, Draft d, bool editSticker, bool editCharm)
	{
		for (var k = 0; k < LayoutIds.Stickers; k++)
		{
			var s = d.HasDecor ? d.Stickers[k] : null;
			var art = s != null && _catalog.StickerById.TryGetValue(s.Id, out var item) ? item.Art : "";
			Exclusive(player, $"pw-rs-art-{k}", "art", art);
			SetClass(player, $"pw-rs-{k}", "has", s != null);
			SetClass(player, $"pw-rs-{k}", "mod", s != null && s.Moved);
			SetClass(player, $"pw-rs-{k}", "sel", d.Tab == 1 && d.Slot == k);

			var ov = $"pw-ov-{k}";
			if (s == null)
			{
				SetClass(player, ov, "off", true);
				continue;
			}
			SetClass(player, ov, "off", false);
			var (fx, fy, px, angle) = StickerOnPreview(d, k, s);
			Exclusive(player, ov, "x", "x" + Step(fx, LayoutIds.Steps));
			Exclusive(player, $"pw-ovy-{k}", "y", "y" + Step(fy, LayoutIds.Steps));
			Exclusive(player, $"pw-ovr-{k}", "ro", "ro" + Step((WrapDeg(angle) + 180f) / 360f, LayoutIds.Rot));
			var ova = $"pw-ova-{k}";
			Exclusive(player, ova, "art", art);
			Exclusive(player, ova, "sz", "sz" + Math.Clamp((int)MathF.Round((px - LayoutIds.SizeMin) / LayoutIds.SizeStep), 0, LayoutIds.Size));
			Exclusive(player, ova, "we", "we" + Step(s.Wear, LayoutIds.StickerWear));
			SetClass(player, ova, "sel", editSticker && d.Slot == k);
		}

		var charm = d.HasDecor ? d.Charm : null;
		var charmArt = charm != null && _catalog.CharmById.TryGetValue(charm.Id, out var charmItem) ? charmItem.Art : "";
		Exclusive(player, "pw-rs-art-kc", "art", charmArt);
		SetClass(player, "pw-rs-k", "has", charm != null);
		SetClass(player, "pw-rs-k", "mod", charm != null && charm.Moved);
		SetClass(player, "pw-rs-k", "sel", d.Tab == 2);
		if (charm == null)
		{
			SetClass(player, "pw-kov", "off", true);
		}
		else
		{
			SetClass(player, "pw-kov", "off", false);
			var (kx, ky, kpx) = CharmOnPreview(d, charm);
			Exclusive(player, "pw-kov", "x", "x" + Step(kx, LayoutIds.Steps));
			Exclusive(player, "pw-kovy", "y", "y" + Step(ky, LayoutIds.Steps));
			Exclusive(player, "pw-kova", "art", charmArt);
			Exclusive(player, "pw-kova", "sz", "sz" + Math.Clamp((int)MathF.Round((kpx - LayoutIds.SizeMin) / LayoutIds.SizeStep), 0, LayoutIds.Size));
			SetClass(player, "pw-kova", "sel", editCharm);
		}

		SetVar(player, "phint", editSticker ? Lang.F("phint.s", d.Slot + 1) : editCharm ? Lang.T("phint.k") : "");
	}

	private void RenderParams(CCSPlayerController player, Draft d)
	{
		SetVar(player, "side_h", Lang.T("side.h"));
		for (var i = 0; i < 3; i++)
			SetClass(player, $"pw-side-{i}", "on", d.Side == i);

		var zone = WearZone(d.Wear);
		var locked = Lang.T("f.locked");
		SetClass(player, "pw-sec-wear", "lock", d.LkWear);
		SetClass(player, "pw-sec-seed", "lock", d.LkSeed);
		SetClass(player, "pw-sec-st", "lock", d.LkSt);
		SetClass(player, "pw-sec-tag", "lock", d.LkTag);
		SetVar(player, "wear_h", d.LkWear
			? Lang.T("wear." + zone) + " - " + locked
			: Lang.F("wear.range", Lang.T("wear." + zone), Lang.Num(d.Min, "0.00##"), Lang.Num(d.Max, "0.00##")));
		for (var i = 0; i < 5; i++)
			SetClass(player, $"pw-wz-{i}", "on", zone == i);
		Exclusive(player, "pw-wknob", "kp", "kp" + Step(d.Wear, LayoutIds.Knob));
		SetVar(player, "wear_v", Lang.Num(d.Wear, "0.0000"));

		SetVar(player, "seed_h", d.LkSeed ? locked : Lang.T("seed.h"));
		SetVar(player, "seed_v", d.Seed.ToString(CultureInfo.InvariantCulture));

		SetVar(player, "st_h", d.LkSt ? locked : Lang.T("st.h"));
		SetClass(player, "pw-st-t", "on", d.St);
		SetVar(player, "st_state", Lang.T(d.St ? "st.on" : "st.off"));
		SetVar(player, "st_v", d.St ? Pad6(d.StCount) : "------");

		SetVar(player, "tag_h", d.LkTag ? locked : Lang.T("tag.h"));
		SetVar(player, "tag_v", d.Tag ?? Lang.T("tag.none"));
	}

	private void RenderPicker(CCSPlayerController player, Ui ui, Draft d)
	{
		BuildPicker(ui);
		var charms = d.Tab == 2;
		var query = charms ? ui.KcQuery : ui.StQuery;
		RootClass(player, "sb-kc", charms);
		RootClass(player, "hasq", query.Length > 0);
		SetVar(player, "sq_v", "\u00ab" + query + "\u00bb");
		for (var i = 0; i < 5; i++)
			SetClass(player, $"pw-sr-{i}", "on", ui.StRar == i);

		// sections and sorting dropdowns
		RootClass(player, "pdd1", ui.PickDd == 1 && !charms);
		RootClass(player, "pdd2", ui.PickDd == 2);
		var sort = charms ? ui.KcSort : ui.StSort;
		SetVar(player, "psort_l", Lang.T("psort." + sort));
		for (var i = 0; i < LayoutIds.PickSorts; i++)
			SetClass(player, $"pw-psort-{i}", "on", sort == i);
		if (!charms)
		{
			SetVar(player, "ssec_l", SectionName(ui.StSec));
			if (ui.PickDd == 1)
			{
				var events = _catalog.StickerEvents;
				for (var i = 0; i < LayoutIds.Sections; i++)
				{
					var id = $"pw-ssec-{i}";
					var row = i - 2;
					if (i >= 2 && row >= events.Count)
					{
						SetClass(player, id, "off", true);
						continue;
					}
					SetClass(player, id, "off", false);
					SetVar(player, $"ssec_{i}", i == 0 ? Lang.T("ssec.reg") : i == 1 ? Lang.T("ssec.all") : events[row].Name);
					SetVar(player, $"ssec_c_{i}", (i == 0 ? _catalog.RegularStickers : i == 1 ? _catalog.Stickers.Count : events[row].Count)
						.ToString(CultureInfo.InvariantCulture));
					SetClass(player, id, "on", StickerSection(i) == ui.StSec);
				}
			}
		}

		int currentId;
		if (charms)
		{
			var c = d.Charm;
			var item = c != null && _catalog.CharmById.TryGetValue(c.Id, out var found) ? found : null;
			currentId = c?.Id ?? -1;
			RootClass(player, "sb-empty", c == null);
			SetVar(player, "sb_e", Lang.T("sb.kc.empty"));
			if (c != null)
			{
				Exclusive(player, "pw-sb-art", "art", item?.Art ?? "");
				SetVar(player, "sb_t", item?.Name ?? "#" + c.Id);
				SetVar(player, "sb_s", Lang.T(c.Moved ? "sb.kc.moved" : "sb.kc.default") + (c.Pattern > 0 ? Lang.F("sb.kc.pattern", c.Pattern) : ""));
			}
		}
		else
		{
			var slot = Math.Clamp(d.Slot, 0, Draft.MaxSlot);
			var s = d.Stickers[slot];
			var item = s != null && _catalog.StickerById.TryGetValue(s.Id, out var found) ? found : null;
			currentId = s?.Id ?? -1;
			RootClass(player, "sb-empty", s == null);
			SetVar(player, "sb_e", Lang.F("sb.empty", slot + 1));
			if (s != null)
			{
				Exclusive(player, "pw-sb-art", "art", item?.Art ?? "");
				SetVar(player, "sb_t", Lang.F("sb.slot", slot + 1, item?.Name ?? "#" + s.Id));
				SetVar(player, "sb_s", Lang.T(s.Moved ? "sb.moved" : "sb.default"));
			}
		}
		SetVar(player, "k_badge", charms ? Lang.T("badge.kc") : Lang.F("badge.slot", Math.Clamp(d.Slot, 0, Draft.MaxSlot) + 1));

		var total = ui.Picker.Count;
		var per = Pages ? LayoutIds.StkCols * 4 : LayoutIds.StkCards;
		var pages = Math.Max(1, (total + per - 1) / per);
		ui.StPage = Math.Clamp(ui.StPage, 0, pages - 1);
		var start = ui.StPage * per;
		var count = Math.Max(0, Math.Min(per, total - start));
		ui.PickerStart = start;
		RootClass(player, "spages", pages > 1);
		SetVar(player, "spg", Pages
			? Lang.F("page", ui.StPage + 1, pages)
			: Lang.F("chunk", count > 0 ? start + 1 : 0, start + count, total));
		SetVar(player, "sk_c", !charms && query.Length > 0 && ui.StSec.Length == 0 ? Lang.F("count.all", total) : Lang.F("count", total));

		var rows = (count + LayoutIds.StkCols - 1) / LayoutIds.StkCols;
		for (var r = 0; r < LayoutIds.StkRows; r++)
			SetClass(player, $"pw-srow-{r}", "off", r >= rows);
		for (var i = 0; i < LayoutIds.StkCards; i++)
		{
			var id = $"pw-k-{i}";
			if (i >= count)
			{
				if (i < rows * LayoutIds.StkCols)
					SetClass(player, id, "off", true);
				continue;
			}
			var item = ui.Picker[start + i];
			SetClass(player, id, "off", false);
			SetVar(player, $"kn_{i}", item.Name);
			Exclusive(player, $"pw-k-art-{i}", "art", item.Art.Length > 0 ? item.Art : "ph-sticker");
			Exclusive(player, id, "r", R(item.Rarity));
			SetClass(player, id, "cur", item.Id == currentId);
		}

		var shown = ui.PickerKey + "#" + ui.StPage;
		if (shown != ui.ShownPickerKey)
		{
			ui.ShownPickerKey = shown;
			if (!Pages)
				ResetScroll(player, ui, "pw-sgrid");
		}
	}

	private string SectionName(string sec) => sec.Length == 0 ? Lang.T("ssec.reg") : sec == "*" ? Lang.T("ssec.all") : sec;

	/// <summary>Where a sticker lands on the preview: fraction of the picture, height in layout px, screen angle.</summary>
	private static (float X, float Y, float Px, float Angle) StickerOnPreview(Draft d, int slot, StickerDraft s)
	{
		var geo = StickerGeo.For(d.Def);
		if (geo == null)
			return (0.3f + 0.13f * slot + s.X, 0.45f + s.Y, 60f, -s.Rot);
		var spot = Spot(d, slot);
		var u = geo.SlotX(spot) + s.X;
		var v = geo.SlotY(spot) + s.Y;
		var (x, y, ppu, angle, mirrored) = geo.Map(SkinArt(d), u, v);
		// CS2 turns a sticker counter-clockwise for a positive "sticker slot N rotation" (screen angles here grow clockwise).
		return (x, y, ppu * geo.Size(spot), angle + (mirrored ? s.Rot : -s.Rot));
	}

	/// <summary>Where the charm ring sits on the preview (fraction of the picture) and the charm height in px.</summary>
	private static (float X, float Y, float Px) CharmOnPreview(Draft d, CharmDraft c)
	{
		var geo = StickerGeo.For(d.Def);
		if (geo == null || !geo.HasCharmGeo)
			return (0.35f, 0.55f, 80f);
		var (x, y, ppu) = geo.MapCharm(SkinArt(d), c.X, c.Z);
		// a charm is about 1.6 model units (inches) tall
		return (x, y, Math.Clamp(1.6f * ppu, 30f, 110f));
	}

	private static float WrapDeg(float a)
	{
		a %= 360f;
		if (a > 180f) a -= 360f;
		if (a < -180f) a += 360f;
		return a;
	}

	private void RenderStickerEditor(CCSPlayerController player, Draft d)
	{
		var slot = Math.Clamp(d.Slot, 0, Draft.MaxSlot);
		var s = d.Stickers[slot]!;
		var item = _catalog.StickerById.TryGetValue(s.Id, out var found) ? found : null;
		Exclusive(player, "pw-se-art", "art", item?.Art ?? "");
		SetVar(player, "se_t", item?.Name ?? "#" + s.Id);
		SetVar(player, "se_s", Lang.F("se.s", slot + 1, RarityName(item?.Rarity ?? 0)));
		SetVar(player, "se_ph", Lang.T("se.ph"));
		SetVar(player, "se_x", Fmt2(s.X));
		SetVar(player, "se_y", Fmt2(s.Y));

		var rot = (int)MathF.Round(s.Rot);
		SetVar(player, "se_rh", Lang.F("deg", rot));
		SetVar(player, "se_r", Lang.F("deg", rot));
		Exclusive(player, "pw-serk", "kp", "kp" + Step((s.Rot + 180f) / 360f, LayoutIds.Knob));

		var pct = (int)MathF.Round(s.Wear * 100f);
		var scrape = s.Wear <= 0f ? 0 : s.Wear < 0.5f ? 1 : s.Wear < 1f ? 2 : 3;
		SetVar(player, "se_wh", pct + "% - " + Lang.T("scrape." + scrape));
		Exclusive(player, "pw-sewk", "kp", "kp" + Step(s.Wear, LayoutIds.Knob));
		for (var i = 0; i < 5; i++)
			SetClass(player, $"pw-sewp-{i}", "on", Math.Abs(s.Wear - i * 0.25f) < 0.001f);
	}

	private void RenderCharmEditor(CCSPlayerController player, Draft d)
	{
		var c = d.Charm!;
		var item = _catalog.CharmById.TryGetValue(c.Id, out var found) ? found : null;
		Exclusive(player, "pw-ke-art", "art", item?.Art ?? "");
		SetVar(player, "ke_t", item?.Name ?? "#" + c.Id);
		SetVar(player, "ke_s", Lang.T("ke.s"));
		SetVar(player, "ke_ph", Lang.T("se.ph"));
		SetVar(player, "ke_x", Fmt2(c.X));
		SetVar(player, "ke_y", Fmt2(c.Z));
		SetVar(player, "ke_th", Lang.T("kpat.h"));
		SetVar(player, "ke_p", c.Pattern.ToString(CultureInfo.InvariantCulture));
	}

	// ------------------------------------------------------------------ keyboard

	private void RenderKeyboard(CCSPlayerController player, Ui ui, PromptState? p)
	{
		RootClass(player, "kb-on", p != null);
		if (p == null)
			return;
		var field = Fields[p.Field];
		RootClass(player, "kb-num", field.Num);
		RootClass(player, "kb-txt", !field.Num);
		RootClass(player, "kb-en", p.En);
		RootClass(player, "kb-shift", p.Shift);
		RootClass(player, "kb-err", p.Err != null);
		RootClass(player, "kb-chat", !KeyboardOnly);
		SetVar(player, "kb_what", FieldTitle(ui, p.Field));
		SetVar(player, "kb_text", p.Text);
		SetVar(player, "kb_lim", p.Text.Length + "/" + field.Max);
		SetVar(player, "kb_hint", p.Err ?? FieldHint(ui, p.Field));
		SetClass(player, "pw-kn-comma", "off", !field.Dec);
		SetClass(player, "pw-kn-minus", "off", !field.Neg);
	}
}
