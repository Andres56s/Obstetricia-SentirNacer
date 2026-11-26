using System;
using System.Collections;
using System.Collections.Generic;
using Obstetricia.Data;
using Obstetricia.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Obstetricia.Modules
{
    /// <summary>
    /// Mesa holográfica UCEN (imagen de referencia): columna con disco azul, letras U-C-E-N y borde luminoso;
    /// sobre ella las dos tarjetas de programa unidas por un emblema circular. Tocar una tarjeta abre el programa.
    /// </summary>
    public class HubController : MonoBehaviour
    {
        [SerializeField] private ObstetriciaTheme theme;
        [SerializeField] private float pedestalRadius = 0.36f;
        [SerializeField] private float pedestalHeight = 0.82f;
        [SerializeField] private Vector3 viewerLocal = new Vector3(0f, 1.5f, -1.1f);
        [SerializeField] private float cardsBottom = 0.9f;
        [SerializeField, Tooltip("Desplazamiento (m) de las tarjetas hacia el fondo de la mesa.")]
        private float cardsDepth = 0.2f;

        private const float CardWidth = 420f;
        private const float CardHeight = 420f;

        private GameObject _pedestal;
        private GameObject _cards;
        private GameObject _hint;
        private Transform _emblemRing;
        private Renderer _rim;
        private MaterialPropertyBlock _rimBlock;
        private Color _rimColor;
        private Coroutine _anim;
        private LogoAnimado _logo;
        private MaletinSala _maletin;
        private bool _logoVisto;

        [SerializeField, Tooltip("Lugar (m, espacio de la mesa) de la columna con el maletín Sentir Nacer: a la izquierda del usuario.")]
        private Vector3 maletinLocal = new Vector3(-0.85f, 0f, -0.12f);
        private readonly List<(Transform t, Vector3 pos, Quaternion rot)> _flotan = new List<(Transform, Vector3, Quaternion)>();
        private readonly List<Transform> _anillos = new List<Transform>();
        private readonly List<SpriteRenderer> _luces = new List<SpriteRenderer>();
        private readonly List<RectTransform> _fotos = new List<RectTransform>();
        private RectTransform _destello;
        private bool _hintDismissed;

        public event Action FisioSelected;

        /// <summary>Se invoca al elegir la sala virtual de obstetricia.</summary>
        public event Action SalaSelected;

        /// <summary>Se invoca al pulsar el botón de ajustes de la mesa.</summary>
        public event Action SettingsRequested;
        public event Action LegalSelected;

        private void Awake()
        {
            BuildPedestal();
            BuildCards();
        }

        /// <summary>Muestra la mesa (se eleva desde el suelo) y luego las tarjetas.</summary>
        public void Show()
        {
            if (_anim != null) StopCoroutine(_anim);
            _pedestal.SetActive(true);
            _cards.SetActive(false);
            _anim = StartCoroutine(ShowRoutine());
        }

        /// <summary>Oculta tarjetas y mesa con animación. "done" se llama al terminar.</summary>
        public void Hide(Action done = null)
        {
            _hintDismissed = true;
            if (_anim != null) StopCoroutine(_anim);
            if (!isActiveAndEnabled || !_pedestal.activeSelf)
            {
                _cards.SetActive(false);
                _pedestal.SetActive(false);
                done?.Invoke();
                return;
            }
            _anim = StartCoroutine(HideRoutine(done));
        }

        /// <summary>Oculta todo al instante (con la vista ya cubierta por la transición).</summary>
        public void HideNow()
        {
            _hintDismissed = true;
            if (_anim != null) StopCoroutine(_anim);
            _anim = null;
            _cards.SetActive(false);
            _pedestal.SetActive(false);
            if (_logo != null) _logo.Ocultar();
            if (_maletin != null) _maletin.Reiniciar();
            if (_hint != null) _hint.SetActive(false);
        }

        private IEnumerator ShowRoutine()
        {
            HoloFeedback.Play(HoloFeedback.Sound.Open, 0.7f);
            if (_logo != null) _logo.Ocultar();
            if (_maletin != null) _maletin.Reiniciar();
            if (_hint != null) _hint.SetActive(false);
            yield return ScalePedestal(0f, 1f, 0.45f, true);
            if (_logo != null)
            {
                // La primera vez la entrada es completa; al volver a la mesa es más rápida.
                bool listo = false;
                _logo.Reproducir(_logoVisto ? 1.7f : 1f, () => listo = true);
                _logoVisto = true;
                while (!listo) yield return null;
            }
            _cards.SetActive(true);
            if (_maletin != null) _maletin.MostrarAyuda(true);
            if (_hint != null) _hint.SetActive(!_hintDismissed);
            _anim = null;
        }

        private IEnumerator HideRoutine(Action done)
        {
            HoloFeedback.Play(HoloFeedback.Sound.Close, 0.6f);
            if (_logo != null) _logo.Despedir();
            if (_maletin != null) _maletin.Reiniciar();
            foreach (Transform child in _cards.transform) HoloAppear.HideObject(child.gameObject);
            yield return new WaitForSecondsRealtime(0.15f);
            yield return ScalePedestal(1f, 0f, 0.3f, false);
            _cards.SetActive(false);
            foreach (Transform child in _cards.transform) child.gameObject.SetActive(true);
            if (_hint != null) _hint.SetActive(false);
            _pedestal.SetActive(false);
            _anim = null;
            done?.Invoke();
        }

        /// <summary>La mesa crece desde el suelo (o se hunde) con un leve rebote.</summary>
        private IEnumerator ScalePedestal(float from, float to, float seconds, bool bounce)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / seconds;
                float e = bounce ? Ease.OutBack(Mathf.Clamp01(t), 1.1f) : Ease.InCubic(Mathf.Clamp01(t));
                float k = Mathf.LerpUnclamped(from, to, e);
                _pedestal.transform.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(k)), Mathf.Max(0.001f, k), Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(k)));
                yield return null;
            }
            _pedestal.transform.localScale = Vector3.one * Mathf.Max(0.001f, to);
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            if (_emblemRing != null) _emblemRing.localRotation = Quaternion.Euler(0f, 0f, -time * 40f);
            if (_rim != null)
            {
                // El borde "respira" suavemente.
                float pulse = 0.75f + 0.25f * Mathf.Sin(time * 2.2f) + (_logo != null ? _logo.Golpe * 0.9f : 0f);
                _rim.GetPropertyBlock(_rimBlock);
                _rimBlock.SetColor("_BaseColor", _rimColor * pulse);
                _rim.SetPropertyBlock(_rimBlock);
            }
            if (_pedestal.activeSelf) AnimarMesa(time);
            if (_cards != null && _cards.activeSelf)
            {
                _cards.transform.localPosition = new Vector3(0f, Mathf.Sin(time * 1.1f) * 0.006f, 0f);
                AnimarTarjetas(time);
            }
        }

        /// <summary>Anillos de luz que suben por la columna y luces que recorren el borde del disco.</summary>
        private void AnimarMesa(float time)
        {
            for (int i = 0; i < _anillos.Count; i++)
            {
                float k = Mathf.Repeat(time * 0.22f + i / (float)_anillos.Count, 1f);
                float w = pedestalRadius * 1.15f * 1.012f, edge = Mathf.Sin(k * Mathf.PI);
                _anillos[i].localPosition = new Vector3(0f, Mathf.Lerp(0.04f, pedestalHeight - 0.05f, k), 0f);
                _anillos[i].localScale = new Vector3(w, 0.0005f + 0.003f * edge, w);
            }
            Camera cam = Camera.main;
            for (int i = 0; i < _luces.Count; i++)
            {
                int grupo = i / 6, cola = i % 6;
                float a = time * 0.9f * (grupo == 0 ? 1f : -0.7f) + grupo * Mathf.PI - cola * 0.07f * (grupo == 0 ? 1f : -1f);
                float y = pedestalHeight + 0.012f + 0.012f * Mathf.Sin(time * 1.7f + grupo);
                _luces[i].transform.localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (pedestalRadius * 1.09f) + Vector3.up * y;
                if (cam != null) _luces[i].transform.rotation = cam.transform.rotation;
            }
        }

        /// <summary>Las tarjetas flotan cada una a su ritmo, las fotos se acercan lentamente y un destello recorre el botón de la sala.</summary>
        private void AnimarTarjetas(float time)
        {
            for (int i = 0; i < _flotan.Count; i++)
            {
                var (t, pos, rot) = _flotan[i];
                float f = time * 0.9f + i * 1.9f;
                t.localPosition = pos + Vector3.up * (Mathf.Sin(f) * 0.004f);
                t.localRotation = rot * Quaternion.Euler(Mathf.Sin(f * 0.8f) * 0.7f, Mathf.Cos(f * 0.6f) * 0.9f, 0f);
            }
            for (int i = 0; i < _fotos.Count; i++)
                _fotos[i].localScale = Vector3.one * (1.04f + 0.04f * Mathf.Sin(time * 0.35f + i * 2.1f));
            if (_emblemRing != null) _emblemRing.localScale = Vector3.one * (1f + 0.07f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 2.2f)), 4f));
            if (_destello != null) _destello.anchoredPosition = new Vector2(Mathf.Lerp(-420f, 420f, Mathf.Repeat(time, 4.5f) / 1.1f), 0f);
        }

        private void BuildMesaFx()
        {
            Material glow = Mat(theme != null ? theme.glow : null, new Color(0.3f, 0.8f, 1f));
            for (int i = 0; i < 3; i++) _anillos.Add(Cylinder("Anillo", glow, Vector3.zero, Vector3.one * 0.01f, false).transform);
            for (int i = 0; i < 12; i++)
            {
                var go = new GameObject("Luz");
                go.transform.SetParent(_pedestal.transform, false);
                var s = go.AddComponent<SpriteRenderer>();
                s.sprite = HoloSprites.Circle;
                float k = 1f - (i % 6) / 6f;
                s.color = new Color(0.55f, 0.92f, 1f, 0.9f * k * k);
                go.transform.localScale = Vector3.one * (0.013f * (0.35f + 0.65f * k) / Mathf.Max(0.001f, s.sprite.bounds.size.x));
                _luces.Add(s);
            }
        }

        private Material Mat(Material m, Color fallback)
        {
            if (m != null) return m;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", fallback);
            return mat;
        }

        private void BuildPedestal()
        {
            _pedestal = new GameObject("MesaUCEN");
            _pedestal.transform.SetParent(transform, false);
            Material body = Mat(theme != null ? theme.pedestalBody : null, new Color(0.92f, 0.93f, 0.95f));
            Material top = Mat(theme != null ? theme.pedestalTop : null, new Color(0.08f, 0.3f, 0.75f));
            Material glow = Mat(theme != null ? theme.glow : null, new Color(0.3f, 0.8f, 1f));

            // Columna blanca
            Cylinder("Columna", body, new Vector3(0f, pedestalHeight / 2f, 0f), new Vector3(pedestalRadius * 1.15f, pedestalHeight / 2f, pedestalRadius * 1.15f));
            // Borde luminoso del disco
            GameObject rim = Cylinder("Borde", glow, new Vector3(0f, pedestalHeight - 0.004f, 0f), new Vector3(pedestalRadius * 2.06f, 0.014f, pedestalRadius * 2.06f), false);
            _rim = rim.GetComponent<Renderer>();
            _rimBlock = new MaterialPropertyBlock();
            _rimColor = glow.HasProperty("_BaseColor") ? glow.GetColor("_BaseColor") : Color.cyan;
            // Disco azul
            GameObject cover = Cylinder("Disco", top, new Vector3(0f, pedestalHeight + 0.006f, 0f), new Vector3(pedestalRadius * 2f, 0.012f, pedestalRadius * 2f));
            cover.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);

            BuildLogo();
            BuildMesaFx();
            BuildMaletin();
        }

        /// <summary>Columna aparte con el maletín Sentir Nacer; al abrirlo aparece el acceso a la sala virtual.</summary>
        private void BuildMaletin()
        {
            // El frente del maletín (la manilla) mira hacia donde está el usuario.
            Vector3 haciaUsuario = new Vector3(viewerLocal.x - maletinLocal.x, 0f, viewerLocal.z - maletinLocal.z);
            float yaw = Quaternion.LookRotation(-haciaUsuario.normalized, Vector3.up).eulerAngles.y;
            _maletin = MaletinSala.Crear(_pedestal.transform, theme, maletinLocal, yaw, pedestalHeight, 0.3f);
            _maletin.SalaElegida += () => SalaSelected?.Invoke();
        }

        private void BuildLogo()
        {
            float y = pedestalHeight + 0.0195f;
            if (theme != null && theme.logoLetra != null && theme.logoLetras != null && theme.logoLetras.Length == 4 && theme.logoLetras[0] != null)
            {
                // Disco vacío con el azul del logo; las letras llegan animadas.
                var blank = new GameObject("DiscoLogo");
                blank.transform.SetParent(_pedestal.transform, false);
                blank.transform.localPosition = new Vector3(0f, y, 0f);
                blank.AddComponent<MeshFilter>().sharedMesh = BuildLogoDisc(pedestalRadius, theme.logoCircle);
                var blue = new Material(theme.pedestalTop != null ? theme.pedestalTop : new Material(Shader.Find("Universal Render Pipeline/Lit"))) { name = "DiscoLogo" };
                blue.SetTexture("_BaseMap", null);
                blue.SetColor("_BaseColor", theme.logoAzul);
                blue.EnableKeyword("_EMISSION");
                blue.SetColor("_EmissionColor", theme.logoAzul.linear * 0.55f);
                var br = blank.AddComponent<MeshRenderer>();
                br.sharedMaterial = blue;
                br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _logo = blank.AddComponent<LogoAnimado>();
                _logo.Construir(pedestalRadius, theme.logoLetras, theme.logoLetra);
                return;
            }
            if (theme != null && theme.ucnLogo != null)
            {
                // El círculo azul del logo se mapea exacto sobre la cara superior del disco.
                var logo = new GameObject("LogoUCEN");
                logo.transform.SetParent(_pedestal.transform, false);
                logo.transform.localPosition = new Vector3(0f, y, 0f);
                logo.AddComponent<MeshFilter>().sharedMesh = BuildLogoDisc(pedestalRadius, theme.logoCircle);
                Material baseMat = theme.pedestalTop != null ? theme.pedestalTop : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                var mat = new Material(baseMat) { name = "LogoUCEN" };
                mat.SetTexture("_BaseMap", theme.ucnLogo);
                mat.SetColor("_BaseColor", Color.white);
                // Brillo propio con el mismo logo: las letras se ven blancas aunque la luz de la sala sea cálida.
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", theme.ucnLogo);
                mat.SetColor("_EmissionColor", new Color(0.55f, 0.55f, 0.55f));
                var renderer = logo.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return;
            }

            // Letras grandes U, C, E, N repartidas sobre el disco como en la imagen (Z+ = lejos del usuario).
            float r = pedestalRadius;
            Letter("U", new Vector2(-0.38f * r, 0.42f * r), 0f, y);
            Letter("C", new Vector2(-0.52f * r, -0.28f * r), -12f, y);
            Letter("E", new Vector2(0.12f * r, 0.12f * r), 0f, y);
            Letter("N", new Vector2(0.5f * r, -0.42f * r), 10f, y);
        }

        /// <summary>Disco horizontal (cara hacia arriba) cuyas UV recorren el círculo del logo (centro xy, radio z en UV).</summary>
        private static Mesh BuildLogoDisc(float radius, Vector3 circle, int segments = 96)
        {
            var vertices = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var normals = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(circle.x, circle.y);
            normals[0] = Vector3.up;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                // +X = derecha del usuario, +Z = hacia el fondo: la parte superior del logo queda lejos del usuario.
                vertices[i + 1] = new Vector3(cos * radius, 0f, sin * radius);
                uvs[i + 1] = new Vector2(circle.x + cos * circle.z, circle.y + sin * circle.z);
                normals[i + 1] = Vector3.up;
                int next = (i + 1) % segments + 1;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = next;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "LogoDisc", vertices = vertices, uv = uvs, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private void Letter(string letter, Vector2 pos, float roll, float y)
        {
            var go = new GameObject("Letra_" + letter);
            go.transform.SetParent(_pedestal.transform, false);
            go.transform.localPosition = new Vector3(pos.x, y, pos.y);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, roll);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = letter;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = 50f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.93f, 0.95f, 1f);
            tmp.margin = Vector4.zero;
            tmp.rectTransform.sizeDelta = new Vector2(pedestalRadius * 0.62f, pedestalRadius * 0.62f);
        }

        private GameObject Cylinder(string name, Material mat, Vector3 pos, Vector3 scale, bool shadows = true)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = SmoothMeshes.Cylinder;
            go.AddComponent<MeshRenderer>();
            go.transform.SetParent(_pedestal.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private void BuildCards()
        {
            _cards = new GameObject("ProgramCards");
            _cards.transform.SetParent(transform, false);
            ProgramCard fisio = theme != null ? theme.fisioCard : new ProgramCard { title = "PROGRAMA FISIO-BALÓN Y MÉTODOS NO FARMACOLÓGICOS DEL DOLOR" };
            ProgramCard legal = theme != null ? theme.legalCard : new ProgramCard { title = "PROGRAMA LEGAL: VIOLENCIA OBSTÉTRICA / LEY INTEGRAL DE LA MUJER" };

            // Las tarjetas se tocan en el centro y se abren hacia el usuario formando una V suave.
            float halfW = CardWidth * HoloUI.MillimeterScale / 2f;
            const float angle = 14f;
            float dx = halfW * Mathf.Cos(angle * Mathf.Deg2Rad);
            float dz = halfW * Mathf.Sin(angle * Mathf.Deg2Rad);
            float cy = cardsBottom + CardHeight * HoloUI.MillimeterScale / 2f;
            BuildCard(fisio, new Vector3(-dx, cy, dz + cardsDepth), angle, () => FisioSelected?.Invoke());
            BuildCard(legal, new Vector3(dx, cy, dz + cardsDepth), -angle, () => LegalSelected?.Invoke());

            // El acceso a la sala virtual está dentro del maletín Sentir Nacer (columna de la izquierda, ver BuildMaletin).

            // Emblema circular en la unión inferior de las tarjetas.
            Canvas emblem = HoloUI.CreateCanvas(_cards.transform, "Emblema", new Vector2(64, 64));
            emblem.transform.localPosition = new Vector3(0f, cardsBottom + 0.01f, cardsDepth - 0.01f);
            Image disc = HoloUI.Icon(emblem.transform, "Disco", "circle", Vector2.zero, 60, new Color(0.05f, 0.18f, 0.4f, 0.95f));
            disc.sprite = HoloSprites.Circle;
            _emblemRing = HoloUI.Icon(emblem.transform, "Anillo", "target", Vector2.zero, 56, HoloColors.Accent).transform;
            emblem.GetComponent<HoloAppear>().scan = false;

            // Ayuda para principiantes (desaparece al elegir un programa).
            Canvas hint = HoloUI.CreateCanvas(transform, "Ayuda", new Vector2(560, 92));
            hint.transform.localPosition = new Vector3(0f, pedestalHeight - 0.12f, -0.43f);
            hint.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            HoloPanel bubble = HoloUI.Panel(hint.transform, "Panel", Vector2.zero, new Vector2(560, 92), new Color(0.02f, 0.08f, 0.18f, 0.9f), HoloColors.Accent);
            bubble.Background.raycastTarget = false;
            HoloUI.Icon(bubble.transform, "Icono", "pointer", new Vector2(-240, 0), 40, HoloColors.Accent);
            HoloUI.Text(bubble.transform, "Texto", "Apunta a un programa y aprieta el <b>gatillo</b> para entrar.\n<size=80%>Mantén el botón <b>Meta</b> para recentrar la vista.</size>",
                18, new Vector2(25, 0), new Vector2(480, 80), TextAlignmentOptions.Left, HoloColors.Text);
            _hint = hint.gameObject;

            // Botón de ajustes junto a las tarjetas (comodidad, nombre, tutorial).
            Canvas gear = HoloUI.CreateCanvas(_cards.transform, "Ajustes", new Vector2(80, 80));
            gear.transform.localPosition = new Vector3(0.5f, cardsBottom + 0.04f, cardsDepth - 0.06f);
            gear.transform.localRotation = Quaternion.Euler(0f, -20f, 0f);
            gear.GetComponent<HoloAppear>().scan = false;
            HoloButton gearBtn = HoloUI.Button(gear.transform, "Boton", null, Vector2.zero, new Vector2(72, 72), () => SettingsRequested?.Invoke(), "gear");
            var gearFx = gearBtn.GetComponent<HoloHoverFx>();
            gearFx.tooltip = "Ajustes";
            gearFx.tooltipOffset = new Vector2(0f, 64f);
            gearFx.BuildTooltip();
            _hint.SetActive(false);
        }

        private void BuildCard(ProgramCard data, Vector3 localPos, float yaw, Action onClick)
        {
            Canvas canvas = HoloUI.CreateCanvas(_cards.transform, "Tarjeta_" + (yaw > 0 ? "Fisio" : "Legal"), new Vector2(CardWidth, CardHeight));
            canvas.transform.localPosition = localPos;
            canvas.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            HoloButton card = HoloUI.Button(canvas.transform, "Card", null, Vector2.zero, new Vector2(CardWidth, CardHeight), () => onClick());
            card.Panel.SetFill(HoloColors.PanelFill);
            HoloPanel header = HoloUI.Panel(card.transform, "Header", new Vector2(0, 155), new Vector2(CardWidth - 24, 88), HoloColors.PanelFillLight);
            header.Background.raycastTarget = false;
            HoloUI.Text(header.transform, "Title", data.title, 19, Vector2.zero, new Vector2(CardWidth - 50, 80), TextAlignmentOptions.Center,
                HoloColors.Text, FontStyles.Bold);
            Image foto = HoloUI.Picture(card.transform, "Foto", data.image, new Vector2(0, -45), new Vector2(CardWidth - 24, 300));
            _fotos.Add(foto.rectTransform);
            _flotan.Add((canvas.transform, canvas.transform.localPosition, canvas.transform.localRotation));
        }
    }
}
