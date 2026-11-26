using Obstetricia.Core;
using Obstetricia.UI;
using UnityEngine;

namespace Obstetricia.Sala
{
    /// <summary>
    /// Simulador de recién nacido: se toma desde la cuna radiante y se puede llevar a la cama para representar el contacto
    /// piel con piel. Si se suelta en otro lugar vuelve flotando a la cuna (nunca cae).
    /// </summary>
    public class RecienNacido : ObjetoSala
    {
        [SerializeField, Tooltip("Lugar en la cama para el contacto piel con piel.")] private Transform lugarPiel;
        [SerializeField] private Renderer marca;

        private bool _held;
        private MaterialPropertyBlock _block;

        private void Start()
        {
            _block = new MaterialPropertyBlock();
            if (marca != null) marca.gameObject.SetActive(false);
        }

        protected override void Update()
        {
            bool held = Tomado;
            bool near = lugarPiel != null && Vector3.Distance(transform.position, lugarPiel.position) < 0.35f;
            if (marca != null)
            {
                if (marca.gameObject.activeSelf != held) marca.gameObject.SetActive(held);
                if (held)
                {
                    float a = 0.25f + 0.12f * Mathf.Sin(Time.time * 2.4f) + (near ? 0.3f : 0f);
                    _block.SetColor("_BaseColor", new Color(1f, 0.62f, 0.5f, a));
                    marca.SetPropertyBlock(_block);
                }
            }
            if (_held && !held && near)
            {
                Destino = lugarPiel;
                HoloFeedback.Play(HoloFeedback.Sound.Success, 0.6f);
                Narrator.Speak("Contacto piel con piel: se recomienda iniciarlo apenas nace y mantenerlo al menos una hora sin interrupciones, si la madre y el recién nacido están estables.");
            }
            _held = held;
            base.Update();
        }
    }
}
