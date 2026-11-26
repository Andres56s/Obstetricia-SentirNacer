using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Obstetricia.UI
{
    /// <summary>Referencia a las dos capas gráficas de un panel holográfico.</summary>
    public class HoloPanel : MonoBehaviour
    {
        public Image Background { get; private set; }
        public Image Outline { get; private set; }

        public void Init(Image background, Image outline)
        {
            Background = background;
            Outline = outline;
        }

        public void SetBorder(Color color)
        {
            if (Outline != null) Outline.color = color;
        }

        public void SetFill(Color color)
        {
            if (Background != null) Background.color = color;
        }
    }
}
