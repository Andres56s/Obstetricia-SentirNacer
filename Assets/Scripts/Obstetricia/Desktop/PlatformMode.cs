using UnityEngine;
using UnityEngine.XR;

namespace Obstetricia.Desktop
{
    /// <summary>
    /// Decide si la experiencia corre en modo VR/MR (Meta Quest) o en modo escritorio "tipo juego"
    /// (navegador WebGL o PC sin visor: teclado + mouse).
    ///   - WebGL (página web)      → siempre escritorio.
    ///   - Android (Quest)         → siempre VR.
    ///   - Editor / Windows        → escritorio si no hay visor conectado, si se activó
    ///                               "Tools > Obstetricia > Modo escritorio en el Editor" o si se
    ///                               ejecuta con el argumento -escritorio.
    /// </summary>
    public static class PlatformMode
    {
        /// <summary>Clave de EditorPrefs para forzar el modo escritorio al dar Play en el Editor.</summary>
        public const string EditorPrefKey = "Obstetricia.ForzarModoEscritorio";

        private static bool? _cached;

        /// <summary>True si se juega con teclado y mouse (sin visor).</summary>
        public static bool IsDesktop
        {
            get
            {
                if (_cached == null) _cached = Detect();
                return _cached.Value;
            }
        }

        /// <summary>True si se usa el visor (Quest / Quest Link).</summary>
        public static bool IsVR => !IsDesktop;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => _cached = null;

        private static bool Detect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#elif UNITY_ANDROID && !UNITY_EDITOR
            return false;
#else
            foreach (string arg in System.Environment.GetCommandLineArgs())
                if (arg == "-escritorio") return true;
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool(EditorPrefKey, false)) return true;
#endif
            return !XRSettings.isDeviceActive;
#endif
        }
    }
}
