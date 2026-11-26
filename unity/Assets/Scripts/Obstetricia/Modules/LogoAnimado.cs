using System;
using System.Collections.Generic;
using Obstetricia.UI;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Obstetricia.Modules
{
    /// <summary>
    /// Logo UCEN animado: el disco aparece vacío y las letras llegan jugando, cada una a su manera (la U cae y rebota, la C
    /// entra rodando, la E salta desde dentro con una voltereta y la N llega a saltitos). Al final quedan exactamente como
    /// en el logo. Después se mueven de vez en cuando y responden si se las toca.
    /// </summary>
    public class LogoAnimado : MonoBehaviour
    {
        private static readonly string[] Nombres = { "U", "C", "E", "N" };
        // Lugar de cada letra en el disco (1 = radio): x derecha, y hacia el fondo.
        private static readonly Vector2[] Lugar =
        {
            new Vector2(-0.61649f, 0.54570f), new Vector2(-0.61168f, -0.28247f), new Vector2(0.10378f, 0.07285f), new Vector2(0.63024f, -0.55876f),
        };
        private static readonly float[] Inicio = { 0.1f, 0.6f, 1.25f, 1.85f };
        private static readonly float[] Dura = { 0.95f, 1.15f, 0.95f, 1.1f };
        private const float Final = 3.0f, Fin = 3.9f;
        private static readonly Color Celeste = new Color(0.45f, 0.9f, 1f);

        private class Letra
        {
            public Transform T;
            public Renderer R;
            public Vector3 Home;
            public int Evento;       // golpes ya sonados de la entrada
            public float Accion = -1f;
            public int Tipo;
            public float Brillo;
        }

        private class Chispa
        {
            public SpriteRenderer S;
            public Vector3 V;
            public float T, Vida;
        }

        private readonly List<Letra> _letras = new List<Letra>();
        private readonly List<Chispa> _chispas = new List<Chispa>();
        private readonly List<(SpriteRenderer s, float t, float max)> _ondas = new List<(SpriteRenderer, float, float)>();
        private MaterialPropertyBlock _block;
        private float _r, _t = -1f, _speed = 1f, _sweep = -9f, _nextIdle, _hide = -1f;
        private Action _done;
        private static readonly int IdDisco = Shader.PropertyToID("_Disco"), IdCorte = Shader.PropertyToID("_Corte"),
            IdBarrido = Shader.PropertyToID("_Barrido"), IdBrillo = Shader.PropertyToID("_Brillo");

        /// <summary>Destello del borde de la mesa (0..1) para acompañar los golpes.</summary>
        public float Golpe { get; private set; }

        public bool Animando => _t >= 0f;

        /// <summary>Crea las letras sobre el disco. Las mallas van en el orden U, C, E, N (1 = radio).</summary>
        public void Construir(float radio, Mesh[] mallas, Material material)
        {
            _r = radio;
            _block = new MaterialPropertyBlock();
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Letra_" + Nombres[i]);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mallas[i];
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                var l = new Letra { T = go.transform, R = r, Home = new Vector3(Lugar[i].x * radio, 0f, Lugar[i].y * radio) };
                var box = go.AddComponent<BoxCollider>();
                box.center = mallas[i].bounds.center + Vector3.up * 0.04f;
                box.size = mallas[i].bounds.size + new Vector3(0f, 0.08f, 0f);
                var touch = go.AddComponent<XRSimpleInteractable>();
                int k = i;
                touch.selectEntered.AddListener(_ => Tocar(k));
                touch.hoverEntered.AddListener(_ => { if (!Animando) l.Brillo = Mathf.Max(l.Brillo, 0.6f); });
                _letras.Add(l);
            }
            Ocultar();
        }

        /// <summary>Deja el disco vacío.</summary>
        public void Ocultar()
        {
            _t = _hide = -1f;
            foreach (Letra l in _letras) l.T.gameObject.SetActive(false);
            foreach (var o in _ondas) if (o.s != null) o.s.gameObject.SetActive(false);
            foreach (Chispa c in _chispas) c.S.gameObject.SetActive(false);
        }

        /// <summary>Reproduce la entrada de las letras. "listo" se llama al comenzar el remate.</summary>
        public void Reproducir(float velocidad, Action listo)
        {
            Ocultar();
            _t = 0f;
            _speed = velocidad;
            _done = listo;
            _sweep = -9f;
            foreach (Letra l in _letras)
            {
                l.Evento = 0;
                l.Accion = -1f;
                l.Brillo = 0f;
            }
        }

        /// <summary>Las letras se despiden con un saltito (mientras la mesa se oculta).</summary>
        public void Despedir()
        {
            if (_t >= 0f) { _t = -1f; _done = null; }
            _hide = 0f;
        }

        private void Tocar(int i)
        {
            if (Animando || _hide >= 0f) return;
            Letra l = _letras[i];
            if (l.Accion >= 0f) return;
            l.Accion = 0f;
            l.Tipo = 2;
            l.Brillo = 1f;
            HoloFeedback.Note(i);
            HoloFeedback.HapticAll(0.2f, 0.06f);
            Onda(l.Home, 0.5f);
        }

        // ------------------------------------------------------------------ Cada cuadro

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Golpe = Mathf.MoveTowards(Golpe, 0f, dt * 2.5f);
            if (_t >= 0f)
            {
                _t += dt * _speed;
                Entrada();
            }
            else Reposo(dt);
            Efectos(dt);

            Matrix4x4 disco = transform.worldToLocalMatrix;
            foreach (Letra l in _letras)
            {
                if (!l.T.gameObject.activeSelf) continue;
                l.Brillo = Mathf.MoveTowards(l.Brillo, 0f, dt * 1.8f);
                l.R.GetPropertyBlock(_block);
                _block.SetMatrix(IdDisco, disco);
                _block.SetFloat(IdBarrido, _sweep);
                _block.SetFloat(IdBrillo, l.Brillo);
                l.R.SetPropertyBlock(_block);
            }
        }

        private void Corte(Letra l, float radio)
        {
            l.R.GetPropertyBlock(_block);
            _block.SetFloat(IdCorte, radio);
            l.R.SetPropertyBlock(_block);
        }

        private void Poner(Letra l, Vector3 pos, Quaternion rot, Vector3 escala)
        {
            l.T.localPosition = pos;
            l.T.localRotation = rot;
            l.T.localScale = escala * _r;
        }

        private void Entrada()
        {
            for (int i = 0; i < 4; i++)
            {
                Letra l = _letras[i];
                float k = (_t - Inicio[i]) / Dura[i];
                if (k < 0f) continue;
                if (!l.T.gameObject.activeSelf)
                {
                    l.T.gameObject.SetActive(true);
                    Corte(l, 99f);
                }
                if (k < 1.35f)
                {
                    float c = Mathf.Clamp01(k);
                    switch (i)
                    {
                        case 0: EntraU(l, c); break;
                        case 1: EntraC(l, c); break;
                        case 2: EntraE(l, c); break;
                        default: EntraN(l, c); break;
                    }
                    // Al posarse, lo que sobra se recorta contra el borde del disco.
                    if (k >= 0.85f) Corte(l, Mathf.Lerp(_r * 1.4f, _r * 0.9985f, Ease.OutCubic(Mathf.InverseLerp(0.85f, 1.2f, k))));
                }
                else
                {
                    Corte(l, _r * 0.9985f);
                    if (_t < Final) Poner(l, l.Home, Quaternion.identity, Vector3.one);
                }
            }

            // Remate: ola de saltitos, destello y chispas
            if (_t >= Final)
            {
                if (_done != null)
                {
                    Action d = _done;
                    _done = null;
                    HoloFeedback.Play(HoloFeedback.Sound.Success, 0.8f);
                    Golpe = 1f;
                    Chispas(new Vector3(0f, 0.02f, 0f), 18, 0.55f);
                    Onda(Vector3.zero, 1f);
                    d();
                }
                float f = _t - Final;
                _sweep = Mathf.Lerp(-_r * 1.8f, _r * 1.8f, Mathf.Clamp01(f / 0.85f));
                for (int i = 0; i < 4; i++)
                {
                    float w = Mathf.Clamp01((f - i * 0.1f) / 0.42f);
                    float h = Mathf.Sin(w * Mathf.PI);
                    Poner(_letras[i], _letras[i].Home + Vector3.up * (h * 0.035f), Quaternion.Euler(0f, 0f, Mathf.Sin(w * Mathf.PI * 2f) * 6f),
                        new Vector3(1f - 0.06f * h, 1f + 0.5f * h, 1f - 0.06f * h));
                }
                if (_t >= Fin)
                {
                    _t = -1f;
                    _sweep = -9f;
                    _nextIdle = Time.unscaledTime + 5f;
                    foreach (Letra l in _letras) Poner(l, l.Home, Quaternion.identity, Vector3.one);
                }
            }
        }

        /// <summary>Aviso único por golpe: nota, onda, chispas y destello del borde.</summary>
        private void Impacto(Letra l, int n, float fuerza, int nota)
        {
            if (l.Evento >= n) return;
            l.Evento = n;
            HoloFeedback.Note(nota, 0.35f + 0.5f * fuerza);
            Onda(l.Home, 0.35f + 0.35f * fuerza);
            if (fuerza > 0.5f) Chispas(l.Home, 8, 0.4f);
            Golpe = Mathf.Max(Golpe, 0.35f + 0.4f * fuerza);
        }

        private static Vector3 Aplastar(float s) => new Vector3(1f + 0.22f * s, 1f - 0.6f * s, 1f + 0.22f * s);

        // La U cae desde arriba, se aplasta y da dos rebotes.
        private void EntraU(Letra l, float k)
        {
            float y, s = 0f, giro = 0f;
            float grow = Ease.OutBack(Mathf.Clamp01(k / 0.22f));
            if (k < 0.42f)
            {
                float f = k / 0.42f;
                y = 0.42f * (1f - f * f);
                giro = (1f - f) * 70f;
                s = -0.35f * f;
            }
            else if (k < 0.75f)
            {
                Impacto(l, 1, 1f, 0);
                float f = (k - 0.42f) / 0.33f;
                y = 0.1f * 4f * f * (1f - f);
                s = f < 0.25f ? 1f - f / 0.25f : 0f;
                giro = Mathf.Sin(f * Mathf.PI) * -10f;
            }
            else
            {
                Impacto(l, 2, 0.4f, 0);
                float f = (k - 0.75f) / 0.25f;
                y = 0.025f * 4f * f * (1f - f);
                s = f < 0.4f ? 0.4f * (1f - f / 0.4f) : 0f;
            }
            Poner(l, l.Home + Vector3.up * y, Quaternion.Euler(0f, giro, 0f), Aplastar(s) * grow);
        }

        // La C entra rodando como rueda, se tambalea y se acuesta.
        private void EntraC(Letra l, float k)
        {
            float rc = 0.332f * _r;
            float z0 = l.Home.z - rc; // línea de contacto
            float x0 = 0.5f * _r;
            if (k < 0.6f)
            {
                float f = Ease.OutCubic(k / 0.6f);
                float x = Mathf.Lerp(x0, l.Home.x, f);
                float ang = -(x - l.Home.x) / rc * Mathf.Rad2Deg;
                float grow = Ease.OutBack(Mathf.Clamp01(k / 0.14f));
                float wob = Mathf.Sin(k * 46f) * 5f * (1f - f);
                Poner(l, new Vector3(x, rc * Mathf.Max(0.2f, grow), z0), Quaternion.Euler(0f, wob, ang) * Quaternion.Euler(-90f, 0f, 0f), Vector3.one * grow);
                return;
            }
            float a;
            if (k < 0.7f) a = 90f + Mathf.Sin((k - 0.6f) / 0.1f * Mathf.PI) * 9f;              // toma impulso hacia quien mira
            else if (k < 0.86f) { float f = (k - 0.7f) / 0.16f; a = 90f * (1f - f * f); }       // cae
            else
            {
                Impacto(l, 1, 0.9f, 1);
                float f = (k - 0.86f) / 0.14f;
                a = 9f * 4f * f * (1f - f);                                                   // pequeño rebote
            }
            float rad = a * Mathf.Deg2Rad;
            Poner(l, new Vector3(l.Home.x, rc * Mathf.Sin(rad), z0 + rc * Mathf.Cos(rad)), Quaternion.Euler(-a, 0f, 0f), Vector3.one);
        }

        // La E sale disparada desde dentro del disco, da una voltereta y aterriza.
        private void EntraE(Letra l, float k)
        {
            if (k > 0.02f) Impacto(l, 1, 0.45f, 4);
            float y, s = 0f, flip = 0f, grow = 1f, yaw = 0f;
            if (k < 0.66f)
            {
                float f = k / 0.66f;
                y = -0.02f + 0.24f * 4f * f * (1f - f) + 0.02f * f;
                flip = Ease.InOutSine(f) * 360f;
                yaw = (1f - f) * -50f;
                grow = Ease.OutBack(Mathf.Clamp01(f / 0.35f), 1.1f);
                s = -0.25f * Mathf.Sin(f * Mathf.PI);
            }
            else
            {
                Impacto(l, 2, 1f, 2);
                float f = (k - 0.66f) / 0.34f;
                y = 0.03f * Mathf.Abs(Mathf.Sin(f * Mathf.PI * 1.5f)) * (1f - f);
                s = Mathf.Cos(f * Mathf.PI * 2.5f) * Mathf.Exp(-f * 4.5f) * 0.9f;
            }
            Poner(l, l.Home + Vector3.up * y, Quaternion.Euler(flip, yaw, 0f), Aplastar(s) * grow);
        }

        // La N llega dando tres saltitos, ladeándose en cada uno.
        private void EntraN(Letra l, float k)
        {
            Vector3 a = new Vector3(0.62f, 0f, 0.52f) * _r;
            const float hops = 3f;
            float u = Mathf.Clamp01(k / 0.82f) * hops;
            int n = Mathf.Min(2, Mathf.FloorToInt(u));
            float f = u - n;
            if (u >= hops) f = 1f;
            for (int i = 1; i <= n; i++) Impacto(l, i, 0.35f, 3);
            Vector3 from = Vector3.Lerp(a, l.Home, n / hops), to = Vector3.Lerp(a, l.Home, (n + 1) / hops);
            float grow = Ease.OutBack(Mathf.Clamp01(k / 0.16f));
            if (k < 0.82f)
            {
                float h = (0.12f - 0.025f * n) * 4f * f * (1f - f);
                float s = f < 0.16f ? 0.45f * (1f - f / 0.16f) : f > 0.9f ? 0.3f * (f - 0.9f) / 0.1f : -0.2f * Mathf.Sin(f * Mathf.PI);
                float tilt = Mathf.Sin(f * Mathf.PI) * (n % 2 == 0 ? 22f : -22f);
                Poner(l, Vector3.Lerp(from, to, f) + Vector3.up * h, Quaternion.Euler(0f, (1f - u / hops) * 60f, tilt), Aplastar(s) * grow);
                return;
            }
            Impacto(l, 3, 1f, 3);
            float g = (k - 0.82f) / 0.18f;
            Poner(l, l.Home, Quaternion.identity, Aplastar((1f - g) * Mathf.Cos(g * Mathf.PI) * 0.8f));
        }

        // ------------------------------------------------------------------ En reposo

        private void Reposo(float dt)
        {
            if (_hide >= 0f)
            {
                _hide += dt;
                for (int i = 0; i < 4; i++)
                {
                    Letra l = _letras[i];
                    if (!l.T.gameObject.activeSelf) continue;
                    float f = Mathf.Clamp01((_hide - i * 0.04f) / 0.22f);
                    Poner(l, l.Home + Vector3.up * (Mathf.Sin(f * Mathf.PI) * 0.04f), Quaternion.Euler(0f, f * 140f, 0f), Vector3.one * Mathf.Max(0.001f, 1f - f * f));
                    if (f >= 1f) l.T.gameObject.SetActive(false);
                }
                return;
            }
            bool any = false;
            for (int i = 0; i < _letras.Count; i++)
            {
                Letra l = _letras[i];
                if (!l.T.gameObject.activeSelf || l.Accion < 0f) continue;
                any = true;
                float dur = l.Tipo == 2 ? 0.75f : 0.6f;
                l.Accion += dt / dur;
                float f = Mathf.Clamp01(l.Accion);
                float h = Mathf.Sin(f * Mathf.PI);
                switch (l.Tipo)
                {
                    case 0: // saltito
                        Poner(l, l.Home + Vector3.up * (h * 0.028f), Quaternion.identity, new Vector3(1f, 1f + 0.5f * h, 1f));
                        break;
                    case 1: // contoneo
                        Poner(l, l.Home + Vector3.up * (h * 0.008f), Quaternion.Euler(0f, Mathf.Sin(f * Mathf.PI * 4f) * 9f * (1f - f), 0f), Vector3.one * (1f + 0.05f * h));
                        break;
                    default: // giro al tocarla
                        Poner(l, l.Home + Vector3.up * (h * 0.05f), Quaternion.Euler(0f, Ease.InOutSine(f) * 360f, 0f), Vector3.one * (1f - 0.12f * h));
                        break;
                }
                if (l.Accion < 1f) continue;
                l.Accion = -1f;
                Poner(l, l.Home, Quaternion.identity, Vector3.one);
                if (l.Tipo == 2) Onda(l.Home, 0.35f);
            }
            if (any || _letras.Count == 0 || !_letras[0].T.gameObject.activeSelf || Time.unscaledTime < _nextIdle) return;
            _nextIdle = Time.unscaledTime + UnityEngine.Random.Range(6f, 10f);
            Letra pick = _letras[UnityEngine.Random.Range(0, 4)];
            pick.Accion = 0f;
            pick.Tipo = UnityEngine.Random.value < 0.5f ? 0 : 1;
        }

        // ------------------------------------------------------------------ Ondas y chispas

        private SpriteRenderer NuevoSprite(string nombre, Sprite sprite)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<SpriteRenderer>();
            s.sprite = sprite;
            s.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return s;
        }

        private void Onda(Vector3 pos, float tamano)
        {
            int i = _ondas.FindIndex(o => !o.s.gameObject.activeSelf);
            if (i < 0)
            {
                if (_ondas.Count >= 8) return;
                SpriteRenderer n = NuevoSprite("Onda", HoloSprites.Icon("ring"));
                n.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _ondas.Add((n, 0f, 0f));
                i = _ondas.Count - 1;
            }
            SpriteRenderer s = _ondas[i].s;
            s.transform.localPosition = new Vector3(pos.x, 0.0012f + i * 0.0002f, pos.z);
            s.gameObject.SetActive(true);
            _ondas[i] = (s, 0f, tamano * _r);
        }

        private void Chispas(Vector3 pos, int n, float fuerza)
        {
            for (int i = 0; i < n; i++)
            {
                Chispa c = _chispas.Find(x => !x.S.gameObject.activeSelf);
                if (c == null)
                {
                    if (_chispas.Count >= 40) return;
                    c = new Chispa { S = NuevoSprite("Chispa", HoloSprites.Circle) };
                    _chispas.Add(c);
                }
                float a = UnityEngine.Random.value * Mathf.PI * 2f;
                float v = fuerza * UnityEngine.Random.Range(0.35f, 1f);
                c.V = new Vector3(Mathf.Cos(a) * v, UnityEngine.Random.Range(0.5f, 1f) * fuerza * 1.5f, Mathf.Sin(a) * v);
                c.T = 0f;
                c.Vida = UnityEngine.Random.Range(0.45f, 0.8f);
                c.S.transform.localPosition = pos + new Vector3(Mathf.Cos(a), 0.1f, Mathf.Sin(a)) * 0.04f;
                c.S.color = UnityEngine.Random.value < 0.5f ? Celeste : Color.white;
                c.S.gameObject.SetActive(true);
            }
        }

        private void Efectos(float dt)
        {
            for (int i = 0; i < _ondas.Count; i++)
            {
                var (s, t, max) = _ondas[i];
                if (!s.gameObject.activeSelf) continue;
                t += dt / 0.6f;
                float d = Mathf.Lerp(0.15f, 2f, Ease.OutCubic(Mathf.Clamp01(t))) * max;
                s.transform.localScale = Vector3.one * (d / Mathf.Max(0.001f, s.sprite.bounds.size.x));
                s.color = new Color(Celeste.r, Celeste.g, Celeste.b, 0.75f * (1f - Mathf.Clamp01(t)));
                if (t >= 1f) s.gameObject.SetActive(false);
                _ondas[i] = (s, t, max);
            }
            Camera cam = Camera.main;
            foreach (Chispa c in _chispas)
            {
                if (!c.S.gameObject.activeSelf) continue;
                c.T += dt;
                c.V += Vector3.down * (1.6f * dt);
                c.S.transform.localPosition += c.V * dt;
                float k = 1f - Mathf.Clamp01(c.T / c.Vida);
                c.S.transform.localScale = Vector3.one * (0.009f * k / Mathf.Max(0.001f, c.S.sprite.bounds.size.x));
                if (cam != null) c.S.transform.rotation = cam.transform.rotation;
                if (k <= 0f) c.S.gameObject.SetActive(false);
            }
        }
    }
}
