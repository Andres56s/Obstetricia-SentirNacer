using System;
using Obstetricia.Media;
using Obstetricia.Modules;
using System.Collections.Generic;
using Obstetricia.Core;
using Obstetricia.Desktop;
using Obstetricia.UI;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;

namespace Obstetricia
{
    /// <summary>
    /// Controlador principal de la experiencia de Obstetricia en realidad mixta.
    /// Coloca la mesa UCEN frente al usuario (en el suelo, anclada), y navega entre el menú principal
    /// y los programas Fisio-balón y Legal (Ley Integral de la Mujer).
    /// </summary>
    public class ObstetriciaApp : MonoBehaviour
    {
        public enum Screen { Hub, Fisio, Legal, Sala }

        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private HubController hub;
        [SerializeField] private FisioModuleController fisio;
        [SerializeField] private QuizController legal;
        [SerializeField] private Video360Player video;
        [SerializeField] private Obstetricia.Sala.SalaVirtual sala;

        [Header("Colocación")]
        [SerializeField, Tooltip("Distancia (m) entre el usuario y el centro de la mesa UCN.")]
        private float distance = 1.1f;
        [SerializeField, Tooltip("Segundos para esperar el tracking de la cabeza antes de colocar.")]
        private float placementDelay = 0.75f;

        [Header("Recentrar")]
        [SerializeField, Tooltip("Botón que, mantenido, recoloca la experiencia frente al usuario (además del botón Meta del sistema).")]
        private InputActionProperty recenterAction = new InputActionProperty(
            new InputAction("Recentrar", InputActionType.Button, "<XRController>{LeftHand}/menuButton"));
        [SerializeField] private float recenterHoldSeconds = 1f;

        /// <summary>Metros extra de distancia a la mesa en modo escritorio.</summary>
        private const float DesktopExtraDistance = 0.45f;

        private ARAnchor _anchor;
        private bool _busy;
        private bool _moving;
        private float _holdTime;
        private float _pendingRecenterAt = -1f;
        private float _nextSubsystemScan;
        private readonly List<XRInputSubsystem> _inputSubsystems = new List<XRInputSubsystem>();
        private GameObject _holdHud;
        private Image _holdRing;

        public Screen Current { get; private set; } = Screen.Hub;

        /// <summary>Se invoca cada vez que la experiencia se recoloca frente al usuario.</summary>
        public event Action Recentered;

        /// <summary>Si existe, el tutorial inicial se muestra la primera vez antes del menú principal.</summary>
        [SerializeField] private TutorialController tutorial;

        private bool _lastSeated;
        private float _lastHeight, _lastDistance;

        private Transform Head => xrOrigin != null && xrOrigin.Camera != null ? xrOrigin.Camera.transform
            : Camera.main != null ? Camera.main.transform : null;

        private void Awake()
        {
            if (PlatformMode.IsDesktop)
            {
                // Modo escritorio: mantener R recentra la mesa (igual que el botón de menú del control).
                InputAction recenter = recenterAction.action;
                if (recenter != null)
                {
                    bool wasEnabled = recenter.enabled;
                    if (wasEnabled) recenter.Disable();
                    recenter.AddBinding("<Keyboard>/r");
                    if (wasEnabled) recenter.Enable();
                }
                // Dentro de la página web, el nombre del alumno llega en la URL.
                string webName = WebBridge.StudentName;
                if (!string.IsNullOrWhiteSpace(webName)) AppSettings.StudentName = webName.Trim();
            }

            if (hub != null)
            {
                hub.FisioSelected += ShowFisio;
                hub.LegalSelected += ShowLegal;
            }
            if (fisio != null)
            {
                fisio.HomeRequested += ShowHub;
                fisio.RecenterRequested += Recenter;
            }
            if (legal != null) legal.Finished += (correct, total, score) =>
            {
                StudentRecords.Save("Ley Integral de la Mujer", correct, total, score);
                ShowHub();
            };
            if (hub != null) hub.SalaSelected += ShowSala;
            if (sala != null) sala.SalirPedido += LeaveSala;
            if (hub != null) hub.SettingsRequested += SettingsPanel.Open;
            if (fisio != null) fisio.SettingsRequested += SettingsPanel.Open;
            SettingsPanel.TutorialRequested += StartTutorial;
            _lastSeated = AppSettings.Seated;
            _lastHeight = AppSettings.HeightOffset;
            _lastDistance = AppSettings.Distance;
            AppSettings.Changed += OnSettingsChanged;
        }

        private void OnDestroy()
        {
            AppSettings.Changed -= OnSettingsChanged;
            SettingsPanel.TutorialRequested -= StartTutorial;
        }

        /// <summary>Al cambiar modo sentada, altura o distancia, la experiencia se reacomoda frente al usuario.</summary>
        private void OnSettingsChanged()
        {
            if (_lastSeated == AppSettings.Seated && Mathf.Approximately(_lastHeight, AppSettings.HeightOffset)
                && Mathf.Approximately(_lastDistance, AppSettings.Distance)) return;
            _lastSeated = AppSettings.Seated;
            _lastHeight = AppSettings.HeightOffset;
            _lastDistance = AppSettings.Distance;
            Recenter();
        }

        /// <summary>Muestra la guía rápida en su ventana flotante; el menú sigue disponible.</summary>
        public void StartTutorial()
        {
            if (tutorial == null || Current == Screen.Sala) return;
            ShowHub();
            tutorial.Run(this, () => AppSettings.TutorialDone = true);
        }

        private void OnEnable()
        {
            if (recenterAction.action != null) recenterAction.action.Enable();
        }

        private void OnDisable()
        {
            foreach (XRInputSubsystem s in _inputSubsystems)
                if (s != null) s.trackingOriginUpdated -= OnTrackingOriginUpdated;
            _inputSubsystems.Clear();
        }

        private void Update()
        {
            WatchSystemRecenter();
            HandleRecenterHold();
            if (_pendingRecenterAt > 0f && Time.unscaledTime >= _pendingRecenterAt)
            {
                _pendingRecenterAt = -1f;
                Recenter();
            }
        }

        /// <summary>
        /// Mantener el botón Meta (Oculus) recentra la vista a nivel del sistema; Unity lo informa con
        /// trackingOriginUpdated. Al recibirlo recolocamos la experiencia frente al usuario.
        /// </summary>
        private void WatchSystemRecenter()
        {
            if (Time.unscaledTime < _nextSubsystemScan) return;
            _nextSubsystemScan = Time.unscaledTime + 1f;
            var found = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(found);
            foreach (XRInputSubsystem s in found)
            {
                if (_inputSubsystems.Contains(s)) continue;
                s.trackingOriginUpdated += OnTrackingOriginUpdated;
                _inputSubsystems.Add(s);
            }
        }

        private void OnTrackingOriginUpdated(XRInputSubsystem subsystem)
        {
            // Esperar un instante a que la cabeza se reubique.
            _pendingRecenterAt = Time.unscaledTime + 0.25f;
        }

        /// <summary>Mantener el botón de menú del control izquierdo muestra un anillo de progreso y recentra al completarse.</summary>
        private void HandleRecenterHold()
        {
            bool pressed = recenterAction.action != null && recenterAction.action.IsPressed() && !HoloKeyboard.IsOpen;
            if (pressed && _holdTime >= 0f)
            {
                _holdTime += Time.unscaledDeltaTime;
                ShowHoldHud(Mathf.Clamp01(_holdTime / recenterHoldSeconds));
                if (_holdTime >= recenterHoldSeconds)
                {
                    _holdTime = -1f; // no repetir hasta soltar
                    if (_holdHud != null) _holdHud.SetActive(false);
                    Recenter();
                }
            }
            else if (!pressed)
            {
                _holdTime = 0f;
                if (_holdHud != null && _holdHud.activeSelf) _holdHud.SetActive(false);
            }
        }

        private void ShowHoldHud(float progress)
        {
            Transform head = Head;
            if (head == null) return;
            if (_holdHud == null)
            {
                Canvas c = HoloUI.CreateCanvas(head, "RecentrarHUD", new Vector2(160, 190));
                c.transform.localPosition = new Vector3(0f, -0.05f, 0.6f);
                c.GetComponent<HoloAppear>().scan = false;
                HoloUI.BringToFront(c, 70);
                HoloUI.Icon(c.transform, "Fondo", "circle", new Vector2(0, 20), 130, new Color(0.02f, 0.08f, 0.18f, 0.85f)).sprite = HoloSprites.Circle;
                _holdRing = HoloUI.Icon(c.transform, "Anillo", "ring", new Vector2(0, 20), 130, HoloColors.Accent);
                _holdRing.type = Image.Type.Filled;
                _holdRing.fillMethod = Image.FillMethod.Radial360;
                _holdRing.fillOrigin = (int)Image.Origin360.Top;
                _holdRing.fillClockwise = true;
                HoloUI.Icon(c.transform, "Icono", "recenter", new Vector2(0, 20), 60, HoloColors.Text);
                HoloUI.Text(c.transform, "Texto", "Recentrando...", 18, new Vector2(0, -75), new Vector2(160, 28),
                    TextAlignmentOptions.Center, HoloColors.Text, FontStyles.Bold);
                _holdHud = c.gameObject;
            }
            if (!_holdHud.activeSelf) _holdHud.SetActive(true);
            _holdRing.fillAmount = progress;
        }

        private async void Start()
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(placementDelay);
                bool restored = await TryRestoreSavedAnchor();
                if (!restored) Recenter();
                WebBridge.SendReady();
                // Abre lo que pidió la web (o la mesa); la guía rápida flota aparte y solo se ve sobre la mesa.
                ShowStart();
                if (!AppSettings.TutorialDone && tutorial != null)
                    tutorial.Run(this, () => AppSettings.TutorialDone = true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Obstetricia] Error al iniciar: {ex}");
            }
        }

        private void LateUpdate()
        {
            if (_anchor != null && _anchor.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
                transform.SetPositionAndRotation(_anchor.transform.position, _anchor.transform.rotation);
        }

        private bool _requestedModuleOpened;

        /// <summary>
        /// Primera pantalla después del tutorial: si la página web pidió un programa (?modulo=fisio|legal)
        /// se abre directamente; si no, la mesa UCEN.
        /// </summary>
        public void ShowStart()
        {
            if (!_requestedModuleOpened)
            {
                _requestedModuleOpened = true;
                switch (WebBridge.RequestedModule)
                {
                    case "fisio":
                    case "fisiobalon":
                        ShowFisio();
                        return;
                    case "legal":
                        ShowLegal();
                        return;
                    case "sala":
                        if (sala == null) break;
                        ShowSala();
                        return;
                }
            }
            ShowHub();
        }

        public void ShowHub()
        {
            Current = Screen.Hub;
            if (fisio != null) fisio.Close();
            if (legal != null) legal.Close();
            if (video != null && video.IsActive) video.Stop();
            if (hub != null) hub.Show();
            Debug.Log("[Obstetricia] Menú principal (mesa UCEN)");
        }

        public void ShowFisio() => OpenModule(Screen.Fisio);

        public void ShowLegal() => OpenModule(Screen.Legal);

        /// <summary>La mesa se hunde con animación y luego aparece el programa elegido.</summary>
        private void OpenModule(Screen screen)
        {
            if (_busy) return;
            _busy = true;
            Current = screen;
            if (fisio != null && screen != Screen.Fisio) fisio.Close();
            if (legal != null && screen != Screen.Legal) legal.Close();
            void Open()
            {
                _busy = false;
                HoloFeedback.Play(HoloFeedback.Sound.Open, 0.8f);
                if (screen == Screen.Fisio && fisio != null) fisio.Open();
                if (screen == Screen.Legal && legal != null) legal.Open();
                Debug.Log(screen == Screen.Fisio ? "[Obstetricia] Programa Fisio-balón" : "[Obstetricia] Programa Legal: Ley Integral");
            }
            if (hub != null) hub.Hide(Open);
            else Open();
        }

        /// <summary>Entra a la sala virtual con una transición suave (la mesa se oculta con la vista cubierta).</summary>
        public void ShowSala()
        {
            if (_busy || sala == null) return;
            if (!sala.Entrar(() => { if (hub != null) hub.HideNow(); })) return;
            Current = Screen.Sala;
            SettingsPanel.Close();
            Debug.Log("[Obstetricia] Sala virtual");
        }

        private void LeaveSala()
        {
            if (sala.Salir(ShowHub)) Debug.Log("[Obstetricia] Saliendo de la sala virtual");
        }

        private bool AnchorsReady => anchorManager != null && anchorManager.enabled && anchorManager.subsystem != null
                                     && anchorManager.subsystem.running;

        /// <summary>Recupera la ubicación de la mesa guardada en una sesión anterior (ancla espacial persistente).</summary>
        private async Awaitable<bool> TryRestoreSavedAnchor()
        {
            string saved = AppSettings.SavedAnchor;
            if (string.IsNullOrEmpty(saved) || !AnchorsReady || !anchorManager.descriptor.supportsSaveAnchor) return false;
            try
            {
                if (!Guid.TryParse(saved, out Guid guid)) return false;
                var result = await anchorManager.TryLoadAnchorAsync(new UnityEngine.XR.ARSubsystems.SerializableGuid(guid));
                if (!result.status.IsSuccess() || result.value == null)
                {
                    Debug.Log("[Obstetricia] No se pudo recuperar la ubicación guardada; se coloca frente al usuario.");
                    return false;
                }
                _anchor = result.value;
                // Esperar hasta 3 s a que el ancla se localice en la sala.
                float waited = 0f;
                while (_anchor != null && _anchor.trackingState != UnityEngine.XR.ARSubsystems.TrackingState.Tracking && waited < 3f)
                {
                    await Awaitable.NextFrameAsync();
                    waited += Time.unscaledDeltaTime;
                }
                if (_anchor == null || _anchor.trackingState != UnityEngine.XR.ARSubsystems.TrackingState.Tracking) return false;
                transform.SetPositionAndRotation(_anchor.transform.position, _anchor.transform.rotation);
                Debug.Log("[Obstetricia] Mesa restaurada en la ubicación guardada.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Obstetricia] Ancla guardada no disponible: {ex.Message}");
                return false;
            }
        }

        /// <summary>Guarda el ancla en el visor (y borra la anterior) para recordar la ubicación entre sesiones.</summary>
        private async Awaitable SaveAnchor(ARAnchor anchor)
        {
            if (anchor == null || !AnchorsReady || !anchorManager.descriptor.supportsSaveAnchor) return;
            try
            {
                string old = AppSettings.SavedAnchor;
                if (!string.IsNullOrEmpty(old) && Guid.TryParse(old, out Guid oldGuid))
                    await anchorManager.TryEraseAnchorAsync(new UnityEngine.XR.ARSubsystems.SerializableGuid(oldGuid));
                var saved = await anchorManager.TrySaveAnchorAsync(anchor);
                if (saved.status.IsSuccess())
                {
                    AppSettings.SavedAnchor = saved.value.guid.ToString();
                    Debug.Log("[Obstetricia] Ubicación de la mesa guardada.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Obstetricia] No se pudo guardar el ancla: {ex.Message}");
            }
        }

        /// <summary>Coloca la experiencia frente al usuario, sobre el suelo, mirando hacia él, y la ancla.</summary>
        public async void Recenter()
        {
            try
            {
                // Dentro de la sala se recentra con un parpadeo: mover el entorno a la vista produciría mareo.
                if (Current == Screen.Sala)
                {
                    sala.Recentrar();
                    return;
                }
                Transform head = Head;
                if (head == null) return;
                Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
                float floorY = xrOrigin != null ? xrOrigin.transform.position.y : 0f;
                // En pantalla (modo escritorio) el campo de visión es menor que en el visor:
                // la mesa se coloca más lejos para que se vean los paneles laterales completos.
                float placeDistance = AppSettings.EffectiveDistance + (PlatformMode.IsDesktop ? DesktopExtraDistance : 0f);
                Vector3 pos = head.position + forward * placeDistance;
                pos.y = floorY + AppSettings.EffectiveHeight;
                var pose = new Pose(pos, Quaternion.LookRotation(forward, Vector3.up));

                if (_moving) return;
                _moving = true;
                if (_anchor != null)
                {
                    Destroy(_anchor.gameObject);
                    _anchor = null;
                }

                // Desplazamiento suave hasta la nueva posición (sin saltos bruscos que mareen).
                HoloFeedback.Play(HoloFeedback.Sound.Recenter, 0.7f);
                HoloFeedback.HapticAll(0.25f, 0.12f);
                Vector3 startPos = transform.position;
                Quaternion startRot = transform.rotation;
                bool far = Vector3.Distance(startPos, pose.position) > 6f;
                float t = far ? 1f : 0f;
                while (t < 1f)
                {
                    t += Time.unscaledDeltaTime / 0.5f;
                    float e = Ease.InOutSine(Mathf.Clamp01(t));
                    transform.SetPositionAndRotation(Vector3.Lerp(startPos, pose.position, e), Quaternion.Slerp(startRot, pose.rotation, e));
                    await Awaitable.NextFrameAsync();
                }
                transform.SetPositionAndRotation(pose.position, pose.rotation);
                _moving = false;

                if (anchorManager != null && anchorManager.enabled && anchorManager.subsystem != null && anchorManager.subsystem.running)
                {
                    var result = await anchorManager.TryAddAnchorAsync(pose);
                    if (result.status.IsSuccess())
                    {
                        _anchor = result.value;
                        await SaveAnchor(_anchor);
                    }
                }
                Recentered?.Invoke();
                Debug.Log($"[Obstetricia] Experiencia colocada en {pose.position} (anclada: {_anchor != null})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Obstetricia] Error al recolocar: {ex}");
            }
        }
    }
}
