# PMM_WeaponSkins 0.1.0 — установка

## Что нужно
- CounterStrikeSharp 1.0.376 или новее (.NET 10)
- WeaponSkins by Star с `WeaponSkinsApi` (версия автора с изменениями для PMM). Со старой версией меню не откроется, и WeaponSkins покажет своё обычное меню.
- Клиентский аддон через MultiAddonManager (тот же, где лежит панорама PMM)

## 1. Сервер
Скопируйте `Server-plugins/counterstrikesharp/` в `game/csgo/addons/counterstrikesharp/`:

```
plugins/PMM_WeaponSkins/PMM_WeaponSkins.dll
plugins/PMM_WeaponSkins/pmm_ws_items.json
plugins/PMM_WeaponSkins/MySqlConnector.dll
plugins/PMM_WeaponSkins/lang/ru.json, en.json, tr.json
configs/plugins/PMM_WeaponSkins/PMM_WeaponSkins.json
```

- Не кладите в папку плагина `CounterStrikeSharp.API.dll`.
- При обновлении не перезаписывайте свой конфиг, если уже меняли его.

В конфиге WeaponSkins (`configs/plugins/WeaponSkins/WeaponSkins.json`) поставьте `"Menu_Style": 3` (RRimmer-PanoramaMenu).
При 1 (встроенное) или 2 (NickFox MenuManager) WeaponSkins открывает своё меню, а эта панорама не открывается.

## 2. Клиент (аддон)
Скопируйте содержимое `Content-addonmanager/` в корень аддона и пересоберите его в Workshop Tools:

```
panorama/layout/custom_game/pmm_ws.xml
panorama/styles/custom_game/pmm_ws.css
panorama/styles/custom_game/pmm_ws_art.css
panorama/styles/custom_game/pmm_ws_stickers.css
panorama/images/custom_game/pmmws_*.png
panorama/images/custom_game/pmmws_*_png.vtex
```

В репозитории лежат только PNG, `.vtex` к ним есть в архиве релиза. После выхода новой версии аддона игрокам нужно перезайти.

## 3. Игрокам
- `!ws`, `!skins` — главный экран; `!knife`, `!gloves`, `!agents`, `!music`, `!pin` — сразу нужный раздел;
  `!stickers` — наклейки оружия в руках.
- Скин: клик по оружию → вкладки «Параметры», «Наклейки», «Брелок» → «Применить изменения».
- Экран 4:3, 5:4 или 16:10: «Масштаб − +» в шапке меню или `!wsscale 75` (16:10 — 90, 4:3 — 75, 5:4 — 70).
  Выбор запоминается вместе с другими настройками игрока (MySQL).
- Наклейки: выберите слот справа от картинки, затем наклейку. «Изменить положение» под картинкой — место, поворот, потёртость.

## 4. Конфиг (`PMM_WeaponSkins.json`)
Комментарии `//` разрешены, у каждого ключа в файле есть пояснение. После правки — перезапуск карты или
`css_plugins reload PMM_WeaponSkins`.

| Ключ | По умолчанию | Что делает |
| --- | --- | --- |
| `Language` | `ru` | `ru`, `en`, `tr` или свой код с файлом `lang/<код>.json` (названия предметов берутся из WeaponSkins, `api.language`) |
| `ListMode` | `scroll` | `scroll` — колесо мыши, `pages` — страницы 1, 2, 3 |
| `InputMode` | `both` | `both` — клавиатура и «Написать в чате», `keyboard`, `chat` |
| `Commands` | `[]` | дополнительные команды открытия, например `["inv"]` |
| `OpenDelay` / `InputDelay` | `0.1` / `0.15` | задержка показа меню и включения курсора |
| `ChatTimeout` | `30` | сколько секунд ждать ответ в чате |
| `Debug` | `false` | подробный лог `[PMM-WS]` |
| `UseWeaponSkinsDatabase` | `true` | настройки игроков (масштаб, сортировки) хранятся в MySQL WeaponSkins — общие для всех серверов на этой базе. Таблица `pmm_ws_players` создаётся сама |
| `Database` | `Host` пустой | своя база MySQL, если `UseWeaponSkinsDatabase` = `false` |
| `RepaintPickedUpDefault` | `false` | перекрашивать только подобранное оружие **без скина** (валялось на карте); чужие скины остаются |
| `RepaintPickedUp` | `false` | подобранное оружие (с земли или у другого игрока) перекрашивается в скин того, кто подобрал; без своего скина — без скина. Нужен WeaponSkins с `ApplyToWeapon` |
| `Sections` | всё `true` | какие разделы видны: Agents, Gloves, Knife, Zeus, Music, Pins, Pistols, SMGs, Shotguns, Rifles, Snipers, MachineGuns |
| `Access` | все `[]` | кто может открыть раздел: `"vip"`, `"vip:ГРУППА"`, флаг админа `"@css/generic"`, группа `"#css/admin"`. Закрытый раздел виден с замком |
| `HiddenWeapons` | `[]` | какие оружия, ножи и перчатки скрыть внутри разделов, например `["weapon_negev", "weapon_knife_kukri", "sporty_gloves"]` (полный список имён — в конфиге) |
| `Scale` | `100` | масштаб меню по умолчанию, %: 100 / 90 / 80 / 75 / 70 / 60 (16:10 — 90, 4:3 — 75, 5:4 — 70) |

## 5. Известные ограничения
- Агент — картинка из клиента CS2 (по пояс), 3D-модель в кастомный HUD вставить нельзя.
- Наклейки: стандартные места слотов, размер и поверхность, на которую можно клеить, взяты из моделей CS2
  (StickerMarkup), поэтому превью совпадает с игрой. Картинка оружия - плоский рендер из инвентаря, на изогнутых
  деталях возможна разница в несколько пикселей. За край поверхности наклейку не сдвинуть (CS2 её там не рисует).
- Размер наклейки в CS2 изменить нельзя (игра игнорирует атрибут scale), поэтому настройки размера нет.
- Брелок: места крепления тоже из моделей CS2, повесить его можно только на оружие. У Dual Berettas доступно
  только стандартное место. На M249 и Negev картинка снята под сильным углом, совпадение хуже.
- Единицы смещения брелка (X/Y/Z) подобраны на глаз; точные границы уточним после теста в игре.
- В режиме прокрутки длинные списки (наклейки, медали) показываются частями по 64/65 карточек с кнопками «<» «>».
  Больше 64 наклеек за раз не показать: каждая картинка - отдельный CSS-класс, а custom_hud_layout помнит не больше
  1024 имён классов. Поэтому наклейки разбиты на разделы (обычные / турниры) с сортировкой и поиском.
