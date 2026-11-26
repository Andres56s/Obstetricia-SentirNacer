#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace MRSetup
{
    /// <summary>
    /// Módulo 3 del setup de Realidad Mixta para Meta Quest 3/3S.
    /// Convierte la Main Camera EXISTENTE en la cámara de un XR Origin (sin crear otra cámara),
    /// la prepara para passthrough (fondo transparente + ARCameraManager) y ajusta URP para
    /// conservar el canal alfa. Es idempotente: si el XR Origin ya existe, no duplica nada.
    /// Uso: menú Tools > MR Setup > 3 - Crear XR Origin con Main Camera.
    /// </summary>
    public static class MRSceneSetup
    {
        private const string OriginName = "XR Origin (XR Rig)";
        private const string OffsetName = "Camera Offset";
        private const string SessionName = "AR Session";

        /// <summary>Crea el XR Origin reutilizando la Main Camera y deja la escena guardada.</summary>
        [MenuItem("Tools/MR Setup/3 - Crear XR Origin con Main Camera")]
        public static void CreateXROrigin()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
                {
                    Debug.LogError("[MRSetup] La escena activa no está guardada en disco.");
                    return;
                }

                if (UnityEngine.Object.FindFirstObjectByType<XROrigin>() != null)
                {
                    Debug.LogWarning("[MRSetup] Ya existe un XR Origin en la escena; no se crea otro.");
                    return;
                }

                Camera cam = FindMainCamera();
                if (cam == null)
                {
                    Debug.LogError("[MRSetup] No se encontró la Main Camera existente.");
                    return;
                }

                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();

                GameObject originGo = new GameObject(OriginName);
                Undo.RegisterCreatedObjectUndo(originGo, "Crear XR Origin");
                GameObject offsetGo = new GameObject(OffsetName);
                offsetGo.transform.SetParent(originGo.transform, false);

                // Reutiliza la Main Camera: se mueve bajo Camera Offset, no se duplica.
                Undo.SetTransformParent(cam.transform, offsetGo.transform, "Mover Main Camera al XR Origin");
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localRotation = Quaternion.identity;
                cam.transform.localScale = Vector3.one;

                XROrigin origin = originGo.AddComponent<XROrigin>();
                origin.Origin = originGo;
                origin.CameraFloorOffsetObject = offsetGo;
                origin.Camera = cam;
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

                ConfigureCameraForPassthrough(cam);
                EnsureTrackedPoseDriver(cam.gameObject);
                EnsureARSession(scene);
                ConfigureUrpForAlpha();

                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();

                Debug.Log($"[MRSetup] XR Origin creado con la Main Camera existente. Escena guardada: {saved}");
                LogHierarchy(scene);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MRSetup] Error al crear el XR Origin: {ex}");
            }
        }

        /// <summary>Busca la Main Camera existente por tag y, si falla, por nombre.</summary>
        private static Camera FindMainCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam;
            GameObject go = GameObject.Find("Main Camera");
            return go != null ? go.GetComponent<Camera>() : null;
        }

        /// <summary>Fondo transparente y ARCameraManager para el passthrough de Meta Quest.</summary>
        private static void ConfigureCameraForPassthrough(Camera cam)
        {
            Undo.RecordObject(cam, "Configurar passthrough");
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            if (cam.GetComponent<ARCameraManager>() == null)
                Undo.AddComponent<ARCameraManager>(cam.gameObject);

            EditorUtility.SetDirty(cam);
        }

        /// <summary>Añade el TrackedPoseDriver (Input System) con bindings del HMD si no existe.</summary>
        private static void EnsureTrackedPoseDriver(GameObject camGo)
        {
            if (camGo.GetComponent<TrackedPoseDriver>() != null) return;

            TrackedPoseDriver driver = Undo.AddComponent<TrackedPoseDriver>(camGo);
            driver.positionInput = new InputActionProperty(new InputAction(
                "Position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            driver.rotationInput = new InputActionProperty(new InputAction(
                "Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            driver.trackingStateInput = new InputActionProperty(new InputAction(
                "Tracking State", InputActionType.Value, "<XRHMD>/trackingState", expectedControlType: "Integer"));
            EditorUtility.SetDirty(driver);
        }

        /// <summary>Crea la AR Session una sola vez.</summary>
        private static void EnsureARSession(Scene scene)
        {
            if (UnityEngine.Object.FindFirstObjectByType<ARSession>() != null) return;

            GameObject go = new GameObject(SessionName);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Crear AR Session");
            go.AddComponent<ARSession>();
        }

        /// <summary>
        /// Desactiva HDR y fija MSAA 4x en los URP Assets en uso: el formato HDR de móvil no
        /// conserva alfa y el passthrough se vería negro.
        /// </summary>
        private static void ConfigureUrpForAlpha()
        {
            HashSet<UniversalRenderPipelineAsset> assets = new HashSet<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset def) assets.Add(def);
            for (int i = 0; i < QualitySettings.count; i++)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset urp) assets.Add(urp);
            }

            foreach (UniversalRenderPipelineAsset asset in assets)
            {
                asset.supportsHDR = false;
                asset.msaaSampleCount = 4;
                EditorUtility.SetDirty(asset);
                Debug.Log($"[MRSetup] URP '{asset.name}': HDR=off, MSAA=4x");
            }
        }

        /// <summary>Escribe la jerarquía de la escena en la Console para verificarla.</summary>
        private static void LogHierarchy(Scene scene)
        {
            StringBuilder sb = new StringBuilder("[MRSetup] Jerarquía:\n");
            foreach (GameObject root in scene.GetRootGameObjects()) Dump(root.transform, 0, sb);
            Debug.Log(sb.ToString());
        }

        private static void Dump(Transform t, int level, StringBuilder sb)
        {
            sb.AppendLine($"{new string(' ', level * 2)}- {t.name}");
            foreach (Transform child in t) Dump(child, level + 1, sb);
        }
    }
}
#endif
