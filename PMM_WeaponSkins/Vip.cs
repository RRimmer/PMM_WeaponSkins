using System.Reflection;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;

namespace PMM_WeaponSkins;

/// <summary>
/// VIP status from whichever VIP plugin the server runs, found through reflection so neither is needed to build:
/// - VIPCore 2.x (capability "vipcore:core", VipCoreApi.IVipCoreApi: IsPlayerVip, GetPlayerVipGroup);
/// - VIPCore by dexter (VIPCore.VIPCore.Current: IsClientVip, GetClientGroup).
/// Without a VIP plugin nobody is VIP.
/// </summary>
internal sealed class Vip
{
	private readonly ILogger _logger;
	private object? _target;
	private MethodInfo? _isVip, _group;
	private string _source = "";
	private long _scanAt;

	public Vip(ILogger logger)
	{
		_logger = logger;
	}

	public bool IsVip(CCSPlayerController player)
	{
		if (!Resolve())
			return false;
		try
		{
			return _isVip!.Invoke(_target, new object[] { player }) is true;
		}
		catch (Exception ex)
		{
			_logger.LogWarning("VIP check through {Source} failed: {Error}", _source, ex.InnerException?.Message ?? ex.Message);
			_target = null;
			return false;
		}
	}

	/// <summary>The player's VIP group, or "" when they are not VIP.</summary>
	public string Group(CCSPlayerController player)
	{
		if (!Resolve() || _group == null)
			return "";
		try
		{
			return _group.Invoke(_target, new object[] { player }) as string ?? "";
		}
		catch (Exception)
		{
			return "";
		}
	}

	private bool Resolve()
	{
		if (_target != null && _isVip != null)
			return true;
		var now = Environment.TickCount64;
		if (now - _scanAt < 5000 && _scanAt != 0)
			return false;
		_scanAt = now;
		try
		{
			return FindVipCore2() || FindDexter();
		}
		catch (Exception ex)
		{
			_logger.LogDebug("VIP plugin lookup failed: {Error}", ex.Message);
			return false;
		}
	}

	private static Type? FindType(string name)
	{
		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			if (assembly.IsDynamic)
				continue;
			try
			{
				if (assembly.GetType(name, false) is { } type)
					return type;
			}
			catch (Exception)
			{
			}
		}
		return null;
	}

	/// <summary>VIPCore 2.x: new PluginCapability&lt;IVipCoreApi&gt;("vipcore:core").Get().</summary>
	private bool FindVipCore2()
	{
		var api = FindType("VipCoreApi.IVipCoreApi");
		var capability = FindType("CounterStrikeSharp.API.Core.Capabilities.PluginCapability`1");
		if (api == null || capability == null)
			return false;
		var cap = Activator.CreateInstance(capability.MakeGenericType(api), "vipcore:core");
		var instance = cap?.GetType().GetMethod("Get", Type.EmptyTypes)?.Invoke(cap, null);
		var isVip = api.GetMethod("IsPlayerVip", new[] { typeof(CCSPlayerController) });
		if (instance == null || isVip == null)
			return false;
		_target = instance;
		_isVip = isVip;
		_group = api.GetMethod("GetPlayerVipGroup", new[] { typeof(CCSPlayerController) });
		_source = "VIPCore 2";
		_logger.LogInformation("VIP status comes from {Source}.", _source);
		return true;
	}

	/// <summary>VIPCore by dexter: the plugin instance in VIPCore.VIPCore.Current.</summary>
	private bool FindDexter()
	{
		var type = FindType("VIPCore.VIPCore");
		var current = type?.GetField("Current", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null);
		var isVip = type?.GetMethod("IsClientVip", new[] { typeof(CCSPlayerController) });
		if (current == null || isVip == null)
			return false;
		_target = current;
		_isVip = isVip;
		_group = type!.GetMethod("GetClientGroup", new[] { typeof(CCSPlayerController) });
		_source = "VIPCore (dexter)";
		_logger.LogInformation("VIP status comes from {Source}.", _source);
		return true;
	}
}
