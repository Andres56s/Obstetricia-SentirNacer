#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace MRSetup
{
    /// <summary>
    /// Módulo 6: iluminación y materiales coherentes con el passthrough.
    /// - Material "solo sombra" sobre la mesa (sombras virtuales sobre el mundo real).
    /// - Luz direccional con sombras suaves y ambiente plano neutro.
    /// - Ajustes de sombras en los URP assets (ligeros para Quest).
    /// - Cámara sin post-proceso (conserva el alfa del passthrough).
    /// - AROcclusionManager con profundidad del entorno.
    /// - Revisión de MetaQuestFeature (Quest 3/3S).
    /// Idempotente. Menú: Tools > MR Setup > 6 - Iluminacion, sombras y oclusion.
    /// </summary>
    public static class MRLightingSetup
    {
        private const string ShaderName = "MR/ShadowCatcher";
        private const string MatPath = "Assets/Materials/MR/ShadowCatcher.mat";

        [MenuItem("Tools/MR Setup/6 - Iluminacion, sombras y oclusion")]
        public static void Run()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
                {
                    Debug.LogError("[MRSetup] La escena activa no está guardada en disco.");
                    return;
                }

                CreateShadowCatcher();
                ConfigureLight();
                ConfigureAmbient();
                ConfigureUrpAssets();
                ConfigureCamera();
                CheckQuestFeature();

                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log($"[MRSetup] Módulo 6 aplicado. Escena guardada: {saved}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MRSetup] Error en módulo 6: {ex}");
            }
        }

        private static void CreateShadowCatcher()
        {
            GameObject root = GameObject.Find("MixedRealityGame");
            Transform items = root != null ? root.transform.Find("GameTable/GameItems") : null;
            if (items == null)
            {
                Debug.LogWarning("[MRSetup] No hay GameItems; se omite el ShadowCatcher.");
                return;
            }
            if (items.Find("ShadowCatcher") != null)
            {
                Debug.Log("[MRSetup] ShadowCatcher ya existe.");
                return;
            }
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[MRSetup] No se encontró el shader {ShaderName}; revisa Assets/Shaders/MRShadowCatcher.shader.");
                return;
            }
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MatPath);
            }

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "ShadowCatcher";
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(items, false);
            quad.transform.localPosition = new Vector3(0f, 0.002f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(2.5f, 2.5f, 1f);
            Renderer r = quad.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            Debug.Log("[MRSetup] ShadowCatcher creado bajo GameItems.");
        }

        private static void ConfigureLight()
        {
            Light dir = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.type == LightType.Directional);
            if (dir == null)
            {
                Debug.LogWarning("[MRSetup] No hay luz direccional.");
                return;
            }
            dir.shadows = LightShadows.Soft;
            dir.shadowStrength = 0.85f;
            dir.intensity = 1.0f;
            dir.color = new Color(1f, 0.96f, 0.9f);
            dir.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Debug.Log("[MRSetup] Luz direccional configurada (sombras suaves).");
        }

        private static void ConfigureAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            Debug.Log("[MRSetup] Ambiente plano neutro aplicado.");
        }

        private static void ConfigureUrpAssets()
        {
            var assets = new HashSet<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset g) assets.Add(g);
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset q) assets.Add(q);
            }
            foreach (UniversalRenderPipelineAsset a in assets)
            {
                try
                {
                    a.shadowDistance = 5f;
                    a.shadowCascadeCount = 1;
                    SerializedObject so = new SerializedObject(a);
                    SerializedProperty soft = so.FindProperty("m_SoftShadowsSupported");
                    if (soft != null) soft.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(a);
                    Debug.Log($"[MRSetup] URP '{a.name}': shadowDistance=5, cascadas=1, soft={(soft != null)}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MRSetup] No se pudo ajustar '{a.name}': {ex.Message}");
                }
            }
        }

        private static void ConfigureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[MRSetup] No se encontró Main Camera.");
                return;
            }
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;

            AROcclusionManager occ = cam.GetComponent<AROcclusionManager>();
            if (occ == null) occ = cam.gameObject.AddComponent<AROcclusionManager>();
            occ.requestedEnvironmentDepthMode = EnvironmentDepthMode.Medium;
            occ.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;
            EditorUtility.SetDirty(cam.gameObject);
            Debug.Log("[MRSetup] Cámara: sin post-proceso, AROcclusionManager (Medium, PreferEnvironmentOcclusion).");
        }

        /// <summary>Lista las propiedades de MetaQuestFeature y marca como activas las de Quest 3/3S.</summary>
        private static void CheckQuestFeature()
        {
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
            {
                Debug.LogWarning("[MRSetup] Sin OpenXRSettings para Android.");
                return;
            }
            OpenXRFeature feature = settings.GetFeatures()
                .FirstOrDefault(f => f != null && f.GetType().Name == "MetaQuestFeature");
            if (feature == null)
            {
                Debug.LogWarning("[MRSetup] MetaQuestFeature no encontrada.");
                return;
            }
            SerializedObject so = new SerializedObject(feature);
            var sb = new StringBuilder("[MRSetup] MetaQuestFeature propiedades:\n");
            SerializedProperty it = so.GetIterator();
            bool changed = false;
            while (it.NextVisible(true))
            {
                sb.AppendLine($"  {it.propertyPath} ({it.propertyType}) = {Describe(it)}");
                if (it.propertyType == SerializedPropertyType.Boolean &&
                    it.name.IndexOf("quest3", StringComparison.OrdinalIgnoreCase) >= 0 && !it.boolValue)
                {
                    it.boolValue = true;
                    changed = true;
                    sb.AppendLine($"    -> activado {it.name}");
                }
            }
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(feature);
            }
            Debug.Log(sb.ToString());
        }

        private static string Describe(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Boolean: return p.boolValue.ToString();
                case SerializedPropertyType.Integer: return p.intValue.ToString();
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Enum: return p.enumDisplayNames.Length > p.enumValueIndex && p.enumValueIndex >= 0
                    ? p.enumDisplayNames[p.enumValueIndex] : p.enumValueIndex.ToString();
                default: return "-";
            }
        }
    }
}
#endif
