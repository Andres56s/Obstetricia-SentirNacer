#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MRSetup
{
    /// <summary>
    /// Herramienta de diagnóstico del módulo 5: carga en una escena aislada (sin tocar la escena activa)
    /// los prefabs de rig de los samples de XRI y vuelca su jerarquía y componentes a Logs/RigDump.txt.
    /// </summary>
    public static class MRRigInspector
    {
        private const string Base = "Assets/Samples/XR Interaction Toolkit/3.7.0";
        private static readonly string[] Prefabs =
        {
            Base + "/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab",
            Base + "/Starter Assets/Prefabs/XR Origin (XR Rig).prefab",
        };

        [MenuItem("Tools/MR Setup/5b0 - Volcar jerarquia de rigs de ejemplo")]
        public static void Dump()
        {
            StringBuilder sb = new StringBuilder();
            foreach (string path in Prefabs)
            {
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    sb.AppendLine($"=== {path}");
                    Walk(root.transform, 0, sb);
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"ERROR {path}: {ex.Message}");
                    Debug.LogError($"[MRSetup] No se pudo leer {path}: {ex}");
                }
                finally
                {
                    if (root != null) PrefabUtility.UnloadPrefabContents(root);
                }
            }
            File.WriteAllText("Logs/RigDump.txt", sb.ToString());
            Debug.Log("[MRSetup] Volcado escrito en Logs/RigDump.txt");
        }

        private static void Walk(Transform t, int level, StringBuilder sb)
        {
            string comps = string.Join(", ", t.GetComponents<Component>()
                .Where(c => c != null && !(c is Transform))
                .Select(c => c.GetType().Name));
            sb.AppendLine($"{new string(' ', level * 2)}- {t.name} [{comps}]");
            foreach (Transform child in t) Walk(child, level + 1, sb);
        }
    }
}
#endif
