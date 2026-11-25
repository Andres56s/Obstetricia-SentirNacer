using Obstetricia.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Obstetricia.Sala
{
    /// <summary>
    /// Monitor fetal (cardiotocografía) simulado: dibuja la frecuencia cardíaca fetal y la actividad uterina en tiempo
    /// acelerado, con cuatro registros para comparar (normal y desaceleraciones tempranas, tardías y variables).
    /// </summary>
    public class MonitorFetal : MonoBehaviour
    {
        [SerializeField, Tooltip("Centro de la pantalla del modelo.")] private Transform pantalla;
        [SerializeField] private AudioSource sonido;
        [SerializeField] private AudioClip latido;

        private const int TexW = 360, TexH = 200, Split = 78; // Split: alto de la zona de actividad uterina
        private const float Ventana = 240f, Acelerado = 4f, Ciclo = 150f, Duracion = 70f;

        private static readonly string[] Nombres = { "Normal", "Tempranas", "Tardías", "Variables" };
        private static readonly string[] Textos =
        {
            "<b>Registro normal.</b> Línea base entre 110 y 160 lpm, variabilidad de 6 a 25 lpm y aceleraciones.",
            "<b>Desaceleraciones tempranas.</b> Son espejo de la contracción: el punto más bajo coincide con el acmé. Se asocian a compresión de la cabeza fetal.",
            "<b>Desaceleraciones tardías.</b> El punto más bajo ocurre después del acmé. Sugieren insuficiencia uteroplacentaria: evaluar de inmediato.",
            "<b>Desaceleraciones variables.</b> Caída brusca, de forma y momento variables. Se asocian a compresión del cordón umbilical.",
        };

        private static readonly Color32 Fondo = new Color32(6, 14, 24, 255), Grilla = new Color32(22, 48, 62, 255),
            GrillaFuerte = new Color32(36, 78, 96, 255), Norma = new Color32(14, 40, 44, 255),
            Verde = new Color32(90, 255, 150, 255), Rosa = new Color32(255, 150, 190, 255);

        private Texture2D _tex;
        private readonly Color32[] _col = new Color32[TexH];
        private TextMeshProUGUI _fcf, _toco, _text;
        private readonly HoloButton[] _btn = new HoloButton[4];
        private HoloButton _sound;
        private Image _heart;
        private int _modo, _x, _lastF, _lastU;
        private float _t, _beat, _nextText;
        private bool _on;

        private void Start()
        {
            Build();
            Elegir(0);
        }

        private void OnDisable()
        {
            _on = false;
            if (_sound != null) _sound.SetSelected(false);
        }

        /// <summary>Actividad uterina (mmHg aproximados): tono basal y contracciones regulares.</summary>
        private static float Utero(float t) => 12f + 62f * Pulso(Mathf.Repeat(t, Ciclo) - 50f, Duracion);

        // Campana suave centrada en 0 con el ancho indicado
        private static float Pulso(float t, float ancho)
        {
            if (Mathf.Abs(t) > ancho / 2f) return 0f;
            float c = 0.5f + 0.5f * Mathf.Cos(t / ancho * 2f * Mathf.PI);
            return c * c * (3f - 2f * c);
        }

        private float Frecuencia(float t)
        {
            float f = 140f + 3.6f * Mathf.Sin(t * 0.43f) + 2.4f * Mathf.Sin(t * 0.91f + 1.3f) + 1.6f * Mathf.Sin(t * 1.9f + 0.4f)
                      + (Mathf.PerlinNoise(t * 0.35f, 3.7f) - 0.5f) * 6f;
            float fase = Mathf.Repeat(t, Ciclo);
            int n = Mathf.FloorToInt(t / Ciclo);
            switch (_modo)
            {
                case 0: f += 19f * Pulso(fase - 45f, 34f) + 17f * Pulso(fase - 115f, 30f); break;
                case 1: f -= 27f * Pulso(fase - 50f, Duracion); break;
                case 2: f -= 29f * Pulso(fase - 84f, Duracion + 8f); break;
                case 3:
                    float c = 38f + 28f * Mathf.PerlinNoise(n * 1.7f, 0.5f), w = 14f + 12f * Mathf.PerlinNoise(n * 2.3f, 4.5f);
                    float d = Mathf.Abs(fase - c);
                    f -= (44f + 12f * Mathf.PerlinNoise(n * 3.1f, 8.5f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(w * 0.55f, w * 0.55f + 7f, d)));
                    break;
            }
            return Mathf.Clamp(f, 65f, 195f);
        }

        private void Elegir(int modo)
        {
            _modo = modo;
            for (int i = 0; i < 4; i++) _btn[i].SetSelected(i == modo);
            _text.text = Textos[modo];
            // Rellenar la pantalla con los últimos minutos del registro
            _x = 0;
            _lastF = _lastU = -1;
            _t = 0f;
            for (int i = 0; i < TexW; i++) Columna();
            _tex.Apply(false);
        }

        private void Columna()
        {
            float f = Frecuencia(_t), u = Utero(_t);
            int yf = Mathf.RoundToInt(Mathf.InverseLerp(60f, 200f, f) * (TexH - Split - 6)) + Split + 3;
            int yu = Mathf.RoundToInt(Mathf.InverseLerp(0f, 100f, u) * (Split - 10)) + 3;
            for (int y = 0; y < TexH; y++)
            {
                Color32 c = Fondo;
                if (y > Split)
                {
                    float lpm = Mathf.Lerp(60f, 200f, (y - Split - 3) / (float)(TexH - Split - 6));
                    if (lpm >= 110f && lpm <= 160f) c = Norma;
                }
                int gy = y > Split ? (y - Split - 3) % 17 : (y - 3) % 17;
                if (gy == 0) c = Grilla;
                if (_x % 15 == 0) c = Grilla;
                if (_x % 90 == 0) c = GrillaFuerte;
                if (y == Split) c = GrillaFuerte;
                _col[y] = c;
            }
            Trazo(yf, _lastF, Verde);
            Trazo(yu, _lastU, Rosa);
            _tex.SetPixels32(_x, 0, 1, TexH, _col);
            _lastF = yf;
            _lastU = yu;
            _x = (_x + 1) % TexW;
            if (_x == 0) _lastF = _lastU = -1;
            _t += Ventana / TexW;
        }

        private void Trazo(int y, int last, Color32 c)
        {
            if (last < 0) last = y;
            int a = Mathf.Max(0, Mathf.Min(y, last) - 1), b = Mathf.Min(TexH - 1, Mathf.Max(y, last) + 1);
            for (int i = a; i <= b; i++) _col[i] = c;
        }

        private void Update()
        {
            if (_tex == null) return;
            _acum += Time.deltaTime * Acelerado;
            float paso = Ventana / TexW;
            bool changed = false;
            while (_acum >= paso)
            {
                _acum -= paso;
                Columna();
                changed = true;
            }
            if (changed)
            {
                // Espacio vacío delante del trazo, como en un monitor real
                for (int y = 0; y < TexH; y++) _col[y] = Fondo;
                for (int k = 0; k < 6; k++) _tex.SetPixels32((_x + k) % TexW, 0, 1, TexH, _col);
                _tex.Apply(false);
            }
            float f = Frecuencia(_t);
            if (Time.time >= _nextText)
            {
                _nextText = Time.time + 0.5f;
                _fcf.text = Mathf.RoundToInt(f).ToString();
                _toco.text = Mathf.RoundToInt(Utero(_t)).ToString();
            }
            _beat += Time.deltaTime * f / 60f;
            if (_beat >= 1f)
            {
                _beat -= 1f;
                if (_on && sonido != null && latido != null) sonido.PlayOneShot(latido, 0.7f);
            }
            _heart.transform.localScale = Vector3.one * (1f + 0.22f * Mathf.Exp(-_beat * 5f));
        }

        private float _acum;

        private void Build()
        {
            _tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Registro" };
            Canvas c = HoloUI.CreateCanvas(pantalla, "Pantalla", new Vector2(540, 380), 0.00138f);
            c.GetComponent<HoloAppear>().scan = false;
            Transform r = c.transform;
            HoloPanel bg = HoloUI.Panel(r, "Fondo", Vector2.zero, new Vector2(540, 380), new Color(0.02f, 0.05f, 0.09f, 1f));
            bg.Outline.enabled = false;
            HoloUI.Text(r, "Titulo", "MONITOR FETAL  ·  SIMULADO", 14, new Vector2(-112, 172), new Vector2(290, 22), TextAlignmentOptions.Left, HoloColors.Accent, FontStyles.Bold);
            HoloUI.Text(r, "Tiempo", "tiempo acelerado x4", 11, new Vector2(185, 172), new Vector2(140, 20), TextAlignmentOptions.Right, HoloColors.TextDim);
            RawImage img = HoloUI.Rect(r, "Registro", new Vector2(-58, 74), new Vector2(396, 172)).gameObject.AddComponent<RawImage>();
            img.texture = _tex;
            img.raycastTarget = false;

            HoloUI.Text(r, "FcfTxt", "FCF (lpm)", 16, new Vector2(204, 146), new Vector2(110, 20), TextAlignmentOptions.Center, new Color(0.45f, 1f, 0.6f));
            _fcf = HoloUI.Text(r, "Fcf", "140", 46, new Vector2(204, 112), new Vector2(116, 54), TextAlignmentOptions.Center, new Color(0.45f, 1f, 0.6f), FontStyles.Bold);
            _heart = HoloUI.Icon(r, "Pulso", "pulse", new Vector2(204, 72), 24, new Color(1f, 0.4f, 0.45f));
            HoloUI.Text(r, "TocoTxt", "Actividad uterina", 14, new Vector2(204, 34), new Vector2(120, 20), TextAlignmentOptions.Center, new Color(1f, 0.65f, 0.78f));
            _toco = HoloUI.Text(r, "Toco", "12", 34, new Vector2(204, 4), new Vector2(116, 42), TextAlignmentOptions.Center, new Color(1f, 0.65f, 0.78f), FontStyles.Bold);
            _text = HoloUI.Text(r, "Texto", "", 19, new Vector2(0, -67), new Vector2(514, 96), TextAlignmentOptions.TopLeft, HoloColors.Text);
            _text.enableAutoSizing = true;
            _text.fontSizeMin = 13f;
            _text.fontSizeMax = 20f;
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                _btn[i] = HoloUI.Button(r, "Registro" + i, Nombres[i], new Vector2(-208 + i * 104, -150), new Vector2(100, 56), () => Elegir(k), null, 16);
            }
            _sound = HoloUI.Button(r, "Sonido", "Sonido", new Vector2(212, -150), new Vector2(100, 56), () =>
            {
                _on = !_on;
                _sound.SetSelected(_on);
            }, null, 16, HoloColors.Gold);
        }
    }
}
