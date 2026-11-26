using System;
using System.Collections;
using Obstetricia.Data;
using Obstetricia.UI;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Obstetricia.Modules
{
    /// <summary>
    /// Maletín "Sentir Nacer" sobre su propia columna, a la izquierda de la mesa UCEN (réplica del kit físico):
    /// cuero blanco con ribete morado; dentro de la tapa, toallas colgadas, balones, aro de luz, aceites y el logo;
    /// en la base, manta, visor, rodillo y silla de parto. Al abrirlo aparece el acceso a la sala virtual.
    ///
    /// Se abre con su botón holográfico (gatillo, pellizco, toque o clic) o tocando el maletín.
    /// Todo se construye en código con piezas simples, como la mesa, para no depender de modelos externos.
    /// </summary>
    public class MaletinSala : MonoBehaviour
    {
        // Medidas del maletín (m). Z- = frente (manilla, hacia el usuario); la bisagra va atrás.
        private const float Ancho = 0.46f, Fondo = 0.30f, AltoBase = 0.11f, AltoTapa = 0.06f, Pared = 0.014f;
        private const float AnguloAbierto = 102f;

        private static readonly Color Cuero = new Color(0.93f, 0.92f, 0.96f);
        private static readonly Color Morado = new Color(0.42f, 0.2f, 0.5f);
        private static readonly Color Forro = new Color(0.78f, 0.12f, 0.14f);
        private static readonly Color Tapete = new Color(0.8f, 0.8f, 0.86f);

        /// <summary>Se invoca al elegir "Sala virtual" dentro del maletín abierto.</summary>
        public event Action SalaElegida;

        public bool Abierto { get; private set; }

        private Material _base;
        private Material _glow;
        private Transform _bisagra;
        private Renderer _aro;
        private MaterialPropertyBlock _bloque;
        private BoxCollider _collider;
        private GameObject _abrir, _menu;
        private Coroutine _anim;
        private float _angulo, _asomo;
        private bool _hover, _ayudaVisible;
        private Vector3 _abrirPos;

        /// <summary>Crea la columna con el maletín. "lugar" y "yaw" en el espacio del padre (la mesa).</summary>
        public static MaletinSala Crear(Transform padre, ObstetriciaTheme theme, Vector3 lugar, float yaw, float alturaColumna, float radioColumna)
        {
            var go = new GameObject("MaletinSentirNacer");
            go.transform.SetParent(padre, false);
            go.transform.localPosition = lugar;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var m = go.AddComponent<MaletinSala>();
            m.Construir(theme, alturaColumna, radioColumna);
            return m;
        }

        // ------------------------------------------------------------------ Construcción

        private void Construir(ObstetriciaTheme theme, float alto, float radio)
        {
            _base = theme != null && theme.pedestalBody != null ? theme.pedestalBody : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _glow = theme != null && theme.glow != null ? theme.glow : _base;
            Material tapaColumna = theme != null && theme.pedestalTop != null ? theme.pedestalTop : _base;
            _bloque = new MaterialPropertyBlock();

            // Columna con el mismo estilo que la mesa UCEN.
            Cilindro(transform, "Columna", _base, Color.white, new Vector3(0f, alto / 2f, 0f), new Vector3(radio * 1.15f, alto / 2f, radio * 1.15f), true);
            Cilindro(transform, "Borde", _glow, null, new Vector3(0f, alto - 0.004f, 0f), new Vector3(radio * 2.06f, 0.014f, radio * 2.06f), false);
            Cilindro(transform, "Disco", tapaColumna, null, new Vector3(0f, alto + 0.006f, 0f), new Vector3(radio * 2f, 0.012f, radio * 2f), true);

            var maletin = new GameObject("Maletin").transform;
            maletin.SetParent(transform, false);
            maletin.localPosition = new Vector3(0f, alto + 0.012f, 0f);

            ConstruirBase(maletin);
            ConstruirTapa(maletin);
            ConstruirBotones(maletin);

            // Tocar el maletín (agarre, toque o clic) también lo abre / cierra.
            _collider = maletin.gameObject.AddComponent<BoxCollider>();
            AjustarCollider();
            var toque = maletin.gameObject.AddComponent<XRSimpleInteractable>();
            toque.selectEntered.AddListener(_ => Alternar());
            toque.hoverEntered.AddListener(_ => _hover = true);
            toque.hoverExited.AddListener(_ => _hover = false);
        }

        private void ConstruirBase(Transform maletin)
        {
            float a = Ancho / 2f, f = Fondo / 2f, h = AltoBase;
            Caja(maletin, "Piso", Cuero, new Vector3(0f, Pared / 2f, 0f), new Vector3(Ancho, Pared, Fondo));
            Caja(maletin, "ParedFrente", Cuero, new Vector3(0f, h / 2f, -f + Pared / 2f), new Vector3(Ancho, h, Pared));
            Caja(maletin, "ParedFondo", Cuero, new Vector3(0f, h / 2f, f - Pared / 2f), new Vector3(Ancho, h, Pared));
            Caja(maletin, "ParedIzq", Cuero, new Vector3(-a + Pared / 2f, h / 2f, 0f), new Vector3(Pared, h, Fondo));
            Caja(maletin, "ParedDer", Cuero, new Vector3(a - Pared / 2f, h / 2f, 0f), new Vector3(Pared, h, Fondo));

            // Forro rojo en el borde interior y tapete gris lila con ribete morado.
            float ai = a - Pared, fi = f - Pared;
            Caja(maletin, "ForroFrente", Forro, new Vector3(0f, h - 0.006f, -fi + 0.002f), new Vector3(ai * 2f, 0.012f, 0.004f));
            Caja(maletin, "ForroFondo", Forro, new Vector3(0f, h - 0.006f, fi - 0.002f), new Vector3(ai * 2f, 0.012f, 0.004f));
            Caja(maletin, "ForroIzq", Forro, new Vector3(-ai + 0.002f, h - 0.006f, 0f), new Vector3(0.004f, 0.012f, fi * 2f));
            Caja(maletin, "ForroDer", Forro, new Vector3(ai - 0.002f, h - 0.006f, 0f), new Vector3(0.004f, 0.012f, fi * 2f));
            float yTapete = h * 0.55f;
            Caja(maletin, "Tapete", Tapete, new Vector3(0f, yTapete, 0f), new Vector3(ai * 2f - 0.01f, 0.006f, fi * 2f - 0.01f));
            Marco(maletin, "RibeteTapete", Morado, new Vector3(0f, yTapete + 0.004f, 0f), ai * 2f - 0.012f, fi * 2f - 0.012f, 0.004f, 0.003f);

            // Ribete morado del canto, manilla negra y cierres metálicos al frente.
            Marco(maletin, "RibeteBase", Morado, new Vector3(0f, h - 0.002f, 0f), Ancho + 0.002f, Fondo + 0.002f, 0.006f, 0.005f);
            Color negro = new Color(0.08f, 0.08f, 0.1f), metal = new Color(0.75f, 0.76f, 0.8f);
            Caja(maletin, "ManillaBarra", negro, new Vector3(0f, h * 0.55f, -f - 0.022f), new Vector3(0.11f, 0.014f, 0.012f));
            Caja(maletin, "ManillaIzq", negro, new Vector3(-0.05f, h * 0.55f, -f - 0.011f), new Vector3(0.012f, 0.014f, 0.022f));
            Caja(maletin, "ManillaDer", negro, new Vector3(0.05f, h * 0.55f, -f - 0.011f), new Vector3(0.012f, 0.014f, 0.022f));
            Caja(maletin, "CierreIzq", metal, new Vector3(-0.15f, h - 0.012f, -f - 0.003f), new Vector3(0.022f, 0.03f, 0.006f), 0.8f);
            Caja(maletin, "CierreDer", metal, new Vector3(0.15f, h - 0.012f, -f - 0.003f), new Vector3(0.022f, 0.03f, 0.006f), 0.8f);

            // Contenido de la base.
            float y = yTapete + 0.003f;
            // Manta azul doblada (adelante a la izquierda).
            Caja(maletin, "Manta", new Color(0.15f, 0.2f, 0.78f), new Vector3(-0.13f, y + 0.012f, -0.06f), new Vector3(0.14f, 0.024f, 0.1f));
            Caja(maletin, "MantaEtiqueta", new Color(0.65f, 0.55f, 0.4f), new Vector3(-0.08f, y + 0.0245f, -0.04f), new Vector3(0.018f, 0.002f, 0.012f));
            // Visor VR (atrás a la izquierda).
            Caja(maletin, "Visor", negro, new Vector3(-0.12f, y + 0.03f, 0.06f), new Vector3(0.13f, 0.06f, 0.075f), 0.4f);
            Caja(maletin, "VisorFranja", new Color(0.6f, 0.6f, 0.65f), new Vector3(-0.12f, y + 0.03f, 0.022f), new Vector3(0.131f, 0.006f, 0.002f), 0.6f);
            Caja(maletin, "ControlVR", new Color(0.18f, 0.18f, 0.2f), new Vector3(-0.12f, y + 0.066f, 0.06f), new Vector3(0.1f, 0.012f, 0.04f));
            // Rodillo de masaje rojo (adelante a la derecha).
            Transform rodillo = Pivote(maletin, "Rodillo", new Vector3(0.1f, y + 0.012f, -0.08f), Quaternion.Euler(0f, -25f, 0f));
            Cilindro(rodillo, "Mango", null, new Color(0.75f, 0.1f, 0.12f), new Vector3(-0.04f, 0f, 0f), new Vector3(0.012f, 0.05f, 0.012f), true, Quaternion.Euler(0f, 0f, 90f));
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 3; j++)
                    Esfera(rodillo, "Bolita", new Color(0.85f, 0.08f, 0.1f), new Vector3(0.03f + i * 0.013f, 0.004f, (j - 1) * 0.012f), 0.013f);
            // Silla de parto blanca en miniatura (atrás a la derecha).
            Transform silla = Pivote(maletin, "SillaParto", new Vector3(0.1f, y, 0.05f), Quaternion.Euler(0f, 15f, 0f));
            Color blanco = new Color(0.97f, 0.97f, 0.98f);
            Caja(silla, "Asiento", blanco, new Vector3(0f, 0.03f, 0f), new Vector3(0.06f, 0.008f, 0.05f));
            Caja(silla, "Respaldo", blanco, new Vector3(0f, 0.062f, 0.03f), new Vector3(0.06f, 0.06f, 0.008f)).transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            Caja(silla, "BrazoIzq", blanco, new Vector3(-0.034f, 0.045f, 0f), new Vector3(0.008f, 0.006f, 0.05f));
            Caja(silla, "BrazoDer", blanco, new Vector3(0.034f, 0.045f, 0f), new Vector3(0.008f, 0.006f, 0.05f));
            for (int i = 0; i < 4; i++)
                Caja(silla, "Pata", blanco, new Vector3(i % 2 == 0 ? -0.025f : 0.025f, 0.015f, i < 2 ? -0.02f : 0.02f), new Vector3(0.006f, 0.03f, 0.006f));
        }

        private void ConstruirTapa(Transform maletin)
        {
            // La tapa gira en la bisagra (canto trasero superior de la base). Cerrada, su cara interior mira hacia abajo:
            // en coordenadas de la tapa, -Y = hacia el interior y -Z = hacia el frente (arriba cuando está abierta).
            _bisagra = new GameObject("Bisagra").transform;
            _bisagra.SetParent(maletin, false);
            _bisagra.localPosition = new Vector3(0f, AltoBase, Fondo / 2f);
            Transform t = _bisagra;
            float a = Ancho / 2f, h = AltoTapa;
            Caja(t, "Techo", Cuero, new Vector3(0f, h - Pared / 2f, -Fondo / 2f), new Vector3(Ancho, Pared, Fondo));
            Caja(t, "ParedFrente", Cuero, new Vector3(0f, h / 2f, -Fondo + Pared / 2f), new Vector3(Ancho, h, Pared));
            Caja(t, "ParedFondo", Cuero, new Vector3(0f, h / 2f, -Pared / 2f), new Vector3(Ancho, h, Pared));
            Caja(t, "ParedIzq", Cuero, new Vector3(-a + Pared / 2f, h / 2f, -Fondo / 2f), new Vector3(Pared, h, Fondo));
            Caja(t, "ParedDer", Cuero, new Vector3(a - Pared / 2f, h / 2f, -Fondo / 2f), new Vector3(Pared, h, Fondo));
            Marco(t, "RibeteTapa", Morado, new Vector3(0f, 0.002f, -Fondo / 2f), Ancho + 0.002f, Fondo + 0.002f, 0.006f, 0.005f);
            Caja(t, "ManillaSuperior", new Color(0.08f, 0.08f, 0.1f), new Vector3(0f, h + 0.006f, -Fondo / 2f), new Vector3(0.1f, 0.012f, 0.014f));

            // Panel interior acolchado (cara que se ve con el maletín abierto).
            float yi = h - Pared;              // plano de la cara interior
            Caja(t, "PanelInterior", Cuero, new Vector3(0f, yi - 0.003f, -Fondo / 2f), new Vector3(Ancho - 2f * Pared, 0.006f, Fondo - 2f * Pared));
            Marco(t, "MarcoLogo", Morado, new Vector3(0f, yi - 0.007f, -0.08f), 0.17f, 0.07f, 0.006f, 0.002f);
            Texto(t, "LogoSentirNacer", "<b>S</b>entir\n<size=80%>nacer</size>", Morado, new Vector3(0f, yi - 0.009f, -0.08f), new Vector2(0.15f, 0.06f));

            // Barras de madera con las toallas colgando (parte alta de la tapa abierta).
            Color madera = new Color(0.85f, 0.72f, 0.5f);
            float zBarra = -Fondo + 0.04f;
            Cilindro(t, "Barra", null, madera, new Vector3(0f, yi - 0.016f, zBarra), new Vector3(0.008f, 0.19f, 0.008f), true, Quaternion.Euler(0f, 0f, 90f));
            Caja(t, "SoporteBarra", new Color(0.9f, 0.88f, 0.82f), new Vector3(-0.2f, yi - 0.012f, zBarra), new Vector3(0.025f, 0.024f, 0.05f));
            Caja(t, "ToallaRoja", new Color(0.82f, 0.12f, 0.15f), new Vector3(-0.09f, yi - 0.02f, zBarra + 0.065f), new Vector3(0.12f, 0.007f, 0.13f));
            for (int i = 0; i < 6; i++) // estrellas blancas
                Caja(t, "Estrella", Color.white, new Vector3(-0.13f + (i % 3) * 0.04f, yi - 0.0245f, zBarra + 0.03f + (i / 3) * 0.06f), new Vector3(0.008f, 0.001f, 0.008f));
            Caja(t, "ToallaTurquesa", new Color(0.0f, 0.6f, 0.65f), new Vector3(0.07f, yi - 0.02f, zBarra + 0.07f), new Vector3(0.12f, 0.007f, 0.14f));
            Caja(t, "Pinza1", new Color(0.92f, 0.88f, 0.75f), new Vector3(-0.09f, yi - 0.022f, zBarra + 0.003f), new Vector3(0.125f, 0.009f, 0.02f));
            Caja(t, "Pinza2", new Color(0.92f, 0.88f, 0.75f), new Vector3(0.07f, yi - 0.022f, zBarra + 0.003f), new Vector3(0.125f, 0.009f, 0.02f));

            // Repisa con los dos balones azules (a la izquierda, bajo las toallas).
            float zRepisa = -0.075f;
            Caja(t, "Repisa", new Color(0.62f, 0.85f, 0.88f), new Vector3(-0.165f, yi - 0.02f, zRepisa), new Vector3(0.1f, 0.04f, 0.006f));
            Esfera(t, "BalonAzul", new Color(0.12f, 0.25f, 0.85f), new Vector3(-0.19f, yi - 0.025f, zRepisa - 0.022f), 0.04f);
            Esfera(t, "BalonCeleste", new Color(0.35f, 0.65f, 0.95f), new Vector3(-0.14f, yi - 0.025f, zRepisa - 0.022f), 0.04f);

            // Aro de luz de colores (abajo a la izquierda, bajo los balones).
            _aro = Cilindro(t, "AroLuz", _glow, null, new Vector3(-0.165f, yi - 0.004f, -0.042f), new Vector3(0.036f, 0.003f, 0.036f), false).GetComponent<Renderer>();
            Cilindro(t, "AroCentro", null, new Color(0.05f, 0.05f, 0.08f), new Vector3(-0.165f, yi - 0.006f, -0.042f), new Vector3(0.024f, 0.003f, 0.024f), false);

            // Gradilla de madera con los frascos de aceite (a la derecha).
            Caja(t, "Gradilla", madera, new Vector3(0.17f, yi - 0.02f, -0.06f), new Vector3(0.085f, 0.04f, 0.008f));
            for (int i = 0; i < 4; i++)
            {
                float x = 0.142f + i * 0.019f;
                Cilindro(t, "Frasco", null, new Color(0.12f, 0.08f, 0.05f), new Vector3(x, yi - 0.022f, -0.09f), new Vector3(0.014f, 0.024f, 0.014f), true, Quaternion.Euler(90f, 0f, 0f));
                Cilindro(t, "Tapita", null, new Color(0.95f, 0.95f, 0.97f), new Vector3(x, yi - 0.022f, -0.121f), new Vector3(0.01f, 0.008f, 0.01f), true, Quaternion.Euler(90f, 0f, 0f));
            }
        }

        private void ConstruirBotones(Transform maletin)
        {
            // Botón para abrir, flotando sobre el maletín cerrado.
            Canvas abrir = HoloUI.CreateCanvas(maletin, "AbrirMaletin", new Vector2(300, 70));
            _abrirPos = new Vector3(0f, AltoBase + AltoTapa + 0.13f, -0.02f);
            abrir.transform.localPosition = _abrirPos;
            abrir.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            HoloUI.Button(abrir.transform, "Boton", "ABRIR MALETÍN", Vector2.zero, new Vector2(300, 66), Abrir, "next", 20);
            _abrir = abrir.gameObject;
            _abrir.SetActive(false);

            // Con el maletín abierto: acceso a la sala virtual y botón para cerrarlo.
            Canvas menu = HoloUI.CreateCanvas(maletin, "MenuMaletin", new Vector2(460, 150));
            menu.transform.localPosition = new Vector3(0f, AltoBase + Fondo + 0.12f, 0.06f);
            menu.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            HoloUI.Button(menu.transform, "Sala", "SALA VIRTUAL DE OBSTETRICIA", new Vector2(0, 32), new Vector2(460, 80), IrALaSala, "home", 21, HoloColors.Gold);
            HoloUI.Button(menu.transform, "Cerrar", "Cerrar maletín", new Vector2(0, -48), new Vector2(220, 50), Cerrar, "cross", 16);
            _menu = menu.gameObject;
            _menu.SetActive(false);
        }

        // ------------------------------------------------------------------ Abrir y cerrar

        /// <summary>Muestra u oculta el botón "Abrir maletín" (se ve junto con las tarjetas de la mesa).</summary>
        public void MostrarAyuda(bool visible)
        {
            _ayudaVisible = visible;
            if (_abrir == null) return;
            bool mostrar = visible && !Abierto;
            if (mostrar && !_abrir.activeSelf) _abrir.SetActive(true);
            else if (!mostrar && _abrir.activeSelf) HoloAppear.HideObject(_abrir);
        }

        /// <summary>Lo deja cerrado al instante (al ocultar o volver a mostrar la mesa).</summary>
        public void Reiniciar()
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = null;
            Abierto = false;
            _angulo = 0f;
            _asomo = 0f;
            _hover = false;
            _ayudaVisible = false;
            AplicarAngulo();
            AjustarCollider();
            if (_menu != null) _menu.SetActive(false);
            if (_abrir != null) _abrir.SetActive(false);
        }

        public void Alternar()
        {
            if (Abierto) Cerrar();
            else Abrir();
        }

        public void Abrir()
        {
            if (Abierto || !isActiveAndEnabled) return;
            Abierto = true;
            HoloFeedback.Play(HoloFeedback.Sound.Open, 0.7f);
            if (_abrir.activeSelf) HoloAppear.HideObject(_abrir);
            Animar(AnguloAbierto, 0.75f, true, () => _menu.SetActive(true)); // HoloAppear anima el menú
            AjustarCollider();
        }

        public void Cerrar()
        {
            if (!Abierto || !isActiveAndEnabled) return;
            Abierto = false;
            HoloFeedback.Play(HoloFeedback.Sound.Close, 0.6f);
            if (_menu.activeSelf) HoloAppear.HideObject(_menu);
            Animar(0f, 0.5f, false, () =>
            {
                HoloFeedback.Play(HoloFeedback.Sound.Click, 0.5f); // la tapa calza
                AjustarCollider();
                MostrarAyuda(_ayudaVisible);
            });
        }

        private void IrALaSala()
        {
            HoloFeedback.Play(HoloFeedback.Sound.Success, 0.5f);
            SalaElegida?.Invoke();
        }

        private void Animar(float hasta, float segundos, bool rebote, Action listo)
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(AnimarTapa(hasta, segundos, rebote, listo));
        }

        private IEnumerator AnimarTapa(float hasta, float segundos, bool rebote, Action listo)
        {
            float desde = _angulo, t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / segundos;
                float k = Mathf.Clamp01(t);
                float e = rebote ? Ease.OutBack(k, 1.3f) : Ease.InOutSine(k);
                _angulo = Mathf.LerpUnclamped(desde, hasta, e);
                AplicarAngulo();
                yield return null;
            }
            _angulo = hasta;
            AplicarAngulo();
            _anim = null;
            listo?.Invoke();
        }

        private void AplicarAngulo()
        {
            if (_bisagra != null) _bisagra.localRotation = Quaternion.Euler(_angulo + _asomo, 0f, 0f);
        }

        /// <summary>Cerrado cubre todo el maletín; abierto solo la base (la tapa queda libre para ver su interior).</summary>
        private void AjustarCollider()
        {
            if (_collider == null) return;
            float alto = Abierto ? AltoBase : AltoBase + AltoTapa;
            _collider.center = new Vector3(0f, alto / 2f, 0f);
            _collider.size = new Vector3(Ancho, alto, Fondo);
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            // Al apuntar el maletín cerrado, la tapa se asoma un poco.
            float asomo = !Abierto && _anim == null && _hover ? 6f : 0f;
            if (!Mathf.Approximately(_asomo, asomo))
            {
                _asomo = Mathf.MoveTowards(_asomo, asomo, Time.unscaledDeltaTime * 40f);
                AplicarAngulo();
            }
            // El botón de abrir flota suavemente.
            if (_abrir != null && _abrir.activeSelf)
                _abrir.transform.localPosition = _abrirPos + Vector3.up * (Mathf.Sin(time * 2f) * 0.008f);
            // El aro de luz cambia de color como el del kit real.
            if (_aro != null && _angulo > 1f)
            {
                _aro.GetPropertyBlock(_bloque);
                _bloque.SetColor("_BaseColor", Color.HSVToRGB(Mathf.Repeat(time * 0.12f, 1f), 0.65f, 1f));
                _aro.SetPropertyBlock(_bloque);
            }
        }

        // ------------------------------------------------------------------ Piezas

        private Transform Pivote(Transform padre, string nombre, Vector3 pos, Quaternion rot)
        {
            var t = new GameObject(nombre).transform;
            t.SetParent(padre, false);
            t.localPosition = pos;
            t.localRotation = rot;
            return t;
        }

        private readonly System.Collections.Generic.Dictionary<(Color, float), Material> _tintes =
            new System.Collections.Generic.Dictionary<(Color, float), Material>();

        /// <summary>Material del maletín de un color (uno por color: muchas piezas comparten el mismo).</summary>
        private Material Tinte(Color color, float brillo)
        {
            if (_tintes.TryGetValue((color, brillo), out Material hecho)) return hecho;
            var m = new Material(_base) { name = "Maletin" };
            _tintes[(color, brillo)] = m;
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", brillo);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", brillo > 0.7f ? 0.8f : 0f);
            return m;
        }

        private GameObject Caja(Transform padre, string nombre, Color color, Vector3 pos, Vector3 escala, float brillo = 0.25f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            return Preparar(go, padre, nombre, Tinte(color, brillo), pos, escala, Quaternion.identity, true);
        }

        private void Esfera(Transform padre, string nombre, Color color, Vector3 pos, float diametro)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Preparar(go, padre, nombre, Tinte(color, 0.6f), pos, Vector3.one * diametro, Quaternion.identity, true);
        }

        /// <summary>Cilindro suave de la mesa (alto = 2 × escala Y). Con "material" null se tiñe el material base.</summary>
        private GameObject Cilindro(Transform padre, string nombre, Material material, Color? color, Vector3 pos, Vector3 escala, bool sombras, Quaternion? rot = null)
        {
            var go = new GameObject(nombre);
            go.AddComponent<MeshFilter>().sharedMesh = SmoothMeshes.Cylinder;
            go.AddComponent<MeshRenderer>();
            Material m = material != null ? material : Tinte(color ?? Color.white, 0.3f);
            return Preparar(go, padre, nombre, m, pos, escala, rot ?? Quaternion.identity, sombras);
        }

        /// <summary>Marco rectangular plano (cuatro listones) en el plano XZ.</summary>
        private void Marco(Transform padre, string nombre, Color color, Vector3 centro, float ancho, float fondo, float grosor, float alto)
        {
            Caja(padre, nombre, color, centro + new Vector3(0f, 0f, -fondo / 2f), new Vector3(ancho, alto, grosor));
            Caja(padre, nombre, color, centro + new Vector3(0f, 0f, fondo / 2f), new Vector3(ancho, alto, grosor));
            Caja(padre, nombre, color, centro + new Vector3(-ancho / 2f, 0f, 0f), new Vector3(grosor, alto, fondo));
            Caja(padre, nombre, color, centro + new Vector3(ancho / 2f, 0f, 0f), new Vector3(grosor, alto, fondo));
        }

        /// <summary>Texto pegado a la cara interior de la tapa, derecho cuando la tapa está abierta.</summary>
        private void Texto(Transform padre, string nombre, string texto, Color color, Vector3 pos, Vector2 tamano)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = pos;
            // Se lee desde -Y (el interior) con su "arriba" hacia -Z (la parte alta de la tapa abierta).
            go.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = texto;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = 40f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.lineSpacing = -25f;
            tmp.color = color;
            tmp.margin = Vector4.zero;
            tmp.rectTransform.sizeDelta = tamano;
        }

        private static GameObject Preparar(GameObject go, Transform padre, string nombre, Material material, Vector3 pos, Vector3 escala, Quaternion rot, bool sombras)
        {
            go.name = nombre;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // solo el maletín completo recibe el rayo
            go.transform.SetParent(padre, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = escala;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            if (!sombras) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
