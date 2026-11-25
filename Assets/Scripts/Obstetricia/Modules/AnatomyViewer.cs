using System;
using System.Collections;
using System.Collections.Generic;
using Obstetricia.Core;
using Obstetricia.Data;
using Obstetricia.Detection;
using Obstetricia.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Obstetricia.Modules
{
    /// <summary>
    /// Panel de vidrio "PELVIS ÓSEA FEMENINA – Vista anterior" con el modelo 3D de la pelvis y sus etiquetas.
    /// Solo se activa cuando el BallDetector confirma una pelota de ejercicio.
    /// Las etiquetas acompañan a la pelvis cuando se gira o se toma, y siempre miran al usuario.
    /// La pelvis se puede tomar con el gatillo lateral o girar con el joystick.
    /// </summary>
    public class AnatomyViewer : MonoBehaviour
    {
        [Serializable]
        public class AnatomyLabel
        {
            public string text;
            [Tooltip("Punto del modelo (espacio local del modelo, metros). Un hijo llamado 'Label_<texto>' lo reemplaza.")]
            public Vector3 anchor;
            [Tooltip("Posición inicial de la etiqueta respecto al panel (mm desde el centro). Luego acompaña a la pelvis.")]
            public Vector2 panelPosition;
            public TextAlignmentOptions alignment = TextAlignmentOptions.Left;
        }

        [SerializeField] private BallDetector detector;
        [SerializeField] private ObstetriciaTheme theme;
        [SerializeField] private Transform head;
        [SerializeField, Tooltip("Si se asigna, el panel aparece en esta posición (p. ej. el panel central del módulo); si no, junto a la pelota.")]
        private Transform placementAnchor;
        [SerializeField, Tooltip("Mantener el modelo visible tras la primera detección, aunque se deje de ver la pelota.")]
        private bool keepAfterDetection = true;
        [SerializeField] private List<AnatomyLabel> labels = new List<AnatomyLabel>
        {
            new AnatomyLabel { text = "Sacro", anchor = new Vector3(0f, 0.049f, 0.014f), panelPosition = new Vector2(0, 150), alignment = TextAlignmentOptions.Center },
            new AnatomyLabel { text = "Ilion", anchor = new Vector3(0.116f, 0.089f, -0.048f), panelPosition = new Vector2(290, 128) },
            new AnatomyLabel { text = "Acetábulo", anchor = new Vector3(0.107f, -0.026f, -0.003f), panelPosition = new Vector2(300, 20) },
            new AnatomyLabel { text = "Sínfisis del pubis", anchor = new Vector3(0f, -0.068f, -0.043f), panelPosition = new Vector2(0, -225), alignment = TextAlignmentOptions.Center },
            new AnatomyLabel { text = "Ángulo subpúbico\n> 90°", anchor = new Vector3(0f, -0.092f, -0.032f), panelPosition = new Vector2(250, -175) },
        };

        [SerializeField, Tooltip("Ancho (m) con que se muestra la pelvis dentro del panel.")]
        private float modelSize = 0.42f;

        [Header("Giro con joystick")]
        [SerializeField] private InputActionProperty rotateRight = new InputActionProperty(
            new InputAction("GirarDer", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis", expectedControlType: "Vector2"));
        [SerializeField] private InputActionProperty rotateLeft = new InputActionProperty(
            new InputAction("GirarIzq", InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis", expectedControlType: "Vector2"));
        [SerializeField, Tooltip("Grados por segundo con el joystick a fondo.")]
        private float rotateSpeed = 110f;

        [SerializeField, Tooltip("Fondo oscuro del panel (para que se lea sobre muros claros).")]
        private bool darkGlass;

        private const float PanelWidth = 0.78f;
        private const float PanelHeight = 0.5f;

        private sealed class LabelView
        {
            public AnatomyLabel Label;
            public Transform Anchor;
            public Vector3 LocalPosition;   // posición de la etiqueta en el espacio del modelo
            public RectTransform Chip;
            public CanvasGroup Group;
            public LineRenderer Line;
            public Transform Dot;
            public Color LineColor = new Color(0.85f, 0.97f, 1f, 0.9f);
        }

        private GameObject _root;
        private Transform _modelPivot;
        private Transform _model;
        private Pose _modelHome;
        private Vector3 _modelScale;
        private XRGrabInteractable _grab;
        private readonly List<LabelView> _labelViews = new List<LabelView>();
        private Canvas _canvas;
        private bool _active;
        private bool _unlocked;
        private bool _labelsVisible = true;
        private Coroutine _modelAnim;

        // Mecanismos del parto y diámetros
        private BirthMechanism _mech;
        private GameObject _mechControls;
        private TextMeshProUGUI _mechTitle, _mechDesc;
        private HoloButton _mechPlay;
        private TextMeshProUGUI _panelTitle, _panelSubtitle;
        private GameObject _btnNames, _btnDiam;
        private bool _mechMode, _diamMode;
        private Renderer[] _modelRenderers;
        private Material[][] _originalMaterials;
        private readonly List<LabelView> _diamViews = new List<LabelView>();
        private readonly List<(Transform a, Transform b, LineRenderer line)> _diamSegments = new List<(Transform, Transform, LineRenderer)>();

        private static readonly (string key, string text, Vector3 offset)[] Diameters =
        {
            ("Conjugado", "Conjugado obstétrico\n≈ 11 cm", new Vector3(-0.15f, 0.06f, -0.12f)),
            ("Transverso", "Diámetro transverso\n≈ 13 cm", new Vector3(0.17f, 0.09f, -0.12f)),
            ("Biespinoso", "Biespinoso\n≈ 10 cm", new Vector3(0.17f, -0.03f, -0.12f)),
            ("Bituberoso", "Bituberoso\n≈ 11 cm", new Vector3(-0.17f, -0.1f, -0.12f)),
            ("APInferior", "AP estrecho inferior\n≈ 11,5 cm", new Vector3(0.11f, -0.14f, -0.12f)),
        };

        private Quaternion HomeRotation => _mechMode ? _modelHome.rotation * Quaternion.Euler(0f, -70f, 0f) : _modelHome.rotation;

        /// <summary>Se invoca cuando cambia el estado (bloqueado / visible) para actualizar textos de la UI.</summary>
        public event Action StateChanged;

        /// <summary>True cuando el modelo está visible (pelota detectada).</summary>
        public bool IsUnlocked => _unlocked;

        public bool LabelsVisible
        {
            get => _labelsVisible;
            set
            {
                _labelsVisible = value;
                ApplyVisibility();
                HoloFeedback.Play(value ? HoloFeedback.Sound.Open : HoloFeedback.Sound.Close, 0.5f);
            }
        }

        /// <summary>Texto de estado del detector para mostrar mientras está bloqueado.</summary>
        public string DetectorStatus => detector != null ? detector.StatusText : "Sin detector de pelota";

        private void Awake()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            Build();
            _root.SetActive(false);
        }

        private void OnEnable()
        {
            if (detector != null) detector.DetectionChanged += OnDetectionChanged;
            rotateRight.action?.Enable();
            rotateLeft.action?.Enable();
        }

        private void OnDisable()
        {
            if (detector != null) detector.DetectionChanged -= OnDetectionChanged;
        }

        /// <summary>Entra a la sección Anatomía: comienza a buscar la pelota.</summary>
        public void Activate()
        {
            ExitMechanisms();
            _active = true;
            _unlocked = false;
            if (detector != null)
            {
                detector.Begin();
                if (detector.IsBallDetected) Unlock();
            }
            StateChanged?.Invoke();
        }

        /// <summary>Modelo de la pelvis (espacio del prefab).</summary>
        public Transform Model => _model;

        /// <summary>Muestra la pelvis sin requerir la pelota (sala virtual).</summary>
        public void ActivateFree()
        {
            Activate();
            Unlock();
        }

        /// <summary>Sale de la sección: oculta el modelo y apaga el detector.</summary>
        public void Deactivate()
        {
            ExitMechanisms();
            _diamMode = false;
            _active = false;
            _unlocked = false;
            _root.SetActive(false);
            if (detector != null) detector.End();
            StateChanged?.Invoke();
        }

        /// <summary>Define dónde aparece el panel (null = junto a la pelota detectada).</summary>
        public void SetPlacementAnchor(Transform anchor) => placementAnchor = anchor;

        /// <summary>Simula la detección de la pelota (modo prueba).</summary>
        public void SimulateBallForTesting()
        {
            if (detector != null) detector.SetSimulated(true);
        }

        /// <summary>Devuelve el modelo, suavemente, a su posición y rotación inicial dentro del panel.</summary>
        public void ResetModel()
        {
            if (_model == null || !_root.activeInHierarchy) return;
            if (_modelAnim != null) StopCoroutine(_modelAnim);
            _modelAnim = StartCoroutine(ReturnHome());
        }

        private IEnumerator ReturnHome()
        {
            Vector3 p0 = _model.localPosition;
            Quaternion r0 = _model.localRotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.45f;
                float e = Ease.InOutSine(Mathf.Clamp01(t));
                _model.localPosition = Vector3.Lerp(p0, _modelHome.position, e);
                _model.localRotation = Quaternion.Slerp(r0, HomeRotation, e);
                yield return null;
            }
            _modelAnim = null;
        }

        private void OnDetectionChanged(bool detected)
        {
            if (!_active) return;
            if (detected) Unlock();
            else if (!keepAfterDetection)
            {
                _unlocked = false;
                _root.SetActive(false);
            }
            StateChanged?.Invoke();
        }

        private void Unlock()
        {
            if (_unlocked && _root.activeSelf) return;
            _unlocked = true;
            if (placementAnchor != null)
                _root.transform.SetPositionAndRotation(placementAnchor.position - placementAnchor.forward * 0.02f, placementAnchor.rotation);
            else
                PlaceNearBall();
            _root.SetActive(true);
            _model.localPosition = _modelHome.position;
            _model.localRotation = HomeRotation;
            ApplyVisibility();
            if (_modelAnim != null) StopCoroutine(_modelAnim);
            _modelAnim = StartCoroutine(MaterializeModel());
            HoloFeedback.Play(HoloFeedback.Sound.Success, 0.8f);
            StateChanged?.Invoke();
        }

        /// <summary>La pelvis se materializa: crece con un leve rebote y un pequeño giro.</summary>
        private IEnumerator MaterializeModel()
        {
            float t = 0f;
            Quaternion home = HomeRotation;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.7f;
                float e = Ease.OutBack(Mathf.Clamp01(t), 1.3f);
                _model.localScale = _modelScale * Mathf.Max(0.001f, e);
                _model.localRotation = home * Quaternion.Euler(0f, (1f - Ease.OutCubic(Mathf.Clamp01(t))) * -50f, 0f);
                yield return null;
            }
            _model.localScale = _modelScale;
            _model.localRotation = home;
            _modelAnim = null;
        }

        /// <summary>Ubica el panel sobre/junto a la pelota, a la altura de los ojos y mirando al usuario.</summary>
        private void PlaceNearBall()
        {
            Vector3 viewer = head != null ? head.position : transform.position + Vector3.back + Vector3.up * 1.5f;
            Vector3 ball = detector != null ? detector.BallPosition : transform.position;
            Vector3 toBall = Vector3.ProjectOnPlane(ball - viewer, Vector3.up);
            if (toBall.sqrMagnitude < 0.04f) toBall = Vector3.ProjectOnPlane(head != null ? head.forward : Vector3.forward, Vector3.up);
            Vector3 dir = toBall.normalized;
            float distance = Mathf.Clamp(toBall.magnitude * 0.85f, 0.8f, 1.6f);
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            Vector3 pos = viewer + dir * distance + right * 0.25f;
            pos.y = Mathf.Clamp(viewer.y - 0.05f, 1.0f, 1.7f);
            _root.transform.position = pos;
            HoloUI.FaceTowards(_root.transform, viewer);
        }

        private void Update()
        {
            if (!_unlocked || !_root.activeSelf || _model == null) return;
            if (_grab != null && _grab.isSelected) return;

            // Joystick (cualquiera de los dos): izquierda/derecha gira, arriba/abajo inclina.
            Vector2 stick = Vector2.zero;
            if (rotateRight.action != null) stick += rotateRight.action.ReadValue<Vector2>();
            if (rotateLeft.action != null) stick += rotateLeft.action.ReadValue<Vector2>();
            if (stick.sqrMagnitude < 0.04f) return;
            if (_modelAnim != null) { StopCoroutine(_modelAnim); _modelAnim = null; }
            float dt = Time.unscaledDeltaTime * rotateSpeed;
            Vector3 center = _modelPivot.position;
            _model.RotateAround(center, _root.transform.up, -stick.x * dt);
            _model.RotateAround(center, _root.transform.right, stick.y * dt);
        }

        private void LateUpdate()
        {
            if (!_root.activeSelf || _model == null) return;
            Vector3 viewer = head != null ? head.position : _root.transform.position - _root.transform.forward;
            float centerDist = Vector3.Distance(viewer, _modelPivot.position);
            foreach (var seg in _diamSegments)
            {
                if (!seg.line.enabled) continue;
                seg.line.SetPosition(0, seg.a.position);
                seg.line.SetPosition(1, seg.b.position);
            }
            foreach (LabelView v in _labelViews) UpdateLabel(v, viewer, centerDist);
            foreach (LabelView v in _diamViews) UpdateLabel(v, viewer, centerDist);
        }

        private void UpdateLabel(LabelView v, Vector3 viewer, float centerDist)
        {
            if (!v.Chip.gameObject.activeSelf) return;
            {
                // La etiqueta orbita con la pelvis y se acerca un poco al usuario para no quedar dentro del hueso.
                Vector3 p = _model.TransformPoint(v.LocalPosition);
                Vector3 toViewer = (viewer - p).normalized;
                p += toViewer * 0.06f;
                v.Chip.position = p;
                v.Chip.rotation = Quaternion.LookRotation(p - viewer, Vector3.up);

                Vector3 anchor = v.Anchor.position;
                v.Dot.position = anchor;
                v.Line.SetPosition(0, ChipEdge(v.Chip, anchor));
                v.Line.SetPosition(1, anchor);

                // Atenuar las etiquetas cuyo punto queda detrás de la pelvis.
                bool behind = Vector3.Distance(viewer, anchor) > centerDist + 0.03f;
                float target = behind ? 0.35f : 1f;
                v.Group.alpha = Mathf.MoveTowards(v.Group.alpha, target, Time.unscaledDeltaTime * 3f);
                Color c = v.LineColor * new Color(1f, 1f, 1f, v.Group.alpha);
                v.Line.startColor = v.Line.endColor = c;
            }
        }

        /// <summary>Punto del borde del chip más cercano al punto anatómico.</summary>
        private static Vector3 ChipEdge(RectTransform chip, Vector3 target)
        {
            Vector3 local = chip.InverseTransformPoint(target);
            Vector2 half = chip.rect.size * 0.5f;
            Vector2 dir = new Vector2(local.x, local.y);
            if (dir.sqrMagnitude < 1e-4f) return chip.position;
            float sx = Mathf.Abs(dir.x) > 1e-4f ? half.x / Mathf.Abs(dir.x) : float.MaxValue;
            float sy = Mathf.Abs(dir.y) > 1e-4f ? half.y / Mathf.Abs(dir.y) : float.MaxValue;
            Vector2 edge = dir * Mathf.Min(sx, sy);
            return chip.TransformPoint(new Vector3(edge.x, edge.y, 0f));
        }

        // ------------------------------------------------------------------ Construcción

        private void Build()
        {
            _root = new GameObject("PelvisPanel");
            _root.transform.SetParent(transform, false);

            _canvas = HoloUI.CreateCanvas(_root.transform, "Glass", new Vector2(PanelWidth * 1000f, PanelHeight * 1000f));
            HoloPanel glass = HoloUI.Panel(_canvas.transform, "Background", Vector2.zero, new Vector2(PanelWidth * 1000f, PanelHeight * 1000f),
                darkGlass ? new Color(0.05f, 0.12f, 0.24f, 0.9f) : HoloColors.Glass, HoloColors.Border);
            glass.Background.raycastTarget = false;
            _panelTitle = HoloUI.Text(glass.transform, "Title", "PELVIS ÓSEA FEMENINA", 30, new Vector2(-120, 210), new Vector2(500, 40),
                TextAlignmentOptions.Left, HoloColors.Text, FontStyles.Bold);
            _panelSubtitle = HoloUI.Text(glass.transform, "Subtitle", "Vista anterior", 18, new Vector2(-120, 180), new Vector2(500, 26),
                TextAlignmentOptions.Left, HoloColors.TextDim);
            _btnNames = SmallButton(glass.transform, "Nombres", "labels", new Vector2(240, 212), "Mostrar/ocultar nombres", () => LabelsVisible = !LabelsVisible);
            _btnDiam = SmallButton(glass.transform, "Diametros", "ruler", new Vector2(296, 212), "Diámetros pélvicos", ToggleDiameters);
            HoloButton reset = HoloUI.Button(glass.transform, "Reset", null, new Vector2(352, 212), new Vector2(46, 46), ResetModel, "restart");
            var resetFx = reset.GetComponent<HoloHoverFx>();
            if (resetFx != null)
            {
                resetFx.tooltip = "Volver a la vista anterior";
                resetFx.tooltipOffset = new Vector2(-60f, 50f);
                resetFx.BuildTooltip();
            }
            // Ayuda breve para quienes recién usan VR.
            HoloUI.Text(glass.transform, "Ayuda", "<b>Gatillo lateral</b>: tomar y girar\n<b>Joystick</b>: rotar la pelvis", 15,
                new Vector2(-250, -218), new Vector2(260, 44), TextAlignmentOptions.Left, HoloColors.TextDim);

            _modelPivot = new GameObject("ModelPivot").transform;
            _modelPivot.SetParent(_root.transform, false);
            _modelPivot.localPosition = new Vector3(0f, -0.025f, -0.12f);

            _model = CreateModel(_modelPivot);
            _modelHome = new Pose(_model.localPosition, _model.localRotation);
            _modelScale = _model.localScale;
            _modelRenderers = _model.GetComponentsInChildren<Renderer>();
            _originalMaterials = new Material[_modelRenderers.Length][];
            for (int i = 0; i < _modelRenderers.Length; i++) _originalMaterials[i] = _modelRenderers[i].sharedMaterials;
            _grab = MakeGrabbable(_model.gameObject);
            BuildLabels();
            BuildDiameters();
            BuildMechanism();
        }

        private GameObject SmallButton(Transform parent, string name, string icon, Vector2 pos, string tooltip, Action onClick)
        {
            HoloButton b = HoloUI.Button(parent, name, null, pos, new Vector2(46, 46), () => onClick(), icon);
            var fx = b.GetComponent<HoloHoverFx>();
            if (fx != null)
            {
                fx.tooltip = tooltip;
                fx.tooltipOffset = new Vector2(-40f, 50f);
                fx.BuildTooltip();
            }
            return b.gameObject;
        }

        // ------------------------------------------------------------------ Modos

        private void ApplyVisibility()
        {
            bool labels = _labelsVisible && !_diamMode && !_mechMode;
            foreach (LabelView v in _labelViews)
            {
                v.Line.enabled = labels;
                v.Chip.gameObject.SetActive(labels);
                v.Dot.gameObject.SetActive(labels);
            }
            bool diam = _diamMode && !_mechMode;
            foreach (LabelView v in _diamViews)
            {
                v.Line.enabled = diam;
                v.Chip.gameObject.SetActive(diam);
                v.Dot.gameObject.SetActive(diam);
            }
            foreach (var seg in _diamSegments) seg.line.enabled = diam;
            if (_btnNames != null) _btnNames.SetActive(!_mechMode);
            if (_btnDiam != null) _btnDiam.SetActive(!_mechMode);
        }

        private void ToggleDiameters()
        {
            _diamMode = !_diamMode;
            ApplyVisibility();
            if (_diamMode) Narrator.Speak("Diámetros pélvicos: conjugado obstétrico, transverso, biespinoso, bituberoso y anteroposterior del estrecho inferior.");
        }

        /// <summary>Muestra la pelvis translúcida con la animación de los mecanismos del parto (no requiere la pelota).</summary>
        public void ActivateMechanisms()
        {
            if (detector != null) detector.End();
            _active = true;
            _mechMode = true;
            _diamMode = false;
            _unlocked = true;
            if (placementAnchor != null)
                _root.transform.SetPositionAndRotation(placementAnchor.position - placementAnchor.forward * 0.02f, placementAnchor.rotation);
            _panelTitle.text = "MECANISMOS DEL PARTO";
            _panelSubtitle.text = "Presentación cefálica · vista lateral";
            SetGhost(true);
            _root.SetActive(true);
            _model.localPosition = _modelHome.position;
            _model.localRotation = HomeRotation;
            _model.localScale = _modelScale;
            ApplyVisibility();
            _mechControls.SetActive(true);
            _mech.Begin();
            StateChanged?.Invoke();
        }

        private void ExitMechanisms()
        {
            if (!_mechMode) return;
            _mechMode = false;
            if (_mech != null) _mech.Stop();
            if (_mechControls != null) _mechControls.SetActive(false);
            SetGhost(false);
            _panelTitle.text = "PELVIS ÓSEA FEMENINA";
            _panelSubtitle.text = "Vista anterior";
            Narrator.Stop();
            ApplyVisibility();
        }

        private void SetGhost(bool on)
        {
            Material ghost = theme != null ? theme.boneGhost : null;
            for (int i = 0; i < _modelRenderers.Length; i++)
            {
                if (_modelRenderers[i] == null) continue;
                if (on && ghost != null)
                {
                    var mats = new Material[_originalMaterials[i].Length];
                    for (int m = 0; m < mats.Length; m++) mats[m] = ghost;
                    _modelRenderers[i].sharedMaterials = mats;
                }
                else _modelRenderers[i].sharedMaterials = _originalMaterials[i];
            }
        }

        private void BuildMechanism()
        {
            _mech = gameObject.AddComponent<BirthMechanism>();
            _mech.Init(_model, theme != null ? theme.fetalHead : null, theme != null ? theme.glow : null);
            _mech.StepChanged += (i, title, desc) =>
            {
                _mechTitle.text = $"{title}";
                _mechDesc.text = desc;
                _mechPlay.SetIcon(_mech.Playing ? "pause" : "play");
                if (i > 0) Narrator.Speak(title.Substring(title.IndexOf('.') + 1).Trim() + ". " + desc);
            };

            Canvas c = HoloUI.CreateCanvas(_root.transform, "ControlesMecanismo", new Vector2(780, 150));
            c.transform.localPosition = new Vector3(0f, -0.335f, -0.03f);
            c.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            HoloUI.Panel(c.transform, "Fondo", Vector2.zero, new Vector2(780, 150));
            HoloUI.Button(c.transform, "Anterior", null, new Vector2(-330, 0), new Vector2(64, 64), () => { _mech.Prev(); }, "prev");
            _mechPlay = HoloUI.Button(c.transform, "Reproducir", null, new Vector2(-255, 0), new Vector2(64, 64), () =>
            {
                _mech.TogglePlay();
                _mechPlay.SetIcon(_mech.Playing ? "pause" : "play");
            }, "pause");
            HoloUI.Button(c.transform, "Siguiente", null, new Vector2(-180, 0), new Vector2(64, 64), () => { _mech.Next(); }, "next");
            _mechTitle = HoloUI.Text(c.transform, "Paso", "", 22, new Vector2(110, 40), new Vector2(500, 32), TextAlignmentOptions.Left,
                HoloColors.Accent, FontStyles.Bold);
            _mechDesc = HoloUI.Text(c.transform, "Descripcion", "", 17, new Vector2(110, -18), new Vector2(500, 80), TextAlignmentOptions.TopLeft,
                HoloColors.Text);
            _mechControls = c.gameObject;
            _mechControls.SetActive(false);
        }

        private void BuildDiameters()
        {
            Material lineMat = theme != null && theme.line != null ? theme.line : new Material(Shader.Find("Sprites/Default"));
            Material dotMat = theme != null && theme.glow != null ? theme.glow : lineMat;
            var gold = new Color(1f, 0.83f, 0.35f, 1f);
            foreach (var d in Diameters)
            {
                Transform a = null, b = null;
                foreach (Transform t in _model.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Diam_" + d.key + "_A") a = t;
                    if (t.name == "Diam_" + d.key + "_B") b = t;
                }
                if (a == null || b == null) continue;

                var lineGo = new GameObject("Diametro_" + d.key);
                lineGo.transform.SetParent(_root.transform, false);
                var line = lineGo.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.widthMultiplier = 0.0035f;
                line.numCapVertices = 4;
                line.sharedMaterial = lineMat;
                line.startColor = line.endColor = gold;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.enabled = false;
                _diamSegments.Add((a, b, line));

                var mid = new GameObject("Diam_" + d.key + "_Medio").transform;
                mid.SetParent(_model, false);
                mid.localPosition = (a.localPosition + b.localPosition) * 0.5f;
                LabelView view = MakeChip(d.text, mid, lineMat, dotMat, new Color(0.14f, 0.1f, 0.02f, 0.88f), gold);
                view.LocalPosition = mid.localPosition + d.offset;
                view.LineColor = new Color(1f, 0.85f, 0.4f, 0.8f);
                _diamViews.Add(view);
            }
            ApplyVisibility();
        }

        private Transform CreateModel(Transform parent)
        {
            GameObject model;
            if (theme != null && theme.pelvisModel != null)
            {
                model = Instantiate(theme.pelvisModel, parent, false);
                model.name = "Pelvis";
                FitToSize(model.transform, modelSize);
            }
            else
            {
                model = PlaceholderPelvis.Create(parent, theme != null ? theme.bone : null, theme != null ? theme.boneDetail : null);
            }
            return model.transform;
        }

        /// <summary>Escala un modelo importado para que su ancho mayor mida targetSize metros y lo centra.</summary>
        private static void FitToSize(Transform model, float targetSize)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (size > 1e-4f) model.localScale *= targetSize / size;
            b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            model.position += model.parent.position - b.center;
        }

        private static XRGrabInteractable MakeGrabbable(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (model.GetComponent<Collider>() == null && renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                var box = model.AddComponent<BoxCollider>();
                box.center = model.transform.InverseTransformPoint(b.center);
                Vector3 s = model.transform.lossyScale;
                box.size = new Vector3(b.size.x / Mathf.Max(1e-4f, s.x), b.size.y / Mathf.Max(1e-4f, s.y), b.size.z / Mathf.Max(1e-4f, s.z));
            }
            var rb = model.GetComponent<Rigidbody>();
            if (rb == null) rb = model.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            var grab = model.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.retainTransformParent = true;
            grab.useDynamicAttach = true;
            grab.smoothPosition = true;
            grab.smoothRotation = true;
            grab.smoothPositionAmount = 14f;
            grab.smoothRotationAmount = 14f;
            return grab;
        }

        private void BuildLabels()
        {
            Material lineMat = theme != null && theme.line != null ? theme.line : new Material(Shader.Find("Sprites/Default"));
            Material dotMat = theme != null && theme.glow != null ? theme.glow : lineMat;
            foreach (AnatomyLabel label in labels)
            {
                var view = new LabelView { Label = label, Anchor = FindAnchor(label) };

                // Posición inicial (como en la imagen de referencia) expresada en el espacio del modelo, para que acompañe al girarlo.
                Vector3 rootPoint = new Vector3(label.panelPosition.x * 0.001f, label.panelPosition.y * 0.001f, _modelPivot.localPosition.z);
                Vector3 labelLocal = _model.InverseTransformPoint(_root.transform.TransformPoint(rootPoint));
                Vector3 anchorLocal = _model.InverseTransformPoint(view.Anchor.position);
                // Un poco más cerca de su estructura para que al girar no se alejen demasiado.
                view.LocalPosition = Vector3.Lerp(anchorLocal, labelLocal, 0.85f);

                LabelView chipView = MakeChip(label.text, view.Anchor, lineMat, dotMat, new Color(0.03f, 0.1f, 0.22f, 0.82f), HoloColors.Accent);
                chipView.Label = label;
                chipView.LocalPosition = view.LocalPosition;
                view = chipView;
                _labelViews.Add(view);
            }
        }

        private LabelView MakeChip(string text, Transform anchor, Material lineMat, Material dotMat, Color fill, Color border)
        {
            var view = new LabelView { Anchor = anchor };
            int lines = text.Split('\n').Length;
            float width = 0f;
            foreach (string l in text.Split('\n')) width = Mathf.Max(width, l.Length);
            Vector2 size = new Vector2(width * 9.5f + 28f, 26f * lines + 10f);
            Canvas chipCanvas = HoloUI.CreateCanvas(_root.transform, "Etiqueta_" + text.Split('\n')[0], size);
            var appear = chipCanvas.GetComponent<HoloAppear>();
            appear.delay = 0.35f + (_labelViews.Count + _diamViews.Count) * 0.07f;
            appear.scan = false;
            HoloPanel chip = HoloUI.Panel(chipCanvas.transform, "Chip", Vector2.zero, size, fill, border);
            chip.Background.raycastTarget = false;
            HoloUI.Text(chip.transform, "Texto", text, 17, Vector2.zero, size - new Vector2(10f, 4f), TextAlignmentOptions.Center,
                HoloColors.Text, FontStyles.Bold);
            view.Chip = (RectTransform)chipCanvas.transform;
            view.Group = chipCanvas.GetComponent<CanvasGroup>();

            var lineGo = new GameObject("Linea_" + text.Split('\n')[0]);
            lineGo.transform.SetParent(_root.transform, false);
            view.Line = lineGo.AddComponent<LineRenderer>();
            view.Line.useWorldSpace = true;
            view.Line.positionCount = 2;
            view.Line.widthMultiplier = 0.0018f;
            view.Line.numCapVertices = 3;
            view.Line.sharedMaterial = lineMat;
            view.Line.startColor = view.Line.endColor = new Color(0.85f, 0.97f, 1f, 0.9f);
            view.Line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "Punto_" + text.Split('\n')[0];
            Destroy(dot.GetComponent<Collider>());
            dot.transform.SetParent(_root.transform, false);
            dot.transform.localScale = Vector3.one * 0.009f;
            var dr = dot.GetComponent<Renderer>();
            dr.sharedMaterial = dotMat;
            dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.Dot = dot.transform;
            return view;
        }

        private Transform FindAnchor(AnatomyLabel label)
        {
            string key = "Label_" + label.text.Split('\n')[0];
            foreach (Transform t in _model.GetComponentsInChildren<Transform>(true))
                if (string.Equals(t.name, key, StringComparison.OrdinalIgnoreCase)) return t;
            var anchor = new GameObject("Anchor_" + label.text.Split('\n')[0]).transform;
            anchor.SetParent(_model, false);
            anchor.position = _model.position + _modelPivot.rotation * label.anchor;
            return anchor;
        }
    }
}
