using System;
using UnityEngine;

namespace Obstetricia.Sala
{
    /// <summary>Reproductor de musicoterapia: pistas en bucle que suenan desde el parlante y cambian con fundido suave.</summary>
    public class Musicoterapia : MonoBehaviour
    {
        [Serializable]
        public class Pista
        {
            public string nombre;
            public AudioClip clip;
        }

        [SerializeField] private AudioSource fuente;
        [SerializeField] private Pista[] pistas = Array.Empty<Pista>();

        private readonly float[] _samples = new float[64];
        private bool _playing;
        private float _fade;
        private int _wanted;

        public Pista[] Pistas => pistas;
        public int Indice => _wanted;
        public bool Sonando => _playing;
        public float Volumen { get; private set; } = 0.6f;
        public event Action Cambio;

        /// <summary>Reproduce una pista; si ya es la que suena, la pausa.</summary>
        public void Elegir(int i)
        {
            if (i < 0 || i >= pistas.Length) return;
            if (i == _wanted && _playing) _playing = false;
            else { _wanted = i; _playing = true; }
            Cambio?.Invoke();
        }

        public void Detener()
        {
            _playing = false;
            Cambio?.Invoke();
        }

        public void AjustarVolumen(float delta)
        {
            Volumen = Mathf.Clamp01(Volumen + delta);
            Cambio?.Invoke();
        }

        /// <summary>Nivel de sonido actual (0..1) para animar la interfaz.</summary>
        public float Nivel()
        {
            if (fuente == null || !fuente.isPlaying) return 0f;
            fuente.GetOutputData(_samples, 0);
            float sum = 0f;
            foreach (float s in _samples) sum += s * s;
            return Mathf.Clamp01(Mathf.Sqrt(sum / _samples.Length) * 4f);
        }

        private void Update()
        {
            if (fuente == null || pistas.Length == 0) return;
            bool right = fuente.clip == pistas[_wanted].clip;
            _fade = Mathf.MoveTowards(_fade, _playing && right ? 1f : 0f, Time.deltaTime / 1.5f);
            if (_fade <= 0f && !right)
            {
                fuente.Stop();
                fuente.clip = pistas[_wanted].clip;
                right = true;
            }
            if (_playing && right && !fuente.isPlaying) fuente.Play();
            else if (!_playing && _fade <= 0f && fuente.isPlaying) fuente.Pause();
            fuente.volume = _fade * Volumen;
        }
    }
}
