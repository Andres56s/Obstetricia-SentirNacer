using System.Collections.Generic;
using UnityEngine;

namespace Obstetricia.Modules
{
    /// <summary>
    /// Mano 3D que muestra un gesto en el tutorial. Cada pose son giros (grados) sobre el reposo del modelo: índice, medio,
    /// anular y meñique (3 articulaciones cada uno) y pulgar (metacarpo x, y, z, proximal, distal).
    /// </summary>
    public class ManoDemo : MonoBehaviour
    {
        public static readonly float[] Reposo = { -4, -6, -4, -4, -6, -4, -2, -4, -2, 0, 0, 0, 0, 0, 0, 0, 0 };
        public static readonly float[] Abierta = { -16, -22, -18, -18, -26, -14, -12, -26, -14, -8, -26, -14, -6, -6, 0, -8, -8 };
        public static readonly float[] Pellizco = { 35, 30, 20, -6, -8, -6, -10, -14, -10, -12, -18, -12, 17, -14, -10, 6, -10 };
        public static readonly float[] Apuntar = { -16, -28, -26, 52, 46, 30, 58, 46, 30, 58, 46, 30, 10, 6, 0, 14, 14 };
        public static readonly float[] Puno = { 58, 48, 30, 58, 46, 30, 60, 46, 28, 58, 44, 28, 16, 10, 0, 22, 24 };

        private static readonly string[] Dedos = { "Index", "Middle", "Ring", "Little" };
        private static readonly string[] Tramos = { "Proximal", "Intermediate", "Distal" };
        private readonly List<Transform> _joints = new List<Transform>();
        private readonly List<Quaternion> _rest = new List<Quaternion>();
        private readonly float[] _pose = new float[17];

        public Transform IndiceYema { get; private set; }
        public Transform PulgarYema { get; private set; }

        /// <summary>Prepara los huesos del modelo ya instanciado como hijo.</summary>
        public void Init()
        {
            _joints.Clear();
            _rest.Clear();
            foreach (string d in Dedos)
                foreach (string t in Tramos) Add(d + t);
            Add("ThumbMetacarpal");
            Add("ThumbProximal");
            Add("ThumbDistal");
            IndiceYema = Find("IndexTip");
            PulgarYema = Find("ThumbTip");
        }

        private void Add(string n)
        {
            Transform t = Find(n);
            _joints.Add(t);
            _rest.Add(t != null ? t.localRotation : Quaternion.identity);
        }

        private Transform Find(string n)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith("_" + n)) return t;
            return null;
        }

        /// <summary>Mezcla dos poses (k de 0 a 1).</summary>
        public void Mostrar(float[] a, float[] b, float k)
        {
            for (int i = 0; i < _pose.Length; i++) _pose[i] = Mathf.Lerp(a[i], b[i], k);
            for (int i = 0; i < 12; i++) Set(i, Quaternion.Euler(_pose[i], 0, 0));
            Set(12, Quaternion.Euler(_pose[12], _pose[13], _pose[14]));
            Set(13, Quaternion.Euler(_pose[15], 0, 0));
            Set(14, Quaternion.Euler(_pose[16], 0, 0));
        }

        private void Set(int i, Quaternion q)
        {
            if (_joints[i] != null) _joints[i].localRotation = _rest[i] * q;
        }
    }
}
