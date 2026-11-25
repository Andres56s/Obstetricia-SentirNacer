using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Obstetricia.Core
{
    /// <summary>
    /// Registro de resultados por estudiante. Cada quiz terminado agrega una fila a "resultados_obstetricia.csv"
    /// en la carpeta de datos de la app. En la Quest, conectada por USB al PC, está en:
    /// Quest 3S\Almacenamiento interno\Android\data\com.ericalvert.mrgame\files\resultados_obstetricia.csv
    /// (se abre con Excel o Google Sheets).
    /// </summary>
    public static class StudentRecords
    {
        public const string FileName = "resultados_obstetricia.csv";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Guarda un resultado. Devuelve false si no se pudo escribir.</summary>
        public static bool Save(string module, int correct, int total, int score)
        {
            // Dentro de la página web, el resultado también se envía para guardarlo en Firestore.
            Desktop.WebBridge.SendResult(module, correct, total, score);
            try
            {
                bool exists = File.Exists(FilePath);
                var sb = new StringBuilder();
                if (!exists) sb.AppendLine("fecha;hora;estudiante;modulo;correctas;total;puntaje;porcentaje");
                DateTime now = DateTime.Now;
                string student = string.IsNullOrWhiteSpace(AppSettings.StudentName) ? "Sin nombre" : AppSettings.StudentName;
                float pct = total > 0 ? correct * 100f / total : 0f;
                sb.AppendLine(string.Join(";",
                    now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    now.ToString("HH:mm", CultureInfo.InvariantCulture),
                    Clean(student), Clean(module), correct, total, score,
                    pct.ToString("0", CultureInfo.InvariantCulture) + "%"));
                // UTF-8 con BOM para que Excel muestre bien los acentos.
                File.AppendAllText(FilePath, sb.ToString(), new UTF8Encoding(!exists));
                Debug.Log($"[Obstetricia] Resultado guardado en {FilePath}: {student} · {module} · {correct}/{total}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Obstetricia] No se pudo guardar el resultado: {ex.Message}");
                return false;
            }
        }

        private static string Clean(string s) => (s ?? "").Replace(";", ",").Replace("\n", " ");
    }
}
