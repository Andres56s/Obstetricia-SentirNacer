#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Obstetricia.Desktop;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ObstetriciaSetup
{
    /// <summary>
    /// Menús para el MODO ESCRITORIO (sin visor):
    ///   Tools > Obstetricia > Modo escritorio en el Editor   → al dar Play se juega con teclado y mouse.
    ///   Tools > Obstetricia > Construir versión Web (WebGL)  → compila la experiencia y la deja en
    ///                                                           ../web/public/unity para que la página la muestre.
    /// La versión para las Quest se sigue compilando igual que antes (Build Profiles > Android).
    /// </summary>
    public static class ObstetriciaWebBuild
    {
        private const string ToggleMenu = "Tools/Obstetricia/Modo escritorio en el Editor";

        /// <summary>Carpeta de salida: web/public/unity (la página la sirve en /unity/index.html).</summary>
        public static string OutputFolder =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "web", "public", "unity"));

        [MenuItem(ToggleMenu, priority = 1)]
        private static void ToggleDesktop()
        {
            bool on = !EditorPrefs.GetBool(PlatformMode.EditorPrefKey, false);
            EditorPrefs.SetBool(PlatformMode.EditorPrefKey, on);
            Debug.Log(on
                ? "[Obstetricia] Modo escritorio FORZADO en el Editor: al dar Play se usa teclado + mouse."
                : "[Obstetricia] Modo escritorio automático: se activa solo si no hay visor conectado.");
        }

        [MenuItem(ToggleMenu, true)]
        private static bool ToggleDesktopValidate()
        {
            Menu.SetChecked(ToggleMenu, EditorPrefs.GetBool(PlatformMode.EditorPrefKey, false));
            return !EditorApplication.isPlaying;
        }

        [MenuItem("Tools/Obstetricia/Construir versión Web (WebGL)", priority = 20)]
        public static void BuildWebGL()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                EditorUtility.DisplayDialog("Falta WebGL",
                    "Instala el módulo \"Web Build Support\" para Unity 6000.3.27f1 desde Unity Hub (Installs > ⚙ > Add modules).",
                    "OK");
                return;
            }

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Obstetricia] No hay escenas activas en Build Settings.");
                return;
            }

            // Gzip + "decompression fallback": funciona en cualquier hosting (Firebase, Vercel, Netlify, Vite)
            // sin configurar cabeceras Content-Encoding en el servidor.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            // Plantilla propia (Assets/WebGLTemplates/SentirNacer): la experiencia llena el iframe de la página.
            PlayerSettings.WebGL.template = "PROJECT:SentirNacer";
            PlayerSettings.runInBackground = true;

            string output = OutputFolder;
            if (Directory.Exists(output))
            {
                // Solo se borra lo que genera Unity; README.md se conserva.
                foreach (string dir in new[] { "Build", "TemplateData", "StreamingAssets" })
                {
                    string d = Path.Combine(output, dir);
                    if (Directory.Exists(d)) Directory.Delete(d, true);
                }
                string index = Path.Combine(output, "index.html");
                if (File.Exists(index)) File.Delete(index);
            }
            Directory.CreateDirectory(output);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Obstetricia] Versión web lista en {output} ({report.summary.totalSize / (1024 * 1024)} MB). " +
                          "Ejecuta 'npm run dev' en la carpeta web para probarla.");
                EditorUtility.RevealInFinder(output);
            }
            else
            {
                Debug.LogError($"[Obstetricia] La build WebGL falló: {report.summary.result} ({report.summary.totalErrors} errores). Revisa la consola.");
            }
        }
    }
}
#endif
