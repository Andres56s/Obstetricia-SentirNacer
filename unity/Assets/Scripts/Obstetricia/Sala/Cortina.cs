using Obstetricia.UI;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Obstetricia.Sala
{
    /// <summary>
    /// Cortina de privacidad: cuelga de un riel y se abre o cierra al tocarla (o desde el menú). La tela se pliega de verdad
    /// y se mece un poco. El riel corre por el eje Z local: 0 es el muro y -largo el extremo libre.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    public class Cortina : MonoBehaviour
    {
        [SerializeField] private float largo = 2.6f;
        [SerializeField] private float alto = 2.05f;
        [SerializeField, Tooltip("Altura del borde inferior sobre el piso.")] private float desdePiso = 0.28f;

        private const int Cols = 96, Rows = 8, Pliegues = 13;
        private Mesh _mesh;
        private Vector3[] _v;
        private BoxCollider _box;
        private float _open = 1f, _target = 1f, _energy;

        public bool Abierta => _target > 0.5f;

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            BuildMesh();
            var i = GetComponent<XRSimpleInteractable>();
            if (i == null) i = gameObject.AddComponent<XRSimpleInteractable>();
            i.selectEntered.AddListener(_ => Alternar());
        }

        public void Alternar()
        {
            _target = Abierta ? 0f : 1f;
            HoloFeedback.Play(Abierta ? HoloFeedback.Sound.Open : HoloFeedback.Sound.Close, 0.4f);
        }

        private void BuildMesh()
        {
            _mesh = new Mesh { name = "Cortina" };
            _mesh.MarkDynamic();
            int one = (Cols + 1) * (Rows + 1);
            _v = new Vector3[one * 2]; // dos caras, para que ambas reciban bien la luz
            var uv = new Vector2[_v.Length];
            var tri = new int[Cols * Rows * 12];
            for (int c = 0; c <= Cols; c++)
                for (int r = 0; r <= Rows; r++)
                    uv[c * (Rows + 1) + r] = uv[one + c * (Rows + 1) + r] = new Vector2(c / (float)Cols, r / (float)Rows);
            int t = 0;
            for (int c = 0; c < Cols; c++)
                for (int r = 0; r < Rows; r++)
                {
                    int a = c * (Rows + 1) + r, b = a + Rows + 1;
                    tri[t++] = a; tri[t++] = a + 1; tri[t++] = b;
                    tri[t++] = a + 1; tri[t++] = b + 1; tri[t++] = b;
                    tri[t++] = one + a; tri[t++] = one + b; tri[t++] = one + a + 1;
                    tri[t++] = one + a + 1; tri[t++] = one + b; tri[t++] = one + b + 1;
                }
            Deform();
            _mesh.vertices = _v;
            _mesh.uv = uv;
            _mesh.triangles = tri;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        private void Deform()
        {
            float span = Mathf.Lerp(largo, largo * 0.16f, _open);
            float amp = Mathf.Lerp(0.035f, 0.1f, _open);
            float time = Time.time;
            for (int c = 0; c <= Cols; c++)
            {
                float u = c / (float)Cols;
                float fold = Mathf.Sin(u * Pliegues * Mathf.PI * 2f);
                for (int r = 0; r <= Rows; r++)
                {
                    float h = r / (float)Rows;
                    float sway = (1f - h) * (0.006f + _energy * 0.05f) * Mathf.Sin(time * 1.4f + u * 5f);
                    _v[c * (Rows + 1) + r] = _v[_v.Length / 2 + c * (Rows + 1) + r] = new Vector3(fold * amp * Mathf.Lerp(1.15f, 0.8f, h) + sway, desdePiso + h * alto, -u * span);
                }
            }
            _box.size = new Vector3(0.16f, alto, span);
            _box.center = new Vector3(0f, desdePiso + alto / 2f, -span / 2f);
        }

        private void Update()
        {
            float before = _open;
            _open = Mathf.MoveTowards(_open, _target, Time.deltaTime / 1.6f);
            _energy = Mathf.MoveTowards(_energy, Mathf.Approximately(before, _open) ? 0f : 1f, Time.deltaTime * 1.2f);
            if (_energy <= 0f) return; // quieta: no gastar CPU
            Deform();
            _mesh.vertices = _v;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }
    }
}
