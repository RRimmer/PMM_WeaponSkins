using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace PMM_WeaponSkins;

/// <summary>
/// Text input. A custom HUD has no text field, so the menu offers an on-screen keyboard (buttons) and/or
/// chat: the menu hides, the next chat message of the player is swallowed and used as the answer.
/// </summary>
public partial class Plugin
{
	private sealed record Field(string Title, bool Num, bool Dec, bool Neg, int Max);

	private static readonly Dictionary<string, Field> Fields = new(StringComparer.Ordinal)
	{
		["tag"] = new("kb.tag", false, false, false, 20),
		["stq"] = new("kb.stq", false, false, false, 24),
		["kcq"] = new("kb.kcq", false, false, false, 24),
		["seed"] = new("kb.seed", true, false, false, 4),
		["wear"] = new("kb.wear", true, true, false, 7),
		["stc"] = new("kb.stc", true, false, false, 6),
		["sx"] = new("kb.sx", true, true, true, 6),
		["sy"] = new("kb.sy", true, true, true, 6),
		["srot"] = new("kb.srot", true, false, true, 4),
		["swear"] = new("kb.swear", true, false, false, 3),
		["kx"] = new("kb.kx", true, true, true, 5),
		["ky"] = new("kb.ky", true, true, true, 5),
		["kpat"] = new("kb.kpat", true, false, false, 6),
	};

	private bool ChatOnly => Config.InputMode == "chat";
	private bool KeyboardOnly => Config.InputMode == "keyboard";

	private void Ask(CCSPlayerController player, Ui ui, string field)
	{
		if (!Fields.ContainsKey(field))
			return;
		ui.Dd = false;
		ui.Prompt = new PromptState { Field = field, En = Lang.En, Token = ui.Token };
		if (ChatOnly)
			StartChat(player, ui);
	}

	private string FieldTitle(Ui ui, string field)
	{
		var title = Lang.T(Fields[field].Title);
		var d = ui.Set;
		if (d == null || field is "stq" or "kcq")
			return title;
		if (field is "sx" or "sy" or "srot" or "swear")
		{
			var s = d.Stickers[Math.Clamp(d.Slot, 0, Draft.MaxSlot)];
			if (s != null && _catalog.StickerById.TryGetValue(s.Id, out var item))
				return title + " " + Lang.F("kb.for", item.Name);
		}
		if (field is "kx" or "ky" or "kpat" && d.Charm != null && _catalog.CharmById.TryGetValue(d.Charm.Id, out var charm))
			return title + " " + Lang.F("kb.for", charm.Name);
		return title + " " + Lang.F("kb.for", d.FullName);
	}

	private string FieldHint(Ui ui, string field)
	{
		if (field == "wear" && ui.Set is { } d)
			return Lang.F("kb.h.wear", Lang.Num(d.Min, "0.####"), Lang.Num(d.Max, "0.####"));
		if (field is "sx" or "sy" && ui.Set is { } sd)
		{
			var r = StickerRange(sd, Math.Clamp(sd.Slot, 0, Draft.MaxSlot));
			return field == "sx"
				? Lang.F("kb.h.sx", Lang.Num(r.MinX, "0.00"), Lang.Num(r.MaxX, "0.00"))
				: Lang.F("kb.h.sy", Lang.Num(r.MinY, "0.00"), Lang.Num(r.MaxY, "0.00"));
		}
		return Lang.T("kb.h." + (field == "kcq" ? "stq" : field));
	}

	// ------------------------------------------------------------------ on-screen keyboard

	private bool KeyClick(CCSPlayerController player, Ui ui, string id)
	{
		var p = ui.Prompt!;
		if (p.Chat)
			return false;
		var field = Fields[p.Field];

		switch (id)
		{
			case "pw-kb-cancel":
				ui.Prompt = null;
				ShowToast(player, Lang.T("toast.cancel"), false);
				return true;
			case "pw-kb-ok":
				Submit(player, ui, p.Text);
				return true;
			case "pw-kb-chat":
				if (!KeyboardOnly)
					StartChat(player, ui);
				return true;
			case "pw-k-shift-ru":
			case "pw-k-shift-en":
				p.Shift = !p.Shift;
				return true;
			case "pw-k-bs-ru":
			case "pw-k-bs-en":
			case "pw-kn-bs":
				if (p.Text.Length > 0)
					p.Text = p.Text[..^1];
				p.Err = null;
				return true;
			case "pw-k-clr":
			case "pw-kn-clr":
				p.Text = "";
				p.Err = null;
				return true;
			case "pw-k-lang":
				p.En = !p.En;
				return true;
			case "pw-k-sp":
				TypeKey(p, field, " ");
				return true;
			case "pw-kn-comma":
				if (field.Dec && !p.Text.Contains(','))
					TypeKey(p, field, p.Text.TrimStart('-').Length == 0 ? "0," : ",");
				return true;
			case "pw-kn-minus":
				if (field.Neg)
					p.Text = p.Text.StartsWith('-') ? p.Text[1..] : "-" + p.Text;
				return true;
		}

		if (IdNum(id, "pw-kd-", out var digit) || IdNum(id, "pw-kn-", out digit))
		{
			TypeKey(p, field, digit.ToString(CultureInfo.InvariantCulture));
			return true;
		}
		if (IdNum(id, "pw-kr-", out var ru))
		{
			var ch = KeyAt(LayoutIds.KeysRu, ru);
			if (ch != null)
				TypeKey(p, field, p.Shift ? ch.ToUpperInvariant() : ch);
			p.Shift = false;
			return true;
		}
		if (IdNum(id, "pw-kl-", out var en))
		{
			var ch = KeyAt(LayoutIds.KeysEn, en);
			if (ch != null)
				TypeKey(p, field, p.Shift ? ch.ToUpperInvariant() : ch);
			p.Shift = false;
			return true;
		}
		return false;
	}

	private static string? KeyAt(string[] rows, int index)
	{
		foreach (var row in rows)
		{
			if (index < row.Length)
				return row[index].ToString();
			index -= row.Length;
		}
		return null;
	}

	private static void TypeKey(PromptState p, Field field, string text)
	{
		if (p.Text.Length + text.Length > field.Max)
			return;
		p.Text += text;
		p.Err = null;
	}

	// ------------------------------------------------------------------ chat

	private void StartChat(CCSPlayerController player, Ui ui)
	{
		var p = ui.Prompt;
		if (p == null)
			return;
		p.Chat = true;
		p.Err = null;
		p.Left = Config.ChatTimeout;
		Capture(player, false);
		SetClass(player, Root, "pw-hidden", true);
		SetClass(player, ChatPanel, "lang-en", Lang.LayoutEn);
		SetClass(player, ChatPanel, "lang-tr", Lang.Tr);
		SetClass(player, ChatPanel, "pw-hidden", false);
		DrawChat(player, ui);
		var token = ++p.Token;
		Tick(player, ui, p, token);
	}

	private void Tick(CCSPlayerController player, Ui ui, PromptState p, int token)
	{
		Later(1f, () =>
		{
			if (!player.IsValid || ui.Prompt != p || p.Token != token || !p.Chat)
				return;
			p.Left--;
			if (p.Left <= 0)
			{
				EndChat(player, ui);
				ShowToast(player, Lang.T("toast.timeout"), false);
				return;
			}
			SetVarOn(player, ChatPanel, "ch_time", Lang.F("ch.time", p.Left));
			Tick(player, ui, p, token);
		});
	}

	private void DrawChat(CCSPlayerController player, Ui ui)
	{
		var p = ui.Prompt;
		if (p == null)
			return;
		SetVarOn(player, ChatPanel, "ch_what", FieldTitle(ui, p.Field));
		SetVarOn(player, ChatPanel, "ch_hint", FieldHint(ui, p.Field) + "\n" + Lang.T("ch.hint"));
		SetVarOn(player, ChatPanel, "ch_err", p.Err ?? "");
		SetVarOn(player, ChatPanel, "ch_time", Lang.F("ch.time", p.Left));
	}

	/// <summary>Leaves chat mode: the menu comes back with the mouse.</summary>
	private void EndChat(CCSPlayerController player, Ui ui)
	{
		ui.Prompt = null;
		SetClass(player, ChatPanel, "pw-hidden", true);
		if (!ui.Open)
			return;
		Render(player);
		SetClass(player, Root, "pw-hidden", false);
		var token = ui.Token;
		Later(Config.InputDelay, () =>
		{
			if (player.IsValid && ui.Open && ui.Token == token && ui.Prompt is not { Chat: true })
				Capture(player, true);
		});
	}

	private HookResult OnSay(CCSPlayerController? player, CommandInfo info)
	{
		if (player == null || !Valid(player))
			return HookResult.Continue;
		var ui = _ui[player.Slot];
		if (!ui.Open || ui.Prompt is not { Chat: true })
			return HookResult.Continue;
		var text = info.GetArg(1);
		Server.NextFrame(() =>
		{
			if (!player.IsValid || ui.Prompt is not { Chat: true })
				return;
			var cancel = Lang.T("cancel.words").Split('|');
			if (cancel.Any(w => string.Equals(text.Trim(), w, StringComparison.OrdinalIgnoreCase)))
			{
				EndChat(player, ui);
				ShowToast(player, Lang.T("toast.cancel"), false);
				return;
			}
			if (Submit(player, ui, text))
				EndChat(player, ui);
			else
				DrawChat(player, ui);
		});
		return HookResult.Handled;
	}

	// ------------------------------------------------------------------ values

	/// <summary>Validates and stores the answer. True when it was accepted.</summary>
	private bool Submit(CCSPlayerController player, Ui ui, string raw)
	{
		var p = ui.Prompt;
		if (p == null)
			return false;
		var text = new string((raw ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
		var field = Fields[p.Field];
		if (text.Length == 0 && p.Field is not ("stq" or "kcq" or "tag"))
		{
			p.Err = Lang.T("kb.err.empty");
			return false;
		}

		var d = ui.Set;
		var number = text.Replace(',', '.');
		switch (p.Field)
		{
			case "stq":
				ui.StQuery = text;
				ui.StPage = 0;
				break;
			case "kcq":
				ui.KcQuery = text;
				ui.StPage = 0;
				break;
			case "tag":
				if (text.Length > field.Max)
				{
					p.Err = Lang.F("kb.err.long", field.Max);
					return false;
				}
				if (d != null)
					d.Tag = text.Length > 0 ? text : null;
				break;
			case "seed":
				if (!Int(number, 0, 1000, p, out var seed))
					return false;
				if (d != null)
					d.Seed = seed;
				break;
			case "stc":
				if (!Int(number, 0, 999999, p, out var count))
					return false;
				if (d != null)
				{
					d.St = true;
					d.StCount = count;
					d.StEdited = true;
				}
				break;
			case "wear":
				if (d == null || !Real(number, d.Min, d.Max, "0.####", p, out var wear))
					return false;
				d.Wear = Math.Max(wear, 0.0001f);
				break;
			case "swear":
				if (!Int(number, 0, 100, p, out var scrape))
					return false;
				if (d?.Stickers[d.Slot] is { } sw)
					sw.Wear = scrape / 100f;
				break;
			case "srot":
				if (!Int(number, -180, 180, p, out var rot))
					return false;
				if (d?.Stickers[d.Slot] is { } sr)
					sr.Rot = rot;
				break;
			case "sx":
			case "sy":
			{
				if (d?.Stickers[Math.Clamp(d.Slot, 0, Draft.MaxSlot)] is not { } so)
					break;
				var slot = Math.Clamp(d.Slot, 0, Draft.MaxSlot);
				var range = StickerRange(d, slot);
				var isX = p.Field == "sx";
				if (!Real(number, isX ? range.MinX : range.MinY, isX ? range.MaxX : range.MaxY, "0.00", p, out var offset))
					return false;
				offset = Round2(offset);
				if (!StickerFits(d, slot, isX ? offset : so.X, isX ? so.Y : offset))
				{
					p.Err = Lang.T("kb.err.surf");
					return false;
				}
				if (isX)
					so.X = offset;
				else
					so.Y = offset;
				break;
			}
			case "kx":
			case "ky":
			{
				if (d?.Charm is not { } kc)
					break;
				var kgeo = StickerGeo.For(d.Def);
				if (kgeo == null || !kgeo.HasCharmGeo)
				{
					p.Err = Lang.T("toast.kc.fixed");
					return false;
				}
				if (!Real(number, -100f, 100f, "0.##", p, out var kv))
					return false;
				var isKx = p.Field == "kx";
				if (!SetCharmPoint(d, kc, isKx ? kv : kc.X, isKx ? kc.Z : kv))
				{
					p.Err = Lang.T("kb.err.kcsurf");
					return false;
				}
				break;
			}
			case "kpat":
				if (!Int(number, 0, 100000, p, out var pattern))
					return false;
				if (d?.Charm != null)
					d.Charm.Pattern = pattern;
				break;
		}
		ui.Prompt = null;
		return true;
	}

	private static bool Int(string text, int min, int max, PromptState p, out int value)
	{
		if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value) && value >= min && value <= max)
			return true;
		p.Err = Lang.F("kb.err.int", min, max);
		return false;
	}

	private static bool Real(string text, float min, float max, string format, PromptState p, out float value)
	{
		if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value) && value >= min - 0.00001f && value <= max + 0.00001f)
		{
			value = Math.Clamp(value, min, max);
			return true;
		}
		p.Err = Lang.F("kb.err.range", Lang.Num(min, format), Lang.Num(max, format));
		return false;
	}
}
