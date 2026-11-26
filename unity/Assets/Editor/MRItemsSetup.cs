#if UNITY_EDITOR
using System;
using System.IO;
using MRGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MRSetup
{
    /// <summary>
    /// Módulo 5c: crea objetos agarrables/lanzables con física y un botón tocable bajo
    /// MixedRealityGame/GameTable/GameItems. Idempotente (no duplica por nombre).
    /// Uso: menú Tools > MR Setup > 5c - Crear objetos interactivos.
    /// </summary>
    public static class MRItemsSetup
    {
        private const string MatFolder = "Assets/Materials/MR";
        private const string PokePrefab =
            "Assets/Samples/XR Interaction Toolkit/3.7.0/Hands Interaction Demo/DemoAssets/Prefabs/PokeButton.prefab";

        [MenuItem("Tools/MR Setup/5c - Crear objetos interactivos")]
        public static void CreateItems()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                GameObject root = GameObject.Find("MixedRealityGame");
                Transform items = root != null ? root.transform.Find("GameTable/GameItems") : null;
                if (items == null)
                {
                    Debug.LogError("[MRSetup] No existe MixedRealityGame/GameTable/GameItems (módulo 4).");
                    return;
                }

                Directory.CreateDirectory(MatFolder);
                PhysicsMaterial bouncy = GetPhysicsMaterial("Bouncy", 0.5f, 0.4f, 0.6f);

                CreateGrabbable(items, "GrabCube", PrimitiveType.Cube, new Vector3(0.08f, 0.08f, 0.08f),
                    new Vector3(-0.30f, 0.15f, 0f), new Color(0.9f, 0.25f, 0.2f), 0.3f, bouncy);
                CreateGrabbable(items, "GrabBall", PrimitiveType.Sphere, new Vector3(0.09f, 0.09f, 0.09f),
                    new Vector3(-0.10f, 0.15f, 0f), new Color(0.2f, 0.6f, 0.95f), 0.15f, bouncy);
                CreateGrabbable(items, "GrabCylinder", PrimitiveType.Cylinder, new Vector3(0.07f, 0.06f, 0.07f),
                    new Vector3(0.10f, 0.15f, 0f), new Color(0.95f, 0.8f, 0.2f), 0.25f, bouncy);
                CreateGrabbable(items, "GrabCapsule", PrimitiveType.Capsule, new Vector3(0.06f, 0.07f, 0.06f),
                    new Vector3(0.28f, 0.15f, 0f), new Color(0.35f, 0.8f, 0.4f), 0.2f, bouncy);

                CreatePokeButton(items);

                if (items.GetComponent<MRItemRespawner>() == null)
                    items.gameObject.AddComponent<MRItemRespawner>();

                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log($"[MRSetup] Objetos interactivos listos bajo GameItems ({items.childCount} hijos). Escena guardada: {saved}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MRSetup] Error al crear objetos interactivos: {ex}");
            }
        }

        private static void CreateGrabbable(Transform parent, string name, PrimitiveType type, Vector3 scale,
            Vector3 localPos, Color color, float mass, PhysicsMaterial physMat)
        {
            if (parent.Find(name) != null)
            {
                Debug.Log($"[MRSetup] Ya existe {name}; se omite.");
                return;
            }
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            Collider col = go.GetComponent<Collider>();
            col.sharedMaterial = physMat;

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.1f;

            XRGrabInteractable grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = true;
            grab.useDynamicAttach = true;
            grab.throwVelocityScale = 1.2f;

            go.GetComponent<Renderer>().sharedMaterial = GetMaterial(name, color);
        }

        private static void CreatePokeButton(Transform parent)
        {
            const string name = "PokeButton";
            if (parent.Find(name) != null) return;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PokePrefab);
            if (prefab == null)
            {
                Debug.LogWarning($"[MRSetup] No se encontró {PokePrefab}; botón omitido.");
                return;
            }
            GameObject btn = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            btn.name = name;
            btn.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            btn.transform.localRotation = Quaternion.identity;
            btn.transform.localScale = Vector3.one * 0.6f;
        }

        private static PhysicsMaterial GetPhysicsMaterial(string name, float dynamicF, float staticF, float bounce)
        {
            string path = $"{MatFolder}/{name}.physicMaterial";
            PhysicsMaterial m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (m != null) return m;
            m = new PhysicsMaterial(name)
            {
                dynamicFriction = dynamicF,
                staticFriction = staticF,
                bounciness = bounce,
                bounceCombine = PhysicsMaterialCombine.Average,
            };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Material GetMaterial(string name, Color color)
        {
            string path = $"{MatFolder}/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(shader) { color = color };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.35f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
#endif
