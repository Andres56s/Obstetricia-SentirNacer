using System;
using Obstetricia.UI;
using UnityEngine;

namespace Obstetricia.Modules
{
    /// <summary>
    /// Animación de los movimientos cardinales del parto en presentación cefálica: una cabeza fetal recorre el
    /// canal de la pelvis (eje de Carus) mostrando encajamiento, descenso, flexión, rotación interna, extensión,
    /// rotación externa y expulsión. Las coordenadas están en el espacio del prefab de la pelvis (~0,3 m de ancho,
    /// -Z = anterior, +Y = craneal), así que la cabeza acompaña a la pelvis si se gira.
    /// </summary>
    public class BirthMechanism : MonoBehaviour
    {
        public struct Step
        {
            public string Title;
            public string Description;
            public Vector3 Position;     // centro de la cabeza
            public Vector3 Lead;         // dirección de avance (la parte que "lidera")
            public Vector3 Occiput;      // hacia dónde mira el occipucio (antes de la flexión)
            public float Flexion;        // grados: + flexión (mentón al pecho), − extensión
            public float Scale;
        }

        public static readonly Step[] Steps =
        {
            new Step { Title = "Inicio", Description = "La cabeza fetal se ubica sobre el estrecho superior, en posición transversa.",
                Position = new Vector3(0f, 0.03f, -0.16f), Lead = new Vector3(0f, -0.15f, 1f), Occiput = Vector3.right, Flexion = 0f, Scale = 1f },
            new Step { Title = "1. Encajamiento", Description = "El diámetro biparietal atraviesa el estrecho superior de la pelvis.",
                Position = new Vector3(0f, 0.012f, -0.035f), Lead = new Vector3(0f, -0.25f, 1f), Occiput = Vector3.right, Flexion = 10f, Scale = 1f },
            new Step { Title = "2. Descenso", Description = "La cabeza avanza por el canal del parto, impulsada por las contracciones y el pujo.",
                Position = new Vector3(0f, -0.018f, 0.012f), Lead = new Vector3(0f, -0.75f, 0.65f), Occiput = Vector3.right, Flexion = 12f, Scale = 1f },
            new Step { Title = "3. Flexión", Description = "El mentón se acerca al tórax: se presenta el diámetro más pequeño (suboccipitobregmático).",
                Position = new Vector3(0f, -0.026f, 0.016f), Lead = new Vector3(0f, -0.8f, 0.6f), Occiput = Vector3.right, Flexion = 38f, Scale = 1f },
            new Step { Title = "4. Rotación interna", Description = "El occipucio gira hacia la sínfisis del pubis (occípito anterior).",
                Position = new Vector3(0f, -0.05f, 0.018f), Lead = new Vector3(0f, -1f, 0.15f), Occiput = Vector3.back, Flexion = 38f, Scale = 1f },
            new Step { Title = "5. Extensión", Description = "La nuca se apoya bajo el pubis y la cabeza se extiende: nacen frente, cara y mentón.",
                Position = new Vector3(0f, -0.115f, -0.05f), Lead = new Vector3(0f, -0.55f, -0.85f), Occiput = Vector3.back, Flexion = -45f, Scale = 1f },
            new Step { Title = "6. Rotación externa", Description = "La cabeza gira hacia el lado del dorso fetal (restitución) mientras rotan los hombros.",
                Position = new Vector3(0f, -0.15f, -0.1f), Lead = new Vector3(0f, -0.5f, -0.85f), Occiput = Vector3.right, Flexion = -15f, Scale = 1f },
            new Step { Title = "7. Expulsión", Description = "Sale el hombro anterior, luego el posterior y finalmente el resto del cuerpo.",
                Position = new Vector3(0f, -0.23f, -0.2f), Lead = new Vector3(0f, -0.45f, -0.9f), Occiput = Vector3.right, Flexion = -15f, Scale = 0.001f },
        };

        [SerializeField] private float moveSeconds = 1.4f;
        [SerializeField] private float holdSeconds = 2.6f;

        private Transform _head;
        private int _index;
        private bool _playing;
        private float _t = 1f;
        private float _hold;
        private Vector3 _fromPos;
        private Quaternion _fromRot;
        private float _fromScale;

        /// <summary>Se invoca al llegar a un paso: (índice, título, descripción).</summary>
        public event Action<int, string, string> StepChanged;

        public bool Playing => _playing;
        public int Index => _index;

        /// <summary>Crea la cabeza fetal como hija de la pelvis (espacio del prefab).</summary>
        public void Init(Transform pelvis, Material skin, Material marker)
        {
            _head = new GameObject("CabezaFetal").transform;
            _head.SetParent(pelvis, false);

            // Ovoide: X = biparietal (~9,3 cm), Y = vértex, Z = occípito-frontal (~11 cm).
            GameObject skull = Part(PrimitiveType.Sphere, _head, "Craneo", Vector3.zero, new Vector3(0.088f, 0.094f, 0.108f), skin);
            // Sutura sagital (línea sobre el vértex, de adelante hacia atrás).
            Part(PrimitiveType.Cube, skull.transform, "SuturaSagital", new Vector3(0f, 0.49f, 0f), new Vector3(0.05f, 0.03f, 0.75f), marker);
            // Occipucio marcado (punto de referencia de las rotaciones).
            Part(PrimitiveType.Sphere, skull.transform, "Occipucio", new Vector3(0f, 0.28f, 0.44f), Vector3.one * 0.2f, marker);
            // Cara (nariz y mentón) para ver la flexión y extensión.
            Part(PrimitiveType.Sphere, skull.transform, "Nariz", new Vector3(0f, -0.12f, -0.5f), new Vector3(0.12f, 0.14f, 0.12f), skin);
            Part(PrimitiveType.Sphere, skull.transform, "Menton", new Vector3(0f, -0.42f, -0.36f), new Vector3(0.22f, 0.16f, 0.2f), skin);
            _head.gameObject.SetActive(false);
        }

        private static GameObject Part(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public void Begin()
        {
            if (_head == null) return;
            _head.gameObject.SetActive(true);
            _index = 0;
            Apply(Steps[0], 1f, true);
            _playing = true;
            _hold = 0f;
            StepChanged?.Invoke(_index, Steps[_index].Title, Steps[_index].Description);
        }

        public void Stop()
        {
            _playing = false;
            if (_head != null) _head.gameObject.SetActive(false);
        }

        public void TogglePlay() => _playing = !_playing;

        public void Next() => GoTo(_index + 1 >= Steps.Length ? 0 : _index + 1);

        public void Prev() => GoTo(Mathf.Max(0, _index - 1));

        private void GoTo(int index)
        {
            _fromPos = _head.localPosition;
            _fromRot = _head.localRotation;
            _fromScale = _head.localScale.x;
            _index = index;
            _t = index == 0 ? 1f : 0f;
            if (index == 0) Apply(Steps[0], 1f, true);
            _hold = 0f;
            StepChanged?.Invoke(_index, Steps[_index].Title, Steps[_index].Description);
        }

        private void Update()
        {
            if (_head == null || !_head.gameObject.activeSelf) return;
            if (_t < 1f)
            {
                _t += Time.unscaledDeltaTime / moveSeconds;
                Apply(Steps[_index], Ease.InOutSine(Mathf.Clamp01(_t)), false);
                return;
            }
            if (!_playing) return;
            _hold += Time.unscaledDeltaTime;
            if (_hold >= (_index == Steps.Length - 1 ? holdSeconds + 1f : holdSeconds)) Next();
        }

        private void Apply(Step s, float k, bool snap)
        {
            Quaternion target = Orientation(s);
            if (snap)
            {
                _head.localPosition = s.Position;
                _head.localRotation = target;
                _head.localScale = Vector3.one * s.Scale;
                return;
            }
            _head.localPosition = Vector3.Lerp(_fromPos, s.Position, k);
            _head.localRotation = Quaternion.Slerp(_fromRot, target, k);
            _head.localScale = Vector3.one * Mathf.Lerp(_fromScale, s.Scale, k);
        }

        /// <summary>Occipucio hacia "Occiput" (ortogonal al avance) inclinado hacia el avance según la flexión.</summary>
        private static Quaternion Orientation(Step s)
        {
            Vector3 lead = s.Lead.normalized;
            Vector3 occ = Vector3.ProjectOnPlane(s.Occiput, lead).normalized;
            if (occ.sqrMagnitude < 1e-4f) occ = Vector3.Cross(lead, Vector3.right).normalized;
            float f = s.Flexion * Mathf.Deg2Rad;
            // Flexión: el occipucio pasa a liderar; extensión: se aleja del avance.
            Vector3 forward = (occ * Mathf.Cos(f) + lead * Mathf.Sin(f)).normalized;
            Vector3 up = (lead * Mathf.Cos(f) - occ * Mathf.Sin(f)).normalized;
            return Quaternion.LookRotation(forward, up);
        }
    }
}
