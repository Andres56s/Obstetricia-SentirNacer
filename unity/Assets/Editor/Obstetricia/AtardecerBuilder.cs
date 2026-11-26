#if UNITY_EDITOR
using System.Collections.Generic;
using Obstetricia.Sala;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ObstetriciaSetup
{
    /// <summary>
    /// Agrega (o actualiza) el atardecer sobre la sala existente sin mover ni borrar el mobiliario: solo reemplaza el objeto
    /// "Atardecer" y el sombreado de muros, piso y techo (conservando su textura).
    /// </summary>
    public static class AtardecerBuilder
    {
        private const string Dir = "Assets/Obstetricia/Sala";
        private const float W = 6f, Z0 = -2f, H = 2.7f;
        private static readonly Vector4 Win = new Vector4(-2.38f, 2.42f, 0.65f, 2.25f); // x0, x1, y0, y1
        private static readonly Vector3 Sun = new Vector3(0.332f, -0.242f, 0.912f);

        [MenuItem("Tools/MR Setup/7 - Aplicar atardecer a la sala virtual")]
        public static void ApplyToScene()
        {
            foreach (GameObject g in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var sala = g.GetComponent<SalaVirtual>();
                if (sala == null) continue;
                Apply(sala);
                EditorSceneManager.MarkSceneDirty(g.scene);
                EditorSceneManager.SaveScene(g.scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[Obstetricia] Atardecer aplicado a la sala.");
                return;
            }
            Debug.LogError("[Obstetricia] No hay sala virtual en la escena.");
        }

        public static void Apply(SalaVirtual sala)
        {
            Import();
            Transform c = sala.transform.Find("Contenido");
            Transform arq = c.Find("Arquitectura");
            Transform old = c.Find("Atardecer");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform at = Empty("Atardecer", c, Vector3.zero);

            // ---------------- Muros, piso y techo
            foreach (string n in new[] { "Pared_Fondo", "Pared_Derecha", "Pared_Izquierda", "Pared_Ventanal", "Piso", "Cielo" })
            {
                Transform t = arq.Find(n);
                if (t == null) continue;
                var r = t.GetComponent<MeshRenderer>();
                Material m = r.sharedMaterial;
                Texture tex = m.GetTexture("_BaseMap");
                Vector2 tile = m.GetTextureScale("_BaseMap"), off = m.GetTextureOffset("_BaseMap");
                Color col = m.GetColor("_BaseColor");
                m.shader = Shader.Find("MR/MuroSala");
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", tile);
                m.SetTextureOffset("_BaseMap", off);
                m.SetColor("_BaseColor", col);
                m.SetTexture("_LuzMap", Tex("Luz_" + n));
                m.SetFloat("_LuzFuerza", 1.5f);
                m.SetFloat("_Brillo", n == "Piso" ? 0.32f : 0f);
                m.renderQueue = -1;
                EditorUtility.SetDirty(m);
            }

            // Muro del ventanal con un hueco real (las UV siguen cubriendo el muro completo)
            Transform wv = arq.Find("Pared_Ventanal");
            if (wv != null) wv.GetComponent<MeshFilter>().sharedMesh = SaveMesh("MuroConHueco", WallWithHole());
            foreach (string n in new[] { "VentanalVista", "VentanalMarco" })
            {
                Transform t = arq.Find(n);
                if (t != null) t.gameObject.SetActive(false);
            }
            Material blanco = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Materiales/Blanco.mat");
            float cx = (Win.x + Win.y) / 2f, cy = (Win.z + Win.w) / 2f, ww = Win.y - Win.x, wh = Win.w - Win.z;
            Frame(at, "MarcoSup", new Vector3(cx, Win.w, Z0), new Vector3(ww + 0.1f, 0.06f, 0.14f), blanco);
            Frame(at, "MarcoInf", new Vector3(cx, Win.z, Z0), new Vector3(ww + 0.1f, 0.06f, 0.14f), blanco);
            Frame(at, "MarcoIzq", new Vector3(Win.x, cy, Z0), new Vector3(0.06f, wh, 0.14f), blanco);
            Frame(at, "MarcoDer", new Vector3(Win.y, cy, Z0), new Vector3(0.06f, wh, 0.14f), blanco);
            foreach (float x in new[] { -1.18f, 0.02f, 1.22f })
                Frame(at, "Montante", new Vector3(x, cy, Z0), new Vector3(0.06f, wh, 0.08f), blanco);
            Frame(at, "Travesano", new Vector3(cx, 1.82f, Z0), new Vector3(ww, 0.045f, 0.07f), blanco);

            // Solo sombra: el sol entra únicamente por el hueco
            Transform sh = Empty("Sombras", at, Vector3.zero);
            Block(sh, new Vector3(0, H + 0.1f, 0), new Vector3(W + 1f, 0.25f, 5.2f));
            Block(sh, new Vector3(-W / 2 - 0.06f, H / 2, 0), new Vector3(0.1f, H + 0.2f, 4.4f));
            Block(sh, new Vector3(W / 2 + 0.06f, H / 2, 0), new Vector3(0.1f, H + 0.2f, 4.4f));
            // El hueco es algo mayor que el ventanal: en muros y piso el borde suave lo dibuja el sombreador MR/MuroSala
            const float g = 0.05f;
            Vector4 w = new Vector4(Win.x - g, Win.y + g, Win.z - g, Win.w + g);
            Block(sh, new Vector3(0, w.z / 2, Z0 - 0.06f), new Vector3(W + 0.2f, w.z, 0.1f));
            Block(sh, new Vector3(0, (H + w.w) / 2, Z0 - 0.06f), new Vector3(W + 0.2f, H - w.w, 0.1f));
            Block(sh, new Vector3((-W / 2 + w.x) / 2, H / 2, Z0 - 0.06f), new Vector3(w.x + W / 2, H, 0.1f));
            Block(sh, new Vector3((W / 2 + w.y) / 2, H / 2, Z0 - 0.06f), new Vector3(W / 2 - w.y, H, 0.1f));
            foreach (float x in new[] { -1.18f, 0.02f, 1.22f }) Block(sh, new Vector3(x, cy, Z0), new Vector3(0.035f, wh, 0.03f));

            // ---------------- Paisaje
            GameObject domo = MeshObject("Paisaje", at, SaveMesh("DomoPaisaje", Dome()), Mat("CieloAtardecer", "Universal Render Pipeline/Unlit"));
            domo.transform.localPosition = new Vector3(0, 1.3f, Z0);
            Material sky = domo.GetComponent<MeshRenderer>().sharedMaterial;
            sky.SetTexture("_BaseMap", Tex("CieloAtardecer"));
            sky.SetColor("_BaseColor", Color.white);
            sky.SetFloat("_Cull", 0f);

            // ---------------- Haces de luz, resplandor y polvo en suspensión
            Material haz = Additive("HazLuz", "Haz", new Color(1f, 0.70f, 0.42f, 0.055f));
            float[] panes = { Win.x, -1.18f, 0.02f, 1.22f, Win.y };
            for (int i = 0; i < 4; i++)
                MeshObject("Haz" + i, at, SaveMesh("Haz" + i, Beam(panes[i] + 0.04f, panes[i + 1] - 0.04f)), haz);
            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            halo.name = "Resplandor";
            Object.DestroyImmediate(halo.GetComponent<Collider>());
            halo.transform.SetParent(at, false);
            halo.transform.localPosition = new Vector3(0, 1.3f, Z0) - Sun.normalized * 13f;
            halo.transform.localRotation = Quaternion.LookRotation(-Sun.normalized);
            halo.transform.localScale = Vector3.one * 9f;
            Plain(halo, Additive("Resplandor", "Suave", new Color(1f, 0.72f, 0.4f, 0.5f)));
            Dust(at);

            // ---------------- Lámparas cálidas
            Material pantalla = Mat("PantallaLampara", "Universal Render Pipeline/Unlit");
            pantalla.SetColor("_BaseColor", new Color(1f, 0.95f, 0.80f));
            Material metal = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Materiales/Metal.mat");
            Lamp(at, new Vector3(1.94f, 1.82f, 1.94f), 0f, 0.3f, pantalla, metal);
            Lamp(at, new Vector3(-2.94f, 1.75f, 0.64f), -90f, 0.25f, pantalla, metal);
            Material techo = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Materiales/PanelLuz.mat");
            if (techo != null) techo.SetColor("_BaseColor", new Color(1f, 0.94f, 0.84f)); // tenues y cálidas

            // ---------------- Sombras de contacto (apoyan los muebles en el piso)
            Material blob = Mat("SombraContacto", "Universal Render Pipeline/Unlit");
            Transparent(blob, BlendMode.OneMinusSrcAlpha);
            blob.SetTexture("_BaseMap", Tex("Suave"));
            blob.SetColor("_BaseColor", new Color(0.10f, 0.05f, 0.03f, 0.62f));
            Blob(at, blob, new Vector3(-0.36f, 0.006f, 1.5f), new Vector2(2.9f, 1.25f));
            Blob(at, blob, new Vector3(-2.7f, 0.006f, 0.4f), new Vector2(1.25f, 3.6f));
            foreach (string n in new[] { "CamaParto" })
            {
                Transform t = c.Find(n);
                if (t == null) continue;
                Transform prev = t.Find("SombraContacto");
                if (prev != null) Object.DestroyImmediate(prev.gameObject);
                Blob(t, blob, new Vector3(0, 0.008f, 0f), new Vector2(1.35f, 2.4f));
            }

            Material tela = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Materiales/Cortina.mat");
            if (tela != null) tela.SetFloat("_Cull", 2f); // la cortina trae sus dos caras
            // Sombra del sol más fina en Quest
            foreach (string q in new[] { "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var rp = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(q);
                if (rp == null) continue;
                var so = new SerializedObject(rp);
                so.FindProperty("m_MainLightShadowmapResolution").intValue = 2048;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---------------- Reflejos y componente
            Transform pr = Empty("Reflejos", at, new Vector3(0, 1.35f, 0));
            var probe = pr.gameObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 128;
            probe.hdr = false;
            probe.size = new Vector3(W + 1f, H + 1f, 5f);
            probe.cullingMask = ~LayerMask.GetMask("UI");
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(0.9f, 0.6f, 0.4f);
            probe.shadowDistance = 0f;

            var amb = sala.GetComponent<AmbienteAtardecer>();
            if (amb == null) amb = sala.gameObject.AddComponent<AmbienteAtardecer>();
            Set(amb, "reflejos", probe);
            Set(sala, "ambiente", amb);
        }

        // ------------------------------------------------------------------ Mallas

        private static Mesh WallWithHole()
        {
            // Espacio del quad (-0.5..0.5). El muro está girado 180°: x local = -x de la sala.
            float x0 = -Win.y / W, x1 = -Win.x / W, y0 = Win.z / H - 0.5f, y1 = Win.w / H - 0.5f;
            var v = new List<Vector3>();
            var t = new List<int>();
            void R(float ax, float ay, float bx, float by)
            {
                int i = v.Count;
                v.Add(new Vector3(ax, ay)); v.Add(new Vector3(ax, by)); v.Add(new Vector3(bx, by)); v.Add(new Vector3(bx, ay));
                t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            R(-0.5f, -0.5f, 0.5f, y0);
            R(-0.5f, y1, 0.5f, 0.5f);
            R(-0.5f, y0, x0, y1);
            R(x1, y0, 0.5f, y1);
            return Finish(v, t, p => new Vector2(p.x + 0.5f, p.y + 0.5f));
        }

        private static Mesh Dome()
        {
            const int nx = 64, ny = 32;
            const float r = 14f;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int y = 0; y <= ny; y++)
                for (int x = 0; x <= nx; x++)
                {
                    float a = Mathf.Lerp(-95f, 95f, x / (float)nx) * Mathf.Deg2Rad, e = Mathf.Lerp(-35f, 80f, y / (float)ny) * Mathf.Deg2Rad;
                    v.Add(new Vector3(-Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), -Mathf.Cos(a) * Mathf.Cos(e)) * r);
                    uv.Add(new Vector2(x / (float)nx, y / (float)ny));
                    if (x == nx || y == ny) continue;
                    int i = y * (nx + 1) + x;
                    t.AddRange(new[] { i, i + nx + 1, i + 1, i + 1, i + nx + 1, i + nx + 2 });
                }
            var m = new Mesh();
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Haz que entra por un paño del ventanal y sigue la dirección del sol: dos láminas cruzadas.</summary>
        private static Mesh Beam(float x0, float x1)
        {
            Vector3 d = Sun.normalized * 3.6f;
            float mx = (x0 + x1) / 2f, my = (Win.z + Win.w) / 2f, z = Z0 + 0.06f;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            void Sheet(Vector3 a, Vector3 b)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(b + d); v.Add(a + d);
                uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, 0));
                t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            Sheet(new Vector3(mx, Win.z, z), new Vector3(mx, Win.w, z));
            Sheet(new Vector3(x0, my + 0.2f, z), new Vector3(x1, my + 0.2f, z));
            var m = new Mesh();
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            var col = new Color[v.Count];
            for (int i = 0; i < col.Length; i++) col[i] = Color.white;
            m.colors = col;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh Finish(List<Vector3> v, List<int> t, System.Func<Vector3, Vector2> uv)
        {
            var m = new Mesh();
            m.SetVertices(v);
            m.SetUVs(0, v.ConvertAll(p => uv(p)));
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh SaveMesh(string name, Mesh mesh)
        {
            if (!AssetDatabase.IsValidFolder(Dir + "/Mallas")) AssetDatabase.CreateFolder(Dir, "Mallas");
            string p = $"{Dir}/Mallas/{name}.asset";
            mesh.name = name;
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(p);
            if (old == null) { AssetDatabase.CreateAsset(mesh, p); return mesh; }
            EditorUtility.CopySerialized(mesh, old);
            return old;
        }

        // ------------------------------------------------------------------ Piezas

        private static void Frame(Transform parent, string name, Vector3 pos, Vector3 size, Material m)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off; // su sombra la dan los bloques y el sombreador
        }

        private static void Block(Transform parent, Vector3 pos, Vector3 size)
        {
            Frame(parent, "Bloqueo", pos, size, null);
            var r = parent.GetChild(parent.childCount - 1).GetComponent<Renderer>();
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Materiales/Blanco.mat");
            r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            r.receiveShadows = false;
        }

        private static void Blob(Transform parent, Material m, Vector3 pos, Vector2 size)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "SombraContacto";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localRotation = Quaternion.Euler(90, 0, 0);
            q.transform.localScale = new Vector3(size.x, size.y, 1);
            Plain(q, m);
        }

        private static void Lamp(Transform at, Vector3 pos, float yaw, float power, Material shade, Material metal)
        {
            Transform t = Empty("Lampara", at, pos);
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(s.GetComponent<Collider>());
            s.name = "Pantalla";
            s.transform.SetParent(t, false);
            s.transform.localPosition = new Vector3(0, 0, -0.14f);
            s.transform.localScale = new Vector3(0.13f, 0.07f, 0.13f);
            Plain(s, shade);
            Frame(t, "Brazo", new Vector3(0, -0.02f, -0.04f), new Vector3(0.03f, 0.03f, 0.12f), metal);
            Frame(t, "Placa", new Vector3(0, -0.02f, 0.02f), new Vector3(0.1f, 0.16f, 0.02f), metal);
            var l = Empty("Luz", t, new Vector3(0, -0.02f, -0.18f)).gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.70f, 0.42f);
            l.intensity = power;
            l.range = 3f;
            l.shadows = LightShadows.None;
        }

        private static void Dust(Transform at)
        {
            Transform t = Empty("Polvo", at, new Vector3(0.2f, 1.1f, -0.5f));
            var ps = t.gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 13f);
            main.startSpeed = 0.012f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.011f);
            main.startColor = new Color(1f, 0.82f, 0.55f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 70;
            var em = ps.emission;
            em.rateOverTime = 6f;
            var s = ps.shape;
            s.shapeType = ParticleSystemShapeType.Box;
            s.scale = new Vector3(4.6f, 1.5f, 2.6f);
            var n = ps.noise;
            n.enabled = true;
            n.strength = 0.02f;
            n.frequency = 0.4f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = Additive("Polvo", "Suave", new Color(1f, 0.8f, 0.55f, 0.8f));
            pr.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            Plain(go, m);
            return go;
        }

        private static void Plain(GameObject go, Material m)
        {
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Transform Empty(string name, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go.transform;
        }

        // ------------------------------------------------------------------ Assets

        private static void Import()
        {
            AssetDatabase.Refresh();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Dir + "/Texturas" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string n = System.IO.Path.GetFileNameWithoutExtension(p);
                bool luz = n.StartsWith("Luz_");
                if (!luz && n != "CieloAtardecer" && n != "Haz") continue;
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.mipmapEnabled = !luz;
                ti.alphaIsTransparency = n == "Haz";
                ti.maxTextureSize = 2048;
                // Degradados suaves: sin compresión para que no se vean bandas (son texturas pequeñas).
                ti.textureCompression = luz ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
        }

        private static Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/Texturas/{n}.png");

        private static Material Mat(string name, string shader)
        {
            string path = $"{Dir}/Materiales/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader)) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Additive(string name, string tex, Color color)
        {
            Material m = Mat(name, "Universal Render Pipeline/Particles/Unlit");
            Transparent(m, BlendMode.One);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_Cull", 0f);
            m.SetTexture("_BaseMap", Tex(tex));
            m.SetColor("_BaseColor", color);
            return m;
        }

        private static void Transparent(Material m, BlendMode dst)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)dst);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
