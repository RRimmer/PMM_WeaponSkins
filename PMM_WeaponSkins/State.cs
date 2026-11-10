using CounterStrikeSharp.API.Core;

namespace PMM_WeaponSkins;

internal enum ViewKind
{
	Home,
	Agents,
	Equip,
	Weapons
}

internal enum DraftKind
{
	Glove,
	Knife,
	Zeus,
	Weapon
}

/// <summary>One card of the right-hand grid.</summary>
internal sealed class Card
{
	public string Art = "";
	public string Ph = "";
	public string Name = "";
	public string Sub = "";
	public int Rarity;
	public bool Eq;
	/// <summary>Equipped for the CT / T side: icons next to "Equipped" (both = the "both teams" icon).</summary>
	public bool EqCT;
	public bool EqT;
	public bool Dim;
	public bool Def;
	public bool MarkCT;
	public bool MarkT;
	public Action<CCSPlayerController>? Click;
}

internal sealed class StickerDraft
{
	public int Id;
	public float X;
	public float Y;
	public float Rot;
	public float Scale = 1f;
	public float Wear;
	/// <summary>
	/// "sticker slot N schema": the model sticker spot X / Y are measured from. -1 = the slot's own spot (slots 0..3);
	/// the fifth sticker (slot 4) always names one.
	/// </summary>
	public int Schema = -1;

	public bool Moved => X != 0f || Y != 0f || Rot != 0f || Wear > 0f;
}

internal sealed class CharmDraft
{
	public int Id;
	public float X;
	public float Y;
	public float Z;
	public int Pattern;
	/// <summary>Hangs on Valve's default spot. X / Y / Z are model units (x towards the muzzle, y side, z up).</summary>
	public bool Default = true;

	public bool Moved => !Default;
}

/// <summary>The skin being configured on the settings screen. Nothing is saved before "Apply".</summary>
internal sealed class Draft
{
	public DraftKind Kind;
	public int Def;
	public int Paint;
	public string Art = "";
	public int Rarity;
	public string SkinName = "";
	public string TypeName = "";
	public float Min;
	public float Max = 1f;
	public float Wear;
	public int Seed;
	public string? Tag;
	public bool St;
	public int StCount;
	public bool StEdited;
	public int Side = 1;
	/// <summary>Highest sticker slot: CS2 takes 5 stickers, slots 0..4.</summary>
	public const int MaxSlot = LayoutIds.Stickers - 1;
	public readonly StickerDraft?[] Stickers = new StickerDraft?[LayoutIds.Stickers];
	public CharmDraft? Charm;
	public int Tab;
	public int Slot;
	public bool EditSticker;
	public bool EditCharm;
	public bool StickersAllowed;

	// features the player may not use (config "Features"): shown greyed with a lock, saved as the default
	public bool LkWear;
	public bool LkSeed;
	public bool LkSt;
	public bool LkTag;
	public bool LkKc;
	/// <summary>Sticker slots the player may fill (config "Features.Stickers" and "StickerSlots"), 0..5.</summary>
	public int StkLimit = LayoutIds.Stickers;
	/// <summary>Wear and pattern a skin gets when the player may not change them.</summary>
	public float WearDefault;

	public bool LkStk => StkLimit <= 0;
	public bool SlotLocked(int slot) => slot >= StkLimit;

	public bool HasDecor => Kind == DraftKind.Weapon && StickersAllowed;
	public bool HasStatTrak => Kind != DraftKind.Glove;
	public string FullName => TypeName + " | " + SkinName;
	public int UsedStickers => Stickers.Count(s => s != null);
}

internal sealed class PromptState
{
	public string Field = "";
	public bool Chat;
	public string Text = "";
	public bool En;
	public bool Shift;
	public string? Err;
	public int Left;
	public int Token;
}

/// <summary>Everything the menu remembers for one player slot.</summary>
internal sealed class Ui
{
	public bool Open;
	public bool Shown;
	public int Token;
	public int Team = 3;
	public ViewKind View = ViewKind.Home;
	public string Sec = "gloves";
	public int GloveDef;
	public int KnifeDef;
	public int Cat;
	public int WeaponDef;
	public readonly Dictionary<string, int> Sort = new(StringComparer.Ordinal);
	public readonly Dictionary<string, int> Page = new(StringComparer.Ordinal);
	public string Fac = "";
	public bool Dd;
	public Draft? Set;
	public PromptState? Prompt;
	public LoadoutDto? Loadout;

	public List<Card> Cards = new();
	public string ListKey = "";
	public string ShownListKey = "";
	public string Crumbs = "";
	public bool Back;
	public bool Sortable;
	public bool Chips;
	public readonly List<(string Key, string Name)> ChipList = new();
	public int CardStart;

	public int StRar;
	/// <summary>Sticker section: "" regular stickers, "*" all, otherwise a tournament name.</summary>
	public string StSec = "";
	public int StSort;
	public int KcSort;
	/// <summary>Open picker dropdown: 0 none, 1 sections, 2 sorting.</summary>
	public int PickDd;
	public string StQuery = "";
	public string KcQuery = "";
	public int StPage;
	public List<Item> Picker = new();
	/// <summary>Filters and sorting ui.Picker was built for.</summary>
	public string PickerKey = "";
	public int PickerStart;
	public string ShownPickerKey = "";

	public string Toast = "";
	public bool ToastErr;
	public int ToastToken;
	public long NextClick;

	public TeamDto? Side => Loadout?.For(Team);

	public void Reset()
	{
		View = ViewKind.Home;
		GloveDef = 0;
		KnifeDef = 0;
		WeaponDef = 0;
		Fac = "";
		Dd = false;
		Set = null;
		Prompt = null;
		Page.Clear();
		Cards = new List<Card>();
		ListKey = "";
		ShownListKey = "";
		StQuery = "";
		KcQuery = "";
		StRar = 0;
		StSec = "";
		PickDd = 0;
		StPage = 0;
		ShownPickerKey = "";
	}
}
