using System;
using UnityEngine;

namespace Obstetricia.Sala
{
    /// <summary>Reloj de muro con la hora real (sirve para registrar contracciones y la hora de nacimiento).</summary>
    public class RelojSala : MonoBehaviour
    {
        [SerializeField] private Transform horas, minutos, segundos;

        private int _last = -1;

        private void Update()
        {
            DateTime n = DateTime.Now;
            if (n.Second == _last) return;
            _last = n.Second;
            Set(horas, (n.Hour % 12 + n.Minute / 60f) * 30f);
            Set(minutos, (n.Minute + n.Second / 60f) * 6f);
            Set(segundos, n.Second * 6f);
        }

        // Visto de frente (+Z hacia quien mira) el avance horario es un giro negativo en Z... positivo en el eje opuesto.
        private static void Set(Transform t, float deg)
        {
            if (t != null) t.localRotation = Quaternion.Euler(0f, 0f, deg);
        }
    }
}
