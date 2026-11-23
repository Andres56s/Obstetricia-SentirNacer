#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using MRGame;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace MRSetup
{
    /// <summary>
    /// Verificación final: vuelca la jerarquía, cuenta duplicados clave y revisa referencias nulas
    /// en los componentes XR/MR principales. Resultado en Logs/FinalReport.txt y en la consola.
    /// </summary>
    public static class MRFinalVerify
    {
        [MenuItem("Tools/MR Setup/7 - Verificacion final")]
        public static void Run()
        {
            var sb = new StringBuilder();
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                sb.AppendLine($"Escena: {scene.path} (dirty={scene.isDirty})");
                sb.AppendLine("--- Jerarquia ---");
                foreach (GameObject root in scene.GetRootGameObjects()) Walk(root.transform, 0, sb);

                sb.AppendLine("--- Conteos (esperado 1) ---");
                Count<Camera>(sb, "Camera");
                Count<AudioListener>(sb, "AudioListener");
                Count<XROrigin>(sb, "XROrigin");
                Count<ARSession>(sb, "ARSession");
                Count<ARPlaneManager>(sb, "ARPlaneManager");
                Count<ARAnchorManager>(sb, "ARAnchorManager");
                Count<ARCameraManager>(sb, "ARCameraManager");
                Count<AROcclusionManager>(sb, "AROcclusionManager");
                Count<MRGameManager>(sb, "MRGameManager");
                Count<MRTableAnchor>(sb, "MRTableAnchor");
                sb.AppendLine($"Luces direccionales: {All<Light>().Count(l => l.type == LightType.Directional)}");

                sb.AppendLine("--- Referencias nulas en componentes clave ---");
                int nulls = 0;
                foreach (Component c in All<Component>())
                {
                    if (!(c is XROrigin || c is MRTableAnchor || c is MRGameManager || c is ARCameraManager)) continue;
                    nulls += CheckNulls(c, sb);
                }
                sb.AppendLine($"Total refs nulas: {nulls}");

                sb.AppendLine("--- Scripts perdidos ---");
                int missing = 0;
                foreach (GameObject go in All<Transform>().Select(t => t.gameObject))
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                sb.AppendLine($"Missing scripts: {missing}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"ERROR: {ex}");
                Debug.LogError($"[MRSetup] Verificación fallida: {ex}");
            }
            File.WriteAllText("Logs/FinalReport.txt", sb.ToString());
            Debug.Log("[MRSetup] Informe final escrito en Logs/FinalReport.txt");
        }

        private static T[] All<T>() where T : Component
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private static void Count<T>(StringBuilder sb, string label) where T : Component
        {
            sb.AppendLine($"{label}: {All<T>().Length}");
        }

        private static int CheckNulls(Component c, StringBuilder sb)
        {
            int n = 0;
            SerializedObject so = new SerializedObject(c);
            SerializedProperty it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.name == "m_Script" || it.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (it.objectReferenceValue == null)
                {
                    sb.AppendLine($"  NULL: {c.GetType().Name}.{it.propertyPath} en {c.gameObject.name}");
                    n++;
                }
            }
            return n;
        }

        private static void Walk(Transform t, int level, StringBuilder sb)
        {
            string comps = string.Join(", ", t.GetComponents<Component>()
                .Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
            sb.AppendLine($"{new string(' ', level * 2)}- {t.name}{(t.gameObject.activeSelf ? "" : " (inactivo)")} [{comps}]");
            foreach (Transform child in t) Walk(child, level + 1, sb);
        }
    }
}
#endif
