using Obstetricia.UI;
using TMPro;
using UnityEngine;

namespace Obstetricia.Sala
{
    /// <summary>Test de Apgar interactivo: se elige un puntaje por criterio y se suma la evaluación del recién nacido.</summary>
    public class PanelApgar : MonoBehaviour
    {
        private static readonly string[] Criterios = { "Color", "Frecuencia cardíaca", "Reflejos", "Tono muscular", "Respiración" };
        private static readonly string[,] Opciones =
        {
            { "Pálido o cianótico", "Cuerpo rosado, extremidades azules", "Completamente rosado" },
            { "Ausente", "Menos de 100 lpm", "100 lpm o más" },
            { "Sin respuesta", "Mueca", "Llanto, tos o estornudo" },
            { "Flácido", "Cierta flexión", "Movimiento activo" },
            { "Ausente", "Lenta o irregular", "Buena, llanto vigoroso" },
        };

        private readonly int[] _puntos = { -1, -1, -1, -1, -1 };
        private readonly HoloButton[,] _btn = new HoloButton[5, 3];
        private TextMeshProUGUI _total, _texto;

        private void Start()
        {
            Canvas c = HoloUI.CreateCanvas(transform, "Apgar", new Vector2(640, 520));
            Transform r = c.transform;
            HoloUI.Panel(r, "Fondo", Vector2.zero, new Vector2(640, 520), new Color(0.03f, 0.09f, 0.2f, 0.94f));
            HoloUI.Text(r, "Titulo", "TEST DE APGAR", 25, new Vector2(-150, 226), new Vector2(300, 36), TextAlignmentOptions.Left, HoloColors.Text, FontStyles.Bold);
            HoloUI.Text(r, "Sub", "Se evalúa al minuto y a los 5 minutos de vida", 14, new Vector2(150, 226), new Vector2(300, 24), TextAlignmentOptions.Right, HoloColors.TextDim);
            for (int k = 0; k < 3; k++)
                HoloUI.Text(r, "P" + k, k + (k == 1 ? " punto" : " puntos"), 14, new Vector2(-88 + k * 156, 192), new Vector2(150, 22), TextAlignmentOptions.Center, HoloColors.Accent, FontStyles.Bold);
            for (int i = 0; i < 5; i++)
            {
                float y = 146 - i * 66;
                HoloUI.Text(r, "C" + i, Criterios[i], 16, new Vector2(-240, y), new Vector2(134, 54), TextAlignmentOptions.Left, HoloColors.Text, FontStyles.Bold);
                for (int k = 0; k < 3; k++)
                {
                    int ci = i, ck = k;
                    _btn[i, k] = HoloUI.Button(r, $"A{i}_{k}", Opciones[i, k], new Vector2(-88 + k * 156, y), new Vector2(150, 58), () => Elegir(ci, ck), null, 14);
                }
            }
            _total = HoloUI.Text(r, "Total", "", 34, new Vector2(-236, -218), new Vector2(130, 50), TextAlignmentOptions.Center, HoloColors.Gold, FontStyles.Bold);
            _texto = HoloUI.Text(r, "Lectura", "", 16, new Vector2(30, -218), new Vector2(370, 60), TextAlignmentOptions.Left, HoloColors.Text);
            HoloUI.Button(r, "ApgarLimpiar", null, new Vector2(276, -218), new Vector2(54, 54), () =>
            {
                for (int i = 0; i < 5; i++) _puntos[i] = -1;
                Refrescar();
            }, "restart");
            Refrescar();
        }

        private void Elegir(int criterio, int puntos)
        {
            _puntos[criterio] = puntos;
            Refrescar();
        }

        private void Refrescar()
        {
            int total = 0, n = 0;
            for (int i = 0; i < 5; i++)
            {
                for (int k = 0; k < 3; k++) _btn[i, k].SetSelected(_puntos[i] == k);
                if (_puntos[i] < 0) continue;
                total += _puntos[i];
                n++;
            }
            _total.text = n == 0 ? "– / 10" : total + " / 10";
            if (n < 5)
            {
                _total.color = HoloColors.TextDim;
                _texto.text = n == 0 ? "Elige un puntaje para cada criterio." : $"Faltan {5 - n} criterios.";
            }
            else if (total >= 7)
            {
                _total.color = HoloColors.Correct;
                _texto.text = "<b>7 a 10:</b> buena adaptación. Cuidados de rutina y contacto piel con piel.";
            }
            else if (total >= 4)
            {
                _total.color = HoloColors.Gold;
                _texto.text = "<b>4 a 6:</b> depresión moderada. Requiere asistencia y reevaluación.";
            }
            else
            {
                _total.color = HoloColors.Wrong;
                _texto.text = "<b>0 a 3:</b> depresión grave. La reanimación no debe esperar al puntaje.";
            }
        }
    }
}
