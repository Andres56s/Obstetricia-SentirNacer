using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Obstetricia.Tools
{
    /// <summary>
    /// Lápiz 3D: con el modo lápiz activo, mantener el gatillo del control derecho dibuja trazos en el aire
    /// (para señalar estructuras de la pelvis o tomar apuntes). Permite deshacer y borrar todo.
    /// </summary>
    public class PenDrawer : MonoBehaviour
    {
        [SerializeField, Tooltip("Punta del lápiz (normalmente el control derecho).")]
        private Transform tip;
        [SerializeField] private Vector3 tipOffset = new Vector3(0f, 0f, 0.06f);
        [SerializeField] private InputActionProperty drawAction = new InputActionProperty(
            new InputAction("Draw", InputActionType.Value, "<XRController>{RightHand}/trigger", expectedControlType: "Axis"));
        [Header("Mano izquierda (modo zurdo) y seguimiento de manos")]
        [SerializeField, Tooltip("Control izquierdo (modo zurdo).")]
        private Transform leftTip;
        [SerializeField, Tooltip("Punta del índice derecho (Poke Interactor de la mano derecha).")]
        private Transform rightHandTip;
        [SerializeField, Tooltip("Punta del índice izquierdo (Poke Interactor de la mano izquierda).")]
        private Transform leftHandTip;
        [SerializeField] private InputActionProperty leftDrawAction = new InputActionProperty(
            new InputAction("DrawLeft", InputActionType.Value, "<XRController>{LeftHand}/trigger", expectedControlType: "Axis"));
        [SerializeField] private InputActionProperty rightPinchAction = new InputActionProperty(
            new InputAction("PinchRight", InputActionType.Value, "<MetaAimHand>{RightHand}/pinchStrengthIndex", expectedControlType: "Axis"));
        [SerializeField] private InputActionProperty leftPinchAction = new InputActionProperty(
            new InputAction("PinchLeft", InputActionType.Value, "<MetaAimHand>{LeftHand}/pinchStrengthIndex", expectedControlType: "Axis"));
        [SerializeField] private float pressThreshold = 0.5f;
        [SerializeField] private float minSegment = 0.004f;
        [SerializeField] private float width = 0.004f;
        [SerializeField] private Color color = new Color(0.35f, 0.85f, 1f, 1f);
        [SerializeField] private Material lineMaterial;
        [SerializeField] private int maxStrokes = 60;

        private readonly List<LineRenderer> _strokes = new List<LineRenderer>();
        private LineRenderer _current;
        private Transform _strokesRoot;
        private GameObject _cursor;

        /// <summary>Activa/desactiva el modo lápiz.</summary>
        public bool PenEnabled
        {
            get => enabled;
            set
            {
                enabled = value;
                if (_cursor != null) _cursor.SetActive(value);
            }
        }

        public int StrokeCount => _strokes.Count;

        private void Awake()
        {
            _strokesRoot = new GameObject("PenStrokes").transform;
            if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
            _cursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _cursor.name = "PenTip";
            Destroy(_cursor.GetComponent<Collider>());
            _cursor.transform.localScale = Vector3.one * 0.012f;
            var r = _cursor.GetComponent<Renderer>();
            r.sharedMaterial = lineMaterial;
            r.material.color = color;
            _cursor.SetActive(false);
            enabled = false;
        }

        private void OnEnable()
        {
            if (drawAction.action != null) drawAction.action.Enable();
            leftDrawAction.action?.Enable();
            rightPinchAction.action?.Enable();
            leftPinchAction.action?.Enable();
        }

        private void OnDisable()
        {
            EndStroke();
            if (_cursor != null) _cursor.SetActive(false);
        }

        private void Update()
        {
            // Mano activa: derecha o izquierda (modo zurdo); con seguimiento de manos se dibuja con la punta del índice.
            bool left = Obstetricia.Core.AppSettings.LeftHanded;
            Transform handTip = left ? leftHandTip : rightHandTip;
            bool usingHand = handTip != null && handTip.gameObject.activeInHierarchy;
            Transform activeTip = usingHand ? handTip : (left ? leftTip : tip);
            if (activeTip == null) return;
            Vector3 p = usingHand ? activeTip.position : activeTip.TransformPoint(tipOffset);
            _cursor.transform.position = p;

            InputAction act = usingHand ? (left ? leftPinchAction.action : rightPinchAction.action)
                                        : (left ? leftDrawAction.action : drawAction.action);
            float value = act != null ? act.ReadValue<float>() : 0f;
            bool pressed = value >= pressThreshold;
            if (usingHand)
            {
                // Con manos: se dibuja solo con las yemas realmente juntas.
                var lector = Obstetricia.Core.LectorMano.De(left);
                if (lector != null) pressed = lector.Pellizco;
            }
            if (pressed && _current == null) BeginStroke(p);
            else if (pressed) Extend(p);
            else if (!pressed && _current != null) EndStroke();
        }

        private void BeginStroke(Vector3 p)
        {
            if (_strokes.Count >= maxStrokes) Undo();
            var go = new GameObject($"Stroke_{_strokes.Count + 1}");
            go.transform.SetParent(_strokesRoot, false);
            _current = go.AddComponent<LineRenderer>();
            _current.useWorldSpace = true;
            _current.sharedMaterial = lineMaterial;
            _current.startColor = _current.endColor = color;
            _current.widthMultiplier = width;
            _current.numCapVertices = 4;
            _current.numCornerVertices = 2;
            _current.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _current.positionCount = 1;
            _current.SetPosition(0, p);
            _strokes.Add(_current);
        }

        private void Extend(Vector3 p)
        {
            int n = _current.positionCount;
            if ((_current.GetPosition(n - 1) - p).sqrMagnitude < minSegment * minSegment) return;
            _current.positionCount = n + 1;
            _current.SetPosition(n, p);
        }

        private void EndStroke() => _current = null;

        /// <summary>Elimina el último trazo.</summary>
        public void Undo()
        {
            if (_strokes.Count == 0) return;
            LineRenderer last = _strokes[_strokes.Count - 1];
            _strokes.RemoveAt(_strokes.Count - 1);
            if (last == _current) _current = null;
            if (last != null) Destroy(last.gameObject);
        }

        /// <summary>Borra todos los trazos.</summary>
        public void Clear()
        {
            while (_strokes.Count > 0) Undo();
        }
    }
}
