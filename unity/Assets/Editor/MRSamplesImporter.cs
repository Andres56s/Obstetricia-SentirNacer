#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace MRSetup
{
    /// <summary>
    /// Módulo 5a: importa los samples oficiales necesarios para interacción con controles y manos
    /// (XR Interaction Toolkit: Starter Assets y Hands Interaction Demo; XR Hands: HandVisualizer).
    /// Uso: menú Tools > MR Setup > 5a - Importar samples de interacción.
    /// </summary>
    public static class MRSamplesImporter
    {
        private const string XriPackage = "com.unity.xr.interaction.toolkit";
        private const string HandsPackage = "com.unity.xr.hands";

        /// <summary>Nombres de sample (coincidencia parcial) que se importan de cada paquete.</summary>
        private static readonly (string package, string[] sampleNames)[] Wanted =
        {
            (XriPackage, new[] { "Starter Assets", "Hands Interaction Demo" }),
            (HandsPackage, new[] { "HandVisualizer" }),
        };

        [MenuItem("Tools/MR Setup/5a - Importar samples de interaccion")]
        public static void ImportSamples()
        {
            try
            {
                foreach ((string package, string[] names) in Wanted)
                {
                    PackageInfo info = PackageInfo.FindForPackageName(package);
                    if (info == null)
                    {
                        Debug.LogError($"[MRSetup] Paquete no instalado: {package}");
                        continue;
                    }

                    var samples = Sample.FindByPackage(package, info.version).ToList();
                    Debug.Log($"[MRSetup] {package}@{info.version}: samples disponibles = " +
                              string.Join(", ", samples.Select(s => s.displayName)));

                    foreach (Sample sample in samples)
                    {
                        if (!names.Any(n => sample.displayName.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                            continue;

                        if (sample.isImported)
                        {
                            Debug.Log($"[MRSetup] Sample ya importado: {sample.displayName}");
                            continue;
                        }
                        bool ok = sample.Import(Sample.ImportOptions.OverridePreviousImports);
                        Debug.Log($"[MRSetup] Import '{sample.displayName}' -> {ok}");
                    }
                }
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MRSetup] Error al importar samples: {ex}");
            }
        }
    }
}
#endif
