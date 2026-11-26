using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Obstetricia.UI
{
    /// <summary>Botón holográfico con acceso a su texto, icono y estado "seleccionado".</summary>
    public class HoloButton : MonoBehaviour
    {
        public UnityEngine.UI.Button Button { get; private set; }
        public HoloPanel Panel { get; private set; }
        public TextMeshProUGUI Label { get; private set; }
        public Image IconImage { get; private set; }

        private Color _accent = HoloColors.Border;

        public void Init(UnityEngine.UI.Button button, HoloPanel panel, TextMeshProUGUI label, Image icon)
        {
            Button = button;
            Panel = panel;
            Label = label;
            IconImage = icon;
            _accent = panel.Outline != null ? panel.Outline.color : HoloColors.Border;
        }

        public bool Interactable
        {
            get => Button.interactable;
            set => Button.interactable = value;
        }

        public void SetText(string text)
        {
            if (Label != null) Label.text = text;
        }

        public void SetSelected(bool selected)
        {
            Panel.SetFill(selected ? HoloColors.Selected : HoloColors.ButtonFill);
            Panel.SetBorder(selected ? HoloColors.Accent : _accent);
        }

        public void SetAccent(Color color)
        {
            _accent = color;
            Panel.SetBorder(color);
            if (Label != null) Label.color = color;
            if (IconImage != null) IconImage.color = color;
        }

        public void SetIcon(string iconId)
        {
            if (IconImage != null) IconImage.sprite = HoloSprites.Icon(iconId);
        }

        public void AddListener(Action action) => Button.onClick.AddListener(() => action());
    }
}
