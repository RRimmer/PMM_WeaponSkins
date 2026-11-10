namespace PMM_WeaponSkins;

/// <summary>
/// Real sticker and charm geometry of every weapon, taken from the game models (weapons/models/*.vmdl_c).
///
/// Stickers (StickerMarkup): CS2 places a sticker in a flat "sticker space" of the weapon. It is the model's side view
/// scaled uniformly: u = a - k*x, v = b - k*z (x towards the muzzle, z up, model units). Each slot has a default point
/// there (Offset), and "sticker slot N offset x/y" shift it in the same units. The Polygons are the surface a sticker
/// can lie on; outside them the game does not draw it.
///
/// Charms (KeychainMarkup): "keychain slot 0 offset x/y/z" are absolute model coordinates of the attachment point, which
/// must lie on one of the quads Valve marked.
///
/// The preview picture is the CS2 inventory render. Each weapon carries homographies from sticker space and from the model
/// side view (x, z) to that picture, derived from projected slot anchors and charm quads of 3D renders and registered onto
/// the inventory renders (see tools/gen_stickergeo.py). Skin renders and the plain weapon render are framed differently,
/// hence two maps of each.
/// </summary>
internal static partial class StickerGeo
{
	/// <summary>Width and height of the preview panel (.pw-wpn) in layout pixels.</summary>
	public const float PreviewW = 720f;
	public const float PreviewH = 540f;

	/// <summary>Projective map to the preview fraction: x = (A u + B v + C) / w, y = (D u + E v + F) / w, w = G u + H v + 1.</summary>
	internal readonly struct Part
	{
		public readonly float A, B, C, D, E, F, G, H;
		public readonly float UMin, UMax;

		public Part(float a, float b, float c, float d, float e, float f, float g, float h, float uMin, float uMax)
		{
			A = a; B = b; C = c; D = d; E = e; F = f; G = g; H = h;
			UMin = uMin; UMax = uMax;
		}

		public bool In(float u) => u >= UMin && u < UMax;

		public (float X, float Y) Apply(float u, float v)
		{
			var w = G * u + H * v + 1f;
			if (MathF.Abs(w) < 1e-6f)
				w = 1e-6f;
			return ((A * u + B * v + C) / w, (D * u + E * v + F) / w);
		}

		/// <summary>Layout pixels per unit and the screen angle (degrees) of the u axis at a point.</summary>
		public (float Ppu, float Angle, bool Mirrored) Local(float u, float v)
		{
			var w = G * u + H * v + 1f;
			if (MathF.Abs(w) < 1e-6f)
				w = 1e-6f;
			var (x, y) = Apply(u, v);
			var j00 = (A - x * G) / w * PreviewW;
			var j01 = (B - x * H) / w * PreviewW;
			var j10 = (D - y * G) / w * PreviewH;
			var j11 = (E - y * H) / w * PreviewH;
			var det = j00 * j11 - j01 * j10;
			return (MathF.Sqrt(MathF.Abs(det)), MathF.Atan2(j10, j00) * 180f / MathF.PI, det < 0f);
		}

		public bool Invert(float x, float y, out float u, out float v)
		{
			// Solve [A - xG, B - xH; D - yG, E - yH] [u v] = [x - C, y - F]
			var m00 = A - x * G;
			var m01 = B - x * H;
			var m10 = D - y * G;
			var m11 = E - y * H;
			var det = m00 * m11 - m01 * m10;
			u = v = 0f;
			if (MathF.Abs(det) < 1e-12f)
				return false;
			var rx = x - C;
			var ry = y - F;
			u = (m11 * rx - m01 * ry) / det;
			v = (m00 * ry - m10 * rx) / det;
			return true;
		}
	}

	internal sealed class Weapon
	{
		/// <summary>Per model sticker spot (markup Index 0..5): default x, default y, sticker size (1 / markup Scale).</summary>
		public readonly float[] Slots;
		/// <summary>Triangles of the sticker surface, 6 floats each.</summary>
		public readonly float[] Tris;
		public readonly Part[] Skin;
		public readonly Part[] Base;
		public readonly float MinU, MaxU, MinV, MaxV;

		/// <summary>Charm surfaces (KeychainMarkup): 12 floats per quad, corners TL, TR, BL, BR as (x, y, z) model units.</summary>
		public readonly float[]? Kc;
		public readonly int KcDefault;
		/// <summary>Model side view (x, z) -> preview fraction, for the skin and the plain render. Null when unknown.</summary>
		public readonly Part[]? KcSkin;
		public readonly Part[]? KcBase;
		/// <summary>Sticker-space units per model unit (k), used to size charm steps.</summary>
		public readonly float KcScale;

		public Weapon(float[] slots, float[] tris, Part[] skin, Part[] @base, float[]? kc, int kcDefault, Part[]? kcSkin, Part[]? kcBase, float kcScale)
		{
			Slots = slots;
			Tris = tris;
			Skin = skin;
			Base = @base;
			var ok = kc != null && kc.Length >= 12 && kcSkin is { Length: > 0 } && kcBase is { Length: > 0 } && kcScale > 0f;
			Kc = ok ? kc : null;
			KcDefault = kcDefault;
			KcSkin = ok ? kcSkin : null;
			KcBase = ok ? kcBase : null;
			KcScale = kcScale;
			MinU = MinV = float.MaxValue;
			MaxU = MaxV = float.MinValue;
			for (var i = 0; i + 1 < tris.Length; i += 2)
			{
				MinU = Math.Min(MinU, tris[i]); MaxU = Math.Max(MaxU, tris[i]);
				MinV = Math.Min(MinV, tris[i + 1]); MaxV = Math.Max(MaxV, tris[i + 1]);
			}
		}

		/// <summary>Sticker spots marked on the model (4..6).</summary>
		public int Spots => Slots.Length / 3;

		public float SlotX(int spot) => Slots[Math.Clamp(spot, 0, Spots - 1) * 3];
		public float SlotY(int spot) => Slots[Math.Clamp(spot, 0, Spots - 1) * 3 + 1];
		public float Size(int spot) => Slots[Math.Clamp(spot, 0, Spots - 1) * 3 + 2];

		public bool OnSurface(float u, float v)
		{
			for (var i = 0; i + 5 < Tris.Length; i += 6)
			{
				if (InTri(u, v, Tris[i], Tris[i + 1], Tris[i + 2], Tris[i + 3], Tris[i + 4], Tris[i + 5]))
					return true;
			}
			return false;
		}

		private static Part PartFor(Part[] parts, float u)
		{
			foreach (var p in parts)
			{
				if (p.In(u))
					return p;
			}
			return parts[0];
		}

		/// <summary>Sticker space -> preview fraction, plus px per unit and screen angle there.</summary>
		public (float X, float Y, float Ppu, float Angle, bool Mirrored) Map(bool skin, float u, float v)
		{
			var p = PartFor(skin ? Skin : Base, u);
			var (x, y) = p.Apply(u, v);
			var (ppu, angle, mirrored) = p.Local(u, v);
			return (x, y, ppu, angle, mirrored);
		}

		/// <summary>Preview fraction -> sticker space. False when the point is off the weapon's sticker surface.</summary>
		public bool Unmap(bool skin, float x, float y, out float u, out float v)
		{
			u = v = 0f;
			var found = false;
			foreach (var p in skin ? Skin : Base)
			{
				if (!p.Invert(x, y, out var pu, out var pv) || !p.In(pu))
					continue;
				u = pu;
				v = pv;
				found = true;
				if (OnSurface(pu, pv))
					return true;
			}
			return found && OnSurface(u, v);
		}

		// ------------------------------------------------------------------ charms

		public bool HasCharmGeo => Kc != null;

		/// <summary>Model step that moves a charm by about 0.01 sticker units (6-8 px on the preview).</summary>
		public float CharmStep => KcScale > 0f ? 0.01f / KcScale : 0.25f;

		/// <summary>Default charm point: centre of the quad Valve marks as DefaultLocation.</summary>
		public (float X, float Y, float Z) CharmDefault()
		{
			if (Kc == null)
				return (0f, 0f, 0f);
			var q = Math.Clamp(KcDefault, 0, Kc.Length / 12 - 1) * 12;
			return ((Kc[q] + Kc[q + 3] + Kc[q + 6] + Kc[q + 9]) / 4f,
				(Kc[q + 1] + Kc[q + 4] + Kc[q + 7] + Kc[q + 10]) / 4f,
				(Kc[q + 2] + Kc[q + 5] + Kc[q + 8] + Kc[q + 11]) / 4f);
		}

		/// <summary>
		/// Finds the charm surface under a side-view point (x along, z up) and returns the side coordinate y there.
		/// When quads overlap, the one nearest the viewer (largest y, the left side shown by the renders) wins.
		/// </summary>
		public bool CharmAt(float x, float z, out float y)
		{
			y = 0f;
			if (Kc == null)
				return false;
			var found = false;
			for (var q = 0; q + 11 < Kc.Length; q += 12)
			{
				// triangles (TL, TR, BR) and (TL, BR, BL)
				if (TriY(x, z, q, 0, 1, 3, out var ty) || TriY(x, z, q, 0, 3, 2, out ty))
				{
					if (!found || ty > y)
						y = ty;
					found = true;
				}
			}
			return found;
		}

		private bool TriY(float x, float z, int q, int a, int b, int c, out float y)
		{
			y = 0f;
			float ax = Kc![q + a * 3], ay = Kc[q + a * 3 + 1], az = Kc[q + a * 3 + 2];
			float bx = Kc[q + b * 3], by = Kc[q + b * 3 + 1], bz = Kc[q + b * 3 + 2];
			float cx = Kc[q + c * 3], cy = Kc[q + c * 3 + 1], cz = Kc[q + c * 3 + 2];
			var det = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
			if (MathF.Abs(det) < 1e-6f)
				return false;
			var w1 = ((bz - cz) * (x - cx) + (cx - bx) * (z - cz)) / det;
			var w2 = ((cz - az) * (x - cx) + (ax - cx) * (z - cz)) / det;
			var w3 = 1f - w1 - w2;
			const float eps = -0.02f;
			if (w1 < eps || w2 < eps || w3 < eps)
				return false;
			y = w1 * ay + w2 * by + w3 * cy;
			return true;
		}

		/// <summary>Charm point (model x along, z up) -> preview fraction and px per model unit there.</summary>
		public (float X, float Y, float Ppu) MapCharm(bool skin, float x, float z)
		{
			var p = (skin ? KcSkin : KcBase)![0];
			var (fx, fy) = p.Apply(x, z);
			return (fx, fy, p.Local(x, z).Ppu);
		}

		/// <summary>Preview fraction -> charm side-view point (x along, z up), without any surface check.</summary>
		public bool UnmapCharm(bool skin, float fx, float fy, out float x, out float z)
		{
			x = z = 0f;
			var parts = skin ? KcSkin : KcBase;
			return parts != null && parts[0].Invert(fx, fy, out x, out z);
		}
	}

	private static bool InTri(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
	{
		var d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
		var d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
		var d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
		var neg = d1 < 0f || d2 < 0f || d3 < 0f;
		var pos = d1 > 0f || d2 > 0f || d3 > 0f;
		return !(neg && pos);
	}

	public static Weapon? For(int def) => Data.TryGetValue(def, out var w) ? w : null;
}
