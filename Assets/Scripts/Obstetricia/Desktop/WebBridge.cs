using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Obstetricia.Desktop
{
    /// <summary>
    /// Comunicación con la página web Sentir Nacer cuando la experiencia corre dentro de un iframe (WebGL).
    ///
    /// Web → Unity: parámetros en la URL del iframe, por ejemplo
    ///     /unity/index.html?modulo=fisio&amp;alumno=Ana%20P%C3%A9rez
    ///   - modulo = "fisio" | "legal" → abre directamente ese programa (sin pasar por la mesa).
    ///   - alumno = nombre del alumno con sesión iniciada (se usa en los resultados).
    ///
    /// Unity → Web: window.parent.postMessage({ type: "obstetricia-...", ... }) mediante
    /// Assets/Plugins/WebGL/ObstetriciaWebBridge.jslib. La página escucha en
    /// web/src/components/ImmersivePlayer.jsx.
    /// Fuera de WebGL estas llamadas no hacen nada.
    /// </summary>
    public static class WebBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void ObstetriciaPostMessage(string json);
        [DllImport("__Internal")] private static extern void ObstetriciaSpeak(string text);
        [DllImport("__Internal")] private static extern void ObstetriciaStopSpeaking();
#endif

        /// <summary>Programa pedido por la web ("fisio", "legal") o null.</summary>
        public static string RequestedModule => Param("modulo")?.Trim().ToLowerInvariant();

        /// <summary>Nombre del alumno enviado por la web o null.</summary>
        public static string StudentName => Param("alumno");

        /// <summary>True si la experiencia está dentro de la página web.</summary>
        public static bool IsEmbedded
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Lee un parámetro de la URL de la página (solo WebGL).</summary>
        public static string Param(string key)
        {
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url)) return null;
            int q = url.IndexOf('?');
            if (q < 0) return null;
            string query = url.Substring(q + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);
            foreach (string pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                string k = eq >= 0 ? pair.Substring(0, eq) : pair;
                if (!string.Equals(Decode(k), key, StringComparison.OrdinalIgnoreCase)) continue;
                return eq >= 0 ? Decode(pair.Substring(eq + 1)) : "";
            }
            return null;
        }

        /// <summary>Avisa a la página que la experiencia terminó de cargar.</summary>
        public static void SendReady() => Post("{\"type\":\"obstetricia-listo\"}");

        /// <summary>Envía a la página el resultado de una evaluación para que lo guarde en Firestore.</summary>
        public static void SendResult(string module, int correct, int total, int score)
        {
            var sb = new StringBuilder();
            sb.Append("{\"type\":\"obstetricia-resultado\",\"module\":\"").Append(Escape(module)).Append('"');
            sb.Append(",\"correct\":").Append(correct.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"total\":").Append(total.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"score\":").Append(score.ToString(CultureInfo.InvariantCulture));
            sb.Append('}');
            Post(sb.ToString());
        }

        /// <summary>Lee un texto en voz alta con la voz del navegador (speechSynthesis).</summary>
        public static void Speak(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { ObstetriciaSpeak(text ?? ""); } catch (Exception ex) { Debug.LogWarning("[Obstetricia] Voz web: " + ex.Message); }
#endif
        }

        public static void StopSpeaking()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { ObstetriciaStopSpeaking(); } catch { }
#endif
        }

        private static void Post(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { ObstetriciaPostMessage(json); }
            catch (Exception ex) { Debug.LogWarning("[Obstetricia] No se pudo avisar a la página: " + ex.Message); }
#else
            Debug.Log("[Obstetricia] (WebBridge) " + json);
#endif
        }

        private static string Decode(string s) => Uri.UnescapeDataString((s ?? "").Replace('+', ' '));

        private static string Escape(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append(' '); break;
                    default:
                        if (c < 0x20) sb.Append(' ');
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
