using System.Collections.Generic;
using Obstetricia.UI;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Obstetricia.Desktop
{
    /// <summary>
    /// MODO ESCRITORIO ("tipo juego"): permite recorrer la experiencia sin visor, con teclado y mouse.
    ///
    /// Se crea solo al iniciar la escena cuando <see cref="PlatformMode.IsDesktop"/> es true (no hay que
    /// agregarlo a la escena). Reutiliza el XR Origin existente:
    ///   - apaga el seguimiento de la cabeza, los controles, las manos y la locomoción XR;
    ///   - mueve el XR Origin con WASD / flechas y gira la cámara con el mouse (cursor bloqueado, como en un juego);
    ///   - agrega un XRRayInteractor que sale de la mira (centro de la pantalla), así los MISMOS botones
    ///     holográficos y objetos agarrables (XRGrabInteractable) de la versión VR funcionan con clic izquierdo;
    ///   - como no hay passthrough, construye una sala simple (piso y paredes) alrededor de la mesa.
    ///
    /// Controles:
    ///   Mouse           mirar (Esc libera el cursor; un clic lo vuelve a tomar)
    ///   WASD / flechas  moverse            Shift          correr
    ///   E               (en la sala virtual) abrir / cerrar su menú
    ///   Clic izquierdo  presionar botones; mantener para tomar objetos
    ///   Rueda           acercar / alejar el objeto tomado
    ///   Tab (mantener)  rotar el objeto tomado moviendo el mouse
    ///   H               devolver el objeto a su lugar original
    ///   I J K L         girar la pelvis    R (mantener)   recentrar la mesa
    ///   C               mostrar / ocultar los controles
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class DesktopRig : MonoBehaviour
    {
        [Header("Cuerpo")]
        [SerializeField] private float eyeHeight = 1.6f;
        [SerializeField] private float walkSpeed = 1.6f;
        [SerializeField] private float runSpeed = 3.2f;
        [SerializeField] private float fieldOfView = 70f;

        [Header("Mirar")]
        [SerializeField, Tooltip("Grados por pixel al mover el mouse.")]
        private float mouseSensitivity = 0.15f;
        [SerializeField] private float minPitch = -70f;
        [SerializeField] private float maxPitch = 70f;

        [Header("Objeto tomado")]
        [SerializeField, Tooltip("Metros por cada paso de la rueda del mouse.")]
        private float scrollStep = 0.12f;
        [SerializeField] private float minHoldDistance = 0.25f;
        [SerializeField] private float maxHoldDistance = 6f;
        [SerializeField, Tooltip("Grados por pixel al rotar el objeto con Tab.")]
        private float rotateSensitivity = 0.35f;

        [Header("Sala")]
        [SerializeField, Tooltip("Mitad del ancho de la sala (m). El jugador no puede salir de ella.")]
        private float roomHalfSize = 5f;
        [SerializeField] private float roomHeight = 3.2f;
        [SerializeField] private Color floorColor = new Color(0.78f, 0.8f, 0.84f);
        [SerializeField] private Color wallColor = new Color(0.9f, 0.92f, 0.96f);
        [SerializeField] private Color skyColor = new Color(0.72f, 0.82f, 0.93f);

        private XROrigin _origin;
        private Camera _cam;
        private XRRayInteractor _pointer;
        private float _yaw, _pitch;
        private Vector3 _roomCenter;
        private GameObject _room;
        private Obstetricia.Sala.SalaVirtual _sala;
        private bool _salaSearched;

        // Medidas de la sala virtual (SalaBuilder: 6 x 4 m) menos un margen para no atravesar los muros.
        private const float SalaHalfX = 2.7f, SalaHalfZ = 1.7f;
        private GameObject _help, _crosshair, _lockHint, _salaHint;
        private readonly List<InputAction> _ownedActions = new List<InputAction>();

        // Pose original (local) de cada objeto agarrable, guardada la primera vez que el rayo lo apunta.
        private readonly Dictionary<XRGrabInteractable, (Transform parent, Vector3 pos, Quaternion rot)> _homes =
            new Dictionary<XRGrabInteractable, (Transform, Vector3, Quaternion)>();
        private XRGrabInteractable _lastGrabbed;

        /// <summary>Crea el modo escritorio automáticamente al cargar la escena (solo sin visor).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!PlatformMode.IsDesktop) return;
            if (FindFirstObjectByType<DesktopRig>() != null) return;
            new GameObject("ModoEscritorio").AddComponent<DesktopRig>();
            Debug.Log("[Obstetricia] Modo escritorio activado (teclado + mouse).");
        }

        private void Awake()
        {
            _origin = FindFirstObjectByType<XROrigin>();
            _cam = _origin != null && _origin.Camera != null ? _origin.Camera : Camera.main;
            if (_cam == null)
            {
                Debug.LogError("[Obstetricia] Modo escritorio: no hay cámara en la escena.");
                enabled = false;
                return;
            }

            DisableXRRig();
            SetupCamera();
            BuildRoom();
            CreatePointer();
            BuildHelp();
        }

        private void OnDestroy()
        {
            foreach (InputAction a in _ownedActions) a.Dispose();
            _ownedActions.Clear();
        }

        // ------------------------------------------------------------------ Configuración

        /// <summary>Apaga todo lo que depende del visor: seguimiento, controles, manos, locomoción y passthrough.</summary>
        private void DisableXRRig()
        {
            foreach (Behaviour b in _cam.GetComponents<Behaviour>())
            {
                if (b == null) continue;
                string type = b.GetType().Name;
                if (type.Contains("TrackedPoseDriver") || b is ARCameraBackground || b is ARCameraManager)
                    b.enabled = false;
            }

            if (_origin == null) return;
            // Desde la cámara hacia arriba: en cada nivel se apagan los hermanos (controles, manos, locomoción...).
            Transform keep = _cam.transform;
            while (keep != null && keep != _origin.transform)
            {
                Transform parent = keep.parent;
                if (parent == null) break;
                foreach (Transform sibling in parent)
                    if (sibling != keep) sibling.gameObject.SetActive(false);
                keep = parent;
            }
        }

        private void SetupCamera()
        {
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = skyColor;
            _cam.fieldOfView = fieldOfView;
            _cam.nearClipPlane = 0.03f;

            Transform body = _origin != null ? _origin.transform : _cam.transform.parent;
            _roomCenter = body != null ? body.position : Vector3.zero;
            _yaw = body != null ? body.eulerAngles.y : _cam.transform.eulerAngles.y;
            _pitch = 0f;
            ApplyPose();

            if (FindFirstObjectByType<Light>() == null)
            {
                var sun = new GameObject("Luz (escritorio)").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.1f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat || RenderSettings.ambientLight.maxColorComponent < 0.2f)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.64f);
            }
        }

        /// <summary>Sala simple (piso, paredes y una alfombra bajo la mesa) en lugar del passthrough.</summary>
        private void BuildRoom()
        {
            var room = new GameObject("Sala (modo escritorio)").transform;
            _room = room.gameObject;
            room.position = _roomCenter;
            float size = roomHalfSize * 2f;

            GameObject floor = Block(room, "Piso", new Vector3(0f, -0.05f, 0f), new Vector3(size, 0.1f, size), floorColor);
            floor.isStatic = true;
            Block(room, "Alfombra", new Vector3(0f, 0.002f, 1.55f), new Vector3(2.6f, 0.004f, 2.6f), new Color(0.32f, 0.45f, 0.75f));

            float h = roomHeight;
            Block(room, "Pared N", new Vector3(0f, h / 2f, roomHalfSize), new Vector3(size, h, 0.1f), wallColor);
            Block(room, "Pared S", new Vector3(0f, h / 2f, -roomHalfSize), new Vector3(size, h, 0.1f), wallColor);
            Block(room, "Pared E", new Vector3(roomHalfSize, h / 2f, 0f), new Vector3(0.1f, h, size), wallColor);
            Block(room, "Pared O", new Vector3(-roomHalfSize, h / 2f, 0f), new Vector3(0.1f, h, size), wallColor);
            // Zócalo de color para dar sensación de profundidad.
            Color trim = new Color(0.45f, 0.3f, 0.6f);
            Block(room, "Zocalo N", new Vector3(0f, 0.06f, roomHalfSize - 0.06f), new Vector3(size, 0.12f, 0.02f), trim);
            Block(room, "Zocalo S", new Vector3(0f, 0.06f, -roomHalfSize + 0.06f), new Vector3(size, 0.12f, 0.02f), trim);
            Block(room, "Zocalo E", new Vector3(roomHalfSize - 0.06f, 0.06f, 0f), new Vector3(0.02f, 0.12f, size), trim);
            Block(room, "Zocalo O", new Vector3(-roomHalfSize + 0.06f, 0.06f, 0f), new Vector3(0.02f, 0.12f, size), trim);
        }

        private static GameObject Block(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            // Las paredes no deben bloquear el rayo del mouse ni empujar objetos.
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            // El material por defecto de los primitivos no es de URP (en la build se ve magenta):
            // se usa un URP/Lit incluido en Resources para garantizar que su shader esté en la build.
            Material mat = RoomMaterial != null ? new Material(RoomMaterial) : r.material;
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static Material _roomMaterial;

        /// <summary>Material base URP/Lit (Assets/Resources/ObstetriciaDesktop/SalaEscritorio.mat).</summary>
        private static Material RoomMaterial
        {
            get
            {
                if (_roomMaterial != null) return _roomMaterial;
                _roomMaterial = Resources.Load<Material>("ObstetriciaDesktop/SalaEscritorio");
                if (_roomMaterial == null)
                {
                    Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                    if (lit != null) _roomMaterial = new Material(lit);
                }
                return _roomMaterial;
            }
        }

        /// <summary>Rayo XRI que sigue al mouse: clic izquierdo = gatillo (botones y tomar objetos).</summary>
        private void CreatePointer()
        {
            var go = new GameObject("PunteroMouse");
            go.transform.SetParent(transform, false);
            go.SetActive(false); // se configura antes de que XRI lo inicialice
            _pointer = go.AddComponent<XRRayInteractor>();
            _pointer.useForceGrab = false;           // el objeto se queda a la distancia en que se tomó
            _pointer.enableUIInteraction = true;     // botones holográficos (TrackedDeviceGraphicRaycaster)
            _pointer.maxRaycastDistance = 20f;
            _pointer.selectActionTrigger = UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor.InputTriggerType.State;
            _pointer.selectInput = MouseButton("Escritorio Seleccionar");
            _pointer.uiPressInput = MouseButton("Escritorio Presionar UI");
            // Acercar/alejar y rotar el objeto tomado se hace a mano (rueda y Tab), no con los joysticks de XRI.
            _pointer.manipulateAttachTransform = false;
            _pointer.hoverEntered.AddListener(args => RememberHome(args.interactableObject as XRGrabInteractable));
            _pointer.selectEntered.AddListener(args =>
            {
                var grab = args.interactableObject as XRGrabInteractable;
                RememberHome(grab);
                if (grab != null) _lastGrabbed = grab;
            });
            go.SetActive(true);
        }

        private XRInputButtonReader MouseButton(string name)
        {
            var performed = new InputAction(name, InputActionType.Button, "<Mouse>/leftButton");
            var value = new InputAction(name + " Valor", InputActionType.Value, "<Mouse>/leftButton");
            performed.Enable();
            value.Enable();
            _ownedActions.Add(performed);
            _ownedActions.Add(value);
            var reader = new XRInputButtonReader(name, name + " Valor", false, XRInputButtonReader.InputSourceMode.InputAction)
            {
                inputActionPerformed = performed,
                inputActionValue = value,
            };
            return reader;
        }

        /// <summary>Recuadro con los controles (H para ocultar).</summary>
        private void BuildHelp()
        {
            var canvasGo = new GameObject("AyudaEscritorio", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(canvasGo.transform, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(16f, 16f);
            panel.sizeDelta = new Vector2(370f, 240f);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.08f, 0.18f, 0.78f);
            bg.raycastTarget = false;

            var textRt = new GameObject("Texto", typeof(RectTransform)).GetComponent<RectTransform>();
            textRt.SetParent(panel, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(14f, 10f);
            textRt.offsetMax = new Vector2(-14f, -10f);
            var text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 15f;
            text.color = Color.white;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.text =
                "<b>CONTROLES</b>\n" +
                "<b>Mouse</b> mirar  ·  <b>WASD</b> moverse  (<b>Shift</b> correr)\n" +
                "<b>Clic izquierdo</b>  botones · mantener para tomar\n" +
                "<b>Rueda</b>  acercar / alejar lo que tomaste\n" +
                "<b>Tab</b> (mantener) + mouse  rotar lo que tomaste\n" +
                "<b>H</b>  devolver el objeto a su lugar\n" +
                "<b>I J K L</b> girar la pelvis  ·  <b>R</b> (mantener) recentrar\n" +
                "<b>E</b> menú de la sala virtual  ·  <b>Esc</b> soltar el cursor\n" +
                "<b>C</b>  ocultar esta ayuda";
            _help = panel.gameObject;

            // Mira en el centro de la pantalla: de ahí sale el rayo cuando el cursor está bloqueado.
            var cross = new GameObject("Mira", typeof(RectTransform)).GetComponent<RectTransform>();
            cross.SetParent(canvasGo.transform, false);
            cross.anchorMin = cross.anchorMax = cross.pivot = new Vector2(0.5f, 0.5f);
            cross.sizeDelta = new Vector2(8f, 8f);
            var border = new GameObject("Borde", typeof(RectTransform)).GetComponent<RectTransform>();
            border.SetParent(cross, false);
            border.anchorMin = Vector2.zero;
            border.anchorMax = Vector2.one;
            border.offsetMin = new Vector2(-2f, -2f);
            border.offsetMax = new Vector2(2f, 2f);
            var borderImg = border.gameObject.AddComponent<Image>();
            borderImg.sprite = HoloSprites.Circle;
            borderImg.color = new Color(0f, 0f, 0f, 0.55f);
            borderImg.raycastTarget = false;
            var dotRt = new GameObject("Punto", typeof(RectTransform)).GetComponent<RectTransform>();
            dotRt.SetParent(cross, false);
            dotRt.anchorMin = Vector2.zero;
            dotRt.anchorMax = Vector2.one;
            dotRt.offsetMin = dotRt.offsetMax = Vector2.zero;
            var dot = dotRt.gameObject.AddComponent<Image>();
            dot.sprite = HoloSprites.Circle;
            dot.color = Color.white;
            dot.raycastTarget = false;
            _crosshair = cross.gameObject;

            // Aviso cuando el cursor está libre (al inicio o después de Esc).
            var hint = new GameObject("AvisoCursor", typeof(RectTransform)).GetComponent<RectTransform>();
            hint.SetParent(canvasGo.transform, false);
            hint.anchorMin = hint.anchorMax = hint.pivot = new Vector2(0.5f, 1f);
            hint.anchoredPosition = new Vector2(0f, -16f);
            hint.sizeDelta = new Vector2(600f, 40f);
            var hintBg = hint.gameObject.AddComponent<Image>();
            hintBg.color = new Color(0.03f, 0.08f, 0.18f, 0.85f);
            hintBg.raycastTarget = false;
            var hintRt = new GameObject("Texto", typeof(RectTransform)).GetComponent<RectTransform>();
            hintRt.SetParent(hint, false);
            hintRt.anchorMin = Vector2.zero;
            hintRt.anchorMax = Vector2.one;
            hintRt.offsetMin = hintRt.offsetMax = Vector2.zero;
            var hintText = hintRt.gameObject.AddComponent<TextMeshProUGUI>();
            hintText.fontSize = 16f;
            hintText.color = Color.white;
            hintText.raycastTarget = false;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.text = "Haz <b>clic</b> en la escena para mirar con el mouse  ·  <b>Esc</b> suelta el cursor";
            _lockHint = hint.gameObject;

            // Aviso dentro de la sala virtual mientras su menú está plegado.
            _salaHint = Instantiate(hint.gameObject, canvasGo.transform);
            _salaHint.name = "AvisoMenuSala";
            var salaRt = (RectTransform)_salaHint.transform;
            salaRt.anchorMin = salaRt.anchorMax = salaRt.pivot = new Vector2(0.5f, 0f);
            salaRt.anchoredPosition = new Vector2(0f, 18f);
            salaRt.sizeDelta = new Vector2(300f, 38f);
            _salaHint.GetComponentInChildren<TextMeshProUGUI>().text = "<b>E</b>  ·  menú de la sala";
            _salaHint.SetActive(false);
        }

        // ------------------------------------------------------------------ Cada frame

        private void Update()
        {
            // Mientras se escribe el nombre, las letras van al teclado holográfico y no mueven al jugador.
            Keyboard kb = HoloKeyboard.IsOpen ? null : Keyboard.current;
            Mouse mouse = Mouse.current;

            if (kb != null && kb.cKey.wasPressedThisFrame && _help != null) _help.SetActive(!_help.activeSelf);

            // Cursor bloqueado = mirar con el mouse, como en un juego. El navegador lo libera con Esc;
            // un clic en la escena lo vuelve a bloquear (ese mismo clic también apunta y presiona).
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (!locked && mouse != null && mouse.leftButton.wasPressedThisFrame && Application.isFocused)
                Cursor.lockState = CursorLockMode.Locked;
            if (_crosshair != null) _crosshair.SetActive(locked);
            if (_lockHint != null) _lockHint.SetActive(!locked);

            // Objeto tomado: rueda = acercar/alejar, Tab + mouse = rotar, H = devolverlo a su lugar.
            XRGrabInteractable held = HeldObject;
            bool rotating = held != null && kb != null && kb.tabKey.isPressed;
            if (held != null) ManipulateHeld(mouse, rotating);
            if (kb != null && kb.hKey.wasPressedThisFrame) ResetObject(held != null ? held : _lastGrabbed);

            // Dentro de la sala virtual o de un video 360° se ve su propio entorno: la habitación simple se oculta
            // (sus paredes, a 5 m, taparían la esfera del video, que mide 12 m de radio).
            bool inSala = SalaAbierta;
            bool hideRoom = inSala || VideoActivo;
            if (_room != null && _room.activeSelf == hideRoom) _room.SetActive(!hideRoom);

            // Mirar: mover el mouse (cursor bloqueado) o clic derecho + arrastrar (cursor libre).
            // Mientras se rota un objeto con Tab, el mouse gira el objeto y no la cámara.
            if (mouse != null && !rotating && (locked || mouse.rightButton.isPressed))
            {
                Vector2 d = mouse.delta.ReadValue();
                _yaw += d.x * mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch - d.y * mouseSensitivity, minPitch, maxPitch);
            }
            // E queda reservada para el menú de la sala virtual (girar se hace con el mouse).
            if (inSala && kb != null && kb.eKey.wasPressedThisFrame) _sala.AlternarMenu();
            if (_salaHint != null) _salaHint.SetActive(inSala && !_sala.MenuVisible && !_sala.Ocupada);

            // Moverse en el plano del piso, según hacia dónde se mira.
            if (kb != null)
            {
                Vector2 move = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (move.sqrMagnitude > 0f)
                {
                    float speed = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed ? runSpeed : walkSpeed;
                    Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
                    Vector3 delta = yawRot * new Vector3(move.x, 0f, move.y).normalized * (speed * Time.unscaledDeltaTime);
                    Transform body = Body;
                    Vector3 p = body.position + delta;
                    if (inSala)
                    {
                        // La sala se mueve alrededor del usuario: el límite se calcula en su propio espacio.
                        Transform t = _sala.transform;
                        Vector3 l = t.InverseTransformPoint(p);
                        l.x = Mathf.Clamp(l.x, -SalaHalfX, SalaHalfX);
                        l.z = Mathf.Clamp(l.z, -SalaHalfZ, SalaHalfZ);
                        Vector3 clamped = t.TransformPoint(l);
                        p.x = clamped.x;
                        p.z = clamped.z;
                    }
                    else
                    {
                        float limit = roomHalfSize - 0.35f;
                        p.x = Mathf.Clamp(p.x, _roomCenter.x - limit, _roomCenter.x + limit);
                        p.z = Mathf.Clamp(p.z, _roomCenter.z - limit, _roomCenter.z + limit);
                    }
                    body.position = p;
                }
            }

            ApplyPose();
            AimPointer(mouse);
        }

        private Transform Body => _origin != null ? _origin.transform : _cam.transform;

        // ------------------------------------------------------------------ Objeto tomado

        private XRGrabInteractable HeldObject =>
            _pointer != null && _pointer.hasSelection ? _pointer.firstInteractableSelected as XRGrabInteractable : null;

        /// <summary>Rueda: acerca o aleja el objeto por el rayo. Tab + mouse: lo rota frente a la cámara.</summary>
        private void ManipulateHeld(Mouse mouse, bool rotating)
        {
            if (mouse == null) return;
            Transform attach = _pointer.attachTransform;
            if (attach == null) return;

            float scroll = Mathf.Clamp(mouse.scroll.ReadValue().y, -1f, 1f);
            if (Mathf.Abs(scroll) > 0.001f)
            {
                // El punto de agarre es hijo del rayo: su z local es la distancia desde el ojo.
                Vector3 local = attach.localPosition;
                local.z = Mathf.Clamp(local.z + scroll * scrollStep, minHoldDistance, maxHoldDistance);
                attach.localPosition = local;
            }

            if (rotating)
            {
                Vector2 d = mouse.delta.ReadValue() * rotateSensitivity;
                Transform cam = _cam.transform;
                attach.rotation = Quaternion.AngleAxis(-d.x, cam.up) * Quaternion.AngleAxis(d.y, cam.right) * attach.rotation;
            }
        }

        private void RememberHome(XRGrabInteractable grab)
        {
            if (grab == null || _homes.ContainsKey(grab)) return;
            Transform t = grab.transform;
            _homes[grab] = (t.parent, t.localPosition, t.localRotation);
        }

        /// <summary>H: suelta el objeto (si está tomado) y lo devuelve a donde estaba la primera vez que se apuntó.</summary>
        private void ResetObject(XRGrabInteractable grab)
        {
            if (grab == null) return;
            if (grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.SelectExit(_pointer, (IXRSelectInteractable)grab);

            // Los objetos de la sala virtual ya saben volver a su sitio.
            var objeto = grab.GetComponent<Obstetricia.Sala.ObjetoSala>();
            if (objeto != null)
            {
                objeto.Restablecer();
                HoloFeedback.Play(HoloFeedback.Sound.Click, 0.4f);
                return;
            }
            if (!_homes.TryGetValue(grab, out var home)) return;
            Transform t = grab.transform;
            if (t.parent != home.parent) t.SetParent(home.parent, false);
            t.localPosition = home.pos;
            t.localRotation = home.rot;
            var rb = grab.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = t.position; // con interpolación no basta mover el transform
                rb.rotation = t.rotation;
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            HoloFeedback.Play(HoloFeedback.Sound.Click, 0.4f);
        }

        private Obstetricia.Media.Video360Player _video;
        private bool _videoSearched;

        private bool VideoActivo
        {
            get
            {
                if (!_videoSearched)
                {
                    _videoSearched = true;
                    _video = FindFirstObjectByType<Obstetricia.Media.Video360Player>(FindObjectsInactive.Include);
                }
                return _video != null && _video.IsActive;
            }
        }

        private bool SalaAbierta
        {
            get
            {
                if (!_salaSearched)
                {
                    _salaSearched = true;
                    _sala = FindFirstObjectByType<Obstetricia.Sala.SalaVirtual>(FindObjectsInactive.Include);
                }
                return _sala != null && _sala.Abierta;
            }
        }

        /// <summary>El giro horizontal va en el XR Origin; la inclinación (mirar arriba/abajo) en la cámara.</summary>
        private void ApplyPose()
        {
            if (_origin != null)
            {
                _origin.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
                Transform parent = _cam.transform.parent;
                float parentY = parent != null ? parent.position.y : _origin.transform.position.y;
                _cam.transform.localPosition = new Vector3(0f, _origin.transform.position.y + eyeHeight - parentY, 0f);
                _cam.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
            else
            {
                _cam.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
        }

        /// <summary>Alinea el rayo XRI con la mira o el puntero del mouse (antes de que XRI procese las interacciones).</summary>
        private void AimPointer(Mouse mouse)
        {
            if (_pointer == null || mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();
            // Con el cursor bloqueado (o mirando con clic derecho) el rayo sale de la mira, al centro de la pantalla.
            if (Cursor.lockState == CursorLockMode.Locked || mouse.rightButton.isPressed)
                screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Ray ray = _cam.ScreenPointToRay(screen);
            _pointer.transform.SetPositionAndRotation(ray.origin, Quaternion.LookRotation(ray.direction, _cam.transform.up));
        }
    }
}
