using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Obstetricia.UI
{
    /// <summary>
    /// Sonidos sintetizados (sin archivos) y vibración de los controles para dar respuesta a cada acción.
    /// Sonidos suaves y cortos, pensados para no cansar en sesiones largas.
    /// </summary>
    public static class HoloFeedback
    {
        public enum Sound { Hover, Click, Open, Close, Success, Error, Recenter }

        private static AudioSource _source;
        private static AudioClip _hover, _click, _open, _close, _success, _error, _recenter;
        private const int Rate = 44100;

        /// <summary>Volumen general de los efectos (0..1).</summary>
        public static float Volume = 0.5f;

        public static void Play(Sound sound, float volume = 1f)
        {
            if (!Obstetricia.Core.AppSettings.Sounds) return;
            AudioSource src = Source();
            if (src == null) return;
            src.PlayOneShot(Clip(sound), volume * Volume);
        }

        /// <summary>Vibración en el control que originó el evento de UI (rayo o toque).</summary>
        public static void Haptic(BaseEventData eventData, float amplitude, float duration)
        {
            if (eventData is TrackedDeviceEventData tracked && tracked.interactor is Component c && c != null)
            {
                HapticImpulsePlayer player = c.GetComponentInParent<HapticImpulsePlayer>();
                if (player != null) player.SendHapticImpulse(amplitude, duration);
            }
        }

        /// <summary>Vibración en ambos controles (por ejemplo al recentrar).</summary>
        public static void HapticAll(float amplitude, float duration)
        {
            foreach (HapticImpulsePlayer p in Object.FindObjectsByType<HapticImpulsePlayer>(FindObjectsSortMode.None))
                p.SendHapticImpulse(amplitude, duration);
        }

        private static AudioSource Source()
        {
            if (_source != null) return _source;
            Camera cam = Camera.main;
            if (cam == null) return null;
            _source = cam.gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.priority = 200;
            return _source;
        }

        private static AudioClip Clip(Sound s)
        {
            switch (s)
            {
                case Sound.Hover: return _hover ??= Tone("hover", 0.035f, 1900f, 2100f, 0.18f);
                case Sound.Click: return _click ??= Chord("click", 0.09f, new[] { 1320f, 1760f }, 0.35f);
                case Sound.Open: return _open ??= Tone("open", 0.28f, 520f, 1150f, 0.28f);
                case Sound.Close: return _close ??= Tone("close", 0.22f, 1000f, 480f, 0.24f);
                case Sound.Success: return _success ??= Arpeggio("success", new[] { 784f, 988f, 1319f }, 0.09f, 0.32f);
                case Sound.Error: return _error ??= Arpeggio("error", new[] { 392f, 311f }, 0.13f, 0.3f);
                default: return _recenter ??= Tone("recenter", 0.4f, 300f, 900f, 0.3f);
            }
        }

        /// <summary>Barrido de frecuencia con envolvente suave.</summary>
        private static AudioClip Tone(string name, float seconds, float f0, float f1, float amp)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(f0, f1, t);
                phase += 2 * Mathf.PI * f / Rate;
                data[i] = Mathf.Sin((float)phase) * Envelope(t) * amp;
            }
            return Make(name, data);
        }

        private static AudioClip Chord(string name, float seconds, float[] freqs, float amp)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n, v = 0f;
                foreach (float f in freqs) v += Mathf.Sin(2 * Mathf.PI * f * i / Rate);
                data[i] = v / freqs.Length * Envelope(t) * amp;
            }
            return Make(name, data);
        }

        private static AudioClip Arpeggio(string name, float[] notes, float noteSeconds, float amp)
        {
            int per = (int)(Rate * noteSeconds);
            var data = new float[per * notes.Length + per];
            for (int k = 0; k < notes.Length; k++)
            {
                int len = per * 2;
                for (int i = 0; i < len && k * per + i < data.Length; i++)
                {
                    float t = i / (float)len;
                    data[k * per + i] += Mathf.Sin(2 * Mathf.PI * notes[k] * i / Rate) * Envelope(t) * amp * 0.7f;
                }
            }
            return Make(name, data);
        }

        private static float Envelope(float t) => Mathf.Min(1f, t * 25f) * Mathf.Pow(1f - t, 2f);

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create("Holo_" + name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
