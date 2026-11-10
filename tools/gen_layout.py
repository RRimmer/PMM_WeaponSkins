#!/usr/bin/env python3
"""Generates the PMM_WeaponSkins Panorama layout, its stylesheet and Layout.g.cs (ids and sizes for C#).

Usage: python3 gen_layout.py <PMM_WeaponSkins project folder>

custom_hud_layout accepts only Panel / Label / Image / Button with id, class, hittest, text, src.
The server can only toggle classes and write {s:...} dialog variables, so every state of the menu is a
class on #pw-root and every picture is a class on an art panel (pmm_ws_art.css / pmm_ws_stickers.css).
"""
import os
import sys
from xml.sax.saxutils import escape, quoteattr

GRID_COLS, GRID_ROWS = 5, 13          # 65 cards: the longest weapon skin list fits without paging
STK_COLS, STK_ROWS = 4, 16            # 64 stickers / charms
CHIPS, NAV = 8, 6
STICKERS = 5                          # sticker slots per weapon (the CS2 limit)
GRID_C, GRID_R = 24, 18               # click grid over the weapon preview (30 px cells)
STEPS = 120                           # x0..x120 / y0..y120 = 0.83 % (6 px) steps for overlays
KNOB = 20                             # kp0..kp20 = 5 % steps for slider knobs
ROT = 72                              # ro0..ro72 = -180..180 by 5 degrees
SZ_MIN, SZ_STEP, SZ = 12, 4, 36       # sz0..sz36 = sticker height 12..156 px on the preview
SWEAR = 10                            # we0..we10 = sticker wear 0..1 by 0.1
KZ = 12                               # kz0..kz12 = charm z -3..3 by 0.5
WEAR_CELLS = 21                       # wear slider: 0..1 by 0.05
ROT_CELLS, SWEAR_CELLS, KZ_CELLS = 25, 21, 13

# Menu colours (config "Theme"): the server cannot send a colour, so every colour is a class baked in here.
# The config takes a name or any #rrggbb, which the plugin snaps to the nearest one. (name, hex, Russian name)
PALETTE = [
    ("white", "#ffffff", "белый"), ("silver", "#c8d0da", "серебристый"), ("gray", "#8a96a3", "серый"),
    ("ice", "#d6ecff", "ледяной"), ("sky", "#a9d0ff", "небесный (КТ по умолчанию)"), ("lightblue", "#7cc4ff", "светло-синий"),
    ("blue", "#4aa8ff", "синий (свечение КТ по умолчанию)"), ("royal", "#2f6bff", "королевский синий"), ("navy", "#2340b8", "тёмно-синий"),
    ("cyan", "#3fe0ff", "голубой"), ("teal", "#20c9b0", "бирюзовый"), ("turquoise", "#5ff2d6", "светло-бирюзовый"),
    ("mint", "#8cf5b0", "мятный"), ("green", "#3ddc6e", "зелёный"), ("lime", "#b6f03c", "лайм"),
    ("olive", "#9aa83a", "оливковый"), ("lemon", "#fff06a", "лимонный"), ("gold", "#ecc75c", "золотой (Т по умолчанию)"),
    ("amber", "#ffb02e", "янтарный (свечение Т по умолчанию)"), ("orange", "#ff8a2b", "оранжевый"), ("copper", "#d9773a", "медный"),
    ("coral", "#ff7a6b", "коралловый"), ("red", "#ff3b3b", "красный"), ("crimson", "#c8102e", "малиновый"),
    ("pink", "#ff7ac8", "розовый"), ("hotpink", "#ff3fa4", "ярко-розовый"), ("magenta", "#e040fb", "пурпурный"),
    ("violet", "#b07cff", "фиалковый"), ("purple", "#8a4dff", "фиолетовый"), ("lavender", "#cbb7ff", "лавандовый"),
    ("peach", "#ffc7a3", "персиковый"), ("brown", "#8d5a3b", "коричневый"),
]
# glow sizes 1..3: blur, spread, alpha
GLOW = {1: (16, 2, "bb"), 2: (32, 6, "dd"), 3: (56, 12, "ff")}

KB_RU = ["йцукенгшщзхъ", "фывапролджэ", "ячсмитьбю.-"]
KB_EN = ["qwertyuiop", "asdfghjkl", "zxcvbnm.-_"]

out = []


def a(**kw):
    parts = []
    for k, v in kw.items():
        if v is None:
            continue
        key = k.rstrip("_")
        parts.append(f"{key}={quoteattr(str(v))}")
    return (" " + " ".join(parts)) if parts else ""


class Node:
    def __init__(self, tag, **kw):
        self.tag, self.kw, self.kids = tag, kw, []

    def add(self, *kids):
        self.kids.extend(kids)
        return self

    def render(self, depth=0):
        pad = "\t" * depth
        if not self.kids:
            return f"{pad}<{self.tag}{a(**self.kw)} />"
        inner = "\n".join(k.render(depth + 1) for k in self.kids)
        return f"{pad}<{self.tag}{a(**self.kw)}>\n{inner}\n{pad}</{self.tag}>"


def P(cls=None, id=None, hit=None, *kids):
    n = Node("Panel", id=id, class_=cls, hittest=hit)
    n.add(*kids)
    return n


def B(id, cls, *kids):
    n = Node("Button", id=id, class_=cls)
    n.add(*kids)
    return n


def T(text, cls="", id=None):
    return Node("Label", id=id, class_=cls.strip() or None, hittest="false", text=text)


# Turkish for every static string, keyed by the English text (an unknown string falls back to English).
TR = {
    'Back': 'Geri',
    'Rarity: rare first': 'Nadirlik: önce nadir',
    'Rarity: common first': 'Nadirlik: önce yaygın',
    'Name: A - Z': 'Ad: A - Z',
    'Name: Z - A': 'Ad: Z - A',
    'Equipped': 'Kuşanıldı',
    'X  Close': 'X  Kapat',
    'Settings': 'Ayarlar',
    'Stickers': 'Çıkartmalar',
    'Charm': 'Takı',
    'Cancel': 'İptal',
    'Apply changes': 'Değişiklikleri uygula',
    'CT': 'AT',
    'Both teams': 'İki taraf',
    'Terrorists': 'Teröristler',
    'Factory New': 'Fabrikadan Yeni Çıkmış',
    'Minimal Wear': 'Az Aşınmış',
    'Field-Tested': 'Görevde Kullanılmış',
    'Well-Worn': 'Eskimiş',
    'Battle-Scarred': 'Savaş Görmüş',
    'step 0.01 - click for an exact value': 'adım 0,01 - tam değer için tıklayın',
    'Wear': 'Aşınma',
    'Random': 'Rastgele',
    'Pattern': 'Desen',
    'StatTrak': 'StatTrak',
    'edit': 'düzenle',
    'Name tag': 'İsim etiketi',
    'Team': 'Taraf',
    'All': 'Tümü',
    'Mil-Spec': 'Askeri Sınıf',
    'Restricted': 'Kısıtlı',
    'Classified': 'Gizli',
    'Covert': 'Saklı',
    'Newest first': 'Önce en yeni',
    'Oldest first': 'Önce en eski',
    'arrows move by 0.01, a click on the weapon puts the sticker there; click X / Y for an exact value': "oklar 0,01 kaydırır, silaha tıklamak çıkartmayı oraya koyar; tam değer için X / Y'ye tıklayın",
    'Position': 'Konum',
    'Rotation': 'Döndürme',
    'exact': 'tam',
    'Scrape': 'Kazıma',
    'Slot positions and sizes come from the CS2 models. A sticker can only lie on the weapon\'s surface. CS2 does not support resizing stickers. Inspect the weapon (F) after "Apply changes" to see it in 3D.': 'Yuva konumları ve boyutları CS2 modellerinden alınır. Çıkartma yalnızca silahın yüzeyinde durabilir. CS2 çıkartma boyutunu değiştirmeyi desteklemez. 3D görmek için "Değişiklikleri uygula"dan sonra silahı inceleyin (F).',
    '< Done': '< Tamam',
    'Reset': 'Sıfırla',
    'a click on the weapon hangs the charm there; arrows move it along the surface': 'silaha tıklamak takıyı oraya asar; oklar onu yüzey boyunca kaydırır',
    'Attachment point': 'Takma noktası',
    'Attachment spots come from the CS2 models: a charm can only hang on the weapon. Inspect the weapon (F) after "Apply changes" to see it in 3D.': 'Takma noktaları CS2 modellerinden alınır: takı yalnızca silaha asılabilir. 3D görmek için "Değişiklikleri uygula"dan sonra silahı inceleyin (F).',
    'Change position': 'Konumu değiştir',
    'Remove': 'Kaldır',
    'Agent': 'Ajan',
    'Gloves': 'Eldivenler',
    'Knife': 'Bıçak',
    'Zeus x27': 'Zeus x27',
    'Music kit': 'Müzik kiti',
    'Pin': 'Rozet',
    'Click to change the agent': 'Ajanı değiştirmek için tıklayın',
    'Pistols': 'Tabancalar',
    'SMGs': 'Hafif makineliler',
    'Shotguns': 'Pompalılar',
    'Rifles': 'Tüfekler',
    'Snipers': 'Keskin nişancı',
    'Machine guns': 'Makineli tüfekler',
    'Back to main': 'Ana menüye dön',
    'Inventory': 'Envanter',
    'Scale': 'Ölçek',
    'Type here': 'Buraya yazın',
    'space': 'boşluk',
    'Clear': 'Temizle',
    '+/- minus': '+/- eksi',
    'Write in chat': 'Sohbete yaz',
    'Done': 'Tamam',
    'Type in chat': 'Sohbete yazın',
}


def L(ru, en, cls=""):
    """A static string in every menu language; #pw-root.lang-en / lang-tr picks one (Russian by default)."""
    return [T(ru, (cls + " l-ru").strip()), T(en, (cls + " l-en").strip()), T(TR.get(en, en), (cls + " l-tr").strip())]


def V(name, cls=""):
    return T("{s:" + name + "}", cls)


F_B = "stratum-bold-tf"
F_M = "stratum-medium-tf"


def rb():
    return P("pw-rb", None, "false")


def lock(extra=""):
    """Padlock on a section the player may not open (class "lock" on the tile; config "Access")."""
    return P(("pw-lock " + extra).strip(), None, "false")


# ---------------------------------------------------------------- top bar

def top_bar():
    return P("pw-top", None, None,
             B("pw-home", "pw-home pw-btn", T("<", "pw-home-arrow " + F_B), *L("Вернуться на главную", "Back to main", "pw-home-t " + F_B)),
             P("pw-ttl", None, "false", *L("Инвентарь", "Inventory", "pw-ttl-t " + F_B), V("subtitle", "pw-ttl-s " + F_M)),
             # menu scale for 4:3 / 5:4 / 16:10 screens; next to the title so it stays on screen even when the edges do not
             P("pw-uis", None, None,
               *L("Масштаб", "Scale", "pw-uis-l " + F_M),
               B("pw-uis-m", "pw-uisb pw-btn", T("-", "pw-uisb-t " + F_B)),
               V("uis_v", "pw-uis-v " + F_B),
               B("pw-uis-p", "pw-uisb pw-btn", T("+", "pw-uisb-t " + F_B))),
             P("pw-sp", None, "false"),
             B("pw-close", "pw-close pw-btn", T("X", "pw-close-t " + F_B)))


# ---------------------------------------------------------------- home

HOME_TILES = [("Агент", "Agent"), ("Перчатки", "Gloves"), ("Нож", "Knife"), ("Zeus x27", "Zeus x27"),
              ("Набор музыки", "Music kit"), ("Медаль", "Pin")]
CATS = [("Пистолеты", "Pistols"), ("ПП", "SMGs"), ("Дробовики", "Shotguns"), ("Винтовки", "Rifles"),
        ("Снайперские", "Snipers"), ("Пулемёты", "Machine guns")]


def home():
    left = P("pw-lcol")
    for i, (ru, en) in enumerate(HOME_TILES):
        left.add(B(f"pw-ht-{i}", "pw-tile pw-ht pw-btn",
                   *L(ru, en, "pw-ht-l " + F_B),
                   P("pw-art pw-ht-art", f"pw-ht-art-{i}", "false"),
                   V(f"ht_n_{i}", "pw-ht-n " + F_M),
                   lock(),
                   rb()))
    hero = B("pw-hero", "pw-hero pw-btn",
             P("pw-hero-clip", None, "false", P("pw-hero-art", "pw-hero-art", "false")),
             P("pw-badge", None, "false", V("team_name", "pw-badge-t " + F_B)),
             P("pw-hero-cap", None, "false", V("hero_n", "pw-hero-n " + F_B), V("hero_f", "pw-hero-f " + F_M)),
             P("pw-hero-hint", None, "false", *L("Нажмите, чтобы сменить агента", "Click to change the agent", "pw-hint-t " + F_M)),
             lock("pw-lock-big"))
    cats = P("pw-cats")
    for r in range(2):
        row = P("pw-cat-row", f"pw-crow-{r}")
        for c in range(3):
            i = r * 3 + c
            ru, en = CATS[i]
            thumbs = P("pw-th-grid", None, "false")
            for tr in range(2):
                trow = P("pw-th-row", None, "false")
                for tc in range(2):
                    j = tr * 2 + tc
                    trow.add(P("pw-th pw-art", f"pw-hc-{i}-{j}", "false", rb()))
                thumbs.add(trow)
            row.add(B(f"pw-hc-{i}", "pw-tile pw-hc pw-btn",
                      *L(ru, en, "pw-hc-l " + F_B), V(f"hc_c_{i}", "pw-hc-c " + F_M), thumbs, lock()))
        cats.add(row)
    other = B("pw-other", "pw-other pw-btn",
              P("pw-other-clip", None, "false", P("pw-other-art", "pw-other-art", "false")),
              P("pw-other-badge", None, "false", V("other_team", "pw-other-t " + F_B)),
              P("pw-other-sw", None, "false", V("other_sw", "pw-other-swt " + F_M)))
    return P("pw-v-home", None, None, left, hero, cats, other)


# ---------------------------------------------------------------- agents / nav / list

def mini_hero():
    return P("pw-mh", None, "false",
             P("pw-hero-clip", None, "false", P("pw-hero-art", "pw-mh-art", "false")),
             P("pw-badge", None, "false", V("team_name", "pw-badge-t " + F_B)),
             P("pw-hero-cap", None, "false", V("hero_n", "pw-hero-n " + F_B), V("hero_f", "pw-hero-f " + F_M)))


def nav():
    n = P("pw-nav")
    for i in range(NAV):
        n.add(B(f"pw-nav-{i}", "pw-tile pw-nv pw-btn",
                P("pw-art pw-nv-art", f"pw-nav-art-{i}", "false"),
                P("pw-nv-tx", None, "false", V(f"nav_l_{i}", "pw-nv-l " + F_B), V(f"nav_s_{i}", "pw-nv-s " + F_M)),
                P("pw-nv-mark", None, "false"),
                lock(),
                rb()))
    return n


SORTS = [("Редкость: сначала редкие", "Rarity: rare first"), ("Редкость: сначала обычные", "Rarity: common first"),
         ("Название: А - Я", "Name: A - Z"), ("Название: Я - А", "Name: Z - A")]


def side_icons(cls):
    """CT / both teams / T icons; the card's classes (eq-ct + eq-t, tm-ct + tm-t) pick which one shows."""
    return [P(f"{cls} {cls}-ct", None, "false"), P(f"{cls} {cls}-both", None, "false"), P(f"{cls} {cls}-t", None, "false")]


def list_panel():
    head = P("pw-ph", None, None,
             B("pw-back", "pw-back pw-btn", T("<", "pw-back-a " + F_B), *L("Назад", "Back", "pw-back-t " + F_M)),
             V("crumbs", "pw-crumbs " + F_B),
             P("pw-sp", None, "false"),
             V("count", "pw-count " + F_M),
             B("pw-sort", "pw-sort pw-btn", V("sort_l", "pw-sort-t " + F_M), T("v", "pw-sort-v " + F_M)))
    dd = P("pw-dd", None, None)
    for i, (ru, en) in enumerate(SORTS):
        dd.add(B(f"pw-sort-{i}", "pw-dd-i pw-btn", *L(ru, en, "pw-dd-t " + F_M)))
    chips = P("pw-chips")
    for i in range(CHIPS):
        chips.add(B(f"pw-chip-{i}", "pw-chip pw-btn", V(f"chip_{i}", "pw-chip-t " + F_M)))
    grid = P("pw-grid", "pw-grid")
    for r in range(GRID_ROWS):
        row = P("pw-row", f"pw-row-{r}")
        for c in range(GRID_COLS):
            i = r * GRID_COLS + c
            row.add(B(f"pw-c-{i}", "pw-card pw-btn",
                      P("pw-art pw-c-art", f"pw-c-art-{i}", "false"),
                      P("pw-eq", None, "false", *L("Надето", "Equipped", "pw-eq-t " + F_B), *side_icons("pw-eqi")),
                      P("pw-tms", None, "false", *side_icons("pw-tmi")),
                      V(f"cn_{i}", "pw-cn " + F_M), V(f"cs_{i}", "pw-cs " + F_M),
                      rb()))
        grid.add(row)
    pager = P("pw-pager", None, None, P("pw-pgin", None, None,
              B("pw-pg-prev", "pw-pg pw-btn", T("<", "pw-pg-t " + F_B)),
              V("page", "pw-pg-l " + F_M),
              B("pw-pg-next", "pw-pg pw-btn", T(">", "pw-pg-t " + F_B))))
    return P("pw-list", None, None, head, chips, grid, pager, dd)


# ---------------------------------------------------------------- settings

def track(prefix, cells, knob_id, extra=""):
    t = P(("pw-track " + extra).strip(), None, None, P("pw-tbar", None, "false"))
    for k in range(1, cells - 1):
        t.add(P(f"pw-notch pw-notch-{k}-{cells}", None, "false"))
    for k in range(cells):
        t.add(B(f"{prefix}-{k}", f"pw-cell pw-cell-{k}-{cells}"))
    t.add(P("pw-knob", knob_id, "false"))
    return t


def stage():
    wpn = P("pw-wpn", None, None, P("pw-s-art pw-art", "pw-s-art", "false"))
    for k in range(STICKERS):
        wpn.add(P("pw-ov", f"pw-ov-{k}", "false",
                  P("pw-ovy", f"pw-ovy-{k}", "false",
                    P("pw-ovr", f"pw-ovr-{k}", "false",
                      P("pw-ova", f"pw-ova-{k}", "false")))))
    wpn.add(P("pw-kov", "pw-kov", "false",
              P("pw-kovy", "pw-kovy", "false",
                P("pw-kring", None, "false"),
                P("pw-kova", "pw-kova", "false"))))
    grid = P("pw-pgrid", None, None)
    for r in range(GRID_R):
        row = P("pw-pgrow", None, None)
        for c in range(GRID_C):
            row.add(B(f"pw-g-{c}-{r}", "pw-gcell"))
        grid.add(row)
    wpn.add(grid)
    inspect = P("pw-inspect", None, None,
                P("pw-tick pw-tick-a", None, "false"), P("pw-tick pw-tick-b", None, "false"),
                P("pw-tick pw-tick-c", None, "false"), P("pw-tick pw-tick-d", None, "false"),
                P("pw-floor", None, "false"),
                wpn,
                V("s_led", "pw-led " + F_B),
                P("pw-plate", None, "false", V("s_tag", "pw-plate-t " + F_M)),
                P("pw-side", None, "false", P("pw-side-ic", "pw-s-sideic", "false"), V("s_side", "pw-side-t " + F_M)),
                P("pw-phint", None, "false", V("phint", "pw-phint-t " + F_M)),
                slot_bar())
    rail = P("pw-rail", None, None, *L("Наклейки", "Stickers", "pw-rail-l " + F_B))
    for k in range(STICKERS):
        rail.add(B(f"pw-rs-{k}", "pw-rslot pw-btn",
                   P("pw-rs-art", f"pw-rs-art-{k}", "false"),
                   T(str(k + 1), "pw-rs-n " + F_M), T("+", "pw-rs-plus " + F_M), T("*", "pw-rs-mod " + F_B), lock("pw-lock-rs")))
    rail.add(B("pw-rs-k", "pw-rslot pw-rkc pw-btn",
               P("pw-rs-art", "pw-rs-art-kc", "false"),
               T("Б", "pw-rs-n l-ru " + F_M), T("C", "pw-rs-n l-en " + F_M), T("T", "pw-rs-n l-tr " + F_M), T("+", "pw-rs-plus " + F_M), T("*", "pw-rs-mod " + F_B), lock("pw-lock-rs")))
    return P("pw-stagec", None, None, inspect, rail, V("s_facts", "pw-facts " + F_M))


SIDES = [("Спецназ", "CT"), ("Обе стороны", "Both teams"), ("Террористы", "Terrorists")]
WEARS = [("FN", "Прямо с завода", "Factory New"), ("MW", "Немного поношенное", "Minimal Wear"),
         ("FT", "После полевых", "Field-Tested"), ("WW", "Поношенное", "Well-Worn"), ("BS", "Закалённое в боях", "Battle-Scarred")]


def sec(title_ru, title_en, var, *kids):
    return P("pw-sec", None, None, P("pw-h5", None, "false", *L(title_ru, title_en, "pw-h5-t " + F_B), V(var, "pw-h5-s " + F_M)), *kids)


def lsec(id, title_ru, title_en, var, *kids):
    """A setting that can be locked (config "Features"): class "lock" on the id greys the body and shows a padlock."""
    return P("pw-sec", id, None, P("pw-h5", None, "false", *L(title_ru, title_en, "pw-h5-t " + F_B), V(var, "pw-h5-s " + F_M)),
             lock("pw-lock-sec"), P("pw-secb", None, None, *kids))


def params():
    sides = P("pw-sides", None, None)
    for i, (ru, en) in enumerate(SIDES):
        sides.add(B(f"pw-side-{i}", "pw-sd pw-btn", P(f"pw-sd-ic pw-sd-ic-{i}", None, "false"), *L(ru, en, "pw-sd-t " + F_M)))
    wz = P("pw-wz", None, None)
    for i, (ab, ru, en) in enumerate(WEARS):
        wz.add(B(f"pw-wz-{i}", f"pw-wzb pw-btn", P(f"pw-wz-strip pw-wz-c{i}", None, "false"), T(ab, "pw-wz-ab " + F_B), *L(ru, en, "pw-wz-n " + F_M)))
    wear = lsec("pw-sec-wear", "Износ", "Wear", "wear_h", wz, track("pw-wc", WEAR_CELLS, "pw-wknob", "pw-wear-track"),
               P("pw-row2", None, None,
                 B("pw-wear-m", "pw-sq pw-btn", T("-", "pw-sq-t " + F_B)),
                 B("pw-wear-v", "pw-big pw-btn", V("wear_v", "pw-big-t " + F_B)),
                 B("pw-wear-p", "pw-sq pw-btn", T("+", "pw-sq-t " + F_B)),
                 *L("шаг 0,01 - точное число по клику", "step 0.01 - click for an exact value", "pw-mut " + F_M)))
    seed = lsec("pw-sec-seed", "Паттерн", "Pattern", "seed_h",
               P("pw-row2", None, None,
                 B("pw-seed-m", "pw-sq pw-btn", T("-", "pw-sq-t " + F_B)),
                 B("pw-seed-v", "pw-big pw-btn", V("seed_v", "pw-big-t " + F_B)),
                 B("pw-seed-p", "pw-sq pw-btn", T("+", "pw-sq-t " + F_B)),
                 B("pw-seed-r", "pw-wide pw-btn", *L("Случайный", "Random", "pw-wide-t " + F_M))))
    st = P("pw-stsec", None, None, lsec("pw-sec-st", "StatTrak", "StatTrak", "st_h",
           P("pw-row2", None, None,
             B("pw-st-t", "pw-sw pw-btn", P("pw-sw-k", None, "false")),
             V("st_state", "pw-st-state " + F_M),
             P("pw-sp", None, "false"),
             B("pw-st-v", "pw-field pw-btn", V("st_v", "pw-led2 " + F_B)))))
    tag = lsec("pw-sec-tag", "Именной тег", "Name tag", "tag_h",
              B("pw-tag", "pw-field pw-fieldw pw-btn", V("tag_v", "pw-field-t " + F_M), P("pw-sp", None, "false"),
                *L("изменить", "edit", "pw-mut " + F_M)))
    body = P("pw-pscroll", "pw-pscroll", None,
             sec("Сторона", "Team", "side_h", sides), wear, seed, st, tag)
    return P("pw-params", None, None, body)


RAR_F = [("Все", "All"), ("Армейское", "Mil-Spec"), ("Запрещённое", "Restricted"), ("Засекреченное", "Classified"), ("Тайное", "Covert")]


PSORTS = [("Сначала новые", "Newest first"), ("Сначала старые", "Oldest first"),
          ("Редкость: сначала редкие", "Rarity: rare first"), ("Редкость: сначала обычные", "Rarity: common first"),
          ("Название: А - Я", "Name: A - Z"), ("Название: Я - А", "Name: Z - A")]
SEC_N = 40                            # sticker sections: regular, all, then tournaments (2 columns x 20)


def picker():
    hd = P("pw-pickhd", None, None,
           B("pw-ssec", "pw-pdd pw-ssec pw-btn", V("ssec_l", "pw-pdd-t " + F_M), T("v", "pw-pdd-v " + F_M)),
           B("pw-psort", "pw-pdd pw-psort pw-btn", V("psort_l", "pw-pdd-t " + F_M), T("v", "pw-pdd-v " + F_M)),
           P("pw-sqbox", None, None,
             V("sq_v", "pw-sqv " + F_M),
             B("pw-sqx", "pw-sqx pw-btn", T("x", "pw-sqx-t " + F_B))),
           B("pw-sq", "pw-sqi pw-btn", P("pw-sqi-ic", None, "false")))
    rar = P("pw-rarf", None, None)
    for i, (ru, en) in enumerate(RAR_F):
        rar.add(B(f"pw-sr-{i}", f"pw-rf pw-btn", P(f"pw-rf-dot pw-rf-c{i}", None, "false"), *L(ru, en, "pw-rf-t " + F_M)))
    grid = P("pw-sgrid", "pw-sgrid")
    for r in range(STK_ROWS):
        row = P("pw-srow", f"pw-srow-{r}")
        for c in range(STK_COLS):
            i = r * STK_COLS + c
            row.add(B(f"pw-k-{i}", "pw-stk pw-btn",
                      P("pw-art pw-k-art", f"pw-k-art-{i}", "false"),
                      P("pw-kbadge", None, "false", V("k_badge", "pw-kbadge-t " + F_B)),
                      V(f"kn_{i}", "pw-kn " + F_M), rb()))
        grid.add(row)
    pager = P("pw-spager", None, None, P("pw-pgin", None, None,
              B("pw-spg-prev", "pw-pg pw-btn", T("<", "pw-pg-t " + F_B)),
              V("spg", "pw-pg-l " + F_M),
              B("pw-spg-next", "pw-pg pw-btn", T(">", "pw-pg-t " + F_B))))
    secdd = P("pw-secdd", None, None)
    half = SEC_N // 2
    for col in range(2):
        c = P("pw-seccol", None, None)
        for i in range(col * half, (col + 1) * half):
            c.add(B(f"pw-ssec-{i}", "pw-dd-i pw-sdi pw-btn", V(f"ssec_{i}", "pw-sdi-t " + F_M), V(f"ssec_c_{i}", "pw-sdi-c " + F_M)))
        secdd.add(c)
    sortdd = P("pw-psdd", None, None)
    for i, (ru, en) in enumerate(PSORTS):
        sortdd.add(B(f"pw-psort-{i}", "pw-dd-i pw-btn", *L(ru, en, "pw-dd-t " + F_M)))
    return P("pw-picker", None, None, hd, rar, V("sk_c", "pw-skc " + F_M), grid, pager, secdd, sortdd)


def slot_bar():
    """Current sticker slot / charm under the weapon picture: what is there and the two actions."""
    return P("pw-sbar", None, None,
             P("pw-sbar-top", None, None,
               P("pw-sb-art", "pw-sb-art", "false"),
               P("pw-sb-tx", None, "false", V("sb_t", "pw-sb-t " + F_B), V("sb_s", "pw-sb-s " + F_M))),
             B("pw-sb-pos", "pw-sbb pw-sbpos pw-btn", *L("Изменить положение", "Change position", "pw-sbb-t " + F_B)),
             B("pw-sb-rm", "pw-sbb pw-btn", *L("Убрать", "Remove", "pw-sbb-t " + F_M)),
             P("pw-sb-empty", None, "false", V("sb_e", "pw-sb-e " + F_M)))


def dpad(prefix):
    return P("pw-dpad", None, None,
             P("pw-dprow", None, None, P("pw-dpx", None, "false"), B(f"{prefix}-u", "pw-sq pw-btn", T("^", "pw-sq-t " + F_B)), P("pw-dpx", None, "false")),
             P("pw-dprow", None, None, B(f"{prefix}-l", "pw-sq pw-btn", T("<", "pw-sq-t " + F_B)), B(f"{prefix}-c", "pw-sq pw-btn", T("o", "pw-sq-t " + F_B)), B(f"{prefix}-r", "pw-sq pw-btn", T(">", "pw-sq-t " + F_B))),
             P("pw-dprow", None, None, P("pw-dpx", None, "false"), B(f"{prefix}-d", "pw-sq pw-btn", T("v", "pw-sq-t " + F_B)), P("pw-dpx", None, "false")))


def ed_head(prefix, tvar, svar):
    return P("pw-edhd", None, None,
             P("pw-ed-art", f"{prefix}-art", "false"),
             P("pw-sb-tx", None, "false", V(tvar, "pw-sb-t " + F_B), V(svar, "pw-sb-s " + F_M)),
             B(f"{prefix}-done", "pw-sbb pw-sbpos pw-btn", *L("< Готово", "< Done", "pw-sbb-t " + F_B)),
             B(f"{prefix}-reset", "pw-sbb pw-btn", *L("Сбросить", "Reset", "pw-sbb-t " + F_M)))


def sticker_editor():
    pos = sec("Расположение", "Position", "se_ph",
              P("pw-posrow", None, None, dpad("pw-se"),
                P("pw-xy", None, None,
                  P("pw-xyr", None, None, T("X", "pw-xyl " + F_B), B("pw-se-x", "pw-big pw-bigw pw-btn", V("se_x", "pw-big-t " + F_B))),
                  P("pw-xyr", None, None, T("Y", "pw-xyl " + F_B), B("pw-se-y", "pw-big pw-bigw pw-btn", V("se_y", "pw-big-t " + F_B))),
                  *L("стрелки двигают на 0,01, клик по оружию слева ставит наклейку туда; точное число по клику на X / Y", "arrows move by 0.01, a click on the weapon puts the sticker there; click X / Y for an exact value", "pw-mut pw-wrap " + F_M))))
    rot = sec("Поворот", "Rotation", "se_rh", track("pw-ser", ROT_CELLS, "pw-serk"),
              P("pw-row2", None, None,
                B("pw-se-r15m", "pw-sq pw-btn", T("-15", "pw-sq-t " + F_B)),
                B("pw-se-r1m", "pw-sq pw-btn", T("-1", "pw-sq-t " + F_B)),
                B("pw-se-rv", "pw-big pw-btn", V("se_r", "pw-big-t " + F_B)),
                B("pw-se-r1p", "pw-sq pw-btn", T("+1", "pw-sq-t " + F_B)),
                B("pw-se-r15p", "pw-sq pw-btn", T("+15", "pw-sq-t " + F_B))))
    wears = P("pw-mini", None, None)
    for i, t in enumerate(["0%", "25%", "50%", "75%", "100%"]):
        wears.add(B(f"pw-sewp-{i}", "pw-mb pw-btn", T(t, "pw-mb-t " + F_M)))
    wears.add(B("pw-sew-v", "pw-mb pw-btn", *L("точно", "exact", "pw-mb-t " + F_M)))
    wear = sec("Потёртость", "Scrape", "se_wh", track("pw-sew", SWEAR_CELLS, "pw-sewk"), wears)
    note = P("pw-note", None, "false", *L("Места и размеры наклеек взяты из моделей CS2. Наклейка лежит только на поверхности оружия - за край её не сдвинуть. Размер наклеек в CS2 не меняется. Вид на 3D-модели: осмотр оружия (F) после \"Применить изменения\".",
                                          "Slot positions and sizes come from the CS2 models. A sticker can only lie on the weapon's surface. CS2 does not support resizing stickers. Inspect the weapon (F) after \"Apply changes\" to see it in 3D.", "pw-mut pw-wrap " + F_M))
    body = P("pw-pscroll", "pw-sedscroll", None, pos, rot, wear, note)
    return P("pw-sed", None, None, ed_head("pw-se", "se_t", "se_s"), body)


def keychain_editor():
    pos = sec("Место крепления", "Attachment point", "ke_ph",
              P("pw-posrow", None, None, dpad("pw-ke"),
                P("pw-xy", None, None,
                  P("pw-xyr", None, None, T("X", "pw-xyl " + F_B), B("pw-ke-x", "pw-big pw-bigw pw-btn", V("ke_x", "pw-big-t " + F_B))),
                  P("pw-xyr", None, None, T("Z", "pw-xyl " + F_B), B("pw-ke-y", "pw-big pw-bigw pw-btn", V("ke_y", "pw-big-t " + F_B))),
                  *L("клик по оружию слева вешает брелок туда; стрелки двигают его по поверхности", "a click on the weapon hangs the charm there; arrows move it along the surface", "pw-mut pw-wrap " + F_M))))
    pat = sec("Шаблон", "Pattern", "ke_th",
              P("pw-row2", None, None,
                B("pw-ke-p", "pw-big pw-bigw pw-btn", V("ke_p", "pw-big-t " + F_B)),
                B("pw-ke-pr", "pw-wide pw-btn", *L("Случайный", "Random", "pw-wide-t " + F_M))))
    note = P("pw-note", None, "false", *L("Места крепления взяты из моделей CS2: брелок можно повесить только на оружие, не в воздух. Вид на 3D-модели: осмотр оружия (F) после \"Применить изменения\".",
                                          "Attachment spots come from the CS2 models: a charm can only hang on the weapon. Inspect the weapon (F) after \"Apply changes\" to see it in 3D.", "pw-mut pw-wrap " + F_M))
    body = P("pw-pscroll", "pw-kedscroll", None, pos, pat, note)
    return P("pw-ked", None, None, ed_head("pw-ke", "ke_t", "ke_s"), body)


def settings():
    head = P("pw-shd", None, None,
             P("pw-shd-tx", None, "false", V("s_kind", "pw-shd-k " + F_M), V("s_name", "pw-shd-n " + F_B)),
             P("pw-rchip", None, "false", T("{s:s_rar}", "pw-rchip-t " + F_B, id="pw-s-rar")),
             P("pw-sp", None, "false"),
             B("pw-s-x", "pw-xbtn pw-btn", *L("X  Закрыть", "X  Close", "pw-xbtn-t " + F_M)))
    tabs = P("pw-tabs", None, None,
             B("pw-tab-p", "pw-tab pw-btn", *L("Параметры", "Settings", "pw-tab-t " + F_B)),
             B("pw-tab-s", "pw-tab pw-btn", *L("Наклейки", "Stickers", "pw-tab-t " + F_B), V("tab_s_c", "pw-tab-c " + F_M), lock("pw-lock-tab")),
             B("pw-tab-k", "pw-tab pw-btn", *L("Брелок", "Charm", "pw-tab-t " + F_B), V("tab_k_c", "pw-tab-c " + F_M), lock("pw-lock-tab")))
    foot = P("pw-pfoot", None, None,
             B("pw-s-cancel", "pw-btn2 pw-btn", *L("Отмена", "Cancel", "pw-btn2-t " + F_M)),
             B("pw-s-apply", "pw-btnA pw-btn", *L("Применить изменения", "Apply changes", "pw-btnA-t " + F_B)))
    pcol = P("pw-pcol", None, None, tabs, params(), picker(), sticker_editor(), keychain_editor(), foot)
    return P("pw-set", None, None, head, P("pw-sbd", None, None, stage(), pcol))


# ---------------------------------------------------------------- keyboard / chat / toast

def keyboard():
    box = P("pw-kbox", None, None,
            P("pw-kbh", None, "false", *L("Напишите здесь", "Type here", "pw-kbh-t " + F_B), V("kb_what", "pw-kbh-w " + F_M)),
            P("pw-kdisp", None, "false", V("kb_text", "pw-kd-t " + F_M), P("pw-car", None, "false"), P("pw-sp", None, "false"), V("kb_lim", "pw-kd-lim " + F_M)),
            V("kb_hint", "pw-khint " + F_M))
    txt = P("pw-keys pw-ktxt", None, None)
    txt.add(P("pw-krow", None, None, *[B(f"pw-kd-{d}", "pw-key pw-btn", T(str(d), "pw-key-t " + F_M)) for d in [1, 2, 3, 4, 5, 6, 7, 8, 9, 0]]))
    for lang, rows, prefix in (("ru", KB_RU, "pw-kr"), ("en", KB_EN, "pw-kl")):
        idx = 0
        group = P(f"pw-kgrp pw-kgrp-{lang}", None, None)
        for ri, row in enumerate(rows):
            r = P("pw-krow", None, None)
            if ri == 2:
                r.add(B(f"pw-k-shift-{lang}", "pw-key pw-keyw pw-kshift pw-btn", T("Shift", "pw-key-t " + F_M)))
            for ch in row:
                r.add(B(f"{prefix}-{idx}", "pw-key pw-btn", T(ch, "pw-key-t pw-lo " + F_M), T(ch.upper(), "pw-key-t pw-up " + F_M)))
                idx += 1
            if ri == 2:
                r.add(B(f"pw-k-bs-{lang}", "pw-key pw-keyw pw-btn", T("<-", "pw-key-t " + F_M)))
            group.add(r)
        txt.add(group)
    txt.add(P("pw-krow", None, None,
              B("pw-k-lang", "pw-key pw-keyw pw-btn", T("RU > EN", "pw-key-t pw-kru " + F_M), T("EN > RU", "pw-key-t pw-ken " + F_M)),
              B("pw-k-sp", "pw-key pw-keysp pw-btn", *L("пробел", "space", "pw-key-t pw-mutk " + F_M)),
              B("pw-k-clr", "pw-key pw-keyw pw-btn", *L("Очистить", "Clear", "pw-key-t " + F_M))))
    num = P("pw-keys pw-knum", None, None)
    for row in ["123", "456", "789"]:
        num.add(P("pw-krow", None, None, *[B(f"pw-kn-{d}", "pw-key pw-keyn pw-btn", T(d, "pw-key-t " + F_M)) for d in row]))
    num.add(P("pw-krow", None, None,
              B("pw-kn-comma", "pw-key pw-keyn pw-btn", T(",", "pw-key-t " + F_M)),
              B("pw-kn-0", "pw-key pw-keyn pw-btn", T("0", "pw-key-t " + F_M)),
              B("pw-kn-bs", "pw-key pw-keyn pw-btn", T("<-", "pw-key-t " + F_M))))
    num.add(P("pw-krow", None, None,
              B("pw-kn-minus", "pw-key pw-keyn pw-btn", *L("+/- минус", "+/- minus", "pw-key-t " + F_M)),
              B("pw-kn-clr", "pw-key pw-keyn pw-btn", *L("Очистить", "Clear", "pw-key-t " + F_M))))
    box.add(txt, num,
            P("pw-kfoot", None, None,
              B("pw-kb-chat", "pw-chatb pw-btn", *L("Написать в чате", "Write in chat", "pw-btn2-t " + F_M)),
              P("pw-sp", None, "false"),
              B("pw-kb-cancel", "pw-btn2 pw-btn", *L("Отмена", "Cancel", "pw-btn2-t " + F_M)),
              B("pw-kb-ok", "pw-btnA pw-kok pw-btn", *L("Готово", "Done", "pw-btnA-t " + F_B))))
    return P("pw-kb", "pw-kb", None, box)


def chat_box():
    return P("pw-chat pw-hidden", "pw-chat", "false",
             P("pw-cbox", None, "false",
               V("ch_time", "pw-ch-time " + F_M),
               *L("Напишите в чате", "Type in chat", "pw-ch-big " + F_B),
               V("ch_what", "pw-ch-what " + F_M),
               V("ch_hint", "pw-ch-hint " + F_M),
               V("ch_err", "pw-ch-err " + F_M)))


def layout():
    styles = Node("styles").add(
        Node("include", src="s2r://panorama/styles/csgostyles.vcss_c"),
        Node("include", src="s2r://panorama/styles/custom_game/pmm_ws.vcss_c"),
        Node("include", src="s2r://panorama/styles/custom_game/pmm_ws_art.vcss_c"),
        Node("include", src="s2r://panorama/styles/custom_game/pmm_ws_stickers.vcss_c"))
    agents = P("pw-v-agents", None, None, mini_hero())
    side = P("pw-v-side", None, None, nav())
    right = P("pw-right", None, None, list_panel(), settings())
    body = P("pw-body", None, None, home(), agents, side, right)
    root = P("pw-root pw-hidden team-ct v-home", "pw-root", None,
             top_bar(), body,
             P("pw-toast", None, "false", V("toast", "pw-toast-t " + F_M)),
             keyboard())
    screen = P("pw-screen", None, "false", root, chat_box())
    return Node("root").add(styles, screen)


# ---------------------------------------------------------------- stylesheet

def ladders():
    css = []
    for i in range(STEPS + 1):
        css.append(f".x{i} {{ position: {i * 100 / STEPS:.2f}% 0px 0px; }}")
        css.append(f".y{i} {{ position: 0px {i * 100 / STEPS:.2f}% 0px; }}")
    for i in range(KNOB + 1):
        css.append(f".kp{i} {{ position: {i * 100 / KNOB:.2f}% 0px 0px; }}")
    for i in range(ROT + 1):
        css.append(f".ro{i} {{ transform: rotateZ( {-180 + i * 360 // ROT}deg ); }}")
    for i in range(SZ + 1):
        h = SZ_MIN + i * SZ_STEP
        css.append(f".sz{i} {{ width: {round(h * 4 / 3)}px; height: {h}px; }}")
    for i in range(SWEAR + 1):
        css.append(f".we{i} {{ opacity: {1 - i * 0.085:.3f}; }}")
    for i in range(KZ + 1):
        z = -3 + i * 0.5
        css.append(f".kz{i} {{ transform: scale3d( {1 + z * 0.1:.2f}, {1 + z * 0.1:.2f}, 1 ); }}")
    for cells in {WEAR_CELLS, ROT_CELLS, SWEAR_CELLS, KZ_CELLS}:
        w = 100 / (cells - 1)
        for k in range(cells):
            css.append(f".pw-cell-{k}-{cells} {{ position: {max((k - 0.5) * w, 0):.3f}% 0px 0px; width: {w if 0 < k < cells - 1 else w / 2:.3f}%; }}")
        for k in range(1, cells - 1):
            css.append(f".pw-notch-{k}-{cells} {{ position: {k * w:.3f}% 0px 0px; }}")
    return "\n".join(css)


def rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


def hx(c):
    return "#%02x%02x%02x" % tuple(max(0, min(255, round(v))) for v in c)


def scale(c, k):
    return hx(tuple(v * k for v in rgb(c)))


def tones(color):
    """Accent, text on the accent, and the two background tints of the menu for one palette colour."""
    r, g, b = rgb(color)
    light = 0.2126 * r + 0.7152 * g + 0.0722 * b > 120
    text = scale(color, 0.12) if light else "#ffffff"
    # the original two themes keep their exact tints
    fixed = {"#a9d0ff": ("#0f1f33", "#1d2c3e", "#33506f"), "#ecc75c": ("#2a2208", "#2f2a1a", "#5c4e26")}
    if color in fixed:
        return fixed[color]
    # backgrounds: a dark and a mid tint, never brighter than the default themes
    k = 255 / max(r, g, b, 1)
    base = tuple(v * k for v in (r, g, b))
    return text, hx(tuple(v * 0.14 + 12 for v in base)), hx(tuple(v * 0.26 + 16 for v in base))


THEME = {
    "root": ".pw-root.ACC {{ background-color: gradient(linear, 0% 0%, 100% 100%, from({b1}f4), to({b2}f4)); }}",
    "main": """.ACC .pw-acc, .ACC .pw-badge, .ACC .pw-nv.on .pw-nv-mark, .ACC .pw-chip.on, .ACC .pw-sd.on, .ACC .pw-rf.on,
.ACC .pw-mb.on, .ACC .pw-tab.on, .ACC .pw-btnA, .ACC .pw-sbpos, .ACC .pw-kbadge, .ACC .pw-pg.on, .ACC .pw-kbox, .ACC.kb-shift .pw-kshift
{{ background-color: {a}; }}
.ACC .pw-badge-t, .ACC .pw-chip.on .pw-chip-t, .ACC .pw-sd.on .pw-sd-t, .ACC .pw-rf.on .pw-rf-t, .ACC .pw-mb.on .pw-mb-t,
.ACC .pw-btnA-t, .ACC .pw-sbpos .pw-sbb-t, .ACC .pw-kbadge-t
{{ color: {t}; }}
.ACC .pw-tile.on, .ACC .pw-stk.cur, .ACC .pw-rslot.sel, .ACC .pw-wzb.on, .ACC .pw-ova.sel, .ACC .pw-kova.sel
{{ border: 2px solid {a}; }}
.ACC .pw-tab.on {{ background-color: {a}30; border-bottom: 3px solid {a}; }}""",
    "other": ".OAC .pw-other {{ background-color: gradient(linear, 0% 0%, 0% 100%, from({b2}aa), to({b1}aa)); }}",
    "dd": ".ACC .pw-dd-i.on .pw-dd-t {{ color: {a}; }}",
    "gcell": ".ACC .pw-gcell:hover {{ background-color: {a}22; border: 1px solid {a}; }}",
    "rsmod": ".ACC .pw-rs-mod {{ color: {a}; }}",
    "sdi": ".ACC .pw-dd-i.on .pw-sdi-t {{ color: {a}; }}",
    "sbar": ".ACC .pw-sbar {{ border-left: 3px solid {a}; }}",
    "kbh": ".ACC .pw-kbh-t {{ color: {a}; }}",
    "car": ".ACC .pw-car {{ background-color: {a}; }}",
}


def theme(base):
    """Fills the /*@THEME:key@*/ markers of pmm_ws.base.css: one rule set per palette colour, in the original place."""
    for key, tpl in THEME.items():
        rules = []
        for i, (_, color, _) in enumerate(PALETTE):
            t, b1, b2 = tones(color)
            rules.append(tpl.format(a=color, t=t, b1=b1, b2=b2).replace("ACC", f"acc-{i}").replace("OAC", f"oac-{i}"))
        if key == "main":
            # "Equipped" badge and frame of a card: only from eqa-N on the card (the colour of the side the skin is on),
            # never from the menu's side - the .acc-N rules above leave .pw-eq / .pw-card.eq alone
            for i, (_, color, _) in enumerate(PALETTE):
                t, _, _ = tones(color)
                rules.append(f".pw-card.eq.eqa-{i} {{ border: 2px solid {color}; }}")
                rules.append(f".eqa-{i} .pw-eq {{ background-color: {color}; }}")
                rules.append(f".eqa-{i} .pw-eq-t {{ color: {t}; }}")
        if key == "root":
            # outer glow around the menu: gw-<colour>-<size>
            for i, (_, color, _) in enumerate(PALETTE):
                for size, (blur, spread, alpha) in GLOW.items():
                    rules.append(f".pw-root.gw-{i}-{size} {{ box-shadow: {color}{alpha} 0px 0px {blur}px {spread}px; }}")
        marker = f"/*@THEME:{key}@*/"
        assert marker in base, marker
        base = base.replace(marker, f"/* theme colours: generated by gen_layout.py (PALETTE) */\n" + "\n".join(rules))
    return base


def main():
    project = sys.argv[1]
    lay = os.path.join(project, "panorama", "layout", "custom_game")
    sty = os.path.join(project, "panorama", "styles", "custom_game")
    os.makedirs(lay, exist_ok=True)
    os.makedirs(sty, exist_ok=True)
    xml = layout().render()
    with open(os.path.join(lay, "pmm_ws.xml"), "w", encoding="utf-8", newline="\n") as f:
        f.write(xml + "\n")
    base = theme(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "pmm_ws.base.css"), encoding="utf-8").read())
    with open(os.path.join(sty, "pmm_ws.css"), "w", encoding="utf-8", newline="\n") as f:
        f.write(base.rstrip() + "\n\n/* ---------- generated ladders (gen_layout.py) ---------- */\n" + ladders() + "\n")
    cs = f"""// Generated by tools/gen_layout.py. Do not edit by hand.
namespace PMM_WeaponSkins;

internal static class LayoutIds
{{
	public const string Path = "panorama/layout/custom_game/pmm_ws.xml";
	public const int GridCols = {GRID_COLS};
	public const int GridRows = {GRID_ROWS};
	public const int GridCards = {GRID_COLS * GRID_ROWS};
	public const int StkCols = {STK_COLS};
	public const int StkRows = {STK_ROWS};
	public const int StkCards = {STK_COLS * STK_ROWS};
	public const int Chips = {CHIPS};
	public const int Stickers = {STICKERS};
	public const int Sections = {SEC_N};
	public const int PickSorts = {len(PSORTS)};
	public const int Nav = {NAV};
	public const int ClickCols = {GRID_C};
	public const int ClickRows = {GRID_R};
	public const int Steps = {STEPS};
	public const int Knob = {KNOB};
	public const int Rot = {ROT};
	public const int Size = {SZ};
	public const int SizeMin = {SZ_MIN};
	public const int SizeStep = {SZ_STEP};
	public const int StickerWear = {SWEAR};
	public const int CharmZ = {KZ};
	public const int WearCells = {WEAR_CELLS};
	public const int RotCells = {ROT_CELLS};
	public const int StickerWearCells = {SWEAR_CELLS};
	public const int CharmZCells = {KZ_CELLS};
	public static readonly string[] KeysRu = {{ {", ".join(chr(34) + r + chr(34) for r in KB_RU)} }};
	public static readonly string[] KeysEn = {{ {", ".join(chr(34) + r + chr(34) for r in KB_EN)} }};
	/// <summary>Menu colours baked into pmm_ws.css (acc-N, oac-N, gw-N-size), see PALETTE in tools/gen_layout.py.</summary>
	public static readonly (string Name, string Hex)[] Palette = {{ {", ".join("(" + chr(34) + n + chr(34) + ", " + chr(34) + h + chr(34) + ")" for n, h, _ in PALETTE)} }};
	public const int GlowSizes = {len(GLOW)};
}}
"""
    with open(os.path.join(project, "Layout.g.cs"), "w", encoding="utf-8", newline="\n") as f:
        f.write(cs)
    print("layout", len(xml), "chars")


if __name__ == "__main__":
    main()
