using System;
using System.Collections;
using System.Collections.Generic;
using Obstetricia.Core;
using Obstetricia.Data;
using Obstetricia.Media;
using Obstetricia.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Obstetricia.Modules
{
    /// <summary>
    /// Quiz de decisiones con el diseño de la imagen de referencia "Ley Integral de la Mujer":
    /// encabezado del módulo, situación, dos tarjetas de opción con foto, panel de puntaje y progreso.
    /// Las tarjetas muestran ✓ / ✗ desde el inicio; al elegir, tras unos segundos pasa a la siguiente situación.
    /// </summary>
    public class QuizController : MonoBehaviour
    {
        [SerializeField] private QuizData data;
        [SerializeField, Tooltip("Reproductor para el video 360° de ambientación (opcional).")]
        private Video360Player video;
        [SerializeField, Tooltip("Escala del canvas (m por unidad).")]
        private float canvasScale = 0.0008f;
        [SerializeField, Tooltip("Segundos que se muestra el resultado antes de pasar a la siguiente situación.")]
        private float revealSeconds = 3.5f;

        private const float CardWidth = 300f;
        private const float CardHeight = 410f;

        private Canvas _canvas;
        private TextMeshProUGUI _label, _title, _subtitle, _situation, _question, _points;
        private Image _headerIcon;
        private readonly List<OptionCard> _cards = new List<OptionCard>();
        private Image _progressFill;
        private TextMeshProUGUI _progressText;

        private int _index;
        private int _score;
        private int _correctCount;
        private bool _answered;
        private float _advanceAt = -1f;
        private float _progressTarget;
        private HoloAppear _situationFx;

        /// <summary>Se invoca al terminar todas las situaciones: (correctas, total, puntaje).</summary>
        public event Action<int, int, int> Finished;

        public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

        private sealed class OptionCard
        {
            public HoloPanel Panel;
            public Image Status;
            public Image Picture;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI Description;
            public HoloButton Button;
        }

        private void Awake()
        {
            Build();
            _canvas.gameObject.SetActive(false);
        }

        /// <summary>Muestra el quiz desde la primera situación.</summary>
        public void Open()
        {
            if (data == null || data.questions == null || data.questions.Length == 0)
            {
                Debug.LogWarning("[Obstetricia] Quiz sin preguntas.");
                return;
            }
            _canvas.gameObject.SetActive(true);
            _label.text = data.moduleLabel;
            _title.text = data.title;
            _subtitle.text = data.subtitle;
            _headerIcon.sprite = HoloSprites.Icon(data.iconId);
            _points.text = $"+{data.pointsPerCorrect} pts";
            _index = 0;
            _score = 0;
            _correctCount = 0;
            ShowQuestion();
        }

        /// <summary>Oculta el quiz y detiene el video de ambiente.</summary>
        public void Close()
        {
            _advanceAt = -1f;
            Narrator.Stop();
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (video != null && video.IsActive) video.Stop();
        }

        private void Update()
        {
            if (_progressFill != null)
                _progressFill.fillAmount = Mathf.Lerp(_progressFill.fillAmount, _progressTarget, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 6f));
            if (_advanceAt < 0f || Time.time < _advanceAt) return;
            _advanceAt = -1f;
            _index++;
            if (_index < data.questions.Length) ShowQuestion();
            else Finished?.Invoke(_correctCount, data.questions.Length, _score);
        }

        private void ShowQuestion()
        {
            QuizQuestion q = data.questions[_index];
            _answered = false;
            _situation.text = q.situation;
            _question.text = q.question;
            Narrator.Speak(q.situation + " " + q.question);

            int optionCount = q.options != null ? Mathf.Clamp(q.options.Length, 1, _cards.Count) : 0;
            for (int i = 0; i < _cards.Count; i++)
            {
                OptionCard card = _cards[i];
                bool visible = i < optionCount;
                card.Panel.gameObject.SetActive(visible);
                if (!visible) continue;
                QuizOption opt = q.options[i];
                card.Title.text = opt != null ? opt.title : "";
                card.Description.text = opt != null ? opt.description : "";
                HoloUI.SetPicture(card.Picture, opt != null ? opt.image : null);
                // Como en la imagen de referencia: ✓ en la opción correcta y ✗ en la incorrecta desde el inicio.
                bool isRight = i == q.correctIndex;
                Color c = isRight ? HoloColors.Correct : HoloColors.Wrong;
                card.Status.enabled = true;
                card.Status.sprite = HoloSprites.Icon(isRight ? "check" : "cross");
                card.Status.color = c;
                card.Button.SetAccent(c);
                card.Button.Interactable = true;
            }
            UpdateProgress(_index);

            // Cada nueva situación entra con animación (la primera ya la anima la apertura del panel).
            if (_index > 0)
            {
                if (_situationFx != null) _situationFx.Play();
                for (int i = 0; i < optionCount; i++)
                {
                    var fx = _cards[i].Panel.GetComponent<HoloAppear>();
                    if (fx != null) fx.Play(0.12f + i * 0.08f);
                }
                HoloFeedback.Play(HoloFeedback.Sound.Open, 0.5f);
            }

            if (video != null)
            {
                bool own = q.environment != null && q.environment.IsValid;
                Video360Source env = own ? q.environment : data.environment;
                if (env != null && env.IsValid && (own || _index == 0 || !video.IsActive)) video.Play(env, loop: true);
            }
        }

        private void Choose(int option)
        {
            if (_answered) return;
            _answered = true;
            QuizQuestion q = data.questions[_index];
            bool correct = option == q.correctIndex;
            if (correct)
            {
                _score += data.pointsPerCorrect;
                _correctCount++;
                StartCoroutine(Punch(_points.transform, 1.35f));
            }
            HoloFeedback.Play(correct ? HoloFeedback.Sound.Success : HoloFeedback.Sound.Error);
            HoloFeedback.HapticAll(correct ? 0.3f : 0.5f, correct ? 0.08f : 0.18f);
            if (option >= 0 && option < _cards.Count) StartCoroutine(Punch(_cards[option].Panel.transform, 1.05f));

            for (int i = 0; i < _cards.Count; i++)
            {
                OptionCard card = _cards[i];
                if (card.Panel.gameObject.activeSelf) card.Button.Interactable = false;
            }
            UpdateProgress(_index + 1);
            _advanceAt = Time.time + revealSeconds;
        }

        private void UpdateProgress(int done)
        {
            int total = data.questions.Length;
            _progressTarget = total > 0 ? done / (float)total : 0f;
            _progressText.text = $"{Mathf.Min(done, total)} / {total}";
        }

        /// <summary>Pequeño "latido" de escala para dar énfasis.</summary>
        private static IEnumerator Punch(Transform t, float peak)
        {
            Vector3 baseScale = Vector3.one;
            float time = 0f;
            while (time < 1f)
            {
                time += Time.unscaledDeltaTime / 0.35f;
                float k = Mathf.Sin(Mathf.Clamp01(time) * Mathf.PI);
                t.localScale = baseScale * Mathf.Lerp(1f, peak, k);
                yield return null;
            }
            t.localScale = baseScale;
        }

        // ------------------------------------------------------------------ UI

        private void Build()
        {
            _canvas = HoloUI.CreateCanvas(transform, "QuizCanvas", new Vector2(760, 1040), canvasScale);
            Transform root = _canvas.transform;

            // Encabezado: icono de balanza en círculo + "MÓDULO EDUCATIVO" + título + subtítulo.
            HoloPanel header = HoloUI.Panel(root, "Header", new Vector2(0, 455), new Vector2(560, 120));
            HoloPanel iconRing = HoloUI.Panel(header.transform, "IconRing", new Vector2(-218, 0), new Vector2(84, 84), HoloColors.PanelFillLight);
            iconRing.Background.sprite = HoloSprites.Circle;
            iconRing.Background.type = Image.Type.Simple;
            iconRing.Outline.enabled = false;
            _headerIcon = HoloUI.Icon(iconRing.transform, "Icon", "scale", Vector2.zero, 58, HoloColors.Text);
            _label = HoloUI.Text(header.transform, "Label", "", 15, new Vector2(50, 36), new Vector2(420, 22),
                TextAlignmentOptions.Left, HoloColors.Accent, FontStyles.Bold);
            _title = HoloUI.Text(header.transform, "Title", "", 30, new Vector2(50, 4), new Vector2(420, 40),
                TextAlignmentOptions.Left, HoloColors.Text, FontStyles.Bold);
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 18f;
            _title.fontSizeMax = 30f;
            _subtitle = HoloUI.Text(header.transform, "Subtitle", "", 16, new Vector2(50, -32), new Vector2(420, 26),
                TextAlignmentOptions.Left, HoloColors.TextDim);

            // Situación
            HoloPanel situation = HoloUI.Panel(root, "Situation", new Vector2(0, 300), new Vector2(620, 165));
            situation.gameObject.AddComponent<CanvasGroup>();
            _situationFx = situation.gameObject.AddComponent<HoloAppear>();
            _situationFx.scan = false;
            _situationFx.enabled = true;
            HoloButton listen = HoloUI.Button(situation.transform, "Escuchar", null, new Vector2(272, 56), new Vector2(46, 46), () =>
            {
                if (_index < data.questions.Length) Narrator.Speak(data.questions[_index].situation + " " + data.questions[_index].question);
            }, "speaker");
            var listenFx = listen.GetComponent<HoloHoverFx>();
            listenFx.tooltip = "Escuchar";
            listenFx.tooltipOffset = new Vector2(0f, 46f);
            listenFx.BuildTooltip();
            HoloUI.Text(situation.transform, "Label", "SITUACIÓN", 16, new Vector2(0, 60), new Vector2(560, 24),
                TextAlignmentOptions.Center, HoloColors.Accent, FontStyles.Bold);
            _situation = HoloUI.Text(situation.transform, "Text", "", 19, new Vector2(0, 8), new Vector2(570, 78));
            _question = HoloUI.Text(situation.transform, "Question", "", 21, new Vector2(0, -56), new Vector2(570, 32),
                TextAlignmentOptions.Center, HoloColors.Accent, FontStyles.Bold);

            // Tarjetas de opción
            for (int i = 0; i < 2; i++) _cards.Add(BuildCard(root, i));

            // Panel de puntaje: trofeo + lema + "+100 pts"
            HoloPanel reward = HoloUI.Panel(root, "Reward", new Vector2(0, -300), new Vector2(560, 92));
            HoloUI.Icon(reward.transform, "Trophy", "trophy", new Vector2(-235, 0), 52, HoloColors.Gold);
            TextMeshProUGUI motto = HoloUI.Text(reward.transform, "Motto", data != null ? data.motto : "", 19, new Vector2(-20, 14), new Vector2(360, 30),
                TextAlignmentOptions.Left, HoloColors.Text, FontStyles.Bold);
            motto.enableAutoSizing = true;
            motto.fontSizeMin = 12f;
            motto.fontSizeMax = 19f;
            HoloUI.Text(reward.transform, "Detail", data != null ? data.mottoDetail : "", 14, new Vector2(-20, -16), new Vector2(360, 24),
                TextAlignmentOptions.Left, HoloColors.TextDim);
            _points = HoloUI.Text(reward.transform, "Points", "+100 pts", 24, new Vector2(220, 0), new Vector2(110, 40),
                TextAlignmentOptions.Center, HoloColors.Text, FontStyles.Bold);

            // Progreso
            HoloPanel progress = HoloUI.Panel(root, "Progress", new Vector2(0, -420), new Vector2(400, 80));
            HoloUI.Text(progress.transform, "Label", "PROGRESO DEL MÓDULO", 14, new Vector2(0, 20), new Vector2(360, 22),
                TextAlignmentOptions.Center, HoloColors.TextDim, FontStyles.Bold);
            _progressFill = HoloUI.ProgressBar(progress.transform, "Bar", new Vector2(-28, -12), new Vector2(290, 10));
            _progressText = HoloUI.Text(progress.transform, "Count", "0 / 5", 18, new Vector2(158, -12), new Vector2(70, 26),
                TextAlignmentOptions.Center, HoloColors.Text, FontStyles.Bold);
        }

        private OptionCard BuildCard(Transform parent, int index)
        {
            var card = new OptionCard();
            float x = (index == 0 ? -1f : 1f) * (CardWidth / 2f + 14f);
            card.Panel = HoloUI.Panel(parent, $"Option{(char)('A' + index)}", new Vector2(x, 0), new Vector2(CardWidth, CardHeight));
            Transform t = card.Panel.transform;
            card.Picture = HoloUI.Picture(t, "Photo", null, new Vector2(0, 100), new Vector2(CardWidth - 28, 175));
            card.Status = HoloUI.Icon(t, "Status", "check", new Vector2(-CardWidth / 2f + 36, CardHeight / 2f - 34), 54, HoloColors.Correct);
            card.Status.enabled = false;
            card.Title = HoloUI.Text(t, "Title", "", 18, new Vector2(0, -18), new Vector2(CardWidth - 30, 48),
                TextAlignmentOptions.Center, HoloColors.Text, FontStyles.Bold);
            card.Description = HoloUI.Text(t, "Description", "", 16, new Vector2(0, -88), new Vector2(CardWidth - 34, 86),
                TextAlignmentOptions.Top, HoloColors.TextDim);
            int captured = index;
            // Toda la tarjeta se puede tocar o apuntar (más fácil que acertar al botón pequeño).
            var whole = card.Panel.gameObject.AddComponent<Button>();
            whole.targetGraphic = card.Panel.Background;
            var nav = whole.navigation; nav.mode = Navigation.Mode.None; whole.navigation = nav;
            whole.onClick.AddListener(() => Choose(captured));
            var fx = card.Panel.gameObject.AddComponent<HoloHoverFx>();
            fx.hoverScale = 1.025f;
            card.Panel.gameObject.AddComponent<CanvasGroup>();
            var appear = card.Panel.gameObject.AddComponent<HoloAppear>();
            appear.scan = false;
            card.Button = HoloUI.Button(t, "Choose", $"OPCIÓN {(char)('A' + index)}", new Vector2(0, -168), new Vector2(160, 48),
                () => Choose(captured), null, 17);
            return card;
        }
    }
}
