#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace MRSetup
{
    /// <summary>
    /// Módulo 2 del setup de Realidad Mixta para Meta Quest 3/3S: plataforma Android,
    /// OpenXR (loader + features) y ajustes de Player. Ejecutar en orden:
    /// 2a (cambia a Android y reimporta) y luego 2b (configura XR y Player).
    /// </summary>
    public static class MRProjectSetup
    {
        private const BuildTargetGroup Group = BuildTargetGroup.Android;
        private const string OpenXRLoader = "UnityEngine.XR.OpenXR.OpenXRLoader";

        /// <summary>Nombres de clase exactos de los features OpenXR que se habilitan para MR en Quest.</summary>
        private static readonly string[] FeatureTypeNames =
        {
            "MetaQuestFeature",                    // Soporte Meta Quest
            "ARSessionFeature",
            "ARCameraFeature",                     // Passthrough
            "ARAnchorFeature",                     // Anclas espaciales
            "ARPlaneFeature",                      // Detección de planos
            "ARRaycastFeature",
            "ARBoundingBoxFeature",                // Objetos de la escena (mesas, etc.)
            "AROcclusionFeature",                  // Oclusión por profundidad
            "HandTracking",                        // XR Hands
            "MetaHandTrackingAim",
            "HandInteractionProfile",
            "HandCommonPosesInteraction",
            "PalmPoseInteraction",
            "OculusTouchControllerProfile",
            "MetaQuestTouchPlusControllerProfile",
            "MetaQuestTouchProControllerProfile",
        };

        /// <summary>Paso 2a: cambia la plataforma activa a Android (provoca un reimport largo).</summary>
        [MenuItem("Tools/MR Setup/2a - Cambiar plataforma a Android")]
        public static void SwitchToAndroid()
        {
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
                {
                    Debug.Log("[MRSetup] La plataforma activa ya es Android.");
                    return;
                }
                Debug.Log("[MRSetup] Cambiando a Android... (puede tardar varios minutos)");
                EditorApplication.delayCall += () =>
                {
                    bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(Group, BuildTarget.Android);
                    Debug.Log($"[MRSetup] SwitchActiveBuildTarget -> {ok}");
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MRSetup] Error al cambiar a Android: {ex}");
            }
        }

        /// <summary>Paso 2b: asigna OpenXR, habilita features de Quest y ajusta Player Settings.</summary>
        [MenuItem("Tools/MR Setup/2b - Configurar OpenXR y Player para Quest")]
        public static void ConfigureXR()
        {
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                {
                    Debug.LogError("[MRSetup] Primero ejecuta '2a - Cambiar plataforma a Android'.");
                    return;
                }

                if (!AssignOpenXRLoader()) return;
                EnableOpenXRFeatures();
                ConfigurePlayer();

                AssetDatabase.SaveAssets();
                Debug.Log("[MRSetup] Módulo 2 completado.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MRSetup] Error al configurar XR: {ex}");
            }
        }

        /// <summary>Activa XR Plug-in Management en Android y asigna el loader de OpenXR.</summary>
        private static bool AssignOpenXRLoader()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                out XRGeneralSettingsPerBuildTarget perTarget);
            XRGeneralSettings settings = perTarget != null ? perTarget.SettingsForBuildTarget(Group) : null;

            if (settings == null || settings.AssignedSettings == null)
            {
                // Abrir la página de XR Plug-in Management crea los assets de configuración.
                SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
                Debug.LogWarning("[MRSetup] Se abrió Project Settings > XR Plug-in Management para crear la " +
                                 "configuración. Marca 'Initialize XR on Startup' si aparece y vuelve a ejecutar 2b.");
                return false;
            }

            settings.InitManagerOnStart = true;
            bool assigned = XRPackageMetadataStore.AssignLoader(settings.AssignedSettings, OpenXRLoader, Group);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(settings.AssignedSettings);
            Debug.Log($"[MRSetup] Loader OpenXR asignado en Android: {assigned}");
            return true;
        }

        /// <summary>Habilita los features OpenXR relevantes para MR en Quest y registra todos los disponibles.</summary>
        private static void EnableOpenXRFeatures()
        {
            OpenXRSettings openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(Group);
            if (openXr == null)
            {
                Debug.LogError("[MRSetup] No existe OpenXRSettings para Android; vuelve a ejecutar 2b.");
                return;
            }

            foreach (OpenXRFeature feature in openXr.GetFeatures())
            {
                bool wanted = FeatureTypeNames.Contains(feature.GetType().Name);
                if (wanted && !feature.enabled)
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                }
                Debug.Log($"[MRSetup] Feature '{feature.name}' -> enabled={feature.enabled}");
            }
            EditorUtility.SetDirty(openXr);
        }

        /// <summary>Ajustes de Player recomendados por Meta para Quest 3/3S (IL2CPP, ARM64, Vulkan).</summary>
        private static void ConfigurePlayer()
        {
            NamedBuildTarget android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            Debug.Log("[MRSetup] Player Settings de Android configurados (IL2CPP, ARM64, Vulkan, API 32+).");
        }
    }
}
#endif
