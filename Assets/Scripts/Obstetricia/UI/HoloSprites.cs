using System;
using System.Collections.Generic;
using UnityEngine;

namespace Obstetricia.UI
{
    /// <summary>
    /// Genera en tiempo de ejecución (sin archivos de imagen) los sprites del estilo holográfico:
    /// paneles redondeados 9-slice, bordes con brillo e iconos vectoriales simples.
    /// Todo se cachea: cada sprite se crea una sola vez por sesión.
    /// </summary>
    public static class HoloSprites
    {
        // Medidas en unidades de canvas (1 = 1 mm). Las texturas se generan a Res x para bordes suaves en VR.
        private const int Res = 3;
        private const int PanelSize = 96;
        private const int PanelRadius = 26;
        private const int OutlineSize = 128;
        private const int OutlineGlow = 16;
        private const float OutlineThickness = 2.6f;
        private const int IconSize = 192;

        private static Sprite _fill;
        private static Sprite _outline;
        private static Sprite _circle;
        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();

        /// <summary>Margen (px de canvas) que ocupa el brillo exterior del borde.</summary>
        public static int OutlineMargin => OutlineGlow;

        /// <summary>Rectángulo redondeado relleno, blanco (se tiñe con Image.color).</summary>
        public static Sprite Fill => _fill != null ? _fill : (_fill = BuildFill());

        /// <summary>Borde redondeado con brillo exterior, blanco.</summary>
        public static Sprite Outline => _outline != null ? _outline : (_outline = BuildOutline());

        /// <summary>Círculo relleno con borde suave.</summary>
        public static Sprite Circle => _circle != null ? _circle : (_circle = BuildIcon("circle", (x, y) => Disc(x, y, 0f, 0f, 0.92f)));

        /// <summary>Devuelve un icono por id ("check", "cross", "scale", "trophy", "home", ...). Desconocido → círculo.</summary>
        public static Sprite Icon(string id)
        {
            if (string.IsNullOrEmpty(id)) return Circle;
            if (Icons.TryGetValue(id, out Sprite cached) && cached != null) return cached;

            Func<float, float, float> shape = ShapeFor(id);
            Sprite sprite = shape != null ? BuildIcon(id, shape) : Circle;
            Icons[id] = sprite;
            return sprite;
        }

        private static Sprite BuildFill()
        {
            int size = PanelSize * Res;
            var tex = NewTexture(size, "HoloFill");
            var px = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half, half - Res, half - Res, PanelRadius * Res);
                float a = Mathf.Clamp01(0.5f - d / (Res * 0.9f));
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            int b = (PanelRadius + 2) * Res;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f * Res, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static Sprite BuildOutline()
        {
            int size = OutlineSize * Res;
            var tex = NewTexture(size, "HoloOutline");
            var px = new Color32[size * size];
            float half = size / 2f;
            float inner = half - OutlineGlow * Res;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Distancias en unidades de canvas para que el aspecto no dependa de Res.
                float d = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half, inner, inner, PanelRadius * Res) / Res;
                float line = Mathf.Clamp01((OutlineThickness * 0.5f - Mathf.Abs(d)) * Res * 0.6f + 0.5f);
                float glow = d > 0f ? Mathf.Pow(Mathf.Clamp01(1f - d / OutlineGlow), 2.4f) * 0.5f : 0f;
                float innerGlow = d < 0f ? Mathf.Pow(Mathf.Clamp01(1f + d / 12f), 2.2f) * 0.22f : 0f;
                float a = Mathf.Clamp01(Mathf.Max(line, glow + innerGlow));
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            int b = (OutlineGlow + PanelRadius + 2) * Res;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f * Res, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static Sprite _scan;

        /// <summary>Barra horizontal de luz con bordes difuminados (efecto de escaneo holográfico).</summary>
        public static Sprite Scan
        {
            get
            {
                if (_scan != null) return _scan;
                const int w = 8, h = 64;
                var tex = NewTexture(w, "HoloScan");
                tex.Reinitialize(w, h);
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    float t = Mathf.Abs(y / (h - 1f) * 2f - 1f);
                    float a = Mathf.Pow(1f - t, 2.5f);
                    for (int x = 0; x < w; x++) px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _scan = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
                return _scan;
            }
        }

        /// <summary>Rasteriza una forma definida como cobertura (0..1) en coordenadas normalizadas [-1,1].</summary>
        private static Sprite BuildIcon(string id, Func<float, float, float> coverage)
        {
            var tex = NewTexture(IconSize, "HoloIcon_" + id);
            var px = new Color32[IconSize * IconSize];
            const int ss = 2; // supersampling para bordes suaves
            for (int y = 0; y < IconSize; y++)
            for (int x = 0; x < IconSize; x++)
            {
                float sum = 0f;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    float nx = ((x + (sx + 0.5f) / ss) / IconSize) * 2f - 1f;
                    float ny = ((y + (sy + 0.5f) / ss) / IconSize) * 2f - 1f;
                    sum += Mathf.Clamp01(coverage(nx, ny));
                }
                px[y * IconSize + x] = new Color(1f, 1f, 1f, sum / (ss * ss));
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, IconSize, IconSize), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Texture2D NewTexture(int size, string name)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, name.StartsWith("HoloIcon"))
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
        }

        // ---------- Formas de los iconos (coordenadas normalizadas, y hacia arriba) ----------

        private static Func<float, float, float> ShapeFor(string id)
        {
            switch (id)
            {
                case "prev": return (x, y) => Max(Seg(x, y, 0.2f, 0.5f, -0.3f, 0f, 0.1f), Seg(x, y, -0.3f, 0f, 0.2f, -0.5f, 0.1f));
                case "speaker": return (x, y) => Max(Box(x, y, -0.45f, 0f, 0.15f, 0.22f), Triangle(x, y, -0.32f, 0.22f, -0.32f, -0.22f, 0.05f, 0f),
                    Triangle(x, y, -0.32f, 0.22f, 0.05f, 0.55f, 0.05f, -0.55f), Triangle(x, y, -0.32f, -0.22f, 0.05f, -0.55f, 0.05f, 0.55f),
                    Arc(x, y, 0.05f, 0f, 0.35f, 0.06f, -50f, 50f), Arc(x, y, 0.05f, 0f, 0.62f, 0.06f, -50f, 50f));
                case "ruler": return (x, y) => Max(Seg(x, y, -0.75f, 0f, 0.75f, 0f, 0.07f),
                    Seg(x, y, -0.75f, -0.3f, -0.75f, 0.3f, 0.07f), Seg(x, y, 0.75f, -0.3f, 0.75f, 0.3f, 0.07f),
                    Triangle(x, y, -0.75f, 0f, -0.45f, 0.18f, -0.45f, -0.18f), Triangle(x, y, 0.75f, 0f, 0.45f, 0.18f, 0.45f, -0.18f));
                case "ring": return (x, y) => Ring(x, y, 0f, 0f, 0.84f, 0.1f);
                case "check": return (x, y) => Max(Ring(x, y, 0f, 0f, 0.86f, 0.09f),
                    Seg(x, y, -0.42f, 0.0f, -0.12f, -0.3f, 0.1f), Seg(x, y, -0.12f, -0.3f, 0.45f, 0.32f, 0.1f));
                case "cross": return (x, y) => Max(Ring(x, y, 0f, 0f, 0.86f, 0.09f),
                    Seg(x, y, -0.33f, -0.33f, 0.33f, 0.33f, 0.1f), Seg(x, y, -0.33f, 0.33f, 0.33f, -0.33f, 0.1f));
                case "scale": return (x, y) => Max(
                    Seg(x, y, 0f, -0.62f, 0f, 0.62f, 0.07f), Seg(x, y, -0.62f, 0.45f, 0.62f, 0.45f, 0.06f),
                    Seg(x, y, -0.3f, -0.66f, 0.3f, -0.66f, 0.08f), Disc(x, y, 0f, 0.66f, 0.1f),
                    Seg(x, y, -0.55f, 0.45f, -0.78f, -0.05f, 0.035f), Seg(x, y, -0.55f, 0.45f, -0.32f, -0.05f, 0.035f),
                    Seg(x, y, 0.55f, 0.45f, 0.78f, -0.05f, 0.035f), Seg(x, y, 0.55f, 0.45f, 0.32f, -0.05f, 0.035f),
                    HalfDisc(x, y, -0.55f, -0.05f, 0.25f), HalfDisc(x, y, 0.55f, -0.05f, 0.25f));
                case "trophy": return (x, y) => Max(
                    Cup(x, y), Ring(x, y, -0.5f, 0.38f, 0.2f, 0.06f), Ring(x, y, 0.5f, 0.38f, 0.2f, 0.06f),
                    Seg(x, y, 0f, -0.2f, 0f, -0.5f, 0.08f), Box(x, y, 0f, -0.62f, 0.32f, 0.08f));
                case "home": return (x, y) => Max(
                    Seg(x, y, -0.72f, 0.02f, 0f, 0.7f, 0.08f), Seg(x, y, 0f, 0.7f, 0.72f, 0.02f, 0.08f),
                    BoxOutline(x, y, 0f, -0.3f, 0.48f, 0.38f, 0.08f), Box(x, y, 0f, -0.46f, 0.12f, 0.22f));
                case "undo": return (x, y) => Max(
                    Arc(x, y, 0.05f, -0.05f, 0.5f, 0.08f, -100f, 160f),
                    Seg(x, y, -0.62f, 0.15f, -0.42f, 0.45f, 0.08f), Seg(x, y, -0.62f, 0.15f, -0.3f, 0.12f, 0.08f));
                case "pointer": return (x, y) => Max(Triangle(x, y, -0.45f, 0.7f, -0.45f, -0.4f, 0.35f, 0.05f),
                    Seg(x, y, -0.05f, -0.1f, 0.3f, -0.7f, 0.1f));
                case "pencil": return (x, y) => Max(Seg(x, y, -0.45f, -0.45f, 0.5f, 0.5f, 0.15f),
                    Triangle(x, y, -0.72f, -0.72f, -0.38f, -0.6f, -0.6f, -0.38f));
                case "gear": return (x, y) => Gear(x, y);
                case "cube": return (x, y) => Max(
                    Seg(x, y, 0f, 0.7f, 0.62f, 0.36f, 0.07f), Seg(x, y, 0f, 0.7f, -0.62f, 0.36f, 0.07f),
                    Seg(x, y, -0.62f, 0.36f, 0f, 0.02f, 0.07f), Seg(x, y, 0.62f, 0.36f, 0f, 0.02f, 0.07f),
                    Seg(x, y, -0.62f, 0.36f, -0.62f, -0.36f, 0.07f), Seg(x, y, 0.62f, 0.36f, 0.62f, -0.36f, 0.07f),
                    Seg(x, y, 0f, 0.02f, 0f, -0.72f, 0.07f), Seg(x, y, -0.62f, -0.36f, 0f, -0.72f, 0.07f),
                    Seg(x, y, 0.62f, -0.36f, 0f, -0.72f, 0.07f));
                case "pulse": return (x, y) => Max(
                    Seg(x, y, -0.85f, 0f, -0.4f, 0f, 0.07f), Seg(x, y, -0.4f, 0f, -0.2f, 0.55f, 0.07f),
                    Seg(x, y, -0.2f, 0.55f, 0.05f, -0.6f, 0.07f), Seg(x, y, 0.05f, -0.6f, 0.28f, 0.2f, 0.07f),
                    Seg(x, y, 0.28f, 0.2f, 0.42f, 0f, 0.07f), Seg(x, y, 0.42f, 0f, 0.85f, 0f, 0.07f));
                case "ball": return (x, y) => Max(Ring(x, y, 0f, 0f, 0.78f, 0.08f),
                    Arc(x, y, -0.95f, 0f, 0.75f, 0.06f, -50f, 50f), Arc(x, y, 0.95f, 0f, 0.75f, 0.06f, 130f, 230f),
                    Seg(x, y, -0.78f, 0f, 0.78f, 0f, 0.05f));
                case "target": return (x, y) => Max(Ring(x, y, 0f, 0f, 0.6f, 0.08f), Disc(x, y, 0f, 0f, 0.14f),
                    Seg(x, y, 0f, 0.42f, 0f, 0.9f, 0.07f), Seg(x, y, 0f, -0.42f, 0f, -0.9f, 0.07f),
                    Seg(x, y, 0.42f, 0f, 0.9f, 0f, 0.07f), Seg(x, y, -0.42f, 0f, -0.9f, 0f, 0.07f));
                case "clipboard": return (x, y) => Max(BoxOutline(x, y, 0f, -0.08f, 0.55f, 0.72f, 0.08f),
                    Box(x, y, 0f, 0.66f, 0.25f, 0.1f), Seg(x, y, -0.3f, 0.25f, 0.3f, 0.25f, 0.06f),
                    Seg(x, y, -0.3f, -0.05f, 0.3f, -0.05f, 0.06f), Seg(x, y, -0.3f, -0.35f, 0.1f, -0.35f, 0.06f));
                case "notes": return (x, y) => Max(BoxOutline(x, y, 0f, 0f, 0.55f, 0.75f, 0.08f),
                    Seg(x, y, -0.28f, 0.35f, 0.28f, 0.35f, 0.06f), Seg(x, y, -0.28f, 0.05f, 0.28f, 0.05f, 0.06f),
                    Seg(x, y, -0.28f, -0.25f, 0.1f, -0.25f, 0.06f));
                case "pin": return (x, y) => Max(Disc(x, y, 0f, 0.35f, 0.32f), Seg(x, y, 0f, 0.1f, 0f, -0.8f, 0.07f));
                case "trash": return (x, y) => Max(BoxOutline(x, y, 0f, -0.2f, 0.42f, 0.55f, 0.08f),
                    Seg(x, y, -0.6f, 0.5f, 0.6f, 0.5f, 0.08f), Box(x, y, 0f, 0.62f, 0.18f, 0.07f),
                    Seg(x, y, -0.14f, 0.1f, -0.14f, -0.5f, 0.05f), Seg(x, y, 0.14f, 0.1f, 0.14f, -0.5f, 0.05f));
                case "play": return (x, y) => Triangle(x, y, -0.4f, 0.6f, -0.4f, -0.6f, 0.6f, 0f);
                case "pause": return (x, y) => Max(Box(x, y, -0.25f, 0f, 0.12f, 0.6f), Box(x, y, 0.25f, 0f, 0.12f, 0.6f));
                case "restart": return (x, y) => Max(Arc(x, y, 0f, 0f, 0.55f, 0.08f, 40f, 330f),
                    Triangle(x, y, 0.3f, 0.65f, 0.65f, 0.25f, 0.75f, 0.68f));
                case "close": return (x, y) => Max(Seg(x, y, -0.5f, -0.5f, 0.5f, 0.5f, 0.1f), Seg(x, y, -0.5f, 0.5f, 0.5f, -0.5f, 0.1f));
                case "video360": return (x, y) => Max(Ring(x, y, 0f, 0f, 0.8f, 0.07f),
                    Triangle(x, y, -0.2f, 0.35f, -0.2f, -0.35f, 0.35f, 0f),
                    Arc(x, y, 0f, 0f, 0.8f, 0.0f, 0f, 0f));
                case "search": return (x, y) => Max(Ring(x, y, -0.15f, 0.15f, 0.45f, 0.09f), Seg(x, y, 0.18f, -0.18f, 0.65f, -0.65f, 0.12f));
                case "next": return (x, y) => Max(Seg(x, y, -0.2f, 0.5f, 0.3f, 0f, 0.1f), Seg(x, y, 0.3f, 0f, -0.2f, -0.5f, 0.1f));
                case "labels": return (x, y) => Max(Disc(x, y, -0.5f, -0.4f, 0.15f), Seg(x, y, -0.5f, -0.4f, 0.1f, 0.4f, 0.05f),
                    Seg(x, y, 0.1f, 0.4f, 0.8f, 0.4f, 0.07f));
                case "recenter": return (x, y) => Max(Ring(x, y, 0f, 0f, 0.35f, 0.08f),
                    Seg(x, y, 0f, 0.55f, 0f, 0.85f, 0.07f), Seg(x, y, 0f, -0.55f, 0f, -0.85f, 0.07f),
                    Seg(x, y, 0.55f, 0f, 0.85f, 0f, 0.07f), Seg(x, y, -0.55f, 0f, -0.85f, 0f, 0.07f));
                default: return null;
            }
        }

        private static float Max(params float[] v)
        {
            float m = 0f;
            for (int i = 0; i < v.Length; i++) if (v[i] > m) m = v[i];
            return m;
        }

        /// <summary>Convierte una distancia con signo (en unidades normalizadas) a cobertura.</summary>
        private static float Cover(float signedDistance) => Mathf.Clamp01(0.5f - signedDistance * IconSize * 0.5f);

        private static float Disc(float x, float y, float cx, float cy, float r) =>
            Cover(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r);

        private static float HalfDisc(float x, float y, float cx, float cy, float r) =>
            y > cy ? 0f : Disc(x, y, cx, cy, r);

        private static float Ring(float x, float y, float cx, float cy, float r, float halfWidth) =>
            Cover(Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r) - halfWidth);

        private static float Arc(float x, float y, float cx, float cy, float r, float halfWidth, float fromDeg, float toDeg)
        {
            if (halfWidth <= 0f) return 0f;
            float ang = Mathf.Atan2(y - cy, x - cx) * Mathf.Rad2Deg;
            if (ang < fromDeg) ang += 360f;
            if (ang < fromDeg || ang > toDeg) return 0f;
            return Ring(x, y, cx, cy, r, halfWidth);
        }

        private static float Seg(float x, float y, float ax, float ay, float bx, float by, float halfWidth)
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Cover(Mathf.Sqrt(dx * dx + dy * dy) - halfWidth);
        }

        private static float Box(float x, float y, float cx, float cy, float hx, float hy) =>
            Cover(Mathf.Max(Mathf.Abs(x - cx) - hx, Mathf.Abs(y - cy) - hy));

        private static float BoxOutline(float x, float y, float cx, float cy, float hx, float hy, float halfWidth) =>
            Cover(Mathf.Abs(Mathf.Max(Mathf.Abs(x - cx) - hx, Mathf.Abs(y - cy) - hy)) - halfWidth);

        private static float Triangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float e0 = Edge(x, y, ax, ay, bx, by), e1 = Edge(x, y, bx, by, cx, cy), e2 = Edge(x, y, cx, cy, ax, ay);
            bool inside = (e0 >= 0 && e1 >= 0 && e2 >= 0) || (e0 <= 0 && e1 <= 0 && e2 <= 0);
            return inside ? 1f : 0f;
        }

        private static float Edge(float x, float y, float ax, float ay, float bx, float by) =>
            (bx - ax) * (y - ay) - (by - ay) * (x - ax);

        private static float Cup(float x, float y)
        {
            if (y > 0.65f || y < -0.25f) return 0f;
            float t = (0.65f - y) / 0.9f;
            float half = Mathf.Lerp(0.42f, 0.08f, t * t);
            return Cover(Mathf.Abs(x) - half);
        }

        private static float Gear(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x);
            float teeth = Mathf.Clamp01(Mathf.Cos(a * 8f) * 3f);
            float outer = 0.62f + 0.18f * teeth;
            float body = Cover(r - outer);
            float hole = Cover(r - 0.26f);
            return Mathf.Clamp01(body - hole);
        }

        private static float RoundedBoxDistance(float px, float py, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(px) - hx + r;
            float qy = Mathf.Abs(py) - hy + r;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }
    }
}
