using System.Globalization;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;

namespace PMM_WeaponSkins;

/// <summary>Colours of one side: the menu accent (and background tint) and the glow around the menu.</summary>
public sealed class SideTheme
{
	[JsonPropertyName("Color")] public string Color { get; set; } = "";
	[JsonPropertyName("Glow")] public string Glow { get; set; } = "";
}

/// <summary>
/// Menu colours (config "Theme"). The server can only toggle classes, so every colour is one of the palette baked into
/// pmm_ws.css (LayoutIds.Palette); a name or any "#rrggbb" is snapped to the nearest palette colour.
/// </summary>
public sealed class ThemeConfig
{
	[JsonPropertyName("CT")] public SideTheme CT { get; set; } = new() { Color = "sky", Glow = "blue" };
	[JsonPropertyName("T")] public SideTheme T { get; set; } = new() { Color = "gold", Glow = "amber" };
	/// <summary>Glow around the menu: 0 off, 1 weak, 2 medium, 3 strong.</summary>
	[JsonPropertyName("GlowSize")] public int GlowSize { get; set; } = 2;
}

public partial class Plugin
{
	private int _accCT = 4, _accT = 17, _glowCT = 6, _glowT = 18, _glowSize = 2;

	private void ParseTheme(PluginConfig config)
	{
		var theme = config.Theme ??= new ThemeConfig();
		theme.CT ??= new SideTheme();
		theme.T ??= new SideTheme();
		theme.GlowSize = Math.Clamp(theme.GlowSize, 0, LayoutIds.GlowSizes);
		_accCT = PaletteIndex(theme.CT.Color, "sky");
		_glowCT = PaletteIndex(theme.CT.Glow, "blue");
		_accT = PaletteIndex(theme.T.Color, "gold");
		_glowT = PaletteIndex(theme.T.Glow, "amber");
		_glowSize = theme.GlowSize;
	}

	/// <summary>A palette name ("sky") or "#rrggbb" / "rrggbb" (nearest palette colour); anything else = the fallback name.</summary>
	private int PaletteIndex(string? value, string fallback)
	{
		var text = (value ?? "").Trim();
		var palette = LayoutIds.Palette;
		for (var i = 0; i < palette.Length; i++)
		{
			if (string.Equals(palette[i].Name, text, StringComparison.OrdinalIgnoreCase))
				return i;
		}
		if (TryRgb(text, out var r, out var g, out var b))
		{
			var best = 0;
			var bestDist = double.MaxValue;
			for (var i = 0; i < palette.Length; i++)
			{
				TryRgb(palette[i].Hex, out var pr, out var pg, out var pb);
				// "redmean" distance: close to how different two colours look
				var rm = (r + pr) / 2.0;
				var dr = r - pr;
				var dg = g - pg;
				var db = b - pb;
				var dist = (2 + rm / 256) * dr * dr + 4 * dg * dg + (2 + (255 - rm) / 256) * db * db;
				if (dist < bestDist)
				{
					bestDist = dist;
					best = i;
				}
			}
			return best;
		}
		if (text.Length > 0)
		{
			try
			{
				Logger.LogWarning("Theme: unknown colour \"{Colour}\", using \"{Fallback}\". Use a name from the list in the config or #rrggbb.", text, fallback);
			}
			catch
			{
				// the config can be parsed before the logger exists
			}
		}
		return Math.Max(0, Array.FindIndex(palette, p => p.Name == fallback));
	}

	private static bool TryRgb(string text, out int r, out int g, out int b)
	{
		r = g = b = 0;
		var hex = text.StartsWith('#') ? text[1..] : text;
		if (hex.Length == 3)
			hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
		if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
			return false;
		r = (v >> 16) & 0xff;
		g = (v >> 8) & 0xff;
		b = v & 0xff;
		return true;
	}

	/// <summary>Accent / background of the side being edited, the other side's tint on its card, and the glow.</summary>
	private void RenderTheme(CCSPlayerController player, Ui ui)
	{
		var ct = ui.Team == 3;
		Exclusive(player, Root, "acc", "acc-" + (ct ? _accCT : _accT));
		Exclusive(player, Root, "oac", "oac-" + (ct ? _accT : _accCT));
		Exclusive(player, Root, "glow", _glowSize > 0 ? $"gw-{(ct ? _glowCT : _glowT)}-{_glowSize}" : null);
	}
}
