using System.Globalization;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;

namespace PMM_WeaponSkins;

/// <summary>Every button of pmm_ws.xml lands here (OnCustomHudClicked gives the button id).</summary>
public partial class Plugin
{
	private static bool IdNum(string id, string prefix, out int value)
	{
		value = -1;
		return id.StartsWith(prefix, StringComparison.Ordinal) &&
			int.TryParse(id.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out value);
	}

	private void OnClick(CCSPlayerController player, CCSCustomHudLayout layout, string id)
	{
		if (_layout == null || layout.Handle != _layout.Handle || !Valid(player))
			return;
		var ui = _ui[player.Slot];
		if (!ui.Open)
			return;
		var now = Environment.TickCount64;
		if (now < ui.NextClick)
			return;
		ui.NextClick = now + ClickGapMs;
		Dbg(player, "click " + id);
		try
		{
			// any click outside the picker dropdowns closes them
			var closedDd = false;
			if (ui.PickDd != 0 && id != "pw-ssec" && id != "pw-psort" &&
				!id.StartsWith("pw-ssec-", StringComparison.Ordinal) && !id.StartsWith("pw-psort-", StringComparison.Ordinal))
			{
				ui.PickDd = 0;
				closedDd = true;
			}
			if ((Handle(player, ui, id) || closedDd) && ui.Open)
				Render(player);
		}
		catch (Exception ex)
		{
			Logger.LogError("Click '{Id}' failed: {Error}", id, ex);
		}
	}

	private bool Handle(CCSPlayerController player, Ui ui, string id)
	{
		if (ui.Prompt != null)
			return KeyClick(player, ui, id);

		switch (id)
		{
			case "pw-close":
				Hide(player);
				return false;
			case "pw-uis-m":
			case "pw-uis-p":
				StepScale(player, id == "pw-uis-p" ? 1 : -1);
				return true;
			case "pw-home":
				GoHome(ui);
				return true;
			case "pw-hero":
				if (!SecShown("agents"))
					return false;
				if (!SecOpen(player, "agents"))
				{
					ShowToast(player, Lang.T("toast.locked"), true);
					return true;
				}
				GoAgents(ui);
				return true;
			case "pw-other":
				ui.Team = ui.Team == 2 ? 3 : 2;
				ui.Set = null;
				ui.Fac = "";
				ShowToast(player, Lang.F("toast.team", TeamName(ui.Team)), false);
				return true;
			case "pw-back":
				GoBack(ui);
				return true;
			case "pw-sort":
				ui.Dd = !ui.Dd;
				return true;
			case "pw-pg-prev":
			case "pw-pg-next":
				ui.Page[ui.ListKey] = Math.Max(0, ui.Page.GetValueOrDefault(ui.ListKey) + (id == "pw-pg-next" ? 1 : -1));
				ui.ShownListKey = "";
				return true;
		}

		if (IdNum(id, "pw-ht-", out var n))
		{
			var sec = n == 0 ? "agents" : n - 1 < Secs.Length ? Secs[n - 1] : "";
			if (sec.Length == 0 || !SecShown(sec))
				return true;
			if (!SecOpen(player, sec))
				ShowToast(player, Lang.T("toast.locked"), true);
			else if (n == 0)
				GoAgents(ui);
			else
				GoSec(ui, sec);
			return true;
		}
		if (IdNum(id, "pw-hc-", out n))
		{
			if (!CatShown(n))
				return true;
			if (CatOpen(player, n))
				GoCat(ui, n);
			else
				ShowToast(player, Lang.T("toast.locked"), true);
			return true;
		}
		if (IdNum(id, "pw-nav-", out n))
		{
			// the side list shows only the visible sections, in order
			if (ui.View == ViewKind.Weapons)
			{
				var cats = ShownCats;
				if (n < cats.Length && CatOpen(player, cats[n]))
					GoCat(ui, cats[n]);
				else if (n < cats.Length)
					ShowToast(player, Lang.T("toast.locked"), true);
			}
			else
			{
				var secs = ShownSecs;
				if (n < secs.Length && SecOpen(player, secs[n]))
					GoSec(ui, secs[n]);
				else if (n < secs.Length)
					ShowToast(player, Lang.T("toast.locked"), true);
			}
			return true;
		}
		if (IdNum(id, "pw-sort-", out n))
		{
			ui.Sort[ui.ListKey] = Math.Clamp(n, 0, 3);
			ui.Page[ui.ListKey] = 0;
			ui.Dd = false;
			ui.ShownListKey = "";
			return true;
		}
		if (IdNum(id, "pw-chip-", out n))
		{
			if (n < ui.ChipList.Count)
				ui.Fac = ui.ChipList[n].Key;
			return true;
		}
		if (IdNum(id, "pw-c-", out n))
		{
			ui.Dd = false;
			var index = ui.CardStart + n;
			if (index >= 0 && index < ui.Cards.Count)
				ui.Cards[index].Click?.Invoke(player);
			return true;
		}

		var d = ui.Set;
		if (d == null)
			return false;
		return SettingsClick(player, ui, d, id);
	}

	/// <summary>True (and a toast) when the setting behind a click is locked for the player (config "Features").</summary>
	private bool FeatureLocked(CCSPlayerController player, Draft d, string id)
	{
		string? what = null;
		if (d.LkWear && (id.StartsWith("pw-wear-", StringComparison.Ordinal) || id.StartsWith("pw-wz-", StringComparison.Ordinal) ||
				id.StartsWith("pw-wc-", StringComparison.Ordinal)))
			what = "f.wear";
		else if (d.LkSeed && id.StartsWith("pw-seed-", StringComparison.Ordinal))
			what = "f.seed";
		else if (d.LkSt && id.StartsWith("pw-st-", StringComparison.Ordinal))
			what = "f.st";
		else if (d.LkTag && id == "pw-tag")
			what = "f.tag";
		else if (d.LkKc && (id == "pw-tab-k" || id == "pw-rs-k" || (d.Tab == 2 && id.StartsWith("pw-k-", StringComparison.Ordinal))))
			what = "f.kc";
		else if (d.HasDecor && (id == "pw-tab-s" ? d.LkStk : IdNum(id, "pw-rs-", out var slot) && d.SlotLocked(slot)))
		{
			ShowToast(player, d.LkStk ? Lang.F("toast.flocked", Lang.T("f.stk")) : Lang.F("toast.stklimit", d.StkLimit, LayoutIds.Stickers), true);
			return true;
		}
		else if (d.Tab == 1 && d.SlotLocked(d.Slot) && (id.StartsWith("pw-k-", StringComparison.Ordinal) || id == "pw-sb-pos"))
			what = "f.stk";
		if (what == null)
			return false;
		ShowToast(player, Lang.F("toast.flocked", Lang.T(what)), true);
		return true;
	}

	private bool SettingsClick(CCSPlayerController player, Ui ui, Draft d, string id)
	{
		if (FeatureLocked(player, d, id))
			return true;
		switch (id)
		{
			case "pw-s-x":
			case "pw-s-cancel":
				ui.Set = null;
				return true;
			case "pw-s-apply":
				ApplySettings(player, ui);
				return true;
			case "pw-tab-p":
				d.Tab = 0;
				d.EditSticker = d.EditCharm = false;
				return true;
			case "pw-tab-s":
				if (d.HasDecor)
				{
					d.Tab = 1;
					d.EditSticker = d.EditCharm = false;
					ui.StPage = 0;
				}
				return true;
			case "pw-tab-k":
			case "pw-rs-k":
				if (d.HasDecor)
				{
					d.Tab = 2;
					d.EditSticker = d.EditCharm = false;
					ui.StPage = 0;
				}
				return true;

			// parameters
			case "pw-wear-m":
			case "pw-wear-p":
				d.Wear = Math.Clamp(MathF.Round((d.Wear + (id == "pw-wear-p" ? 0.01f : -0.01f)) * 10000f) / 10000f, d.Min, d.Max);
				return true;
			case "pw-wear-v":
				Ask(player, ui, "wear");
				return true;
			case "pw-seed-m":
			case "pw-seed-p":
				d.Seed = Math.Clamp(d.Seed + (id == "pw-seed-p" ? 1 : -1), 0, 1000);
				return true;
			case "pw-seed-r":
				d.Seed = Random.Shared.Next(0, 1001);
				return true;
			case "pw-seed-v":
				Ask(player, ui, "seed");
				return true;
			case "pw-st-t":
				d.St = !d.St;
				return true;
			case "pw-st-v":
				Ask(player, ui, "stc");
				return true;
			case "pw-tag":
				Ask(player, ui, "tag");
				return true;

			// sticker / charm picker
			case "pw-ssec":
				ui.PickDd = ui.PickDd == 1 ? 0 : 1;
				return true;
			case "pw-psort":
				ui.PickDd = ui.PickDd == 2 ? 0 : 2;
				return true;
			case "pw-sq":
				Ask(player, ui, d.Tab == 2 ? "kcq" : "stq");
				return true;
			case "pw-sqx":
				if (d.Tab == 2)
					ui.KcQuery = "";
				else
					ui.StQuery = "";
				ui.StPage = 0;
				return true;
			case "pw-spg-prev":
			case "pw-spg-next":
				ui.StPage = Math.Max(0, ui.StPage + (id == "pw-spg-next" ? 1 : -1));
				ui.ShownPickerKey = "";
				return true;
			case "pw-sb-pos":
				if (d.Tab == 2 && d.Charm != null)
					d.EditCharm = true;
				else if (d.Tab == 1 && d.Stickers[d.Slot] != null)
					d.EditSticker = true;
				return true;
			case "pw-sb-rm":
				if (d.Tab == 2)
					d.Charm = null;
				else
					d.Stickers[d.Slot] = null;
				return true;

			// sticker editor
			case "pw-se-done":
				d.EditSticker = false;
				return true;
			case "pw-se-reset":
				if (d.Stickers[d.Slot] is { } reset)
				{
					reset.X = reset.Y = reset.Rot = reset.Wear = 0f;
				}
				return true;
			case "pw-se-x":
				Ask(player, ui, "sx");
				return true;
			case "pw-se-y":
				Ask(player, ui, "sy");
				return true;
			case "pw-se-rv":
				Ask(player, ui, "srot");
				return true;
			case "pw-sew-v":
				Ask(player, ui, "swear");
				return true;

			// charm editor
			case "pw-ke-done":
				d.EditCharm = false;
				return true;
			case "pw-ke-reset":
				if (d.Charm != null)
					ResetCharm(d, d.Charm);
				return true;
			case "pw-ke-x":
				Ask(player, ui, "kx");
				return true;
			case "pw-ke-y":
				Ask(player, ui, "ky");
				return true;
			case "pw-ke-p":
				Ask(player, ui, "kpat");
				return true;
			case "pw-ke-pr":
				if (d.Charm != null)
					d.Charm.Pattern = Random.Shared.Next(0, 100001);
				return true;
		}

		int n;
		if (IdNum(id, "pw-side-", out n))
		{
			d.Side = Math.Clamp(n, 0, 2);
			return true;
		}
		if (IdNum(id, "pw-wz-", out n))
		{
			d.Wear = Math.Clamp(Math.Max(WearZoneStart[Math.Clamp(n, 0, 4)], 0.0001f), d.Min, d.Max);
			return true;
		}
		if (IdNum(id, "pw-wc-", out n))
		{
			d.Wear = Math.Clamp(Math.Max(n * 0.05f, 0.0001f), d.Min, d.Max);
			return true;
		}
		if (IdNum(id, "pw-rs-", out n))
		{
			if (d.HasDecor)
			{
				d.Tab = 1;
				d.Slot = Math.Clamp(n, 0, Draft.MaxSlot);
				d.EditSticker = false;
				d.EditCharm = false;
			}
			return true;
		}
		if (IdNum(id, "pw-ssec-", out n))
		{
			ui.StSec = StickerSection(n);
			ui.StPage = 0;
			ui.PickDd = 0;
			return true;
		}
		if (IdNum(id, "pw-psort-", out n))
		{
			n = Math.Clamp(n, 0, LayoutIds.PickSorts - 1);
			if (d.Tab == 2)
				ui.KcSort = n;
			else
				ui.StSort = n;
			ui.StPage = 0;
			ui.PickDd = 0;
			SaveSorts(player, ui);
			return true;
		}
		if (IdNum(id, "pw-sr-", out n))
		{
			ui.StRar = Math.Clamp(n, 0, 4);
			ui.StPage = 0;
			return true;
		}
		if (IdNum(id, "pw-k-", out n))
		{
			Pick(ui, ui.PickerStart + n);
			return true;
		}
		if (id.StartsWith("pw-g-", StringComparison.Ordinal))
		{
			var parts = id.Split('-');
			if (parts.Length == 4 && int.TryParse(parts[2], out var col) && int.TryParse(parts[3], out var row))
			{
				if (d.EditSticker && d.Stickers[d.Slot] is { } sticker)
				{
					if (!PlaceSticker(d, sticker, d.Slot, col, row))
						ShowToast(player, Lang.T("toast.offsurf"), true);
				}
				else if (d.EditCharm && d.Charm != null)
				{
					if (!PlaceCharm(d, d.Charm, col, row))
						ShowToast(player, Lang.T(StickerGeo.For(d.Def)?.HasCharmGeo == true ? "toast.kc.off" : "toast.kc.fixed"), true);
				}
			}
			return true;
		}

		// sticker editor
		var s = d.Stickers[Math.Clamp(d.Slot, 0, Draft.MaxSlot)];
		if (s != null)
		{
			switch (id)
			{
				// Sticker space: y grows downwards, x grows towards the stock (right on the preview).
				case "pw-se-u": Nudge(player, d, s, 0f, -StickerStep); return true;
				case "pw-se-d": Nudge(player, d, s, 0f, StickerStep); return true;
				case "pw-se-l": Nudge(player, d, s, -StickerStep, 0f); return true;
				case "pw-se-r": Nudge(player, d, s, StickerStep, 0f); return true;
				case "pw-se-c": s.X = s.Y = 0f; return true;
				case "pw-se-r15m": s.Rot = Wrap(s.Rot - 15f); return true;
				case "pw-se-r1m": s.Rot = Wrap(s.Rot - 1f); return true;
				case "pw-se-r1p": s.Rot = Wrap(s.Rot + 1f); return true;
				case "pw-se-r15p": s.Rot = Wrap(s.Rot + 15f); return true;
			}
			if (IdNum(id, "pw-ser-", out n))
			{
				s.Rot = Math.Clamp(-180 + n * 15, -180, 180);
				return true;
			}
			if (IdNum(id, "pw-sew-", out n))
			{
				s.Wear = Math.Clamp(n * 0.05f, 0f, 1f);
				return true;
			}
			if (IdNum(id, "pw-sewp-", out n))
			{
				s.Wear = Math.Clamp(n * 0.25f, 0f, 1f);
				return true;
			}
		}

		// charm editor
		var c = d.Charm;
		if (c != null)
		{
			switch (id)
			{
				// model x grows towards the muzzle, which is on the left of every render
				case "pw-ke-u": NudgeCharm(player, d, c, 0, 1); return true;
				case "pw-ke-d": NudgeCharm(player, d, c, 0, -1); return true;
				case "pw-ke-l": NudgeCharm(player, d, c, 1, 0); return true;
				case "pw-ke-r": NudgeCharm(player, d, c, -1, 0); return true;
				case "pw-ke-c": ResetCharm(d, c); return true;
			}
		}
		return false;
	}

	private void Nudge(CCSPlayerController player, Draft d, StickerDraft s, float dx, float dy)
	{
		if (!MoveSticker(d, s, Math.Clamp(d.Slot, 0, Draft.MaxSlot), dx, dy))
			ShowToast(player, Lang.T("toast.offsurf"), true);
	}

	private void NudgeCharm(CCSPlayerController player, Draft d, CharmDraft c, int dx, int dz)
	{
		if (!MoveCharm(d, c, dx, dz))
			ShowToast(player, Lang.T(StickerGeo.For(d.Def)?.HasCharmGeo == true ? "toast.kc.edge" : "toast.kc.fixed"), true);
	}

	private static float Wrap(float rot)
	{
		while (rot > 180f)
			rot -= 360f;
		while (rot < -180f)
			rot += 360f;
		return rot;
	}
}
