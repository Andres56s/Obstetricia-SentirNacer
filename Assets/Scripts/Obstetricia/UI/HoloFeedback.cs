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

        private static readonly AudioClip[] Notes = new AudioClip[5];
        // Do, mi, sol, do agudo y un "pop" grave
        private static readonly float[] NoteFreq = { 523.25f, 659.25f, 783.99f, 1046.5f, 330f };

        /// <summary>Nota tipo marimba (0 a 3 suben como acorde; 4 es un golpe grave).</summary>
        public static void Note(int i, float volume = 0.8f)
        {
            if (!Obstetricia.Core.AppSettings.Sounds) return;
            AudioSource src = Source();
            if (src == null) return;
            i = Mathf.Clamp(i, 0, Notes.Length - 1);
            if (Notes[i] == null)
            {
                int n = (int)(Rate * 0.55f);
                var data = new float[n];
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)Rate, w = 2f * Mathf.PI * NoteFreq[i] * t;
                    float body = i == 4 ? Mathf.Sin(w * (1f + 0.5f * Mathf.Exp(-t * 18f))) : Mathf.Sin(w);
                    data[k] = (body * Mathf.Exp(-t * 9f) + 0.35f * Mathf.Sin(w * 3.93f) * Mathf.Exp(-t * 28f)) * Mathf.Min(1f, t * 400f) * 0.4f;
                }
                Notes[i] = Make("nota" + i, data);
            }
            src.PlayOneShot(Notes[i], volume * Volume);
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

        /// <summary>Vibración en el control que sostiene un objeto.</summary>
        public static void Haptic(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable held, float amplitude, float duration)
        {
            if (held == null || held.interactorsSelecting.Count == 0 || !(held.interactorsSelecting[0] is Component c)) return;
            HapticImpulsePlayer player = c.GetComponentInParent<HapticImpulsePlayer>();
            if (player != null) player.SendHapticImpulse(amplitude, duration);
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
