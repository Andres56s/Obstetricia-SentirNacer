using System;
using System.Collections.Generic;
using Obstetricia.Core;
using Obstetricia.Data;
using Obstetricia.Media;
using Obstetricia.Tools;
using Obstetricia.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Obstetricia.Modules
{
    /// <summary>
    /// Programa Fisio-balón con el diseño de la imagen de referencia: menú lateral de vidrio (Anatomía, Fisiología,
    /// Trabajo de parto, Mecanismos del parto, Evaluación), panel central (pelvis 3D cuando se detecta la pelota),
    /// panel de Notas y barra de herramientas (inicio, deshacer, puntero, lápiz, ajustes).
    /// Las secciones de explicación reproducen su video 360°.
    /// </summary>
    public class FisioModuleController : MonoBehaviour
    {
        [SerializeField] private FisioModuleData data;
        [SerializeField] private Video360Player video;
        [SerializeField] private AnatomyViewer anatomy;
        [SerializeField] private QuizController evaluation;
        [SerializeField] private PenDrawer pen;
        [SerializeField, Tooltip("Muestra el panel de prueba en Anatomía (estado del detector y botón para ver la pelvis sin pelota). Desactivar para la versión final.")]
        private bool showTestControls = true;

        [Header("Distribución (m, relativa a este objeto; el usuario está hacia -Z)")]
        [SerializeField] private Vector3 viewerLocal = new Vector3(0f, 1.5f, -1.1f);
        [SerializeField] private Vector3 menuPosition = new Vector3(-0.62f, 1.22f, -0.15f);
        [SerializeField] private Vector3 centerPosition = new Vector3(0.05f, 1.42f, 0f);
        [SerializeField] private Vector3 notesPosition = new Vector3(0.6f, 1.15f, -0.18f);
        [SerializeField] private Vector3 toolbarPosition = new Vector3(0f, 0.98f, -0.32f);

        private GameObject _root;
        private Transform _centerAnchor;
        private readonly List<HoloButton> _menuItems = new List<HoloButton>();
        private TextMeshProUGUI _noteStudent, _noteActivity, _noteDate;
        private Image _pointerIcon, _penIcon;
        private GameObject _testPanel;
        private TextMeshProUGUI _testStatus;
        private int _current = -1;

        /// <summary>Se invoca al pulsar el icono de inicio.</summary>
        public event Action HomeRequested;

        /// <summary>Se invoca al pulsar el icono de fijar (recolocar frente al usuario).</summary>
        public event Action RecenterRequested;

        /// <summary>Se invoca al pulsar el engranaje (abrir ajustes).</summary>
        public event Action SettingsRequested;

        public bool IsOpen => _root != null && _root.activeSelf;

        private void Awake()
        {
            Build();
            _root.SetActive(false);
            if (anatomy != null) anatomy.SetPlacementAnchor(_centerAnchor);
            if (evaluation != null) evaluation.Finished += (correct, total, score) =>
            {
                StudentRecords.Save("Evaluación Fisio-balón", correct, total, score);
                Select(0);
            };
            AppSettings.Changed += RefreshStudent;
        }

        public void Open()
        {
            _root.SetActive(true);
            RefreshStudent();
            _noteDate.text = DateTime.Now.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            SetPen(false);
            Select(0);
        }

        public void Close()
        {
            LeaveCurrent();
            SetPen(false);
            if (pen != null) pen.Clear();
            _current = -1;
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>Activa la sección indicada del menú lateral.</summary>
        public void Select(int index)
        {
            if (data == null || index < 0 || index >= data.sections.Length) return;
            LeaveCurrent();
            _current = index;
            FisioSection s = data.sections[index];
            for (int i = 0; i < _menuItems.Count; i++) SetMenuSelected(_menuItems[i], i == index);
            _noteActivity.text = string.IsNullOrWhiteSpace(s.activity) ? s.menuLabel : s.activity;

            switch (s.kind)
            {
                case FisioSectionKind.Anatomy:
                    if (anatomy != null) anatomy.Activate();
                    break;
                case FisioSectionKind.Mechanisms:
                    if (anatomy != null) anatomy.ActivateMechanisms();
                    break;
                case FisioSectionKind.Evaluation:
                    if (evaluation != null) evaluation.Open();
                    break;
                default:
                    if (video != null) video.Play(s.video, loop: false);
                    break;
            }
        }

        private void LeaveCurrent()
        {
            if (anatomy != null) anatomy.Deactivate();
            if (evaluation != null && evaluation.IsOpen) evaluation.Close();
            if (video != null && video.IsActive) video.Stop();
        }

        private void SetPen(bool on)
        {
            if (pen != null) pen.PenEnabled = on;
            if (_penIcon != null) _penIcon.color = on ? HoloColors.Accent : HoloColors.Text;
            if (_pointerIcon != null) _pointerIcon.color = on ? HoloColors.Text : HoloColors.Accent;
        }

        private void Undo()
        {
            if (pen != null) pen.Undo();
        }

        private void RefreshStudent()
        {
            if (_noteStudent == null) return;
            string name = AppSettings.StudentName;
            _noteStudent.text = !string.IsNullOrWhiteSpace(name) ? name
                : data != null && !string.IsNullOrWhiteSpace(data.studentName) ? data.studentName : "Estudiante";
        }

        private void OnDestroy() => AppSettings.Changed -= RefreshStudent;

        private void ToggleLabels()
        {
            if (anatomy != null) anatomy.LabelsVisible = !anatomy.LabelsVisible;
        }

        // ------------------------------------------------------------------ UI

        private Vector3 Viewer => transform.TransformPoint(viewerLocal);

        private Canvas PlacedCanvas(string name, Vector2 size, Vector3 localPos, float tilt = 0f)
        {
            Canvas c = HoloUI.CreateCanvas(_root.transform, name, size);
            c.transform.localPosition = localPos;
            HoloUI.FaceTowards(c.transform, Viewer);
            if (Mathf.Abs(tilt) > 0.01f) c.transform.rotation *= Quaternion.Euler(tilt, 0f, 0f);
            return c;
        }

        private void Build()
        {
            _root = new GameObject("FisioUI");
            _root.transform.SetParent(transform, false);

            _centerAnchor = new GameObject("CenterAnchor").transform;
            _centerAnchor.SetParent(_root.transform, false);
            _centerAnchor.localPosition = centerPosition;
            HoloUI.FaceTowards(_centerAnchor, Viewer);

            BuildMenu();
            BuildNotes();
            BuildToolbar();
            BuildTestPanel();
        }

        /// <summary>Panel de prueba: estado en vivo del detector y botón para ver la pelvis sin pelota.</summary>
        private void BuildTestPanel()
        {
            Canvas canvas = PlacedCanvas("PanelPrueba", new Vector2(480, 190), centerPosition);
            _testPanel = canvas.gameObject;
            HoloUI.Panel(canvas.transform, "Panel", Vector2.zero, new Vector2(480, 190), HoloColors.Glass, HoloColors.Gold);
            HoloUI.Text(canvas.transform, "Title", "MODO PRUEBA", 18, new Vector2(0, 66), new Vector2(440, 26),
                TextAlignmentOptions.Center, HoloColors.Gold, FontStyles.Bold);
            _testStatus = HoloUI.Text(canvas.transform, "Status", "", 17, new Vector2(0, 22), new Vector2(440, 50),
                TextAlignmentOptions.Center, HoloColors.Text);
            HoloUI.Button(canvas.transform, "VerPelvis", "Ver pelvis sin pelota", new Vector2(0, -52), new Vector2(300, 58),
                () => { if (anatomy != null) anatomy.SimulateBallForTesting(); }, "cube", 19, HoloColors.Gold);
            _testPanel.SetActive(false);
        }

        private void Update()
        {
            if (_testPanel == null) return;
            bool show = showTestControls && IsOpen && anatomy != null && _current >= 0 && data != null
                        && data.sections[_current].kind == FisioSectionKind.Anatomy && !anatomy.IsUnlocked;
            if (_testPanel.activeSelf != show) _testPanel.SetActive(show);
            if (show) _testStatus.text = anatomy.DetectorStatus;
        }

        /// <summary>Menú lateral: un panel de vidrio con filas separadas por líneas finas.</summary>
        private void BuildMenu()
        {
            int count = data != null ? data.sections.Length : 0;
            const float width = 250f, rowH = 76f;
            float height = count * rowH + 16f;
            Canvas menu = PlacedCanvas("SideMenu", new Vector2(width, height), menuPosition);
            HoloUI.Panel(menu.transform, "Background", Vector2.zero, new Vector2(width, height), HoloColors.Glass);
            for (int i = 0; i < count; i++)
            {
                FisioSection s = data.sections[i];
                float y = height / 2f - 8f - rowH / 2f - i * rowH;
                int captured = i;
                HoloButton row = HoloUI.Button(menu.transform, "Menu_" + s.menuLabel, s.menuLabel, new Vector2(0, y),
                    new Vector2(width - 20f, rowH - 8f), () => Select(captured), s.iconId, 19, HoloColors.Text);
                row.Label.alignment = TextAlignmentOptions.Left;
                SetMenuSelected(row, false);
                _menuItems.Add(row);
                if (i < count - 1)
                {
                    RectTransform sep = HoloUI.Rect(menu.transform, "Separator", new Vector2(0, y - rowH / 2f), new Vector2(width - 36f, 1.5f));
                    Image line = sep.gameObject.AddComponent<Image>();
                    line.color = new Color(1f, 1f, 1f, 0.18f);
                    line.raycastTarget = false;
                }
            }
        }

        private static void SetMenuSelected(HoloButton row, bool selected)
        {
            row.Panel.SetFill(selected ? new Color(0.55f, 0.8f, 1f, 0.28f) : new Color(1f, 1f, 1f, 0.001f));
            row.Panel.Outline.enabled = selected;
            row.Panel.SetBorder(new Color(0.6f, 0.85f, 1f, 0.7f));
        }

        /// <summary>Panel "Notas": estudiante, actividad, fecha y los iconos fijar / borrar.</summary>
        private void BuildNotes()
        {
            Canvas canvas = PlacedCanvas("Notes", new Vector2(240, 330), notesPosition);
            HoloPanel p = HoloUI.Panel(canvas.transform, "Panel", Vector2.zero, new Vector2(240, 330), HoloColors.Glass);
            HoloUI.Icon(p.transform, "Icon", "notes", new Vector2(-90, 138), 26, HoloColors.Text);
            HoloUI.Text(p.transform, "Title", "Notas", 20, new Vector2(10, 138), new Vector2(170, 30), TextAlignmentOptions.Left,
                HoloColors.Text, FontStyles.Bold);
            _noteStudent = NoteField(p.transform, "Estudiante:", 88);
            _noteActivity = NoteField(p.transform, "Actividad:", 28);
            _noteDate = NoteField(p.transform, "Fecha:", -32);
            IconButton(p.transform, "Pin", "pin", new Vector2(-40, -128), 34, () => RecenterRequested?.Invoke(), "Recentrar");
            IconButton(p.transform, "Trash", "trash", new Vector2(40, -128), 34, () => { if (pen != null) pen.Clear(); }, "Borrar dibujos");
        }

        private static TextMeshProUGUI NoteField(Transform parent, string label, float y)
        {
            HoloUI.Text(parent, label, label, 14, new Vector2(0, y + 14), new Vector2(200, 20), TextAlignmentOptions.Left, HoloColors.TextDim);
            return HoloUI.Text(parent, label + "Value", "", 17, new Vector2(0, y - 8), new Vector2(200, 26), TextAlignmentOptions.Left,
                HoloColors.Text);
        }

        /// <summary>Barra inferior: un panel de vidrio con cinco iconos.</summary>
        private void BuildToolbar()
        {
            Canvas canvas = PlacedCanvas("Toolbar", new Vector2(420, 70), toolbarPosition, 30f);
            HoloUI.Panel(canvas.transform, "Panel", Vector2.zero, new Vector2(420, 70), HoloColors.Glass);
            float x0 = -164f, step = 82f;
            IconButton(canvas.transform, "Home", "home", new Vector2(x0, 0), 40, () => HomeRequested?.Invoke(), "Inicio");
            IconButton(canvas.transform, "Undo", "undo", new Vector2(x0 + step, 0), 40, Undo, "Deshacer trazo");
            _pointerIcon = IconButton(canvas.transform, "Pointer", "pointer", new Vector2(x0 + step * 2, 0), 40, () => SetPen(false), "Puntero");
            _penIcon = IconButton(canvas.transform, "Pen", "pencil", new Vector2(x0 + step * 3, 0), 40, () => SetPen(true), "Lápiz (gatillo)");
            IconButton(canvas.transform, "Settings", "gear", new Vector2(x0 + step * 4, 0), 40, () => SettingsRequested?.Invoke(), "Ajustes");
        }

        /// <summary>Icono pulsable sin marco (como en la imagen de referencia). Devuelve la imagen del icono.</summary>
        private static Image IconButton(Transform parent, string name, string iconId, Vector2 pos, float size, Action onClick, string tooltip = null)
        {
            Image icon = HoloUI.Icon(parent, name, iconId, pos, size, HoloColors.Text);
            icon.raycastTarget = true;
            // Zona de toque algo mayor que el icono (invisible).
            RectTransform hit = HoloUI.Rect(icon.transform, "Hit", Vector2.zero, new Vector2(size * 1.6f, size * 1.6f));
            Image hitImg = hit.gameObject.AddComponent<Image>();
            hitImg.color = new Color(1f, 1f, 1f, 0f);
            var button = icon.gameObject.AddComponent<Button>();
            button.targetGraphic = icon;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.colorMultiplier = 1.5f;
            button.colors = colors;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.onClick.AddListener(() => onClick());
            var fx = icon.gameObject.AddComponent<HoloHoverFx>();
            fx.hoverScale = 1.2f;
            fx.glow = false;
            fx.tooltip = tooltip;
            fx.tooltipOffset = new Vector2(0f, size * 1.25f);
            fx.BuildTooltip();
            return icon;
        }
    }
}
