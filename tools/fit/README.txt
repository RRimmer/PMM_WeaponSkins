Research scripts that produced tools/sticker_fit.json (kab, Hc, R). Paths inside are from the build session; adjust before re-running.

Inputs: reference data se-data.json (per weapon: 705x378 3D render "clean", sticker "mask", slot "anchors" and charm "quads",
all projected from the real models from the 3D renders), tools/sticker_markup.json, the inventory renders (512x384).

1. kcfit.py    - Hc: homography model (x, z) -> reference render, from all charm quad corners (median error 1-3 px).
2. kab2.py     - checks k, a, b (sticker space = a - k*x, b - k*z) from the slot anchors (1-2 px) against the mask.
3. reg2.py     - R: homography reference render -> inventory render (skins / base), alpha silhouette registration.
4. assemble2.py- writes kab / Hc / R into tools/sticker_fit.json (k, a, b by RANSAC over anchors).
Then: python3 tools/gen_stickergeo.py gen PMM_WeaponSkins
