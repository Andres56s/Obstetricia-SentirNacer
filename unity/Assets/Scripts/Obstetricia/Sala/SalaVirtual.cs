using System;
using System.Collections.Generic;
using Obstetricia.Core;
using Obstetricia.Modules;
using Obstetricia.UI;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace Obstetricia.Sala
{
    /// <summary>
    /// Sala virtual de obstetricia: entra y sale con un velo suave, permite moverse entre puntos marcados en el piso,
    /// ajustar la cama, usar el guatero y la musicoterapia, y combinarlo con la pelvis 3D.
    /// El mobiliario y los muros viven en la escena (hijos de "Contenido") para poder editarlos a mano.
    /// </summary>
    public class SalaVirtual : MonoBehaviour
    {
        [SerializeField] private GameObject contenido;
        [SerializeField] private VeloTransicion velo;
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private AROcclusionManager oclusion;
        [SerializeField] private EquipoRodante cama;
        [SerializeField] private Cortina cortina;
        [SerializeField] private Guatero guatero;
        [SerializeField] private Musicoterapia musica;
        [SerializeField] private AnatomyViewer pelvis;
        [SerializeField, Tooltip("Lugar del panel de la pelvis.")] private Transform lugarPelvis;
        [SerializeField, Tooltip("Lugar del panel de musicoterapia.")] private Transform lugarMusica;
        [SerializeField, Tooltip("Puntos del piso a los que se puede ir.")] private Transform[] puntos = Array.Empty<Transform>();
        [SerializeField] private Material materialZona;
        [SerializeField] private AmbienteAtardecer ambiente;

        private Transform _paneles;
        private Canvas _menu;
        private HoloButton _btnPelvis, _btnMec;
        private TextMeshProUGUI _info, _volumen;
        private readonly List<HoloButton> _tracks = new List<HoloButton>();
        private readonly List<Image> _bars = new List<Image>();
        private readonly List<GameObject> _spots = new List<GameObject>();
        private readonly List<(Transform zone, Renderer renderer, string text)> _zones = new List<(Transform, Renderer, string)>();
        private MaterialPropertyBlock _block;
        private Vector3 _localStand;
        private int _punto;
        private int _modoPelvis; // 0 oculta, 1 anatomía, 2 mecanismos
        private bool _wasHeld;
        private bool _occlusionWasOn;
        private float _level;

        public bool Abierta => contenido.activeSelf;
        public bool Ocupada => velo.Busy;

        /// <summary>Se invoca al pulsar "Salir de la sala".</summary>
        public event Action SalirPedido;

        private Transform Head => xrOrigin != null && xrOrigin.Camera != null ? xrOrigin.Camera.transform : Camera.main.transform;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            contenido.SetActive(false);
        }

        /// <summary>Pasa de la realidad mixta a la sala. "oculta" se ejecuta con la vista cubierta.</summary>
        public bool Entrar(Action oculta)
        {
            if (Abierta || velo.Busy) return false;
            HoloFeedback.Play(HoloFeedback.Sound.Open, 0.6f);
            return velo.Run(1.3f, 1.6f, () =>
            {
                oculta?.Invoke();
                _occlusionWasOn = oclusion != null && oclusion.enabled;
                if (oclusion != null) oclusion.enabled = false; // que el mundo real no recorte los muros virtuales
                if (ambiente != null) ambiente.Activar();
                _punto = 0;
                Ubicar();
                contenido.SetActive(true);
                if (ambiente != null) ambiente.CapturarReflejos();
                if (_paneles == null) BuildPanels();
                AcomodarMenu();
                // En escritorio el menú parte plegado: se despliega con la tecla E.
                if (MenuPlegable) _menu.gameObject.SetActive(false);
            },() => Info("Bienvenida/o a la sala de obstetricia. Apunta a los círculos del piso para moverte y toma objetos con el gatillo lateral o cerrando la mano. Apunta a un equipo para ver para qué se usa.", true));
        }

        /// <summary>Vuelve a la realidad mixta. "muestra" se ejecuta con la vista cubierta.</summary>
        public bool Salir(Action muestra)
        {
            if (!Abierta || velo.Busy) return false;
            Narrator.Stop();
            HoloFeedback.Play(HoloFeedback.Sound.Close, 0.6f);
            return velo.Run(1.1f, 1.4f, () =>
            {
                MostrarPelvis(0);
                musica.Detener();
                guatero.Restablecer(true);
                cama.Restablecer();
                ObjetoSala.RestablecerTodos();
                contenido.SetActive(false);
                if (ambiente != null) ambiente.Desactivar();
                if (oclusion != null) oclusion.enabled = _occlusionWasOn;
                muestra?.Invoke();
            });
        }

        /// <summary>Reubica la sala en torno al usuario con un parpadeo corto (recentrar o cambiar de punto).</summary>
        public void Recentrar()
        {
            if (!Abierta) return;
            velo.Run(0.3f, 0.45f, () =>
            {
                Ubicar();
                AcomodarMenu();
                if (_modoPelvis != 0) ActivarPelvis();
            });
        }

        private void IrA(int punto)
        {
            if (velo.Busy || punto == _punto) return;
            _punto = punto;
            Recentrar();
        }

        /// <summary>Mueve la sala (no al usuario) para que el punto elegido quede bajo sus pies, mirando hacia donde indica.</summary>
        private void Ubicar()
        {
            cama.Soltar();
            Transform head = Head;
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Transform p = puntos[_punto];
            Vector3 lp = transform.InverseTransformPoint(p.position);
            float ly = Vector3.SignedAngle(Vector3.forward, transform.InverseTransformDirection(p.forward), Vector3.up);
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0f, -ly, 0f);
            Vector3 floor = new Vector3(head.position.x, xrOrigin != null ? xrOrigin.transform.position.y : 0f, head.position.z);
            transform.SetPositionAndRotation(floor - rot * new Vector3(lp.x, 0f, lp.z), rot);
            Physics.SyncTransforms();
            if (ambiente != null) ambiente.Orientar();
            if (contenido.activeSelf && ambiente != null) ambiente.CapturarReflejos();
            for (int i = 0; i < _spots.Count; i++) _spots[i].SetActive(i != _punto);
        }

        /// <summary>El menú queda siempre a la izquierda del usuario y a su altura.</summary>
        private void AcomodarMenu()
        {
            Transform head = Head;
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            // En pantalla el campo de visión es menor que en el visor: el menú se despliega casi al frente.
            Vector3 dir = Quaternion.Euler(0f, MenuPlegable ? -8f : -48f, 0f) * fwd;
            Vector3 pos = head.position + dir * (MenuPlegable ? 0.95f : 0.85f);
            pos.y = Mathf.Clamp(head.position.y - (MenuPlegable ? 0.06f : 0.18f), 0.85f, 1.5f);
            _menu.transform.position = pos;
            HoloUI.FaceTowards(_menu.transform, head.position);
        }

        // ------------------------------------------------------------------ Menú desplegable (modo escritorio)

        /// <summary>Sin visor (teclado y mouse) el menú se pliega: se abre con E y se cierra al elegir una opción.</summary>
        private static bool MenuPlegable => Obstetricia.Desktop.PlatformMode.IsDesktop;

        public bool MenuVisible => _menu != null && _menu.gameObject.activeSelf;

        /// <summary>Tecla E: despliega el menú (ocultando la pelvis o los mecanismos) o lo vuelve a plegar.</summary>
        public void AlternarMenu()
        {
            if (!Abierta || velo.Busy || _menu == null) return;
            if (MenuVisible) OcultarMenu();
            else AbrirMenu();
        }

        private void AbrirMenu()
        {
            if (_modoPelvis != 0)
            {
                _modoPelvis = 0;
                ActivarPelvis();
            }
            AcomodarMenu();
            _menu.gameObject.SetActive(true); // HoloAppear anima la aparición
            HoloFeedback.Play(HoloFeedback.Sound.Open, 0.4f);
        }

        private void OcultarMenu()
        {
            if (!MenuVisible) return;
            HoloAppear.HideObject(_menu.gameObject);
        }

        /// <summary>Ejecuta la opción y, en escritorio, pliega el menú para dejar a la vista lo elegido.</summary>
        private UnityEngine.Events.UnityAction Opcion(Action accion) => () =>
        {
            accion();
            if (MenuPlegable) OcultarMenu();
        };

        // ------------------------------------------------------------------ Pelvis y calor

        private void MostrarPelvis(int modo)
        {
            _modoPelvis = modo == _modoPelvis ? 0 : modo;
            ActivarPelvis();
        }

        private void ActivarPelvis()
        {
            // El panel aparece frente al usuario, esté en el punto de la sala que esté.
            HoloUI.PlaceInFront(lugarPelvis, 1.0f);
            Vector3 p = lugarPelvis.position;
            p.y = Mathf.Max(p.y - 0.05f, transform.position.y + 1.2f);
            lugarPelvis.position = p;
            if (_modoPelvis != 0) pelvis.Deactivate();
            if (guatero.Aplicado && _modoPelvis != 1) guatero.Soltar();
            if (_modoPelvis == 0) pelvis.Deactivate();
            else if (_modoPelvis == 1) pelvis.ActivateFree();
            else pelvis.ActivateMechanisms();
            if (_btnPelvis != null)
            {
                _btnPelvis.SetSelected(_modoPelvis == 1);
                _btnMec.SetSelected(_modoPelvis == 2);
            }
            foreach (var z in _zones) z.zone.gameObject.SetActive(_modoPelvis == 1);
            if (_modoPelvis == 1)
                Info("Pelvis activada. Con el guatero tibio, acércalo a una de las zonas que brillan para aplicar calor local.", true);
        }

        private void BuildZones()
        {
            Transform model = pelvis.Model;
            AddZone(model, new Vector3(0f, 0.03f, 0.115f), "Zona lumbosacra: el calor local ayuda a relajar la musculatura y a aliviar el dolor lumbar durante el trabajo de parto.");
            AddZone(model, new Vector3(0f, -0.03f, -0.115f), "Zona suprapúbica: el calor suave ayuda a aliviar la molestia de las contracciones. Usa siempre un paño entre el guatero y la piel.");
        }

        private void AddZone(Transform model, Vector3 local, string text)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ZonaCalor";
            Destroy(go.GetComponent<Collider>());
            var zone = new GameObject("Zona").transform;
            zone.SetParent(model, false);
            zone.localPosition = local;
            zone.localRotation = Quaternion.identity;
            go.transform.SetParent(zone, false);
            go.transform.localScale = new Vector3(0.075f, 0.06f, 0.02f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = materialZona;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // El guatero se modeló de pie (origen abajo): este punto lo deja centrado en la zona.
            var apoyo = new GameObject("Apoyo").transform;
            apoyo.SetParent(zone, false);
            apoyo.localPosition = new Vector3(0f, -0.12f / Mathf.Max(0.01f, model.lossyScale.y), 0f);
            zone.gameObject.SetActive(false);
            _zones.Add((zone, r, text));
        }

        private void UpdateHeat()
        {
            bool held = guatero.Tomado;
            bool released = _wasHeld && !held;
            _wasHeld = held;
            if (_modoPelvis != 1) return;
            Vector3 center = guatero.transform.TransformPoint(new Vector3(0f, 0.12f, 0f));
            foreach (var z in _zones)
            {
                float d = Vector3.Distance(center, z.zone.position);
                bool near = d < 0.22f;
                if (released && near)
                {
                    if (guatero.Calor >= 1f)
                    {
                        guatero.Aplicar(z.zone.Find("Apoyo"));
                        HoloFeedback.Play(HoloFeedback.Sound.Success);
                        Info(z.text + " Aplica entre 15 y 20 minutos, controlando la temperatura.", true);
                    }
                    else Info("El guatero está frío: caliéntalo en el lavamanos o con el botón «Calentar guatero».", true);
                    released = false;
                }
                float pulse = 0.22f + 0.12f * Mathf.Sin(Time.time * 2.2f) + (near ? 0.25f : 0f);
                z.renderer.GetPropertyBlock(_block);
                _block.SetColor("_BaseColor", new Color(1f, 0.45f, 0.22f, pulse));
                z.renderer.SetPropertyBlock(_block);
            }
        }

        private void Update()
        {
            if (!Abierta || _paneles == null) return;
            UpdateHeat();
            _level = Mathf.Lerp(_level, musica.Nivel(), Time.deltaTime * 6f);
            for (int i = 0; i < _bars.Count; i++)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * (1.3f + i * 0.17f) + i * 1.1f);
                Vector2 size = _bars[i].rectTransform.sizeDelta;
                size.y = 6f + _level * (18f + 46f * wave);
                _bars[i].rectTransform.sizeDelta = size;
            }
        }

        // ------------------------------------------------------------------ Paneles

        private void Info(string text, bool speak)
        {
            if (_info == null) return;
            _info.text = text;
            if (speak) Narrator.Speak(text);
        }

        private void BuildPanels()
        {
            _paneles = new GameObject("Paneles").transform;
            _paneles.SetParent(contenido.transform, false);
            pelvis.SetPlacementAnchor(lugarPelvis);
            BuildZones();
            guatero.Listo += () => Info("El guatero está tibio y listo. Acércalo a una zona de calor de la pelvis.", true);
            BuildMenu();
            BuildMusic();
            BuildSpots();
            musica.Cambio += RefreshMusic;
            RefreshMusic();
        }

        private void BuildMenu()
        {
            _menu = HoloUI.CreateCanvas(_paneles, "MenuSala", new Vector2(440, 640));
            Transform r = _menu.transform;
            HoloUI.Panel(r, "Fondo", Vector2.zero, new Vector2(440, 640), new Color(0.03f, 0.09f, 0.2f, 0.94f));
            HoloUI.Text(r, "Titulo", "SALA DE OBSTETRICIA", 25, new Vector2(0, 284), new Vector2(400, 36), TextAlignmentOptions.Center, HoloColors.Text, FontStyles.Bold);
            _info = HoloUI.Text(r, "Info", "", 16, new Vector2(0, 205), new Vector2(400, 110), TextAlignmentOptions.Center, HoloColors.TextDim);
            _info.enableAutoSizing = true;
            _info.fontSizeMin = 11f;
            _info.fontSizeMax = 16f;

            Height(r, "Cama de parto", 74, cama);
            _btnPelvis = HoloUI.Button(r, "Pelvis", "Pelvis 3D", new Vector2(-102, -24), new Vector2(194, 58), Opcion(() => MostrarPelvis(1)), "cube", 18);
            _btnMec = HoloUI.Button(r, "Mecanismos", "Mecanismos", new Vector2(102, -24), new Vector2(194, 58), Opcion(() => MostrarPelvis(2)), "target", 18);
            HoloUI.Button(r, "Calentar", "Calentar guatero", new Vector2(-102, -94), new Vector2(194, 56), Opcion(() => guatero.Calentar(1f)), "pulse", 16);
            HoloUI.Button(r, "Cortina", "Cortina", new Vector2(102, -94), new Vector2(194, 56), Opcion(() => cortina.Alternar()), "labels", 17);
            HoloUI.Button(r, "Devolver", "Devolver guatero", new Vector2(-102, -160), new Vector2(194, 56), Opcion(() => guatero.Restablecer(false)), "undo", 16);
            HoloUI.Button(r, "Ordenar", "Ordenar sala", new Vector2(102, -160), new Vector2(194, 56), Opcion(() =>
            {
                cama.Restablecer();
                ObjetoSala.RestablecerTodos();
            }), "restart", 16);
            HoloUI.Button(r, "Salir", "Salir de la sala", new Vector2(0, -254), new Vector2(398, 68), () => SalirPedido?.Invoke(), "home", 21, HoloColors.Gold);
        }

        private static void Height(Transform r, string label, float y, EquipoRodante equipo)
        {
            HoloUI.Text(r, label, label, 18, new Vector2(-92, y), new Vector2(216, 30), TextAlignmentOptions.Left, HoloColors.Text, FontStyles.Bold);
            HoloUI.Button(r, label + "Baja", "Bajar", new Vector2(60, y), new Vector2(96, 52), () => equipo.Ajustar(-0.25f), null, 17);
            HoloUI.Button(r, label + "Sube", "Subir", new Vector2(162, y), new Vector2(96, 52), () => equipo.Ajustar(0.25f), null, 17);
        }

        private void BuildMusic()
        {
            Canvas c = HoloUI.CreateCanvas(_paneles, "Musicoterapia", new Vector2(400, 420));
            c.transform.SetPositionAndRotation(lugarMusica.position, lugarMusica.rotation);
            Transform r = c.transform;
            HoloUI.Panel(r, "Fondo", Vector2.zero, new Vector2(400, 420), new Color(0.03f, 0.09f, 0.2f, 0.94f));
            HoloUI.Text(r, "Titulo", "MUSICOTERAPIA", 24, new Vector2(0, 178), new Vector2(360, 34), TextAlignmentOptions.Center, HoloColors.Text, FontStyles.Bold);
            for (int i = 0; i < 15; i++)
            {
                RectTransform b = HoloUI.Rect(r, "Barra", new Vector2(-126 + i * 18, 122), new Vector2(9, 6));
                var img = b.gameObject.AddComponent<Image>();
                img.color = Color.Lerp(HoloColors.Accent, HoloColors.Correct, i / 14f);
                img.raycastTarget = false;
                _bars.Add(img);
            }
            for (int i = 0; i < musica.Pistas.Length; i++)
            {
                int k = i;
                _tracks.Add(HoloUI.Button(r, "Pista" + i, musica.Pistas[i].nombre, new Vector2(0, 52 - i * 58), new Vector2(350, 50), () => musica.Elegir(k), "play", 18));
            }
            HoloUI.Text(r, "VolTxt", "Volumen", 17, new Vector2(-110, -180), new Vector2(120, 28), TextAlignmentOptions.Left, HoloColors.TextDim);
            HoloUI.Button(r, "VolMenos", "-", new Vector2(10, -180), new Vector2(50, 46), () => musica.AjustarVolumen(-0.1f), null, 24);
            _volumen = HoloUI.Text(r, "Vol", "", 18, new Vector2(80, -180), new Vector2(76, 28), TextAlignmentOptions.Center, HoloColors.Accent, FontStyles.Bold);
            HoloUI.Button(r, "VolMas", "+", new Vector2(150, -180), new Vector2(50, 46), () => musica.AjustarVolumen(0.1f), null, 24);
        }

        private void RefreshMusic()
        {
            for (int i = 0; i < _tracks.Count; i++)
            {
                bool on = musica.Sonando && musica.Indice == i;
                _tracks[i].SetSelected(on);
                _tracks[i].SetIcon(on ? "pause" : "play");
            }
            if (_volumen != null) _volumen.text = Mathf.RoundToInt(musica.Volumen * 100f) + "%";
        }

        private void BuildSpots()
        {
            for (int i = 0; i < puntos.Length; i++)
            {
                int k = i;
                Canvas c = HoloUI.CreateCanvas(contenido.transform, "Ir_" + puntos[i].name, new Vector2(420, 420));
                c.transform.position = puntos[i].position + Vector3.up * 0.012f;
                c.transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);
                var appear = c.GetComponent<HoloAppear>();
                appear.scan = false;
                HoloButton b = HoloUI.Button(c.transform, "Punto", null, Vector2.zero, new Vector2(400, 400), () => IrA(k), null);
                b.Panel.Background.sprite = HoloSprites.Circle;
                b.Panel.Background.type = Image.Type.Simple;
                b.Panel.SetFill(new Color(0.12f, 0.45f, 0.8f, 0.55f));
                b.Panel.Outline.sprite = HoloSprites.Icon("ring");
                b.Panel.Outline.type = Image.Type.Simple;
                HoloUI.Text(b.transform, "Nombre", puntos[i].name, 44, Vector2.zero, new Vector2(330, 120), TextAlignmentOptions.Center, HoloColors.Text, FontStyles.Bold);
                _spots.Add(c.gameObject);
                c.gameObject.SetActive(i != _punto);
            }
        }
    }
}
