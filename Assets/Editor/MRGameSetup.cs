#if UNITY_EDITOR
using System;
using System.Text;
using MRGame;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MRSetup
{
    /// <summary>
    /// Módulo 4 del setup de Realidad Mixta: crea la raíz "MixedRealityGame" con la mesa/tablero virtual
    /// anclable y añade al XR Origin existente los gestores de planos, anclas y raycast.
    /// Idempotente: si "MixedRealityGame" ya existe no se crea nada.
    /// Uso: menú Tools > MR Setup > 4 - Crear MixedRealityGame y mesa anclada.
    /// </summary>
    public static class MRGameSetup
    {
        private const string RootName = "MixedRealityGame";
        private const string MaterialFolder = "Assets/Materials/MR";
        private const float BoardWidth = 1.0f;
        private const float BoardDepth = 0.6f;
        private const float BoardThickness = 0.03f;
        private const float LegHeight = 0.85f;
        private const float LegSize = 0.05f;

        [MenuItem("Tools/MR Setup/4 - Crear MixedRealityGame y mesa anclada")]
        public static void CreateGame()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
                {
                    Debug.LogError("[MRSetup] La escena activa no está guardada en disco.");
                    return;
                }
                if (GameObject.Find(RootName) != null)
                {
                    Debug.LogWarning("[MRSetup] 'MixedRealityGame' ya existe; no se crea otro.");
                    return;
                }

                XROrigin origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
                if (origin == null)
                {
                    Debug.LogError("[MRSetup] No hay XR Origin; ejecuta primero el paso 3.");
                    return;
                }

                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();

                ARPlaneManager planes = EnsureComponent<ARPlaneManager>(origin.gameObject);
                planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
                ARAnchorManager anchors = EnsureComponent<ARAnchorManager>(origin.gameObject);
                EnsureComponent<ARRaycastManager>(origin.gameObject);

                Material top = GetOrCreateMaterial("TableTop", new Color(0.55f, 0.38f, 0.22f));
                Material legsMat = GetOrCreateMaterial("TableLegs", new Color(0.25f, 0.18f, 0.12f));

                GameObject root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Crear MixedRealityGame");

                GameObject table = new GameObject("GameTable");
                table.transform.SetParent(root.transform, false);
                table.transform.position = new Vector3(0f, LegHeight, 0.8f);

                BuildBoard(table.transform, top);
                GameObject legs = BuildLegs(table.transform, legsMat);

                GameObject items = new GameObject("GameItems");
                items.transform.SetParent(table.transform, false);
                items.transform.localPosition = new Vector3(0f, BoardThickness, 0f);

                MRTableAnchor anchor = table.AddComponent<MRTableAnchor>();
                WireTableAnchor(anchor, planes, anchors, origin, legs);

                MRGameManager manager = root.AddComponent<MRGameManager>();
                SerializedObject so = new SerializedObject(manager);
                so.FindProperty("table").objectReferenceValue = anchor;
                so.FindProperty("itemsRoot").objectReferenceValue = items.transform;
                so.ApplyModifiedPropertiesWithoutUndo();

                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();

                Debug.Log($"[MRSetup] MixedRealityGame creado. Escena guardada: {saved}");
                LogHierarchy(scene);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MRSetup] Error al crear MixedRealityGame: {ex}");
            }
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }

        /// <summary>Asigna las referencias serializadas del MRTableAnchor.</summary>
        private static void WireTableAnchor(MRTableAnchor anchor, ARPlaneManager planes,
            ARAnchorManager anchors, XROrigin origin, GameObject legs)
        {
            SerializedObject so = new SerializedObject(anchor);
            so.FindProperty("planeManager").objectReferenceValue = planes;
            so.FindProperty("anchorManager").objectReferenceValue = anchors;
            so.FindProperty("xrOrigin").objectReferenceValue = origin;
            so.FindProperty("legs").objectReferenceValue = legs;
            so.FindProperty("fallbackHeight").floatValue = LegHeight;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Tablero: cubo con collider; su cara superior queda a BoardThickness sobre el origen de la mesa.</summary>
        private static void BuildBoard(Transform parent, Material mat)
        {
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(parent, false);
            board.transform.localPosition = new Vector3(0f, BoardThickness / 2f, 0f);
            board.transform.localScale = new Vector3(BoardWidth, BoardThickness, BoardDepth);
            board.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>Cuatro patas bajo el tablero (solo visibles con la mesa virtual de respaldo).</summary>
        private static GameObject BuildLegs(Transform parent, Material mat)
        {
            GameObject legs = new GameObject("Legs");
            legs.transform.SetParent(parent, false);
            float x = BoardWidth / 2f - LegSize;
            float z = BoardDepth / 2f - LegSize;
            Vector2[] corners = { new Vector2(-x, -z), new Vector2(x, -z), new Vector2(-x, z), new Vector2(x, z) };
            for (int i = 0; i < corners.Length; i++)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = $"Leg_{i + 1}";
                leg.transform.SetParent(legs.transform, false);
                leg.transform.localPosition = new Vector3(corners[i].x, -LegHeight / 2f, corners[i].y);
                leg.transform.localScale = new Vector3(LegSize, LegHeight, LegSize);
                leg.GetComponent<Renderer>().sharedMaterial = mat;
            }
            return legs;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials", "MR");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

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
