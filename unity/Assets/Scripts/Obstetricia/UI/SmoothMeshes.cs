using System.Collections.Generic;
using UnityEngine;

namespace Obstetricia.UI
{
    /// <summary>
    /// Mallas procedurales lisas. El cilindro tiene las mismas proporciones que el primitivo de Unity
    /// (diámetro 1, alto 2, centrado) para que las escalas existentes sigan valiendo, pero con muchos más lados
    /// y un bisel suave en los bordes superior e inferior.
    /// </summary>
    public static class SmoothMeshes
    {
        private static Mesh _cylinder;

        public static Mesh Cylinder => _cylinder != null ? _cylinder : (_cylinder = BuildCylinder(128, 0.06f));

        /// <param name="segments">Lados alrededor.</param>
        /// <param name="bevel">Bisel como fracción del radio (0 = arista viva).</param>
        private static Mesh BuildCylinder(int segments, float bevel)
        {
            const float r = 0.5f, h = 1f;
            float b = r * bevel;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Perfil (radio, altura, normal) de abajo hacia arriba, con bisel de 3 pasos en cada borde.
            var profile = new List<(float rad, float y, Vector2 n)>();
            const int bevelSteps = 3;
            for (int i = 0; i <= bevelSteps; i++)
            {
                float a = Mathf.Lerp(-90f, 0f, i / (float)bevelSteps) * Mathf.Deg2Rad;
                profile.Add((r - b + Mathf.Cos(a) * b, -h + b + Mathf.Sin(a) * b, new Vector2(Mathf.Cos(a), Mathf.Sin(a))));
            }
            for (int i = 0; i <= bevelSteps; i++)
            {
                float a = Mathf.Lerp(0f, 90f, i / (float)bevelSteps) * Mathf.Deg2Rad;
                profile.Add((r - b + Mathf.Cos(a) * b, h - b + Mathf.Sin(a) * b, new Vector2(Mathf.Cos(a), Mathf.Sin(a))));
            }

            // Lateral
            int rings = profile.Count;
            for (int s = 0; s <= segments; s++)
            {
                float ang = s / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                for (int k = 0; k < rings; k++)
                {
                    var p = profile[k];
                    verts.Add(new Vector3(cos * p.rad, p.y, sin * p.rad));
                    normals.Add(new Vector3(cos * p.n.x, p.n.y, sin * p.n.x).normalized);
                    uvs.Add(new Vector2(s / (float)segments, (p.y + h) / (2f * h)));
                }
            }
            for (int s = 0; s < segments; s++)
            for (int k = 0; k < rings - 1; k++)
            {
                int a0 = s * rings + k, a1 = a0 + 1, b0 = a0 + rings, b1 = b0 + 1;
                tris.Add(a0); tris.Add(a1); tris.Add(b0);
                tris.Add(a1); tris.Add(b1); tris.Add(b0);
            }

            // Tapas
            AddCap(verts, normals, uvs, tris, segments, r - b, h, true);
            AddCap(verts, normals, uvs, tris, segments, r - b, -h, false);

            var mesh = new Mesh { name = "SmoothCylinder" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void AddCap(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, int segments, float radius, float y, bool up)
        {
            int center = v.Count;
            Vector3 normal = up ? Vector3.up : Vector3.down;
            v.Add(new Vector3(0f, y, 0f)); n.Add(normal); uv.Add(new Vector2(0.5f, 0.5f));
            for (int s = 0; s <= segments; s++)
            {
                float ang = s / (float)segments * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(ang) * radius, y, Mathf.Sin(ang) * radius));
                n.Add(normal);
                uv.Add(new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
            }
            for (int s = 0; s < segments; s++)
            {
                int a = center + 1 + s, b = a + 1;
                if (up) { t.Add(center); t.Add(b); t.Add(a); }
                else { t.Add(center); t.Add(a); t.Add(b); }
            }
        }
    }
}
