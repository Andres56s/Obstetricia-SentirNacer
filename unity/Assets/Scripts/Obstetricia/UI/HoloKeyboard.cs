using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Obstetricia.UI
{
    /// <summary>
    /// Teclado holográfico simple (letras, Ñ, espacio, borrar, listo) para escribir el nombre del estudiante.
    /// Se abre frente al usuario; teclas grandes para apuntar con el rayo o tocar con el dedo.
    /// </summary>
    public class HoloKeyboard : MonoBehaviour
    {
        private static HoloKeyboard _instance;

        private Canvas _canvas;
        private TextMeshProUGUI _title, _field;
        private string _text = "";
        private Action<string> _done;
        private const int MaxLength = 32;

        /// <summary>Abre el teclado frente a la cabeza. "done" recibe el texto (null si se canceló).</summary>
        public static void Open(string title, string initial, Action<string> done)
        {
            if (_instance == null)
            {
                var go = new GameObject("TecladoHolografico");
                _instance = go.AddComponent<HoloKeyboard>();
                _instance.Build();
            }
            _instance.Show(title, initial, done);
        }

        private void Show(string title, string initial, Action<string> done)
        {
            _title.text = title;
            _text = initial ?? "";
            _done = done;
            Refresh();
            HoloUI.PlaceInFront(transform, 0.62f, -0.18f, 18f);
            _canvas.gameObject.SetActive(true);
        }

        private void Build()
        {
            _canvas = HoloUI.CreateCanvas(transform, "Teclado", new Vector2(760, 420));
            HoloUI.BringToFront(_canvas, 60);
            HoloUI.Panel(_canvas.transform, "Fondo", Vector2.zero, new Vector2(760, 420), new Color(0.03f, 0.08f, 0.18f, 0.97f));
            _title = HoloUI.Text(_canvas.transform, "Titulo", "", 20, new Vector2(0, 182), new Vector2(700, 30),
                TextAlignmentOptions.Center, HoloColors.Accent, FontStyles.Bold);
            HoloPanel field = HoloUI.Panel(_canvas.transform, "Campo", new Vector2(0, 132), new Vector2(680, 56), new Color(0.01f, 0.04f, 0.1f, 0.95f));
            field.Background.raycastTarget = false;
            _field = HoloUI.Text(field.transform, "Texto", "", 26, Vector2.zero, new Vector2(650, 50), TextAlignmentOptions.Left,
                HoloColors.Text, FontStyles.Bold);

            string[] rows = { "QWERTYUIOP", "ASDFGHJKLÑ", "ZXCVBNM" };
            const float key = 62f, gap = 8f;
            for (int r = 0; r < rows.Length; r++)
            {
                string row = rows[r];
                float width = row.Length * key + (row.Length - 1) * gap;
                float y = 62f - r * (key + gap);
                for (int i = 0; i < row.Length; i++)
                {
                    char c = row[i];
                    float x = -width / 2f + key / 2f + i * (key + gap) + (r == 2 ? -80f : 0f);
                    HoloUI.Button(_canvas.transform, "Tecla_" + c, c.ToString(), new Vector2(x, y), new Vector2(key, key), () => Type(c), null, 24);
                }
            }
            HoloUI.Button(_canvas.transform, "Borrar", "Borrar", new Vector2(250, 62f - 2 * (key + gap)), new Vector2(150, key), Backspace, "undo", 18);
            HoloUI.Button(_canvas.transform, "Espacio", "Espacio", new Vector2(-150, -162), new Vector2(300, 56), () => Type(' '), null, 18);
            HoloUI.Button(_canvas.transform, "Cancelar", "Cancelar", new Vector2(95, -162), new Vector2(160, 56), Cancel, "close", 18, HoloColors.Wrong);
            HoloUI.Button(_canvas.transform, "Listo", "Listo", new Vector2(275, -162), new Vector2(170, 56), Accept, "check", 20, HoloColors.Correct);
            _canvas.gameObject.SetActive(false);
        }

        private void Type(char c)
        {
            if (_text.Length >= MaxLength) return;
            // Primera letra de cada palabra en mayúscula, el resto en minúscula.
            bool startOfWord = _text.Length == 0 || _text[_text.Length - 1] == ' ';
            _text += startOfWord ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
            Refresh();
        }

        private void Backspace()
        {
            if (_text.Length > 0) _text = _text.Substring(0, _text.Length - 1);
            Refresh();
        }

        private void Refresh()
        {
            bool blink = Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0;
            _field.text = _text + (blink ? "<color=#5FD3FF>|</color>" : " ");
        }

        /// <summary>True mientras el teclado está abierto (el modo escritorio no mueve al jugador con WASD).</summary>
        public static bool IsOpen => _instance != null && _instance._canvas != null && _instance._canvas.gameObject.activeSelf
                                     && _instance._done != null;

        private void OnEnable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput += OnPhysicalKey;
        }

        private void OnDisable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnPhysicalKey;
        }

        /// <summary>Modo escritorio: también se puede escribir con el teclado físico (Enter = Listo, Esc = Cancelar).</summary>
        private void OnPhysicalKey(char c)
        {
            // Borrar, Enter y Esc se leen en Update (aquí solo letras y espacio, para no duplicarlos).
            if (IsOpen && (char.IsLetter(c) || c == ' ')) Type(c);
        }

        private void Update()
        {
            if (_canvas != null && _canvas.gameObject.activeSelf)
            {
                Refresh();
                Keyboard kb = Keyboard.current;
                if (kb != null && _done != null)
                {
                    if (kb.backspaceKey.wasPressedThisFrame) Backspace();
                    else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Accept();
                    else if (kb.escapeKey.wasPressedThisFrame) Cancel();
                }
            }
        }

        private void Accept() => Close(_text.Trim());

        private void Cancel() => Close(null);

        private void Close(string result)
        {
            HoloAppear.HideObject(_canvas.gameObject);
            Action<string> cb = _done;
            _done = null;
            cb?.Invoke(result);
        }
    }
}
