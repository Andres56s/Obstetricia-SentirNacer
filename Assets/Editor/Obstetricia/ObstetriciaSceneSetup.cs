#if UNITY_EDITOR
using System;
using Obstetricia;
using Obstetricia.Data;
using Obstetricia.Detection;
using Obstetricia.Media;
using Obstetricia.Modules;
using Obstetricia.Tools;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Meta;

namespace ObstetriciaSetup
{
    /// <summary>
    /// Paso 5 del setup MR: construye la experiencia de Obstetricia (mesa UCN, programa Fisio-balón con pelvis 3D
    /// activada por la pelota, programa Legal "Ley Integral de la Mujer" y reproductor 360°).
    /// Retira la mesa de pruebas anterior (GameTable), crea materiales y datos, y guarda la escena.
    /// Idempotente: reconstruye "ObstetriciaMR" sin tocar los datos ya editados.
    /// </summary>
    public static class ObstetriciaSceneSetup
    {
        private const string RootName = "ObstetriciaMR";
        private const string Base = "Assets/Obstetricia";
        private const string DataFolder = Base + "/Data";
        private const string MatFolder = Base + "/Materials";

        [MenuItem("Tools/MR Setup/5 - Construir experiencia Obstetricia")]
        public static void Build()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                XROrigin origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
                if (origin == null)
                {
                    Debug.LogError("[Obstetricia] No hay XR Origin en la escena; ejecuta antes los pasos 1-4.");
                    return;
                }

                EnsureFolders();
                ObstetriciaTheme theme = BuildTheme();
                QuizData legal = LoadOrCreate<QuizData>(DataFolder + "/LeyIntegral_Quiz.asset", ObstetriciaContent.FillLegal);
                QuizData evaluation = LoadOrCreate<QuizData>(DataFolder + "/FisioBalon_Evaluacion.asset", ObstetriciaContent.FillEvaluation);
                FisioModuleData fisioData = LoadOrCreate<FisioModuleData>(DataFolder + "/FisioBalon_Programa.asset", ObstetriciaContent.FillFisio);
                foreach (FisioSection sec in fisioData.sections)
                    if (sec.menuLabel == "Mecanismos del parto" && sec.kind != FisioSectionKind.Mechanisms)
                    {
                        sec.kind = FisioSectionKind.Mechanisms;
                        sec.activity = "Mecanismos del parto";
                        EditorUtility.SetDirty(fisioData);
                    }
                if (fisioData.evaluation == null)
                {
                    fisioData.evaluation = evaluation;
                    EditorUtility.SetDirty(fisioData);
                }

                RemoveOldTable();
                EnsureEventSystem();
                EnableCameraImages();

                GameObject old = GameObject.Find(RootName);
                if (old != null) Undo.DestroyObjectImmediate(old);
                BuildHierarchy(scene, origin, theme, legal, evaluation, fisioData);

                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Obstetricia] Experiencia construida. Escena guardada: {saved}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Obstetricia] Error en el setup: {ex}");
            }
        }

        private static void BuildHierarchy(Scene scene, XROrigin origin, ObstetriciaTheme theme, QuizData legal,
            QuizData evaluation, FisioModuleData fisioData)
        {
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Crear ObstetriciaMR");
            root.transform.position = new Vector3(0f, 0f, 1.1f);

            Transform cam = origin.Camera != null ? origin.Camera.transform : Camera.main.transform;
            ARCameraManager cameraManager = cam.GetComponent<ARCameraManager>();
            ARAnchorManager anchorManager = origin.GetComponent<ARAnchorManager>();

            Video360Player video = Child<Video360Player>(root, "Video360", Vector3.zero);
            Set(video, "sphereMaterial", theme.video360);
            Set(video, "head", cam);

            BallDetector detector = Child<BallDetector>(root, "BallDetector", Vector3.zero);
            Set(detector, "cameraManager", cameraManager);
            Set(detector, "head", cam);
            var model = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(Base + "/Modelos/yolo_ball_detector.onnx");
            if (model == null)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:ModelAsset", new[] { Base }))
                {
                    model = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    if (model != null) break;
                }
            }
            Set(detector, "model", model);

            PenDrawer pen = Child<PenDrawer>(root, "Pen", Vector3.zero);
            Transform rightController = FindDeep(origin.transform, "Right Controller");
            Set(pen, "tip", rightController);
            Set(pen, "leftTip", FindDeep(origin.transform, "Left Controller"));
            Set(pen, "rightHandTip", FindChildPath(origin.transform, "Right Hand", "Poke Interactor"));
            Set(pen, "leftHandTip", FindChildPath(origin.transform, "Left Hand", "Poke Interactor"));
            Set(pen, "lineMaterial", theme.line);

            HubController hub = Child<HubController>(root, "HubUCN", Vector3.zero);
            Set(hub, "theme", theme);

            QuizController legalQuiz = Child<QuizController>(root, "ProgramaLegal", new Vector3(0f, 1.3f, 0f));
            Set(legalQuiz, "data", legal);
            Set(legalQuiz, "video", video);

            FisioModuleController fisio = Child<FisioModuleController>(root, "ProgramaFisioBalon", Vector3.zero);
            AnatomyViewer anatomy = Child<AnatomyViewer>(fisio.gameObject, "Anatomia", Vector3.zero);
            Set(anatomy, "detector", detector);
            Set(anatomy, "theme", theme);
            Set(anatomy, "head", cam);
            QuizController evalQuiz = Child<QuizController>(fisio.gameObject, "Evaluacion", new Vector3(0f, 1.33f, 0.02f));
            Set(evalQuiz, "data", evaluation);
            SetFloat(evalQuiz, "canvasScale", 0.00072f);
            Set(fisio, "data", fisioData);
            Set(fisio, "video", video);
            Set(fisio, "anatomy", anatomy);
            Set(fisio, "evaluation", evalQuiz);
            Set(fisio, "pen", pen);

            TutorialController tutorial = Child<TutorialController>(root, "Tutorial", Vector3.zero);
            Set(tutorial, "grabMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MR/GrabCube.mat"));
            ManosSetup.Tutorial(tutorial);
            ManosSetup.Aplicar();

            ObstetriciaApp app = root.AddComponent<ObstetriciaApp>();
            Set(app, "tutorial", tutorial);
            Set(app, "xrOrigin", origin);
            Set(app, "anchorManager", anchorManager);
            Set(app, "hub", hub);
            Set(app, "fisio", fisio);
            Set(app, "legal", legalQuiz);
            Set(app, "video", video);
            Set(app, "sala", SalaBuilder.Ensure(scene, origin, theme));
        }

        // ------------------------------------------------------------------ Escena

        /// <summary>Retira la mesa de pruebas anterior (se desactiva para poder recuperarla si hiciera falta).</summary>
        private static void RemoveOldTable()
        {
            GameObject oldGame = GameObject.Find("MixedRealityGame");
            if (oldGame == null) return;
            Undo.RecordObject(oldGame, "Retirar mesa anterior");
            oldGame.SetActive(false);
            Debug.Log("[Obstetricia] Mesa de pruebas 'MixedRealityGame/GameTable' retirada (desactivada). Puedes borrarla de la jerarquía.");
        }

        private static void EnsureEventSystem()
        {
            EventSystem es = UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                Undo.RegisterCreatedObjectUndo(go, "Crear EventSystem");
                es = go.GetComponent<EventSystem>();
            }
            foreach (BaseInputModule module in es.GetComponents<BaseInputModule>())
                if (!(module is XRUIInputModule)) Undo.DestroyObjectImmediate(module);
            if (es.GetComponent<XRUIInputModule>() == null) Undo.AddComponent<XRUIInputModule>(es.gameObject);
        }

        /// <summary>Activa "Camera Image Support" (imagen CPU de la cámara passthrough) en OpenXR Android.</summary>
        private static void EnableCameraImages()
        {
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            ARCameraFeature feature = settings != null ? settings.GetFeature<ARCameraFeature>() : null;
            if (feature == null)
            {
                Debug.LogWarning("[Obstetricia] No se encontró la feature 'Meta Quest: Camera (Passthrough)' en OpenXR Android.");
                return;
            }
            var so = new SerializedObject(feature);
            SerializedProperty prop = so.FindProperty("m_CameraImageSupport");
            if (prop != null && !prop.boolValue)
            {
                prop.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Obstetricia] OpenXR: 'Camera Image Support' activado (necesario para detectar la pelota).");
            }
            if (!feature.enabled) feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }

        // ------------------------------------------------------------------ Assets

        private static void EnsureFolders()
        {
            Folder("Assets", "Obstetricia");
            Folder(Base, "Data");
            Folder(Base, "Materials");
            Folder(Base, "Modelos");
            Folder(Base, "Imagenes");
            Folder("Assets", "StreamingAssets");
            Folder("Assets/StreamingAssets", "Videos360");
        }

        private static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        private static ObstetriciaTheme BuildTheme()
        {
            var theme = LoadOrCreate<ObstetriciaTheme>(DataFolder + "/ObstetriciaTheme.asset", null);
            if (theme.pedestalBody == null) theme.pedestalBody = LitMaterial("PedestalCuerpo", new Color(0.93f, 0.94f, 0.96f), 0.75f);
            if (theme.pedestalTop == null) theme.pedestalTop = LitMaterial("PedestalCubierta", new Color(0.07f, 0.28f, 0.72f), 0.85f, new Color(0.02f, 0.09f, 0.3f));
            if (theme.glow == null) theme.glow = UnlitMaterial("Brillo", new Color(0.35f, 0.85f, 1f), false);
            if (theme.bone == null) theme.bone = LitMaterial("Hueso", new Color(0.93f, 0.89f, 0.8f), 0.35f);
            if (theme.boneDetail == null) theme.boneDetail = LitMaterial("HuesoDetalle", new Color(0.8f, 0.74f, 0.63f), 0.3f);
            if (theme.video360 == null) theme.video360 = UnlitMaterial("Video360", Color.white, true);
            if (theme.line == null) theme.line = SpriteMaterial("Lineas");
            if (theme.fetalHead == null) theme.fetalHead = LitMaterial("CabezaFetal", new Color(0.96f, 0.76f, 0.68f), 0.45f);
            if (theme.boneGhost == null) theme.boneGhost = GhostMaterial("HuesoTranslucido",
                AssetDatabase.LoadAssetAtPath<Texture2D>(Base + "/Modelos/Pelvis/PelvisFemenina_BaseColor.png"));
            if (theme.ucnLogo == null) theme.ucnLogo = AssetDatabase.LoadAssetAtPath<Texture2D>(Base + "/Imagenes/LogoUCEN.png");
            LetrasLogo(theme);
            EditorUtility.SetDirty(theme);
            return theme;
        }

        /// <summary>Asigna las letras 3D del logo (animación de la mesa).</summary>
        public static void LetrasLogo(ObstetriciaTheme theme)
        {
            string path = Base + "/Modelos/LetrasUCEN.fbx";
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) return;
            if (mi.importAnimation || mi.animationType != ModelImporterAnimationType.None)
            {
                mi.importAnimation = false;
                mi.animationType = ModelImporterAnimationType.None;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.SaveAndReimport();
            }
            var letras = new Mesh[4];
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Mesh m)
                    for (int i = 0; i < 4; i++)
                        if (m.name == "Letra" + "UCEN"[i]) letras[i] = m;
            theme.logoLetras = letras;
            if (theme.logoLetra == null)
            {
                theme.logoLetra = new Material(Shader.Find("MR/LetraLogo")) { name = "LetraLogo" };
                AssetDatabase.CreateAsset(theme.logoLetra, MatFolder + "/LetraLogo.mat");
            }
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
        }

        private static T LoadOrCreate<T>(string path, Action<T> fill) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            fill?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Material LitMaterial(string name, Color color, float smoothness, Color? emission = null)
        {
            string path = $"{MatFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>URP Lit transparente (se crea como asset para que el shader transparente se incluya en la build).</summary>
        private static Material GhostMaterial(string name, Texture2D baseMap)
        {
            string path = $"{MatFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Smoothness", 0.4f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", new Color(1f, 0.97f, 0.9f, 0.32f));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material UnlitMaterial(string name, Color color, bool insideOut)
        {
            string path = $"{MatFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            mat.SetColor("_BaseColor", color);
            if (insideOut) mat.SetFloat("_Cull", 1f); // Render Face = Back: se ve desde dentro de la esfera
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material SpriteMaterial(string name)
        {
            string path = $"{MatFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Sprites/Default")) { name = name };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ------------------------------------------------------------------ Utilidades

        private static T Child<T>(GameObject parent, string name, Vector3 localPos) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            return go.AddComponent<T>();
        }

        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[Obstetricia] Campo '{field}' no existe en {target.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(UnityEngine.Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(UnityEngine.Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindChildPath(Transform root, string parentName, string childName)
        {
            Transform parent = FindDeep(root, parentName);
            if (parent == null) return null;
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == childName) return t;
            return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
#endif
