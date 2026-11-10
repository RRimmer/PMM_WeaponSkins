using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;

namespace PMM_WeaponSkins;

/// <summary>
/// Menu scale per player. The layout is drawn for a 16:9 screen (1843 x 1004 layout px); Panorama keeps the height at
/// 1080 layout px, so a 4:3 screen is only 1440 px wide and needs about 75 %, 5:4 about 70 %, 16:10 about 90 %.
/// The server cannot learn a client's resolution, so the player picks it: "Scale - +" in the top bar or !wsscale N.
/// Choices are kept with the other player settings (MySQL, see PlayerStore).
/// </summary>
public partial class Plugin
{
	/// <summary>Presets, %; each has a ui-scale class uis-NN in pmm_ws.css (100 has none).</summary>
	private static readonly int[] ScalePresets = { 100, 90, 80, 75, 70, 60 };

	private static int NearestScale(int pct) => ScalePresets.MinBy(s => Math.Abs(s - pct));

	private int ScaleOf(CCSPlayerController player) =>
		Store.Get(player.SteamID) is { Scale: > 0 } prefs ? NearestScale(prefs.Scale) : NearestScale(Config.Scale);

	private int SetScale(CCSPlayerController player, int pct)
	{
		var scale = NearestScale(pct);
		var prefs = Store.Get(player.SteamID) ?? new PlayerPrefs();
		prefs.Scale = scale;
		Store.Save(player.SteamID, prefs);
		return scale;
	}

	/// <summary>"-" makes the menu smaller, "+" bigger.</summary>
	private void StepScale(CCSPlayerController player, int dir)
	{
		var i = Array.IndexOf(ScalePresets, ScaleOf(player));
		var scale = SetScale(player, ScalePresets[Math.Clamp(i + (dir < 0 ? 1 : -1), 0, ScalePresets.Length - 1)]);
		ShowToast(player, Lang.F("scale.set", scale), false);
	}

	private void RenderScale(CCSPlayerController player)
	{
		var scale = ScaleOf(player);
		Exclusive(player, Root, "uis", scale == 100 ? "" : "uis-" + scale);
		SetVar(player, "uis_v", scale + "%");
	}

	[ConsoleCommand("css_wsscale", "PMM_WeaponSkins menu scale, %: !wsscale 75")]
	public void OnScaleCommand(CCSPlayerController? player, CommandInfo info)
	{
		if (player == null || !player.IsValid || player.IsBot)
			return;
		if (info.ArgCount < 2 || !int.TryParse(info.GetArg(1).Trim('%'), out var pct))
		{
			Chat(player, Lang.F("scale.usage", ScaleOf(player), string.Join(", ", ScalePresets)));
			return;
		}
		var scale = SetScale(player, pct);
		Chat(player, Lang.F("scale.set", scale));
		if (UiOf(player) is { Open: true })
			Render(player);
	}
}
