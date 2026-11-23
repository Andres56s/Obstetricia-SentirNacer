using System;
using Obstetricia.Core;
using TMPro;
using UnityEngine;

namespace Obstetricia.UI
{
    /// <summary>
    /// Panel de ajustes de comodidad y accesibilidad: modo sentada, altura, distancia, modo zurdo, sonidos,
    /// narración, nombre del estudiante y repetir el tutorial. Se abre frente al usuario.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        private static SettingsPanel _instance;

        private Canvas _canvas;
        private HoloButton _seated, _left, _sounds, _narration;
        private TextMeshProUGUI _height, _distance, _student;

        /// <summary>Se invoca al pulsar "Repetir tutorial".</summary>
        public static event Action TutorialRequested;

        public static void Open()
        {
            if (_instance == null)
            {
                var go = new GameObject("PanelAjustes");
                _instance = go.AddComponent<SettingsPanel>();
                _instance.Build();
            }
            _instance.Refresh();
            HoloUI.PlaceInFront(_instance.transform, 0.7f, -0.12f, 12f);
            _instance._canvas.gameObject.SetActive(true);
        }

        public static void Close()
        {
            if (_instance != null) HoloAppear.HideObject(_instance._canvas.gameObject);
        }

        private void Build()
        {
            _canvas = HoloUI.CreateCanvas(transform, "Ajustes", new Vector2(620, 640));
            HoloUI.BringToFront(_canvas);
            HoloUI.Panel(_canvas.transform, "Fondo", Vector2.zero, new Vector2(620, 640), new Color(0.03f, 0.08f, 0.18f, 0.97f));
            Transform r = _canvas.transform;
            HoloUI.Icon(r, "Icono", "gear", new Vector2(-250, 280), 40);
            HoloUI.Text(r, "Titulo", "AJUSTES", 28, new Vector2(-40, 280), new Vector2(360, 40), TextAlignmentOptions.Left, HoloColors.Text, FontStyles.Bold);
            HoloUI.Button(r, "Cerrar", null, new Vector2(260, 280), new Vector2(54, 54), Close, "close", 18, HoloColors.Wrong);

            float y = 205f;
            const float step = 66f;
            _seated = Toggle(r, "Modo sentada", "Baja y acerca los paneles", y, () => AppSettings.Seated = !AppSettings.Seated); y -= step;
            _height = Stepper(r, "Altura de los paneles", y, () => AppSettings.HeightOffset -= 0.05f, () => AppSettings.HeightOffset += 0.05f); y -= step;
            _distance = Stepper(r, "Distancia", y, () => AppSettings.Distance -= 0.1f, () => AppSettings.Distance += 0.1f); y -= step;
            _left = Toggle(r, "Modo zurdo", "Lápiz con la mano izquierda", y, () => AppSettings.LeftHanded = !AppSettings.LeftHanded); y -= step;
            _sounds = Toggle(r, "Sonidos", "Efectos al tocar y elegir", y, () => AppSettings.Sounds = !AppSettings.Sounds); y -= step;
            _narration = Toggle(r, "Narración por voz", "Lee en voz alta las situaciones", y, () => AppSettings.Narration = !AppSettings.Narration); y -= step;

            HoloUI.Text(r, "EstudianteLbl", "Estudiante", 19, new Vector2(-150, y), new Vector2(260, 30), TextAlignmentOptions.Left, HoloColors.Text, FontStyles.Bold);
            _student = HoloUI.Text(r, "EstudianteVal", "", 17, new Vector2(-150, y - 24), new Vector2(260, 26), TextAlignmentOptions.Left, HoloColors.Accent);
            HoloUI.Button(r, "CambiarNombre", "Cambiar", new Vector2(200, y - 8), new Vector2(170, 52), EditName, "notes", 17);
            y -= step + 16f;

            HoloUI.Button(r, "Tutorial", "Repetir tutorial", new Vector2(0, y), new Vector2(300, 56), () =>
            {
                Close();
                TutorialRequested?.Invoke();
            }, "pointer", 18);

            AppSettings.Changed += Refresh;
            _canvas.gameObject.SetActive(false);
        }

        private void OnDestroy() => AppSettings.Changed -= Refresh;

        private HoloButton Toggle(Transform parent, string label, string hint, float y, Action onClick)
        {
            HoloUI.Text(parent, label, label, 19, new Vector2(-150, y + 10), new Vector2(260, 28), TextAlignmentOptions.Left, HoloColors.Text, FontStyles.Bold);
            HoloUI.Text(parent, label + "Ayuda", hint, 14, new Vector2(-150, y - 14), new Vector2(260, 22), TextAlignmentOptions.Left, HoloColors.TextDim);
            return HoloUI.Button(parent, label + "Btn", "No", new Vector2(200, y), new Vector2(170, 52), () => onClick(), null, 18);
        }

        private TextMeshProUGUI Stepper(Transform parent, string label, float y, Action minus, Action plus)
        {
            HoloUI.Text(parent, label, label, 19, new Vector2(-150, y), new Vector2(260, 28), TextAlignmentOptions.Left, HoloColors.Text, FontStyles.Bold);
            HoloUI.Button(parent, label + "Menos", "–", new Vector2(130, y), new Vector2(52, 52), () => minus(), null, 26);
            TextMeshProUGUI value = HoloUI.Text(parent, label + "Valor", "", 18, new Vector2(200, y), new Vector2(84, 30), TextAlignmentOptions.Center, HoloColors.Accent, FontStyles.Bold);
            HoloUI.Button(parent, label + "Mas", "+", new Vector2(270, y), new Vector2(52, 52), () => plus(), null, 26);
            return value;
        }

        private void Refresh()
        {
            if (_canvas == null) return;
            SetToggle(_seated, AppSettings.Seated);
            SetToggle(_left, AppSettings.LeftHanded);
            SetToggle(_sounds, AppSettings.Sounds);
            SetToggle(_narration, AppSettings.Narration);
            float h = AppSettings.HeightOffset * 100f;
            _height.text = (h > 0 ? "+" : "") + h.ToString("0") + " cm";
            _distance.text = AppSettings.Distance.ToString("0.0") + " m";
            _student.text = string.IsNullOrWhiteSpace(AppSettings.StudentName) ? "(sin nombre)" : AppSettings.StudentName;
        }

        private static void SetToggle(HoloButton b, bool on)
        {
            b.SetText(on ? "Sí" : "No");
            b.SetSelected(on);
        }

        private void EditName()
        {
            HoloKeyboard.Open("Escribe tu nombre", AppSettings.StudentName, name =>
            {
                if (name != null) AppSettings.StudentName = name;
            });
        }
    }
}
