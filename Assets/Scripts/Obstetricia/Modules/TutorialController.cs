using System;
using Obstetricia.Core;
using Obstetricia.Desktop;
using Obstetricia.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Obstetricia.Modules
{
    /// <summary>
    /// Guía rápida en una ventana flotante pequeña, a un costado de la vista: una mano 3D muestra cada gesto y el paso
    /// avanza solo al lograrlo. No tapa ni bloquea el menú; se puede cerrar en cualquier momento.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        [SerializeField] private Material grabMaterial;
        [SerializeField, Tooltip("Modelo de la mano derecha (con huesos).")] private GameObject handModel;
        [SerializeField] private Material handMaterial;
        [SerializeField] private Material glowMaterial;

        private const float Ancho = 480f, Alto = 250f, Distancia = 0.78f, Costado = 35f, CostadoEscritorio = 24f;

        private Canvas _canvas;
        private TextMeshProUGUI _title, _body, _state;
        private Image _bar, _ring, _big;
        private Image[] _dots;
        private HoloButton _action, _second;
        private Transform _stage, _handT, _glow, _ray, _target;
        private ManoDemo _hand;
        private GameObject _cube;
        private ObstetriciaApp _app;
        private Action _done;
        private InputAction _trigger;
        private int _index, _pinches;
        private bool _running, _visible, _waitingGrab, _waitingRecenter, _waitingPinch, _usingHands, _wasPinching, _advancing;
        private float _stepTime, _awayTime, _stageScale;
        private Vector3 _pos, _vel;
        private bool _moving;

        private struct Step
        {
            public string Icon, Title, DesktopTitle, Hand, Control, Desktop, Button;
        }

        private static readonly Step[] Steps =
        {
            new Step { Icon = "pointer", Title = "Junta los dedos", DesktopTitle = "Mira y haz clic",
                Hand = "Une la yema del <b>índice</b> con la del <b>pulgar</b> hasta que se toquen. Luego sepáralas.",
                Control = "Aprieta y suelta el <b>gatillo</b> del control.",
                Desktop = "Mueve el <b>mouse</b> para mirar y haz <b>clic izquierdo</b>. <b>Esc</b> suelta el cursor." },
            new Step { Icon = "pointer", Title = "Apunta y elige",
                Hand = "Apunta al botón con la mano y <b>junta los dedos</b> para elegirlo.",
                Control = "Apunta al botón con el control y aprieta el <b>gatillo</b>.",
                Desktop = "Pon la <b>mira</b> (el punto blanco) sobre el botón y haz <b>clic izquierdo</b>.", Button = "Elígeme" },
            new Step { Icon = "cube", Title = "Toma y suelta",
                Hand = "Acerca la mano al <b>cubo</b> y cierra los dedos. Muévelo y <b>abre la mano</b> para soltarlo.",
                Control = "Acércate al <b>cubo</b>, mantén el <b>gatillo lateral</b>, muévelo y suéltalo.",
                Desktop = "<b>Mantén el clic</b> sobre el cubo. <b>Rueda</b>: acercar · <b>Tab</b>+mouse: rotar · <b>H</b>: devolver." },
            new Step { Icon = "notes", Title = "Toca de cerca", DesktopTitle = "Camina",
                Hand = "También puedes <b>tocar</b> los botones con la punta del índice.",
                Control = "También puedes <b>tocar</b> los botones con la punta del control.",
                Desktop = "Camina con <b>W A S D</b> (Shift para correr) y haz clic en este botón.", Button = "Tócame" },
            new Step { Icon = "recenter", Title = "Recentrar la vista",
                Hand = "Mira tu <b>palma derecha</b>, junta los dedos y mantenlos hasta completar el círculo.",
                Control = "Mantén el <b>botón Meta</b> (derecho) o el de <b>menú</b> (izquierdo) por un segundo.",
                Desktop = "Si la mesa queda lejos o detrás tuyo, <b>mantén la tecla R</b> por un segundo.", Button = "Después" },
            new Step { Icon = "check", Title = "¡Listo!",
                Hand = "Escribe tu nombre para guardar tus resultados, o empieza cuando quieras.",
                Control = "Escribe tu nombre para guardar tus resultados, o empieza cuando quieras.",
                Desktop = "Escribe tu nombre con el teclado o empieza cuando quieras. <b>C</b> muestra los controles.", Button = "Empezar" },
        };

        /// <summary>Muestra la guía desde el principio.</summary>
        public void Run(ObstetriciaApp app, Action done)
        {
            if (_running) Close();
            _app = app;
            _done = done;
            if (_canvas == null) Build();
            if (_trigger == null)
            {
                _trigger = new InputAction("GatilloTutorial", InputActionType.Button);
                _trigger.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
                _trigger.AddBinding("<XRController>{RightHand}/{TriggerButton}");
                if (PlatformMode.IsDesktop) _trigger.AddBinding("<Mouse>/leftButton");
            }
            _trigger.Enable();
            if (_app != null) _app.Recentered += OnRecentered;
            _running = true;
            _visible = true;
            _index = 0;
            Place(true);
            Show();
        }

        // ------------------------------------------------------------------ Construcción

        private void Build()
        {
            _canvas = HoloUI.CreateCanvas(transform, "Tutorial", new Vector2(Ancho, Alto));
            HoloUI.BringToFront(_canvas);
            HoloUI.Panel(_canvas.transform, "Fondo", Vector2.zero, new Vector2(Ancho, Alto), new Color(0.03f, 0.08f, 0.18f, 0.93f));
            Transform r = _canvas.transform;
            HoloUI.Text(r, "Guia", "GUÍA RÁPIDA", 12, new Vector2(-150, 106), new Vector2(150, 20), TextAlignmentOptions.Left, HoloColors.Accent, FontStyles.Bold);
            HoloUI.Button(r, "Saltar", null, new Vector2(210, 96), new Vector2(36, 36), Finish, "cross", 14, HoloColors.TextDim).Panel.SetBorder(new Color(0.72f, 0.82f, 0.92f, 0.3f));

            HoloPanel view = HoloUI.Panel(r, "Vista", new Vector2(-133, -8), new Vector2(186, 196), new Color(0.06f, 0.15f, 0.3f, 0.75f));
            view.Outline.color = new Color(0.25f, 0.72f, 1f, 0.35f);
            _ring = HoloUI.Icon(view.transform, "Aro", "circle", Vector2.zero, 150, new Color(0.35f, 0.9f, 1f, 0.3f));
            _ring.sprite = HoloSprites.Circle;
            _ring.type = Image.Type.Filled;
            _ring.fillMethod = Image.FillMethod.Radial360;
            _ring.fillOrigin = (int)Image.Origin360.Top;
            _big = HoloUI.Icon(view.transform, "Icono", "check", Vector2.zero, 96, HoloColors.Text);

            _title = HoloUI.Text(r, "Titulo", "", 24, new Vector2(88, 70), new Vector2(230, 36), TextAlignmentOptions.Left, HoloColors.Text, FontStyles.Bold);
            _body = HoloUI.Text(r, "Texto", "", 17, new Vector2(94, 6), new Vector2(242, 84), TextAlignmentOptions.TopLeft, HoloColors.Text);
            _state = HoloUI.Text(r, "Estado", "", 13, new Vector2(94, -46), new Vector2(242, 18), TextAlignmentOptions.Left, HoloColors.TextDim);
            _bar = HoloUI.ProgressBar(r, "Cierre", new Vector2(94, -62), new Vector2(242, 8));
            _action = HoloUI.Button(r, "Accion", "Elígeme", new Vector2(46, -58), new Vector2(146, 48), OnAction, null, 18, HoloColors.Correct);
            _second = HoloUI.Button(r, "Nombre", "Mi nombre", new Vector2(172, -58), new Vector2(94, 48), OnName, null, 15, HoloColors.Accent);

            _dots = new Image[Steps.Length];
            for (int i = 0; i < _dots.Length; i++)
            {
                _dots[i] = HoloUI.Icon(r, "Punto" + i, "circle", new Vector2(-18 + i * 17, -104), 9, HoloColors.TextDim);
                _dots[i].sprite = HoloSprites.Circle;
            }
            HoloUI.Button(r, "SaltarPaso", "Saltar paso", new Vector2(175, -103), new Vector2(92, 28), Next, null, 12, HoloColors.TextDim).Panel.SetBorder(new Color(0.72f, 0.82f, 0.92f, 0.3f));

            _stage = new GameObject("Escena3D").transform;
            _stage.SetParent(transform, false);
            _stage.localPosition = new Vector3(-0.122f, -0.008f, -0.04f);
            if (handModel != null)
            {
                _handT = new GameObject("Mano").transform;
                _handT.SetParent(_stage, false);
                GameObject m = Instantiate(handModel, _handT);
                foreach (Animator a in m.GetComponentsInChildren<Animator>()) a.enabled = false;
                foreach (Renderer re in m.GetComponentsInChildren<Renderer>())
                {
                    re.sharedMaterial = handMaterial;
                    re.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (re is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
                }
                _hand = _handT.gameObject.AddComponent<ManoDemo>();
                _hand.Init();
            }
            _glow = Piece(PrimitiveType.Sphere, "Contacto", new Color(0.35f, 1f, 0.55f));
            _ray = Piece(PrimitiveType.Cube, "Rayo", new Color(0.45f, 0.85f, 1f));
            _target = Piece(PrimitiveType.Cube, "Objetivo", new Color(0.2f, 0.55f, 0.95f), handMaterial);
            _stage.gameObject.SetActive(false);
            _canvas.gameObject.SetActive(false);
        }

        private Transform Piece(PrimitiveType type, string name, Color color, Material material = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_stage, false);
            var re = go.GetComponent<Renderer>();
            re.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (material == null) material = glowMaterial;
            if (material != null) re.sharedMaterial = material;
            re.material.color = color;
            return go.transform;
        }

        // ------------------------------------------------------------------ Pasos

        private void Show()
        {
            Step s = Steps[_index];
            _advancing = false;
            _stepTime = 0f;
            _pinches = 0;
            _waitingPinch = _index == 0;
            _waitingGrab = _index == 2;
            _waitingRecenter = _index == 4;
            bool last = _index == Steps.Length - 1;
            _usingHands = HandsInUse();
            _title.text = PlatformMode.IsDesktop && !string.IsNullOrEmpty(s.DesktopTitle) ? s.DesktopTitle : s.Title;
            _body.text = Texto(s, _usingHands);
            _action.gameObject.SetActive(!string.IsNullOrEmpty(s.Button));
            if (!string.IsNullOrEmpty(s.Button)) _action.SetText(s.Button);
            _second.gameObject.SetActive(last);
            _canvas.transform.Find("SaltarPaso").gameObject.SetActive(!last);
            SetBar(_waitingPinch);
            _state.text = "";
            for (int i = 0; i < _dots.Length; i++)
                _dots[i].color = i < _index ? HoloColors.Correct : i == _index ? HoloColors.Accent : new Color(1f, 1f, 1f, 0.2f);
            _big.sprite = HoloSprites.Icon(s.Icon);
            _ring.fillAmount = 0f;
            if (!_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);
            Narrator.Speak(_title.text + ". " + _body.text);
            if (_waitingGrab) SpawnCube();
            else DestroyCube();
        }

        private void SetBar(bool on)
        {
            _bar.transform.parent.gameObject.SetActive(on);
            _state.gameObject.SetActive(on);
        }

        private static string Texto(Step s, bool hands) =>
            PlatformMode.IsDesktop && !string.IsNullOrEmpty(s.Desktop) ? s.Desktop : hands ? s.Hand : s.Control;

        // En el navegador no hay manos ni controles: se muestran los textos de teclado y mouse, sin la mano 3D.
        private static bool HandsInUse() => !PlatformMode.IsDesktop && (LectorMano.De(false) != null || LectorMano.De(true) != null || !ControlsTracked());

        private static bool ControlsTracked()
        {
            foreach (InputDevice d in InputSystem.devices)
                if (d is UnityEngine.InputSystem.XR.XRController c && !(d is UnityEngine.XR.Hands.XRHandDevice) && c.isTracked != null && c.isTracked.isPressed) return true;
            return false;
        }

        private void OnAction()
        {
            if (_index == Steps.Length - 1) Finish();
            else Next();
        }

        private void OnName()
        {
            HoloKeyboard.Open("Escribe tu nombre", AppSettings.StudentName, name =>
            {
                if (!string.IsNullOrWhiteSpace(name)) AppSettings.StudentName = name;
                if (_running) Finish();
            });
        }

        private void Next()
        {
            if (!_running || _advancing) return;
            _advancing = true;
            HoloFeedback.Play(HoloFeedback.Sound.Success, 0.5f);
            _dots[_index].color = HoloColors.Correct;
            CancelInvoke();
            Invoke(nameof(Advance), 0.35f);
        }

        private void Advance()
        {
            _index++;
            if (_index >= Steps.Length) Finish();
            else Show();
        }

        private void OnRecentered()
        {
            if (!_running) return;
            Place(true);
            if (!_waitingRecenter) return;
            _waitingRecenter = false;
            Invoke(nameof(Next), 0.6f);
        }

        private void Finish()
        {
            Close();
            Action done = _done;
            _done = null;
            done?.Invoke();
        }

        private void Close()
        {
            _running = false;
            if (_app != null) _app.Recentered -= OnRecentered;
            _trigger?.Disable();
            CancelInvoke();
            DestroyCube();
            Narrator.Stop();
            if (_canvas != null) HoloAppear.HideObject(_canvas.gameObject);
            if (_stage != null) _stage.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_app != null) _app.Recentered -= OnRecentered;
            _trigger?.Dispose();
        }

        // ------------------------------------------------------------------ Cada cuadro

        private void Update()
        {
            if (!_running) return;
            bool visible = _app == null || _app.Current == ObstetriciaApp.Screen.Hub;
            if (visible != _visible)
            {
                // Dentro de un programa o de la sala la guía se guarda; vuelve al regresar a la mesa.
                _visible = visible;
                if (!visible)
                {
                    HoloAppear.HideObject(_canvas.gameObject);
                    _stage.gameObject.SetActive(false);
                    DestroyCube();
                }
                else
                {
                    Place(true);
                    Show();
                }
            }
            if (!_visible) return;
            _stepTime += Time.unscaledDeltaTime;
            Follow();
            ReadHands();
            Animate();
        }

        private void ReadHands()
        {
            bool hands = HandsInUse();
            if (hands != _usingHands)
            {
                _usingHands = hands;
                _body.text = Texto(Steps[_index], hands);
            }
            LectorMano a = LectorMano.De(false), b = LectorMano.De(true);
            float cierre = Mathf.Max(a != null ? a.Cierre : 0f, b != null ? b.Cierre : 0f);
            bool pinch = (a != null && a.Pellizco) || (b != null && b.Pellizco) || (_trigger != null && _trigger.IsPressed());
            if (_waitingPinch)
            {
                _bar.fillAmount = pinch ? 1f : cierre;
                _bar.color = pinch ? HoloColors.Correct : HoloColors.Accent;
                _state.text = pinch ? "¡Eso! Ahora sepáralos" : $"Hazlo 2 veces  ·  {_pinches}/2";
                if (_wasPinching && !pinch && ++_pinches >= 2)
                {
                    _waitingPinch = false;
                    _state.text = "¡Muy bien!";
                    Next();
                }
                else if (pinch && !_wasPinching) HoloFeedback.HapticAll(0.2f, 0.05f);
            }
            _wasPinching = pinch;
        }

        /// <summary>La ventana queda quieta; solo se acerca con suavidad si la persona deja de tenerla cerca de su vista.</summary>
        private void Follow()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 to = Vector3.ProjectOnPlane(transform.position - cam.transform.position, Vector3.up);
            Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            float angle = Vector3.Angle(to, fwd);
            bool away = angle > 62f || to.magnitude > 1.3f || to.magnitude < 0.4f;
            _awayTime = away ? _awayTime + Time.unscaledDeltaTime : 0f;
            if (_awayTime > 1.2f) { Place(false); _moving = true; _awayTime = 0f; }
            if (!_moving) return;
            transform.position = Vector3.SmoothDamp(transform.position, _pos, ref _vel, 0.45f, 2f, Time.unscaledDeltaTime);
            Face(cam.transform.position);
            if ((transform.position - _pos).sqrMagnitude < 0.0001f) _moving = false;
        }

        private void Place(bool now)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            // A la izquierda para dejar libre la mano derecha (al revés en modo zurdo).
            // En pantalla el campo de visión es menor que en el visor: la ventana se acerca al centro para que no se corte.
            float side = PlatformMode.IsDesktop ? CostadoEscritorio : Costado;
            Vector3 dir = Quaternion.Euler(0f, AppSettings.LeftHanded ? side : -side, 0f) * fwd;
            _pos = cam.transform.position + dir * Distancia + Vector3.down * 0.1f;
            if (!now) return;
            transform.position = _pos;
            _moving = false;
            _vel = Vector3.zero;
            Face(cam.transform.position);
        }

        private void Face(Vector3 eye)
        {
            Vector3 d = transform.position - eye;
            d.y *= 0.5f;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }

        // ------------------------------------------------------------------ Demostración animada

        private static float Ramp(float t, float a, float b) => Ease.InOutSine(Mathf.InverseLerp(a, b, t));

        private void Animate()
        {
            bool demo = _hand != null && _usingHands && _index < Steps.Length - 1;
            _big.enabled = !demo;
            _ring.enabled = demo && _index == 4;
            if (_stage.gameObject.activeSelf != demo)
            {
                _stage.gameObject.SetActive(demo);
                _stageScale = 0f;
            }
            if (!demo) return;
            _stageScale = Mathf.MoveTowards(_stageScale, 1f, Time.unscaledDeltaTime / 0.35f);
            float s = 0.66f * Ease.OutCubic(_stageScale);
            _stage.localScale = new Vector3(AppSettings.LeftHanded ? -s : s, s, s);

            float t = _stepTime % 3.2f;
            float close = 0f;
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.Euler(-24f, -64f, 0f);
            bool ray = false, target = false;
            float press = 0f;
            Vector3 targetPos = Vector3.zero, targetScale = Vector3.one * 0.04f;
            float[] from = ManoDemo.Abierta, to = ManoDemo.Pellizco;
            switch (_index)
            {
                case 0:
                    close = Ramp(t, 0.5f, 1.2f) - Ramp(t, 2.1f, 2.7f);
                    pos = new Vector3(0.012f, 0.012f, 0f);
                    break;
                case 1:
                    close = Ramp(t, 1.0f, 1.5f) - Ramp(t, 2.2f, 2.7f);
                    pos = new Vector3(0.034f, -0.028f, 0f);
                    rot = Quaternion.Euler(-34f, -64f, 0f);
                    ray = target = true;
                    targetPos = new Vector3(-0.078f, 0.062f, 0f);
                    targetScale = new Vector3(0.045f, 0.028f, 0.008f);
                    press = close > 0.95f ? 1f : 0f;
                    from = ManoDemo.Reposo;
                    break;
                case 2:
                    // Acercarse, cerrar, levantar, abrir
                    float reach = Ramp(t, 0.1f, 0.7f) - Ramp(t, 2.8f, 3.2f);
                    close = Ramp(t, 0.7f, 1.1f) - Ramp(t, 2.2f, 2.6f);
                    float lift = Ramp(t, 1.15f, 1.7f);
                    float drop = Ramp(t, 2.8f, 3.1f);
                    pos = new Vector3(Mathf.Lerp(0.07f, 0.034f, reach), Mathf.Lerp(-0.005f, 0.05f, lift * (1f - drop)), 0f);
                    target = true;
                    targetPos = new Vector3(-0.038f, -0.04f + 0.055f * lift * (1f - drop), 0f);
                    targetScale = Vector3.one * 0.036f;
                    to = Agarrar;
                    break;
                case 3:
                    press = Ramp(t, 0.7f, 1.2f) - Ramp(t, 1.7f, 2.2f);
                    pos = new Vector3(Mathf.Lerp(0.052f, 0.026f, press), 0.0f, 0f);
                    from = to = ManoDemo.Apuntar;
                    rot = Quaternion.Euler(-4f, -72f, 0f);
                    target = true;
                    targetPos = new Vector3(-0.086f + (press > 0.9f ? -0.004f : 0f), -0.004f, 0f);
                    targetScale = new Vector3(0.01f, 0.055f, 0.055f);
                    press = press > 0.9f ? 1f : 0f;
                    break;
                case 4:
                    close = Ramp(t, 0.3f, 0.8f) - Ramp(t, 2.6f, 3.0f);
                    pos = new Vector3(0.02f, -0.016f, 0f);
                    rot = Quaternion.LookRotation(new Vector3(0f, 1f, 0.12f), new Vector3(0f, 0f, 1f));
                    _ring.fillAmount = Mathf.InverseLerp(0.8f, 2.4f, t) * (t < 2.8f ? 1f : 0f);
                    break;
            }
            _handT.localPosition = pos;
            _handT.localRotation = rot;
            _hand.Mostrar(from, to, close);

            Vector3 mid = _stage.InverseTransformPoint((_hand.IndiceYema.position + _hand.PulgarYema.position) * 0.5f);
            bool glow = close > 0.9f && _index != 2 && _index != 3;
            _glow.gameObject.SetActive(glow);
            if (glow)
            {
                _glow.localPosition = mid + new Vector3(0f, 0f, -0.009f);
                _glow.localScale = Vector3.one * (0.014f + 0.003f * Mathf.Sin(Time.unscaledTime * 9f));
            }
            _target.gameObject.SetActive(target);
            if (target)
            {
                _target.localPosition = targetPos;
                _target.localScale = targetScale;
                _target.localRotation = _index == 2 ? Quaternion.Euler(0f, 25f, 0f) : Quaternion.identity;
                _target.GetComponent<Renderer>().material.color = press > 0.5f || (_index == 2 && close > 0.9f) ? new Color(0.3f, 0.95f, 0.5f) : new Color(0.2f, 0.55f, 0.95f);
            }
            _ray.gameObject.SetActive(ray);
            if (ray)
            {
                Vector3 d = targetPos - mid;
                _ray.localPosition = mid + d * 0.5f;
                _ray.localRotation = Quaternion.LookRotation(d.normalized);
                _ray.localScale = new Vector3(0.0022f, 0.0022f, d.magnitude);
            }
        }

        // Dedos alrededor de un objeto pequeño
        private static readonly float[] Agarrar = { 26, 24, 14, 28, 24, 14, 30, 24, 14, 30, 24, 14, 10, -8, -6, 4, 0 };

        // ------------------------------------------------------------------ Cubo de práctica

        private void SpawnCube()
        {
            DestroyCube();
            _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube.name = "CuboTutorial";
            _cube.transform.localScale = Vector3.one * 0.085f;
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, fwd) * (AppSettings.LeftHanded ? -1f : 1f);
                _cube.transform.position = cam.transform.position + fwd * 0.48f + side * 0.2f + Vector3.down * 0.28f;
            }
            if (grabMaterial != null) _cube.GetComponent<Renderer>().sharedMaterial = grabMaterial;
            var rb = _cube.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            var grab = _cube.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;
            grab.selectEntered.AddListener(_ => HoloFeedback.HapticAll(0.3f, 0.1f));
            grab.selectExited.AddListener(OnReleased);
            _cube.AddComponent<TutorialSpin>();
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (!_waitingGrab || args.isCanceled) return;
            _waitingGrab = false;
            Invoke(nameof(Next), 0.5f);
        }

        private void DestroyCube()
        {
            if (_cube != null) Destroy(_cube);
            _cube = null;
        }
    }
}
