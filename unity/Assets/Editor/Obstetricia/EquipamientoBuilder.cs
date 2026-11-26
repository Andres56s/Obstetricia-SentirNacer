#if UNITY_EDITOR
using System.Collections.Generic;
using Obstetricia.Sala;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ObstetriciaSetup
{
    /// <summary>
    /// Equipamiento obstétrico de la sala virtual (monitor fetal, cuna radiante, balón, liana, etc.). Solo reemplaza el
    /// objeto "Equipamiento"; no mueve ni borra el resto del mobiliario.
    /// </summary>
    public static class EquipamientoBuilder
    {
        private const string Dir = "Assets/Obstetricia/Sala";

        // Materiales por nombre (los asigna Blender): color, suavidad, metal
        private static readonly Dictionary<string, (Color c, float s, float m)> Colores = new Dictionary<string, (Color, float, float)>
        {
            ["Rueda"] = (new Color(0.12f, 0.12f, 0.13f), 0.3f, 0f), ["MetalClaro"] = (new Color(0.78f, 0.79f, 0.8f), 0.6f, 0.45f),
            ["PlasticoGris"] = (new Color(0.5f, 0.53f, 0.56f), 0.4f, 0f), ["PlasticoClaro"] = (new Color(0.93f, 0.93f, 0.92f), 0.5f, 0f),
            ["PlasticoOscuro"] = (new Color(0.12f, 0.14f, 0.17f), 0.6f, 0f), ["PlasticoAzul"] = (new Color(0.2f, 0.45f, 0.78f), 0.5f, 0f),
            ["PlasticoAzulClaro"] = (new Color(0.6f, 0.8f, 0.9f), 0.5f, 0f), ["Papel"] = (new Color(0.98f, 0.93f, 0.9f), 0.1f, 0f),
            ["TransductorAzul"] = (new Color(0.25f, 0.5f, 0.85f), 0.5f, 0f), ["TransductorRosa"] = (new Color(0.92f, 0.5f, 0.64f), 0.5f, 0f),
            ["Correa"] = (new Color(0.94f, 0.82f, 0.84f), 0.1f, 0f), ["Colchon"] = (new Color(0.8f, 0.9f, 0.94f), 0.25f, 0f),
            ["Acrilico"] = (new Color(0.85f, 0.95f, 1f, 0.28f), 0.9f, 0f),
            ["Balon"] = (new Color(0.55f, 0.4f, 0.72f), 0.45f, 0f), ["TapizVerde"] = (new Color(0.45f, 0.68f, 0.62f), 0.35f, 0f),
            ["MaderaClara"] = (new Color(0.8f, 0.62f, 0.43f), 0.4f, 0f), ["TapizAzul"] = (new Color(0.36f, 0.55f, 0.68f), 0.25f, 0f),
            ["TapizAzulBorde"] = (new Color(0.28f, 0.44f, 0.56f), 0.2f, 0f), ["TelaLiana"] = (new Color(0.82f, 0.45f, 0.45f), 0.05f, 0f),
            ["Suero"] = (new Color(0.9f, 0.96f, 1f, 0.5f), 0.9f, 0f), ["PapelEtiqueta"] = (Color.white, 0.1f, 0f),
            ["Via"] = (new Color(0.92f, 0.96f, 1f, 0.6f), 0.8f, 0f), ["Campo"] = (new Color(0.3f, 0.58f, 0.58f), 0.05f, 0f),
            ["Acero"] = (new Color(0.82f, 0.83f, 0.84f), 0.7f, 0.5f), ["PlasticoAmarillo"] = (new Color(0.97f, 0.84f, 0.3f), 0.5f, 0f),
            ["Pera"] = (new Color(0.3f, 0.52f, 0.8f), 0.4f, 0f), ["Gasa"] = (new Color(0.98f, 0.98f, 0.96f), 0.02f, 0f),
            ["ToallaBlanca"] = (new Color(0.97f, 0.97f, 0.96f), 0.02f, 0f), ["ToallaVerde"] = (new Color(0.62f, 0.8f, 0.74f), 0.02f, 0f),
            ["ToallaRosa"] = (new Color(0.91f, 0.73f, 0.74f), 0.02f, 0f), ["Calefactor"] = (new Color(1f, 0.55f, 0.28f), 0f, 0f),
            ["LuzCuna"] = (new Color(1f, 0.96f, 0.85f), 0f, 0f),
        };

        [MenuItem("Tools/MR Setup/10 - Equipamiento obstetrico de la sala")]
        public static void ApplyToScene()
        {
            var sala = Object.FindFirstObjectByType<SalaVirtual>(FindObjectsInactive.Include);
            if (sala == null)
            {
                Debug.LogError("[Obstetricia] No hay sala virtual en la escena.");
                return;
            }
            Apply(sala);
            EditorSceneManager.MarkSceneDirty(sala.gameObject.scene);
            EditorSceneManager.SaveScene(sala.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Obstetricia] Equipamiento obstétrico agregado a la sala.");
        }

        public static void Apply(SalaVirtual sala)
        {
            Import();
            Transform c = sala.transform.Find("Contenido");
            Transform old = c.Find("Equipamiento");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform eq = Empty("Equipamiento", c, Vector3.zero);
            Transform cama = c.Find("CamaParto");

            // ---------------- Monitor fetal (entre la cama y el muro derecho)
            GameObject mon = Modelo("MonitorFetal", eq, new Vector3(2.72f, 0f, 0.35f), -100f);
            Col(mon, new Vector3(0, 0.55f, 0), new Vector3(0.46f, 1.1f, 0.46f));
            Col(mon, new Vector3(0, 1.46f, -0.03f), new Vector3(0.76f, 0.54f, 0.05f));
            Sombra(mon.transform, new Vector2(0.75f, 0.75f));
            Transform pantalla = Empty("Pantalla", mon.transform, new Vector3(0f, 1.46f, 0.0f));
            pantalla.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var mf = mon.AddComponent<MonitorFetal>();
            Set(mf, "pantalla", pantalla);
            Set(mf, "sonido", Audio(mon, null, false));
            Set(mf, "latido", Clip("LatidoFetal"));
            FichaEn(mon, "Monitor fetal (cardiotocógrafo)", "Registra la frecuencia cardíaca fetal y las contracciones. Elige un registro en la pantalla para comparar el trazado normal con los tipos de desaceleraciones.", 1.0f);

            // ---------------- Cuna radiante y recién nacido
            GameObject cuna = Modelo("CunaRadiante", eq, new Vector3(-1.75f, 0f, -1.46f), 0f);
            Col(cuna, new Vector3(0, 0.45f, 0), new Vector3(0.62f, 0.9f, 0.78f));
            Col(cuna, new Vector3(0, 1.35f, -0.42f), new Vector3(0.32f, 0.95f, 0.1f));
            Sombra(cuna.transform, new Vector2(0.95f, 1.1f));
            var calor = Empty("LuzCalor", cuna.transform, new Vector3(0, 1.66f, 0.02f)).gameObject.AddComponent<Light>();
            calor.type = LightType.Spot;
            calor.transform.localRotation = Quaternion.Euler(90, 0, 0);
            calor.spotAngle = 62f;
            calor.innerSpotAngle = 30f;
            calor.range = 1.5f;
            calor.intensity = 0.4f;
            calor.color = new Color(1f, 0.72f, 0.48f);
            calor.shadows = LightShadows.None;
            Vida(calor.gameObject, 1, null, 0f);
            FichaEn(cuna, "Cuna radiante", "Superficie con calor para la atención inmediata del recién nacido: secar, abrigar, evaluar y reanimar si hace falta, evitando la pérdida de calor.", 1.25f);

            GameObject rn = Modelo("RecienNacido", eq, new Vector3(-1.75f, 0.949f, -1.42f), -38f);
            Col(rn, new Vector3(0, 0.07f, 0), new Vector3(0.21f, 0.14f, 0.49f));
            foreach (MeshRenderer pr in rn.GetComponentsInChildren<MeshRenderer>()) pr.sharedMaterial = PielBebe();
            Rb(rn, 3f);
            var bebe = rn.AddComponent<RecienNacido>();
            Flag(bebe, "vuelveSolo", true);
            Vida(rn, 0, rn.transform.GetChild(0), 0f);
            FichaEn(rn, "Recién nacido (simulador)", "Tómalo y llévalo a la cama, a la zona que brilla, para representar el contacto piel con piel. Si lo sueltas en otro lugar vuelve solo a la cuna.", 0.35f);
            Transform mov = cama != null ? cama.Find("PartesMoviles") : null;
            if (mov != null)
            {
                Transform prev = mov.Find("LugarPielConPiel");
                if (prev != null) Object.DestroyImmediate(prev.gameObject);
                Transform lugar = Empty("LugarPielConPiel", mov, new Vector3(0f, 0.86f, -0.40f));
                lugar.localRotation = Quaternion.Euler(36f, 0f, 0f);
                lugar.localScale = Vector3.one / cama.localScale.x;
                GameObject marca = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marca.name = "Marca";
                Object.DestroyImmediate(marca.GetComponent<Collider>());
                marca.transform.SetParent(lugar, false);
                marca.transform.localPosition = new Vector3(0, 0.04f, 0);
                marca.transform.localScale = new Vector3(0.36f, 0.05f, 0.56f);
                var mr = marca.GetComponent<MeshRenderer>();
                mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Materiales/ZonaCalor.mat");
                mr.shadowCastingMode = ShadowCastingMode.Off;
                Set(bebe, "lugarPiel", lugar);
                Set(bebe, "marca", mr);
            }
            Transform apgar = Empty("PanelApgar", eq, new Vector3(-2.45f, 1.42f, -1.12f));
            apgar.localRotation = Quaternion.LookRotation(new Vector3(-1.0f, -0.06f, -0.42f));
            apgar.gameObject.AddComponent<PanelApgar>();

            // ---------------- Rincón de trabajo de parto: colchoneta, balón, liana y banquito
            GameObject col = Modelo("Colchoneta", eq, new Vector3(-1.2f, 0f, 0f), 0f);
            Col(col, new Vector3(0, 0.025f, 0), new Vector3(1.2f, 0.05f, 0.75f));
            GameObject balon = Modelo("Balon", eq, new Vector3(-1.3f, 0.378f, 0.02f), 25f);
            var sc = balon.AddComponent<SphereCollider>();
            sc.radius = 0.325f;
            sc.sharedMaterial = Rebote();
            Rb(balon, 1.2f).angularDamping = 1.2f;
            balon.AddComponent<ObjetoSala>();
            FichaEn(balon, "Balón kinésico", "Sentada sobre el balón, la mujer mueve la pelvis en círculos y balanceos: alivia el dolor lumbar, favorece la posición vertical y el descenso fetal.", 0.5f);
            Transform colgador = Empty("ColgadorLiana", eq, new Vector3(-1.62f, 2.7f, 0.02f));
            GameObject liana = Modelo("Liana", colgador, Vector3.zero, 90f);
            GameObject giro = Empty("Giro", colgador, Vector3.zero).gameObject;
            liana.transform.SetParent(giro.transform, false);
            Col(giro, new Vector3(0, -1.0f, 0), new Vector3(0.22f, 1.5f, 0.34f));
            giro.AddComponent<Liana>();
            FichaEn(giro, "Liana o tela de parto", "Permite colgarse y descargar peso en cuclillas o de pie durante las contracciones. Favorece la verticalidad y amplía los diámetros de la pelvis. Tómala para mecerla.", -1.0f);
            GameObject ban = Modelo("BanquitoParto", eq, new Vector3(-0.28f, 0f, -1.62f), 0f);
            Col(ban, new Vector3(0, 0.2f, 0), new Vector3(0.48f, 0.4f, 0.4f));
            Sombra(ban.transform, new Vector2(0.65f, 0.6f));
            FichaEn(ban, "Banquito de parto", "Asiento bajo y abierto adelante para el parto vertical: mantiene la posición en cuclillas apoyada y aprovecha la gravedad en el expulsivo.", 0.6f);

            // ---------------- Portasuero junto a la cabecera
            GameObject ps = Modelo("Portasuero", eq, new Vector3(2.72f, 0f, 1.6f), -90f);
            Col(ps, new Vector3(0, 0.95f, 0), new Vector3(0.16f, 1.9f, 0.16f));
            Sombra(ps.transform, new Vector2(0.75f, 0.75f));
            GameObject gota = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gota.name = "Gota";
            Object.DestroyImmediate(gota.GetComponent<Collider>());
            gota.transform.SetParent(ps.transform, false);
            gota.transform.localPosition = new Vector3(-0.14f, 1.565f, 0f);
            gota.transform.localScale = new Vector3(0.007f, 0.011f, 0.007f);
            gota.GetComponent<MeshRenderer>().sharedMaterial = Material_("Via");
            gota.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Vida(ps, 2, gota.transform, 0.09f);
            FichaEn(ps, "Portasuero y bomba de infusión", "Para hidratación y medicamentos endovenosos, como la oxitocina, con goteo controlado y según indicación.", 1.3f);

            // ---------------- Sobre la mesa auxiliar: bandeja, Pinard, doppler, balanza y toallas
            Transform mesa = c.Find("MesaAuxiliar");
            Vector3 m0 = mesa != null ? mesa.localPosition : new Vector3(-0.36f, 0f, 1.5f);
            GameObject ban2 = Modelo("BandejaParto", eq, m0 + new Vector3(0.72f, 0.84f, -0.12f), 180f);
            Col(ban2, new Vector3(0, 0.03f, 0), new Vector3(0.44f, 0.06f, 0.29f));
            FichaEn(ban2, "Bandeja de atención del parto", "Dos pinzas para ligar el cordón, tijera, clamps umbilicales, pera de goma y gasas, sobre un campo estéril.", 0.35f);
            GameObject pin = Modelo("Pinard", eq, m0 + new Vector3(-0.22f, 0.872f, -0.2f), 15f);
            Col(pin, Vector3.zero, new Vector3(0.155f, 0.062f, 0.062f));
            Rb(pin, 0.2f);
            pin.AddComponent<ObjetoSala>();
            FichaEn(pin, "Estetoscopio de Pinard", "Se apoya sobre el dorso fetal, ubicado con las maniobras de Leopold, para auscultar los latidos sin electricidad.", 0.28f);
            GameObject dop = Modelo("DopplerFetal", eq, m0 + new Vector3(-0.42f, 0.865f, -0.2f), 170f);
            Col(dop, new Vector3(0, 0, 0), new Vector3(0.18f, 0.044f, 0.19f));
            Rb(dop, 0.3f);
            AudioSource ds = Audio(dop, Clip("LatidoFetalBucle"), true);
            Set(dop.AddComponent<ObjetoSala>(), "alTomar", ds);
            FichaEn(dop, "Doppler fetal portátil", "Tómalo para escuchar latidos fetales simulados a 140 lpm. El rango normal de la línea base es de 110 a 160 lpm.", 0.28f);
            GameObject bal = Modelo("BalanzaNeonatal", eq, m0 + new Vector3(-0.62f, 0.2925f, 0f), 180f);
            Col(bal, new Vector3(0, 0.07f, 0), new Vector3(0.54f, 0.14f, 0.31f));
            FichaEn(bal, "Balanza neonatal", "Pesa al recién nacido. El control puede esperar a que termine la primera hora de contacto piel con piel.", 0.4f);
            GameObject toa = Modelo("Toallas", eq, m0 + new Vector3(0.45f, 0.2925f, 0f), 180f);
            Col(toa, new Vector3(0, 0.11f, 0), new Vector3(0.37f, 0.22f, 0.27f));
            FichaEn(toa, "Toallas y mantas tibias", "Secar al recién nacido y cambiar el paño húmedo evita la pérdida de calor por evaporación.", 0.45f);

            // ---------------- Muro del fondo: dilatación y reloj
            Transform dil = Empty("PanelDilatacion", eq, new Vector3(-0.8f, 1.42f, 1.72f));
            dil.gameObject.AddComponent<PanelDilatacion>();
            GameObject reloj = Modelo("Reloj", eq, new Vector3(-0.1f, 2.15f, 1.975f), 180f);
            Material negro = Material_("PlasticoOscuro"), rojo = Material_("Segundero", new Color(0.85f, 0.2f, 0.2f), 0.3f, 0f);
            var rs = reloj.AddComponent<RelojSala>();
            Set(rs, "horas", Manecilla(reloj.transform, "Horas", 0.075f, 0.011f, 0.024f, negro));
            Set(rs, "minutos", Manecilla(reloj.transform, "Minutos", 0.115f, 0.007f, 0.027f, negro));
            Set(rs, "segundos", Manecilla(reloj.transform, "Segundos", 0.12f, 0.003f, 0.03f, rojo));

            // ---------------- Fichas del mobiliario que ya existía
            if (cama != null) FichaEn(cama.gameObject, "Cama de parto", "Se ajusta en altura y posición para acompañar distintas posturas. Tómala con el gatillo lateral o cerrando la mano para moverla.", 1.15f);

            Puntos(sala, c);
        }

        private static void Vida(GameObject go, int tipo, Transform objetivo, float caida)
        {
            var v = go.AddComponent<VidaSuave>();
            var so = new SerializedObject(v);
            so.FindProperty("tipo").enumValueIndex = tipo;
            so.FindProperty("objetivo").objectReferenceValue = objetivo;
            so.FindProperty("caida").floatValue = caida;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Puntos(SalaVirtual sala, Transform c)
        {
            Transform pts = c.Find("Puntos");
            var so = new SerializedObject(sala);
            SerializedProperty arr = so.FindProperty("puntos");
            foreach (var (name, pos, yaw) in new[] { ("Cuna", new Vector3(-1.45f, 0f, -0.66f), 212f), ("Balón", new Vector3(-0.35f, 0f, -0.05f), -86f) })
            {
                Transform p = pts.Find(name);
                if (p == null)
                {
                    p = Empty(name, pts, pos);
                    arr.arraySize++;
                    arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = p;
                }
                p.localPosition = pos;
                p.localRotation = Quaternion.Euler(0, yaw, 0);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ Piezas

        private static Transform Empty(string name, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go.transform;
        }

        private static GameObject Modelo(string name, Transform parent, Vector3 pos, float yaw)
        {
            GameObject go = Empty(name, parent, pos).gameObject;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/Modelos/{name}.fbx");
            var m = (GameObject)PrefabUtility.InstantiatePrefab(src);
            m.transform.SetParent(go.transform, false);
            foreach (MeshRenderer r in m.GetComponentsInChildren<MeshRenderer>())
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Material_(mats[i] != null ? mats[i].name : "PlasticoClaro");
                r.sharedMaterials = mats;
            }
            return go;
        }

        /// <summary>Material del recién nacido con las texturas del modelo entregado.</summary>
        private static Material PielBebe()
        {
            string path = Dir + "/Materiales/RecienNacido.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/Texturas/RecienNacido_Color.png"));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/Texturas/RecienNacido_Normal.png"));
            m.EnableKeyword("_NORMALMAP");
            m.SetColor("_BaseColor", new Color(0.95f, 0.95f, 0.95f));
            m.SetFloat("_Smoothness", 0.15f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Material_(string n)
        {
            if (n == "RecienNacido") return PielBebe();
            if (!Colores.TryGetValue(n, out var d)) d = (Color.magenta, 0.2f, 0f);
            return Material_(n, d.c, d.s, d.m);
        }

        private static Material Material_(string n, Color color, float smooth, float metal)
        {
            string path = $"{Dir}/Materiales/Eq_{n}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m; // no pisar ediciones hechas a mano
            bool unlit = n == "Calefactor" || n == "LuzCuna";
            m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
            m.SetColor("_BaseColor", color);
            if (!unlit)
            {
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metal);
            }
            if (n == "TelaLiana") m.SetFloat("_Cull", 0f);
            if (color.a < 1f)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void Col(GameObject go, Vector3 center, Vector3 size)
        {
            var b = go.AddComponent<BoxCollider>();
            b.center = center;
            b.size = size;
        }

        private static Rigidbody Rb(GameObject go, float mass)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 1.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            return rb;
        }

        private static PhysicsMaterial Rebote()
        {
            string p = Dir + "/Materiales/Balon.physicMaterial";
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(p);
            if (m != null) return m;
            m = new PhysicsMaterial { dynamicFriction = 0.7f, staticFriction = 0.8f, bounciness = 0.55f, bounceCombine = PhysicsMaterialCombine.Maximum };
            AssetDatabase.CreateAsset(m, p);
            return m;
        }

        private static Transform Manecilla(Transform reloj, string name, float largo, float ancho, float z, Material mat)
        {
            Transform t = Empty(name, reloj, new Vector3(0, 0, z));
            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.transform.SetParent(t, false);
            b.transform.localPosition = new Vector3(0, largo * 0.4f, 0);
            b.transform.localScale = new Vector3(ancho, largo, 0.002f);
            b.GetComponent<MeshRenderer>().sharedMaterial = mat;
            b.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return t;
        }

        private static void Sombra(Transform parent, Vector2 size)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Materiales/SombraContacto.mat");
            if (mat == null) return;
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "SombraContacto";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = new Vector3(0, 0.008f, 0);
            q.transform.localRotation = Quaternion.Euler(90, 0, 0);
            q.transform.localScale = new Vector3(size.x, size.y, 1);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static AudioSource Audio(GameObject go, AudioClip clip, bool loop)
        {
            var a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.loop = loop;
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.minDistance = 0.6f;
            a.maxDistance = 6f;
            a.volume = 0.8f;
            return a;
        }

        private static AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Dir}/Audio/{n}.wav");

        private static void FichaEn(GameObject go, string titulo, string texto, float altura)
        {
            var f = go.GetComponent<Ficha>();
            if (f == null) f = go.AddComponent<Ficha>();
            var so = new SerializedObject(f);
            so.FindProperty("titulo").stringValue = titulo;
            so.FindProperty("texto").stringValue = texto;
            so.FindProperty("altura").floatValue = altura;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Flag(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Import()
        {
            AssetDatabase.Refresh();
            foreach (string n in new[] { "Color", "Normal" })
            {
                var ti = AssetImporter.GetAtPath($"{Dir}/Texturas/RecienNacido_{n}.png") as TextureImporter;
                if (ti == null) continue;
                var type = n == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                int max = n == "Normal" ? 1024 : 2048;
                if (ti.textureType == type && ti.maxTextureSize == max) continue;
                ti.textureType = type;
                ti.maxTextureSize = max;
                ti.anisoLevel = 4;
                ti.SaveAndReimport();
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Dir + "/Modelos" }))
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (!mi.importAnimation && mi.animationType == ModelImporterAnimationType.None) continue;
                mi.importAnimation = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.animationType = ModelImporterAnimationType.None;
                mi.SaveAndReimport();
            }
        }
    }
}
#endif
