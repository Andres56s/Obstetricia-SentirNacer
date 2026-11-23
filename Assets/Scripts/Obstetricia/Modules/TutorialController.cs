using System;
using Obstetricia.Core;
using Obstetricia.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Obstetricia.Modules
{
    /// <summary>
    /// Tutorial guiado para quienes usan VR por primera vez: apuntar y seleccionar, tomar un objeto, recentrar
    /// la vista y escribir su nombre. Cada paso avanza solo al lograrlo; siempre se puede saltar.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        [SerializeField] private Material grabMaterial;

        private Canvas _canvas;
        private TextMeshProUGUI _step, _title, _body;
        private Image _icon;
        private HoloButton _action;
        private GameObject _cube;
        private ObstetriciaApp _app;
        private Action _done;
        private int _index;
        private bool _waitingGrab, _waitingRecenter;

        private struct Step
        {
            public string Icon, Title, Body, Button;
        }

        private static readonly Step[] Steps =
        {
            new Step { Icon = "pointer", Title = "¡Bienvenida/o!",
                Body = "Apunta con el control (o con tu mano) a este botón y aprieta el <b>gatillo</b> (o junta <b>índice y pulgar</b>).",
                Button = "Comenzar" },
            new Step { Icon = "cube", Title = "Tomar objetos",
                Body = "Acércate al <b>cubo</b> y mantén el <b>gatillo lateral</b> (o pellizca) para tomarlo. Muévelo y suéltalo.",
                Button = null },
            new Step { Icon = "recenter", Title = "Recentrar la vista",
                Body = "Si algo queda lejos o detrás tuyo, <b>mantén el botón Meta</b> (control derecho) o el botón de <b>menú</b> (control izquierdo) por un segundo.",
                Button = "Lo haré después" },
            new Step { Icon = "notes", Title = "Tu nombre",
                Body = "Escribe tu nombre para que tus resultados queden registrados.",
                Button = "Escribir nombre" },
            new Step { Icon = "check", Title = "¡Listo!",
                Body = "Ya sabes lo necesario. Elige un programa sobre la mesa para empezar.",
                Button = "Ir a la mesa" },
        };

        /// <summary>Ejecuta el tutorial desde el principio.</summary>
        public void Run(ObstetriciaApp app, Action done)
        {
            _app = app;
            _done = done;
            if (_canvas == null) Build();
            if (_app != null) _app.Recentered += OnRecentered;
            _index = 0;
            Show();
        }

        private void Build()
        {
            _canvas = HoloUI.CreateCanvas(transform, "Tutorial", new Vector2(640, 380));
            HoloUI.BringToFront(_canvas);
            HoloUI.Panel(_canvas.transform, "Fondo", Vector2.zero, new Vector2(640, 380), new Color(0.03f, 0.08f, 0.18f, 0.96f));
            Transform r = _canvas.transform;
            _step = HoloUI.Text(r, "Paso", "", 15, new Vector2(-180, 160), new Vector2(240, 24), TextAlignmentOptions.Left, HoloColors.Accent, FontStyles.Bold);
            HoloUI.Button(r, "Saltar", "Saltar tutorial", new Vector2(215, 158), new Vector2(180, 44), Finish, "next", 15, HoloColors.TextDim);
            HoloPanel ring = HoloUI.Panel(r, "Aro", new Vector2(-230, 60), new Vector2(110, 110), HoloColors.PanelFillLight);
            ring.Background.sprite = HoloSprites.Circle;
            ring.Background.type = Image.Type.Simple;
            ring.Outline.enabled = false;
            _icon = HoloUI.Icon(ring.transform, "Icono", "pointer", Vector2.zero, 70, HoloColors.Text);
            _title = HoloUI.Text(r, "Titulo", "", 30, new Vector2(60, 95), new Vector2(440, 44), TextAlignmentOptions.Left, HoloColors.Text, FontStyles.Bold);
            _body = HoloUI.Text(r, "Texto", "", 20, new Vector2(60, 15), new Vector2(440, 120), TextAlignmentOptions.TopLeft, HoloColors.Text);
            _action = HoloUI.Button(r, "Accion", "Comenzar", new Vector2(0, -130), new Vector2(300, 66), OnAction, "next", 22, HoloColors.Correct);
            _canvas.gameObject.SetActive(false);
        }

        private void Show()
        {
            Step s = Steps[_index];
            HoloUI.PlaceInFront(transform, 0.85f, -0.08f, 6f);
            _step.text = $"TUTORIAL · PASO {_index + 1} DE {Steps.Length}";
            _title.text = s.Title;
            _body.text = s.Body;
            _icon.sprite = HoloSprites.Icon(s.Icon);
            _action.gameObject.SetActive(!string.IsNullOrEmpty(s.Button));
            if (!string.IsNullOrEmpty(s.Button)) _action.SetText(s.Button);
            if (_canvas.gameObject.activeSelf) _canvas.GetComponent<HoloAppear>().Play();
            else _canvas.gameObject.SetActive(true);
            Narrator.Speak(s.Title + ". " + s.Body);

            _waitingGrab = _index == 1;
            _waitingRecenter = _index == 2;
            if (_waitingGrab) SpawnCube();
            else DestroyCube();
        }

        private void OnAction()
        {
            switch (_index)
            {
                case 3:
                    HoloKeyboard.Open("Escribe tu nombre", AppSettings.StudentName, name =>
                    {
                        if (!string.IsNullOrWhiteSpace(name)) AppSettings.StudentName = name;
                        Next();
                    });
                    return;
                case 4:
                    Finish();
                    return;
                default:
                    Next();
                    return;
            }
        }

        private void Next()
        {
            HoloFeedback.Play(HoloFeedback.Sound.Success, 0.7f);
            _index++;
            if (_index >= Steps.Length) Finish();
            else Show();
        }

        private void OnRecentered()
        {
            if (!_waitingRecenter) return;
            _waitingRecenter = false;
            Invoke(nameof(Next), 0.6f);
        }

        private void Finish()
        {
            if (_app != null) _app.Recentered -= OnRecentered;
            CancelInvoke();
            DestroyCube();
            Narrator.Stop();
            if (_canvas != null) HoloAppear.HideObject(_canvas.gameObject);
            Action done = _done;
            _done = null;
            done?.Invoke();
        }

        private void SpawnCube()
        {
            DestroyCube();
            _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube.name = "CuboTutorial";
            _cube.transform.localScale = Vector3.one * 0.12f;
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                _cube.transform.position = cam.transform.position + fwd * 0.5f + right * 0.32f + Vector3.down * 0.25f;
            }
            if (grabMaterial != null) _cube.GetComponent<Renderer>().sharedMaterial = grabMaterial;
            var rb = _cube.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            var grab = _cube.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;
            grab.selectEntered.AddListener(OnGrabbed);
            _cube.AddComponent<TutorialSpin>();
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (!_waitingGrab) return;
            _waitingGrab = false;
            HoloFeedback.HapticAll(0.3f, 0.1f);
            Invoke(nameof(Next), 1.2f);
        }

        private void DestroyCube()
        {
            if (_cube != null) Destroy(_cube);
            _cube = null;
        }
    }
}
