using UnityEngine;

namespace Obstetricia.Modules
{
    /// <summary>
    /// Pelvis provisional construida con primitivas (≈ 0.28 m de ancho, cara anterior hacia -Z) para probar el
    /// flujo mientras no se importe el modelo anatómico detallado. Incluye hijos "Label_*" para las etiquetas.
    /// </summary>
    public static class PlaceholderPelvis
    {
        public static GameObject Create(Transform parent, Material bone, Material detail)
        {
            if (bone == null)
            {
                bone = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                bone.SetColor("_BaseColor", new Color(0.93f, 0.89f, 0.8f));
            }
            if (detail == null) detail = bone;

            var root = new GameObject("Pelvis (provisional)");
            root.transform.SetParent(parent, false);

            // Sacro (posterior, centro) y cóccix
            Part(root, "Sacro", PrimitiveType.Sphere, bone, new Vector3(0f, 0.035f, 0.04f), new Vector3(0.085f, 0.12f, 0.045f), new Vector3(-15f, 0f, 0f));
            Part(root, "Coccix", PrimitiveType.Capsule, bone, new Vector3(0f, -0.04f, 0.055f), new Vector3(0.018f, 0.025f, 0.018f), new Vector3(-30f, 0f, 0f));

            for (int s = -1; s <= 1; s += 2)
            {
                string side = s < 0 ? "Der" : "Izq";
                // Ilion (ala ilíaca abierta hacia adelante)
                Part(root, "Ilion_" + side, PrimitiveType.Sphere, bone, new Vector3(0.095f * s, 0.06f, 0.005f),
                    new Vector3(0.13f, 0.11f, 0.025f), new Vector3(0f, -35f * s, 18f * s));
                // Cresta ilíaca
                Part(root, "Cresta_" + side, PrimitiveType.Capsule, bone, new Vector3(0.1f * s, 0.11f, 0.005f),
                    new Vector3(0.018f, 0.07f, 0.018f), new Vector3(0f, -35f * s, 90f - 15f * s));
                // Acetábulo (cavidad de la cadera)
                Part(root, "Acetabulo_" + side, PrimitiveType.Sphere, detail, new Vector3(0.088f * s, -0.035f, -0.025f),
                    new Vector3(0.05f, 0.05f, 0.04f), Vector3.zero);
                // Rama superior del pubis
                Part(root, "RamaPubis_" + side, PrimitiveType.Capsule, bone, new Vector3(0.045f * s, -0.055f, -0.048f),
                    new Vector3(0.02f, 0.05f, 0.02f), new Vector3(0f, 25f * s, 70f * s));
                // Isquion / rama isquiopubiana (forma el ángulo subpúbico abierto > 90°)
                Part(root, "Isquion_" + side, PrimitiveType.Capsule, bone, new Vector3(0.06f * s, -0.085f, -0.02f),
                    new Vector3(0.02f, 0.05f, 0.02f), new Vector3(0f, 0f, -40f * s));
                // Agujero obturador (marco)
                Part(root, "Obturador_" + side, PrimitiveType.Cylinder, detail, new Vector3(0.06f * s, -0.07f, -0.04f),
                    new Vector3(0.035f, 0.003f, 0.045f), new Vector3(90f, 0f, 20f * s));
            }

            // Sínfisis del pubis
            Part(root, "Sinfisis", PrimitiveType.Cube, detail, new Vector3(0f, -0.07f, -0.058f), new Vector3(0.016f, 0.04f, 0.018f), Vector3.zero);

            Marker(root, "Label_Sacro", new Vector3(0f, 0.05f, 0.02f));
            Marker(root, "Label_Ilion", new Vector3(0.13f, 0.07f, -0.005f));
            Marker(root, "Label_Acetábulo", new Vector3(0.088f, -0.035f, -0.045f));
            Marker(root, "Label_Sínfisis del pubis", new Vector3(0f, -0.07f, -0.068f));
            Marker(root, "Label_Ángulo subpúbico", new Vector3(0f, -0.1f, -0.055f));
            return root;
        }

        private static void Part(GameObject root, string name, PrimitiveType type, Material mat, Vector3 pos, Vector3 scale, Vector3 euler)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void Marker(GameObject root, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root.transform, false);
            t.localPosition = pos;
        }
    }
}
