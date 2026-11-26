#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MRGame;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MRSetup
{
    /// <summary>
    /// Módulo 5b: sustituye el XR Origin manual por el rig de ejemplo de XRI con controles y manos
    /// (Hands Interaction Demo), pero conservando la Main Camera EXISTENTE (no se duplica ninguna cámara).
    /// Desactiva locomoción, teletransporte y gaze, que en realidad mixta desalinearían el mundo real
    /// con las anclas. Idempotente. Uso: menú Tools > MR Setup > 5b - Crear rig de interaccion.
    /// </summary>
    public static class MRInteractionRigSetup
    {
        private const string RigPrefab =
            "Assets/Samples/XR Interaction Toolkit/3.7.0/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab";
        private const string RigFinalName = "XR Origin (XR Rig)";

        private static readonly string[] ObjectsToDisable =
        {
            "Locomotion", "Gaze Interactor", "Gaze Stabilized", "Teleport Interactor", "Hand Teleport Interactor",
        };

        [MenuItem("Tools/MR Setup/5b - Crear rig de interaccion (controles y manos)")]
        public static void CreateRig()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
                {
                    Debug.LogError("[MRSetup] La escena activa no está guardada en disco.");
                    return;
                }

                XROrigin[] origins = UnityEngine.Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (origins.Length != 1)
                {
                    Debug.LogError($"[MRSetup] Se esperaba 1 XR Origin y hay {origins.Length}; abortado.");
                    return;
                }
                XROrigin oldOrigin = origins[0];
                if (HasInteractionRig(oldOrigin))
                {
                    Debug.LogWarning("[MRSetup] El rig de interacción ya existe; no se crea otro.");
                    return;
                }

                Camera myCam = oldOrigin.Camera;
                if (myCam == null)
                {
                    Debug.LogError("[MRSetup] El XR Origin actual no tiene cámara.");
                    return;
                }
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab);
                if (prefab == null)
                {
                    Debug.LogError($"[MRSetup] No se encontró el prefab: {RigPrefab}. Ejecuta antes 5a.");
                    return;
                }

                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();

                GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(rig, "Crear rig de interacción");
                PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                XROrigin newOrigin = rig.GetComponent<XROrigin>();
                Camera sampleCam = newOrigin.Camera;
                Transform offset = newOrigin.CameraFloorOffsetObject.transform;

                // 1) Mover la Main Camera existente dentro del nuevo rig.
                GameObject myCamGo = myCam.gameObject;
                myCamGo.transform.SetParent(offset, false);
                myCamGo.transform.localPosition = Vector3.zero;
                myCamGo.transform.localRotation = Quaternion.identity;
                myCamGo.transform.SetSiblingIndex(0);

                // 2) Reapuntar toda referencia a la cámara del ejemplo hacia la existente y borrar la del ejemplo.
                GameObject sampleCamGo = sampleCam.gameObject;
                int remapped = RemapCameraReferences(rig, sampleCamGo, myCam);
                newOrigin.Camera = myCam;
                UnityEngine.Object.DestroyImmediate(sampleCamGo);

                // 3) Retirar el XR Origin manual del módulo 3 (ya sin la cámara).
                UnityEngine.Object.DestroyImmediate(oldOrigin.gameObject);

                // 4) Gestores AR para planos/anclas en el nuevo origin y recableado del tablero.
                ARPlaneManager planes = rig.AddComponent<ARPlaneManager>();
                planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
                ARAnchorManager anchors = rig.AddComponent<ARAnchorManager>();
                rig.AddComponent<ARRaycastManager>();
                RewireTable(planes, anchors, newOrigin);

                // 5) Desactivar lo que no tiene sentido en MR.
                DisableForMixedReality(rig);

                rig.name = RigFinalName;
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();

                Debug.Log($"[MRSetup] Rig de interacción creado con la Main Camera existente ({remapped} referencias remapeadas). Escena guardada: {saved}");
                LogSummary(scene);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MRSetup] Error al crear el rig de interacción: {ex}");
            }
        }

        /// <summary>True si el origin ya contiene los controles/manos del rig de ejemplo (idempotencia).</summary>
        private static bool HasInteractionRig(XROrigin origin)
        {
            foreach (Transform t in origin.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Left Controller" || t.name == "Left Hand Interaction Visual")
                    return true;
            }
            return false;
        }

        /// <summary>Remapea referencias serializadas de la cámara del ejemplo a la Main Camera existente.</summary>
        private static int RemapCameraReferences(GameObject rig, GameObject sampleCamGo, Camera myCam)
        {
            GameObject myGo = myCam.gameObject;
            var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>
            {
                { sampleCamGo, myGo },
                { sampleCamGo.transform, myGo.transform },
            };
            AddIfBoth(map, sampleCamGo.GetComponent<Camera>(), myCam);
            AddIfBoth(map, sampleCamGo.GetComponent<AudioListener>(), myGo.GetComponent<AudioListener>());
            AddIfBoth(map, sampleCamGo.GetComponent<TrackedPoseDriver>(), myGo.GetComponent<TrackedPoseDriver>());

            int count = 0;
            foreach (Component c in rig.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c.gameObject == sampleCamGo) continue;
                SerializedObject so = new SerializedObject(c);
                SerializedProperty it = so.GetIterator();
                bool changed = false;
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    UnityEngine.Object value = it.objectReferenceValue;
                    if (value != null && map.TryGetValue(value, out UnityEngine.Object replacement))
                    {
                        it.objectReferenceValue = replacement;
                        changed = true;
                        count++;
                    }
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
            return count;
        }

        private static void AddIfBoth(Dictionary<UnityEngine.Object, UnityEngine.Object> map, UnityEngine.Object from, UnityEngine.Object to)
        {
            if (from != null && to != null) map[from] = to;
        }

        /// <summary>Reapunta el MRTableAnchor a los gestores del nuevo XR Origin.</summary>
        private static void RewireTable(ARPlaneManager planes, ARAnchorManager anchors, XROrigin origin)
        {
            MRTableAnchor table = UnityEngine.Object.FindFirstObjectByType<MRTableAnchor>();
            if (table == null)
            {
                Debug.LogWarning("[MRSetup] No hay MRTableAnchor que recablear (¿falta el módulo 4?).");
                return;
            }
            SerializedObject so = new SerializedObject(table);
            so.FindProperty("planeManager").objectReferenceValue = planes;
            so.FindProperty("anchorManager").objectReferenceValue = anchors;
            so.FindProperty("xrOrigin").objectReferenceValue = origin;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Desactiva locomoción, teletransporte, gaze y el CharacterController del rig.</summary>
        private static void DisableForMixedReality(GameObject rig)
        {
            foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (ObjectsToDisable.Contains(t.name))
                {
                    t.gameObject.SetActive(false);
                    Debug.Log($"[MRSetup] Desactivado: {t.name}");
                }
            }
            CharacterController cc = rig.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
        }

        private static void LogSummary(Scene scene)
        {
            StringBuilder sb = new StringBuilder("[MRSetup] Raíces de la escena:\n");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                int cams = root.GetComponentsInChildren<Camera>(true).Length;
                sb.AppendLine($"- {root.name} (cámaras: {cams})");
            }
            Debug.Log(sb.ToString());
        }
    }
}
#endif
