using System;
using UnityEngine;

namespace Obstetricia.Core
{
    /// <summary>
    /// Narración por voz con el motor de texto a voz de Android (en español). Si el visor no tiene un motor
    /// de voz instalado, la narración simplemente no suena (se informa en el log).
    /// </summary>
    public static class Narrator
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _tts;
        private static bool _ready;
        private static bool _initializing;
        private static string _pending;

        private sealed class InitListener : AndroidJavaProxy
        {
            public InitListener() : base("android.speech.tts.TextToSpeech$OnInitListener") { }

            // Android llama a este método desde su propio hilo.
            public void onInit(int status)
            {
                _initializing = false;
                if (status != 0)
                {
                    Debug.LogWarning("[Obstetricia] Texto a voz no disponible en este visor (status " + status + ").");
                    return;
                }
                try
                {
                    using (var locale = new AndroidJavaObject("java.util.Locale", "es", "CL"))
                    {
                        int r = _tts.Call<int>("setLanguage", locale);
                        if (r < 0)
                            using (var es = new AndroidJavaObject("java.util.Locale", "es"))
                                _tts.Call<int>("setLanguage", es);
                    }
                    _tts.Call<int>("setSpeechRate", 0.95f);
                    _ready = true;
                    if (_pending != null) { string p = _pending; _pending = null; SpeakNow(p); }
                }
                catch (Exception ex) { Debug.LogWarning("[Obstetricia] Texto a voz: " + ex.Message); }
            }
        }
#endif

        /// <summary>Lee el texto en voz alta (interrumpe lo anterior). Respeta el ajuste de narración.</summary>
        public static void Speak(string text)
        {
            if (!AppSettings.Narration || string.IsNullOrWhiteSpace(text)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_tts == null && !_initializing)
                {
                    _initializing = true;
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    {
                        var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                        _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, new InitListener());
                    }
                }
                if (!_ready) { _pending = text; return; }
                SpeakNow(text);
            }
            catch (Exception ex) { Debug.LogWarning("[Obstetricia] Texto a voz: " + ex.Message); }
#else
            Debug.Log("[Obstetricia] (Narración) " + text);
#endif
        }

        /// <summary>Detiene la narración en curso.</summary>
        public static void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { if (_ready) _tts.Call<int>("stop"); } catch { }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void SpeakNow(string text)
        {
            // QUEUE_FLUSH = 0
            using (var bundle = new AndroidJavaObject("android.os.Bundle"))
                _tts.Call<int>("speak", StripTags(text), 0, bundle, "ucen");
        }
#endif

        private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<.*?>", "");
    }
}
