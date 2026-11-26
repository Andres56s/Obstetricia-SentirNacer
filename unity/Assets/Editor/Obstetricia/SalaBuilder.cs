#if UNITY_EDITOR
using System;
using Obstetricia.Data;
using Obstetricia.Modules;
using Obstetricia.Sala;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace ObstetriciaSetup
{
    /// <summary>
    /// Construye la sala virtual de obstetricia en la escena (objeto "SalaObstetricia"). Solo la crea si no existe, para no
    /// pisar lo que se edite a mano: mover el lavamanos, poner fotos reales en los materiales Pared_*, etc.
    /// </summary>
    public static class SalaBuilder
    {
        private const string Name = "SalaObstetricia";
        private const string Dir = "Assets/Obstetricia/Sala";
        private const float W = 6f, Z0 = -2f, Z1 = 2f, H = 2.7f;

        [MenuItem("Tools/MR Setup/6 - Reconstruir sala virtual (borra cambios manuales)")]
        public static void Rebuild()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Sala virtual", "Se perderán los cambios hechos a mano en la sala. ¿Continuar?", "Reconstruir", "Cancelar")) return;
            RebuildNow();
        }

        public static void RebuildNow()
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject g in scene.GetRootGameObjects())
                if (g.name == Name) UnityEngine.Object.DestroyImmediate(g);
            ObstetriciaSceneSetup.Build();
        }

        public static SalaVirtual Ensure(Scene scene, XROrigin origin, ObstetriciaTheme theme)
        {
            foreach (GameObject g in scene.GetRootGameObjects())
                if (g.name == Name) return g.GetComponent<SalaVirtual>();
            return Build(scene, origin, theme);
        }

        private static SalaVirtual Build(Scene scene, XROrigin origin, ObstetriciaTheme theme)
        {
            ImportSettings();
            var root = new GameObject(Name);
            SceneManager.MoveGameObjectToScene(root, scene);
            Transform c = Empty("Contenido", root.transform, Vector3.zero);

            // ---------------- Arquitectura
            Transform arq = Empty("Arquitectura", c, Vector3.zero);
            float depth = Z1 - Z0, mid = (Z0 + Z1) / 2f;
            Texture2D pared = Tex("Pared");
            Wall(arq, "Pared_Fondo", new Vector3(0, H / 2, Z1), 0, W, pared);
            Wall(arq, "Pared_Derecha", new Vector3(W / 2, H / 2, mid), 90, depth, pared);
            Wall(arq, "Pared_Izquierda", new Vector3(-W / 2, H / 2, mid), -90, depth, pared);
            Wall(arq, "Pared_Ventanal", new Vector3(0, H / 2, Z0), 180, W, pared);
            Quad(arq, "Piso", new Vector3(0, 0, mid), Quaternion.Euler(90, 0, 0), new Vector2(W, depth),
                Lit("Piso", Color.white, 0.35f, Tex("Piso"), null, new Vector2(W / 1.2f, depth / 1.2f)));
            Quad(arq, "Cielo", new Vector3(0, H, mid), Quaternion.Euler(-90, 0, 0), new Vector2(W, depth),
                Lit("Cielo", Color.white, 0.1f, Tex("Cielo"), null, new Vector2(W / 0.65f, depth / 0.65f)));

            Material madera = Lit("Madera", new Color(0.72f, 0.56f, 0.39f), 0.35f);
            Material blanco = Lit("Blanco", new Color(0.93f, 0.94f, 0.94f), 0.4f);
            Material gris = Lit("Gris", new Color(0.45f, 0.5f, 0.53f), 0.4f);
            Material metal = Lit("Metal", new Color(0.78f, 0.8f, 0.82f), 0.75f, null, null, null, 0.8f);
            Material luz = Unlit("PanelLuz", new Color(1f, 0.99f, 0.95f), null, false);
            Box(arq, "Zocalo_Fondo", new Vector3(0, 0.05f, Z1 - 0.01f), new Vector3(W, 0.1f, 0.02f), gris);
            Box(arq, "Zocalo_Der", new Vector3(W / 2 - 0.01f, 0.05f, mid), new Vector3(0.02f, 0.1f, depth), gris);
            Box(arq, "Zocalo_Izq", new Vector3(-W / 2 + 0.01f, 0.05f, mid), new Vector3(0.02f, 0.1f, depth), gris);
            Box(arq, "Zocalo_Ent", new Vector3(0, 0.05f, Z0 + 0.01f), new Vector3(W, 0.1f, 0.02f), gris);
            foreach (float x in new[] { -1.9f, 0f, 1.9f })
                foreach (float z in new[] { -0.9f, 0.9f })
                    Box(arq, "Luz", new Vector3(x, H - 0.012f, z), new Vector3(0.6f, 0.02f, 1.2f), luz);
            // Distribución según el plano entregado (izquierda = -X, arriba = +Z).
            // Puerta: muro izquierdo, abajo
            Box(arq, "PuertaMarco", new Vector3(-W / 2 + 0.02f, 1.06f, -1.52f), new Vector3(0.04f, 2.12f, 0.94f), blanco);
            Box(arq, "Puerta", new Vector3(-W / 2 + 0.04f, 1.03f, -1.52f), new Vector3(0.04f, 2.06f, 0.84f), madera);
            Box(arq, "PuertaManilla", new Vector3(-W / 2 + 0.09f, 1.02f, -1.2f), new Vector3(0.05f, 0.025f, 0.13f), metal);
            // Ventanal: muro de abajo
            Box(arq, "VentanalMarco", new Vector3(0.02f, 1.45f, Z0 + 0.02f), new Vector3(4.92f, 1.72f, 0.04f), blanco);
            GameObject vista = Quad(arq, "VentanalVista", new Vector3(0.02f, 1.45f, Z0 + 0.045f), Quaternion.Euler(0, 180, 0), new Vector2(4.8f, 1.6f),
                Unlit("Ventanal", Color.white, Tex("Ventana"), false));
            UnityEngine.Object.DestroyImmediate(vista.GetComponent<Collider>());
            foreach (float x in new[] { -1.18f, 0.02f, 1.22f })
                Box(arq, "VentanalMontante", new Vector3(x, 1.45f, Z0 + 0.055f), new Vector3(0.05f, 1.6f, 0.02f), blanco);
            Box(arq, "VentanalRepisa", new Vector3(0.02f, 0.58f, Z0 + 0.07f), new Vector3(5f, 0.04f, 0.16f), blanco);
            // Panel de cabecera sobre la cama
            Box(arq, "Cabecera", new Vector3(1.94f, 1.5f, Z1 - 0.04f), new Vector3(1.4f, 0.2f, 0.08f), blanco);
            foreach (float x in new[] { -0.5f, -0.28f, 0.28f, 0.5f })
                Box(arq, "Toma", new Vector3(1.94f + x, 1.5f, Z1 - 0.085f), new Vector3(0.08f, 0.08f, 0.02f), gris);

            // ---------------- Veladores a ambos lados del lavamanos (muro izquierdo)
            Transform vel = Empty("Veladores", c, new Vector3(-W / 2 + 0.26f, 0f, 0f));
            Material claro = ByName("MuebleClaro");
            Veladores(vel, "Velador_Superior", 1.46f, 0.96f, 2, blanco, claro, madera, metal);
            Veladores(vel, "Velador_Inferior", -0.43f, 1.44f, 3, blanco, claro, madera, metal);

            // ---------------- Cortina
            Material tela = Lit("Cortina", new Color(0.62f, 0.8f, 0.83f), 0.1f);
            tela.SetFloat("_Cull", 0f);
            var cortinaGo = new GameObject("Cortina");
            cortinaGo.transform.SetParent(c, false);
            cortinaGo.transform.localPosition = new Vector3(1.15f, 0f, Z1 - 0.06f);
            cortinaGo.AddComponent<MeshFilter>();
            cortinaGo.AddComponent<MeshRenderer>().sharedMaterial = tela;
            cortinaGo.AddComponent<BoxCollider>();
            Cortina cortina = cortinaGo.AddComponent<Cortina>();
            Box(arq, "CortinaRiel", new Vector3(1.15f, 2.36f, 0.64f), new Vector3(0.035f, 0.035f, 2.66f), metal);
            foreach (float z in new[] { -0.62f, 1.9f })
                Box(arq, "CortinaSoporte", new Vector3(1.15f, 2.53f, z), new Vector3(0.02f, 0.34f, 0.02f), metal);

            // ---------------- Cama
            EquipoRodante cama = Equipo(c, "CamaParto", new Vector3(1.94f, 0f, 0.84f), 180f, EscalaCama, 0.22f, out Transform cm);
            Col(cama.transform, new Vector3(0, 0.2f, -0.1f), new Vector3(0.72f, 0.34f, 1.55f), 0);
            Col(cm, new Vector3(0, 0.6f, 0.16f), new Vector3(0.68f, 0.2f, 0.64f), 0);
            Col(cm, new Vector3(0, 0.83f, -0.42f), new Vector3(0.76f, 0.16f, 0.7f), 32);
            Col(cm, new Vector3(0, 0.5f, 0.68f), new Vector3(0.6f, 0.12f, 0.52f), 20);

            // ---------------- Mesa auxiliar, parlante, guatero
            Transform mesa = Empty("MesaAuxiliar", c, new Vector3(-0.36f, 0f, 1.5f));
            Box(mesa, "Cubierta", new Vector3(0, 0.82f, 0), new Vector3(2.2f, 0.04f, 0.78f), blanco, true);
            Box(mesa, "Repisa", new Vector3(0, 0.28f, 0), new Vector3(2.1f, 0.025f, 0.68f), blanco);
            foreach (float x in new[] { -1.04f, 1.04f })
                foreach (float z in new[] { -0.33f, 0.33f })
                    Box(mesa, "Pata", new Vector3(x, 0.4f, z), new Vector3(0.03f, 0.8f, 0.03f), metal);
            GameObject parlante = Prop("Parlante", mesa, new Vector3(-0.72f, 0.84f, 0.12f), 180f);
            var src = parlante.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0.6f;
            src.minDistance = 1.2f;
            src.maxDistance = 12f;
            src.volume = 0f;
            Musicoterapia musica = parlante.AddComponent<Musicoterapia>();
            var so = new SerializedObject(musica);
            so.FindProperty("fuente").objectReferenceValue = src;
            SerializedProperty list = so.FindProperty("pistas");
            string[,] tracks = { { "Piano suave", "PianoSuave" }, { "Olas del mar", "Olas" }, { "Lluvia", "Lluvia" }, { "Cuencos", "Cuencos" } };
            list.arraySize = tracks.GetLength(0);
            for (int i = 0; i < list.arraySize; i++)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("nombre").stringValue = tracks[i, 0];
                list.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = Clip(tracks[i, 1]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject gObj = Prop("Guatero", c, mesa.localPosition + mesa.localRotation * new Vector3(0.2f, 0.885f, -0.2f), 0f);
            gObj.transform.localRotation = mesa.localRotation * Quaternion.Euler(90, 0, 0);
            var gc = gObj.AddComponent<BoxCollider>();
            gc.center = new Vector3(0, 0.15f, 0);
            gc.size = new Vector3(0.19f, 0.3f, 0.07f);
            var grb = gObj.AddComponent<Rigidbody>();
            grb.mass = 1.5f;
            grb.angularDamping = 2f;
            grb.linearDamping = 0.3f;
            grb.interpolation = RigidbodyInterpolation.Interpolate;
            grb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            gc.sharedMaterial = PhysMat();
            Guatero guatero = gObj.AddComponent<Guatero>();
            Set(guatero, "cuerpo", gObj.GetComponentInChildren<Renderer>());
            Set(guatero, "vapor", Steam(gObj.transform));

            // ---------------- Lavamanos provisional (entre los veladores)
            GameObject lav = Prop("Lavamanos", c, new Vector3(-W / 2 + 0.26f, 0f, 0.64f), 90f);
            lav.name = "Lavamanos (provisional)";
            var lc = lav.AddComponent<BoxCollider>();
            lc.center = new Vector3(0, 0.5f, 0);
            lc.size = new Vector3(0.64f, 1f, 0.5f);
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = "Chorro";
            UnityEngine.Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.SetParent(lav.transform, false);
            water.transform.localPosition = new Vector3(0, 0.93f, -0.03f);
            water.transform.localScale = new Vector3(0.018f, 0.1f, 0.018f);
            water.GetComponent<Renderer>().sharedMaterial = Unlit("Agua", new Color(0.75f, 0.9f, 1f, 0.55f), null, true);
            water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var ws = lav.AddComponent<AudioSource>();
            ws.clip = Clip("Agua");
            ws.loop = true;
            ws.playOnAwake = false;
            ws.spatialBlend = 1f;
            ws.maxDistance = 6f;
            Lavamanos lavamanos = lav.AddComponent<Lavamanos>();
            Set(lavamanos, "chorro", water.transform);
            Set(lavamanos, "sonido", ws);
            Set(lavamanos, "guatero", guatero);

            // ---------------- Puntos del piso
            Transform pts = Empty("Puntos", c, Vector3.zero);
            Transform[] puntos =
            {
                Point(pts, "Centro", new Vector3(-0.4f, 0, -0.75f), 10),
                Point(pts, "Mesa", new Vector3(-0.36f, 0, 0.6f), 0),
                Point(pts, "Cama", new Vector3(0.75f, 0, 0.15f), 80),
                Point(pts, "Lavamanos", new Vector3(-1.75f, 0, 0.64f), -90),
            };
            Transform lugarPelvis = Empty("LugarPelvis", c, new Vector3(-0.4f, 1.38f, 0.3f));
            Transform lugarMusica = Empty("LugarMusica", c, new Vector3(0.42f, 1.4f, 1.72f));

            var pel = new GameObject("PelvisSala");
            pel.transform.SetParent(c, false);
            AnatomyViewer anatomy = pel.AddComponent<AnatomyViewer>();
            Transform cam = origin.Camera != null ? origin.Camera.transform : null;
            Set(anatomy, "theme", theme);
            var dark = new SerializedObject(anatomy);
            dark.FindProperty("darkGlass").boolValue = true;
            dark.ApplyModifiedPropertiesWithoutUndo();
            Set(anatomy, "head", cam);

            var veloGo = new GameObject("Velo");
            veloGo.transform.SetParent(root.transform, false);
            VeloTransicion velo = veloGo.AddComponent<VeloTransicion>();
            Set(velo, "material", Asset("VeloTransicion", "MR/VeloTransicion"));

            SalaVirtual sala = root.AddComponent<SalaVirtual>();
            Set(sala, "contenido", c.gameObject);
            Set(sala, "velo", velo);
            Set(sala, "xrOrigin", origin);
            Set(sala, "oclusion", cam != null ? cam.GetComponent<AROcclusionManager>() : null);
            Set(sala, "cama", cama);
            Set(sala, "cortina", cortina);
            Set(sala, "guatero", guatero);
            Set(sala, "musica", musica);
            Set(sala, "pelvis", anatomy);
            Set(sala, "lugarPelvis", lugarPelvis);
            Set(sala, "lugarMusica", lugarMusica);
            Set(sala, "materialZona", Unlit("ZonaCalor", new Color(1f, 0.45f, 0.22f, 0.3f), null, true));
            var ss = new SerializedObject(sala);
            SerializedProperty arr = ss.FindProperty("puntos");
            arr.arraySize = puntos.Length;
            for (int i = 0; i < puntos.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = puntos[i];
            ss.ApplyModifiedPropertiesWithoutUndo();
            AtardecerBuilder.Apply(sala);
            EquipamientoBuilder.Apply(sala);
            c.gameObject.SetActive(false);
            return sala;
        }

        // ------------------------------------------------------------------ Piezas

        // Escala real: cama de 2,2 x 1,0 m, asiento a 70 cm
        private const float EscalaCama = 1.15f;

        /// <summary>Aplica la escala real a la cama sin reconstruir la sala.</summary>
        [MenuItem("Tools/MR Setup/8 - Escala real de la cama")]
        public static void AplicarEscala()
        {
            foreach (EquipoRodante e in UnityEngine.Object.FindObjectsByType<EquipoRodante>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (e.name != "CamaParto") continue;
                e.transform.localScale = Vector3.one * EscalaCama;
                Vector3 p = e.transform.localPosition;
                p.z = Mathf.Min(p.z, 0.84f); // que no toque el muro del fondo
                e.transform.localPosition = p;
                EditorSceneManager.MarkSceneDirty(e.gameObject.scene);
            }
            EditorSceneManager.SaveOpenScenes();
        }

        private static EquipoRodante Equipo(Transform parent, string name, Vector3 pos, float yaw, float scale, float travel, out Transform movil)
        {
            GameObject go = Prop(name, parent, pos, yaw);
            go.transform.localScale = Vector3.one * scale;
            // La deformación de altura necesita un SkinnedMeshRenderer (Unity importa un MeshRenderer simple).
            MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            for (int i = go.transform.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
            var smr = Empty("Malla", go.transform, Vector3.zero).gameObject.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            if (mesh != null) smr.localBounds = new Bounds(mesh.bounds.center, mesh.bounds.size * 1.3f);
            if (mesh == null || mesh.blendShapeCount == 0) throw new Exception("El modelo " + name + " no trae la deformación de altura.");
            smr.sharedMaterial = Lit(name, Color.white, 0.4f, Tex(name + "_Color"), Tex(name + "_Normal"));
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 60f;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            movil = Empty("PartesMoviles", go.transform, Vector3.zero);
            EquipoRodante e = go.AddComponent<EquipoRodante>();
            Set(e, "malla", smr);
            Set(e, "partesMoviles", movil);
            var so = new SerializedObject(e);
            so.FindProperty("recorrido").floatValue = travel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return e;
        }

        /// <summary>Tramo de veladores: cuerpo, cubierta y cajones con tirador (frente hacia +X).</summary>
        private static void Veladores(Transform parent, string name, float z, float largo, int modulos, Material cuerpo, Material frente, Material cubierta, Material tirador)
        {
            Transform t = Empty(name, parent, new Vector3(0, 0, z));
            Box(t, "Cuerpo", new Vector3(0, 0.43f, 0), new Vector3(0.48f, 0.78f, largo), cuerpo, true);
            Box(t, "Zocalo", new Vector3(-0.02f, 0.03f, 0), new Vector3(0.42f, 0.06f, largo - 0.02f), tirador);
            Box(t, "Cubierta", new Vector3(0.01f, 0.85f, 0), new Vector3(0.52f, 0.05f, largo), cubierta, true);
            float w = largo / modulos;
            for (int m = 0; m < modulos; m++)
                for (int k = 0; k < 3; k++)
                {
                    float cz = -largo / 2f + w * (m + 0.5f), y = 0.19f + k * 0.25f;
                    Box(t, "Cajon", new Vector3(0.245f, y, cz), new Vector3(0.02f, 0.235f, w - 0.02f), frente);
                    Box(t, "Tirador", new Vector3(0.265f, y + 0.06f, cz), new Vector3(0.02f, 0.015f, 0.14f), tirador);
                }
        }

        private static void Col(Transform parent, Vector3 center, Vector3 size, float pitch)
        {
            Transform t = Empty("Colision", parent, center);
            t.localRotation = Quaternion.Euler(pitch, 0, 0);
            var b = t.gameObject.AddComponent<BoxCollider>();
            b.size = size;
        }

        /// <summary>Modelo dentro de un contenedor limpio (escala 1), con sus materiales por nombre.</summary>
        private static GameObject Prop(string name, Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/Modelos/{name}.fbx");
            var m = (GameObject)PrefabUtility.InstantiatePrefab(src);
            m.transform.SetParent(go.transform, false);
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = ByName(mats[i] != null ? mats[i].name : "Blanco");
                r.sharedMaterials = mats;
            }
            return go;
        }

        private static Material ByName(string n)
        {
            switch (n)
            {
                case "Guatero_Goma": return Lit(n, new Color(0.25f, 0.42f, 0.62f), 0.45f);
                case "Guatero_Tapa": return Lit(n, new Color(0.94f, 0.94f, 0.92f), 0.5f);
                case "Mueble": return Lit(n, new Color(0.9f, 0.91f, 0.9f), 0.35f);
                case "MuebleClaro": return Lit(n, new Color(0.62f, 0.78f, 0.78f), 0.4f);
                case "Ceramica": return Lit(n, new Color(0.97f, 0.97f, 0.97f), 0.85f);
                case "Metal": return Lit(n, new Color(0.78f, 0.8f, 0.82f), 0.75f, null, null, null, 0.8f);
                case "Parlante": return Lit(n, new Color(0.16f, 0.2f, 0.26f), 0.4f);
                case "ParlanteMalla": return Lit(n, new Color(0.07f, 0.08f, 0.1f), 0.2f);
                default: return Lit("Blanco", new Color(0.93f, 0.94f, 0.94f), 0.4f);
            }
        }

        private static ParticleSystem Steam(Transform parent)
        {
            Transform t = Empty("Vapor", parent, new Vector3(0, 0.16f, 0));
            var ps = t.gameObject.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 2.2f;
            main.startSpeed = 0.06f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new Color(1f, 1f, 1f, 0.16f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.012f;
            main.maxParticles = 40;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(0.14f, 0.2f, 0.04f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            Material m = Asset("Vapor", "Universal Render Pipeline/Particles/Unlit");
            Transparent(m);
            m.SetTexture("_BaseMap", Tex("Suave"));
            m.SetColor("_BaseColor", Color.white);
            pr.sharedMaterial = m;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        private static Transform Empty(string name, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go.transform;
        }

        private static Transform Point(Transform parent, string name, Vector3 pos, float yaw)
        {
            Transform t = Empty(name, parent, pos);
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            return t;
        }

        private static void Wall(Transform parent, string name, Vector3 pos, float yaw, float width, Texture2D tex)
        {
            // Cada muro tiene su propio material: para usar una foto real basta cambiar su textura.
            Quad(parent, name, pos, Quaternion.Euler(0, yaw, 0), new Vector2(width, H), Lit(name, Color.white, 0.12f, tex));
        }

        private static GameObject Quad(Transform parent, string name, Vector3 pos, Quaternion rot, Vector2 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var b = go.AddComponent<BoxCollider>();
            b.size = new Vector3(1f, 1f, 0.4f);
            b.center = new Vector3(0, 0, 0.2f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collide = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collide) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (parent.name == "Arquitectura") r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ Assets

        private static void ImportSettings()
        {
            AssetDatabase.Refresh();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Dir + "/Texturas" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                bool normal = p.EndsWith("_Normal.png");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.maxTextureSize = p.Contains("Parto") || p.Contains("RecienNacido_Color") ? 2048 : 1024;
                ti.alphaIsTransparency = p.EndsWith("Suave.png");
                ti.anisoLevel = 4;
                ti.SaveAndReimport();
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Dir + "/Modelos" }))
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                mi.importAnimation = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.animationType = ModelImporterAnimationType.None;
                mi.importBlendShapes = true;
                mi.importBlendShapeNormals = ModelImporterNormals.None;
                mi.SaveAndReimport();
            }
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Dir + "/Audio" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var ai = (AudioImporter)AssetImporter.GetAtPath(p);
                AudioImporterSampleSettings s = ai.defaultSampleSettings;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.5f;
                s.loadType = p.EndsWith("Agua.wav") ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.Streaming;
                ai.defaultSampleSettings = s;
                ai.SaveAndReimport();
            }
        }

        private static Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/Texturas/{n}.png");

        private static AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Dir}/Audio/{n}.wav");

        private static Material Asset(string name, string shader)
        {
            if (!AssetDatabase.IsValidFolder(Dir + "/Materiales")) AssetDatabase.CreateFolder(Dir, "Materiales");
            string path = $"{Dir}/Materiales/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find(shader)) { name = name };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Material Lit(string name, Color color, float smooth, Texture2D tex = null, Texture2D normal = null, Vector2? tiling = null, float metallic = 0f)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/Materiales/{name}.mat") != null;
            Material m = Asset(name, "Universal Render Pipeline/Lit");
            if (exists) return m; // no pisar ediciones (p. ej. fotos reales en los muros)
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metallic);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Unlit(string name, Color color, Texture2D tex, bool transparent)
        {
            Material m = Asset(name, "Universal Render Pipeline/Unlit");
            m.SetColor("_BaseColor", color);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            if (transparent) Transparent(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
        }

        private static PhysicsMaterial PhysMat()
        {
            string p = Dir + "/Materiales/Guatero.physicMaterial";
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(p);
            if (m != null) return m;
            m = new PhysicsMaterial { dynamicFriction = 0.8f, staticFriction = 0.9f, bounciness = 0.05f };
            AssetDatabase.CreateAsset(m, p);
            return m;
        }

        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[Obstetricia] Campo '{field}' no existe en {target.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
