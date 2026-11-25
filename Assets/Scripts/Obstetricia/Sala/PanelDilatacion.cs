using Obstetricia.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Obstetricia.Sala
{
    /// <summary>Simulador de dilatación cervical a tamaño real (0 a 10 cm) con la etapa del trabajo de parto.</summary>
    public class PanelDilatacion : MonoBehaviour
    {
        private RectTransform _hole, _rim;
        private TextMeshProUGUI _cm, _fase, _texto;
        private int _target;
        private float _value;

        private void Start()
        {
            // 1 unidad = 1 mm: los círculos quedan a escala real
            Canvas c = HoloUI.CreateCanvas(transform, "Dilatacion", new Vector2(600, 360));
            Transform r = c.transform;
            HoloUI.Panel(r, "Fondo", Vector2.zero, new Vector2(600, 360), new Color(0.03f, 0.09f, 0.2f, 0.94f));
            HoloUI.Text(r, "Titulo", "DILATACIÓN CERVICAL", 27, new Vector2(0, 146), new Vector2(540, 40), TextAlignmentOptions.Center, HoloColors.Text, FontStyles.Bold);
            _rim = Disc(r, "Cuello", new Vector2(-185, 4), 150, new Color(0.86f, 0.46f, 0.46f));
            Disc(_rim, "Borde", Vector2.zero, 140, new Color(0.93f, 0.58f, 0.56f));
            _hole = Disc(r, "Orificio", new Vector2(-185, 4), 4, new Color(0.28f, 0.07f, 0.1f));
            HoloUI.Text(r, "Escala", "tamaño real", 15, new Vector2(-185, -96), new Vector2(170, 24), TextAlignmentOptions.Center, HoloColors.TextDim);
            _cm = HoloUI.Text(r, "Cm", "", 46, new Vector2(95, 92), new Vector2(350, 56), TextAlignmentOptions.Left, HoloColors.Accent, FontStyles.Bold);
            _fase = HoloUI.Text(r, "Fase", "", 24, new Vector2(95, 46), new Vector2(350, 34), TextAlignmentOptions.Left, HoloColors.Gold, FontStyles.Bold);
            _texto = HoloUI.Text(r, "Texto", "", 19, new Vector2(95, -26), new Vector2(350, 100), TextAlignmentOptions.TopLeft, HoloColors.Text);
            HoloUI.Button(r, "DilMenos", "-", new Vector2(-30, -124), new Vector2(96, 60), () => Cambiar(-1), null, 28);
            HoloUI.Button(r, "DilMas", "+", new Vector2(78, -124), new Vector2(96, 60), () => Cambiar(1), null, 28);
            Cambiar(0);
        }

        private static RectTransform Disc(Transform parent, string name, Vector2 pos, float size, Color color)
        {
            RectTransform rt = HoloUI.Rect(parent, name, pos, new Vector2(size, size));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = HoloSprites.Circle;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private void Cambiar(int d)
        {
            _target = Mathf.Clamp(_target + d, 0, 10);
            _cm.text = _target + " cm";
            if (_target == 0)
            {
                _fase.text = "Cuello cerrado";
                _texto.text = "Sin dilatación. El borramiento puede comenzar antes que la dilatación.";
            }
            else if (_target < 5)
            {
                _fase.text = "Fase latente";
                _texto.text = "Contracciones dolorosas y cambios del cuello, con dilatación más lenta hasta los 5 cm (OMS, 2018).";
            }
            else if (_target < 10)
            {
                _fase.text = "Fase activa";
                _texto.text = "Desde los 5 cm: contracciones regulares y dilatación más rápida hasta completarse (OMS, 2018).";
            }
            else
            {
                _fase.text = "Dilatación completa";
                _texto.text = "Comienza el segundo período del parto: el expulsivo.";
            }
        }

        private void Update()
        {
            if (_hole == null) return;
            _value = Mathf.Lerp(_value, _target, 1f - Mathf.Exp(-Time.deltaTime * 5f));
            // El disco del sprite ocupa 92 % de la imagen
            float mm = Mathf.Max(3f, _value * 10f) / 0.92f;
            _hole.sizeDelta = new Vector2(mm, mm);
        }
    }
}
