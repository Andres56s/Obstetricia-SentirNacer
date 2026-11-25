using Obstetricia.UI;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Obstetricia.Sala
{
    /// <summary>
    /// Lavamanos provisional: al apuntarlo o tocarlo abre y cierra el agua. Con el agua abierta calienta el guatero que
    /// se acerque al chorro. Se puede mover o borrar sin afectar al resto de la sala.
    /// </summary>
    public class Lavamanos : MonoBehaviour
    {
        [SerializeField] private Transform chorro;
        [SerializeField] private AudioSource sonido;
        [SerializeField] private Guatero guatero;

        private bool _open;
        private float _flow;
        private Vector3 _scale;

        private void Awake()
        {
            _scale = chorro.localScale;
            var i = GetComponent<XRSimpleInteractable>();
            if (i == null) i = gameObject.AddComponent<XRSimpleInteractable>();
            i.selectEntered.AddListener(_ => Alternar());
            chorro.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            _open = false;
            _flow = 0f;
            if (chorro != null) chorro.gameObject.SetActive(false);
        }

        public void Alternar()
        {
            _open = !_open;
            HoloFeedback.Play(HoloFeedback.Sound.Click, 0.6f);
            if (_open && sonido != null) sonido.Play();
        }

        private void Update()
        {
            _flow = Mathf.MoveTowards(_flow, _open ? 1f : 0f, Time.deltaTime * 3f);
            if (chorro.gameObject.activeSelf != _flow > 0f) chorro.gameObject.SetActive(_flow > 0f);
            if (sonido != null)
            {
                sonido.volume = _flow * 0.35f;
                if (_flow <= 0f && sonido.isPlaying) sonido.Stop();
            }
            if (_flow <= 0f) return;
            float w = _flow * (1f + 0.08f * Mathf.Sin(Time.time * 37f));
            chorro.localScale = new Vector3(_scale.x * w, _scale.y, _scale.z * w);
            if (guatero != null && Vector3.Distance(guatero.transform.position, chorro.position) < 0.38f)
                guatero.Calentar(Time.deltaTime / 4f);
        }
    }
}
