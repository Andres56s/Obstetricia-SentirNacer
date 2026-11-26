#if UNITY_EDITOR
using Obstetricia.Data;

namespace ObstetriciaSetup
{
    /// <summary>
    /// Contenido inicial (borrador) de los programas. Se copia a los assets de datos una sola vez; después se edita
    /// directamente en los assets de Assets/Obstetricia/Data. DEBE ser validado por el equipo docente de Obstetricia.
    /// </summary>
    public static class ObstetriciaContent
    {
        public static void FillLegal(QuizData q)
        {
            q.moduleLabel = "MÓDULO EDUCATIVO";
            q.title = "LEY INTEGRAL DE LA MUJER";
            q.subtitle = "Conoce tus derechos. Decidir informada es tu poder.";
            q.iconId = "scale";
            q.pointsPerCorrect = 100;
            q.motto = "Aprende, decide y protege tus derechos.";
            q.mottoDetail = "Cada decisión te acerca a un parto respetado";
            q.questions = new[]
            {
                new QuizQuestion
                {
                    situation = "Durante tu trabajo de parto te proponen hacerte una episiotomía sin explicarte por qué ni pedirte tu consentimiento.",
                    question = "¿Cuál es la mejor respuesta según la ley?",
                    options = new[]
                    {
                        new QuizOption { title = "Puedo decidir sobre mi cuerpo.", description = "Tengo derecho a recibir información clara y a otorgar mi consentimiento para cualquier procedimiento." },
                        new QuizOption { title = "El personal de salud decide lo que es mejor para mí.", description = "Debo obedecer las indicaciones aunque no esté de acuerdo." },
                    },
                    correctIndex = 0,
                },
                new QuizQuestion
                {
                    situation = "Al ingresar a preparto te dicen que tu acompañante debe esperar afuera \"porque estorba\", sin darte ninguna razón clínica.",
                    question = "¿Qué puedes hacer?",
                    options = new[]
                    {
                        new QuizOption { title = "Solicitar estar acompañada.", description = "Puedo pedir que una persona de mi confianza me acompañe y que, si no es posible, me expliquen la razón clínica." },
                        new QuizOption { title = "Aceptar estar sola.", description = "El acompañamiento es un favor del equipo y no puedo pedirlo." },
                    },
                    correctIndex = 0,
                },
                new QuizQuestion
                {
                    situation = "Mientras expresas dolor, una persona del equipo te dice: \"No grites, si cuando lo hiciste no te quejabas\".",
                    question = "¿Cómo se debe entender esta situación?",
                    options = new[]
                    {
                        new QuizOption { title = "Es normal en un parto.", description = "Son comentarios sin importancia; debo aguantar en silencio." },
                        new QuizOption { title = "Es un trato humillante.", description = "Tengo derecho a un trato digno y respetuoso; puedo reclamar o denunciar esta conducta." },
                    },
                    correctIndex = 1,
                },
                new QuizQuestion
                {
                    situation = "Quieres moverte y usar el balón kinésico para aliviar el dolor, pero te ordenan permanecer acostada sin ninguna indicación clínica.",
                    question = "¿Cuál es la mejor respuesta?",
                    options = new[]
                    {
                        new QuizOption { title = "Pedir información y elegir mi posición.", description = "Si no hay contraindicación, puedo moverme y usar métodos no farmacológicos para el dolor." },
                        new QuizOption { title = "Quedarme acostada siempre.", description = "La posición la decide solo el equipo, aunque no haya razón clínica." },
                    },
                    correctIndex = 0,
                },
                new QuizQuestion
                {
                    situation = "Después del parto quieres reclamar por un trato que consideras violento.",
                    question = "¿Qué camino corresponde?",
                    options = new[]
                    {
                        new QuizOption { title = "Ya no se puede hacer nada.", description = "Una vez terminado el parto no existen vías para reclamar." },
                        new QuizOption { title = "Reclamar y denunciar.", description = "Puedo presentar un reclamo en la OIRS del establecimiento, acudir a la Superintendencia de Salud y denunciar." },
                    },
                    correctIndex = 1,
                },
            };
        }

        public static void FillEvaluation(QuizData q)
        {
            q.moduleLabel = "EVALUACIÓN";
            q.title = "FISIO-BALÓN";
            q.subtitle = "Comprueba lo aprendido en el programa.";
            q.iconId = "clipboard";
            q.pointsPerCorrect = 100;
            q.motto = "Responde y suma puntos.";
            q.mottoDetail = "Elige la alternativa correcta";
            q.questions = new[]
            {
                Q("¿Qué ángulo subpúbico es característico de la pelvis femenina?", "Ángulo subpúbico",
                    "Mayor a 90°", "Arco amplio que facilita el paso fetal.", "Menor a 70°", "Arco estrecho, típico de la pelvis masculina.", 0),
                Q("¿Qué hueso forma la pared posterior de la pelvis?", "Anatomía pélvica",
                    "Pubis", "Se ubica en la parte anterior.", "Sacro", "Se articula con ambos iliacos en las articulaciones sacroilíacas.", 1),
                Q("¿Cuál es un beneficio del uso del balón durante el trabajo de parto?", "Fisio-balón",
                    "Favorece la movilidad pélvica", "Permite posiciones verticales, balanceos y ayuda al alivio del dolor.",
                    "Reemplaza la monitorización", "Hace innecesario el control de la madre y el feto.", 0),
                Q("¿Cuál es un método NO farmacológico de alivio del dolor?", "Métodos no farmacológicos",
                    "Anestesia epidural", "Analgesia farmacológica neuroaxial.", "Masaje y calor local", "Junto con respiración, deambulación y balón.", 1),
                Q("¿Cuál es el primer movimiento cardinal del mecanismo del parto (presentación cefálica)?", "Mecanismos del parto",
                    "Encajamiento", "La cabeza fetal atraviesa el estrecho superior.", "Rotación externa", "Ocurre tras la salida de la cabeza.", 0),
            };
        }

        private static QuizQuestion Q(string question, string situation, string aTitle, string aDesc, string bTitle, string bDesc,
            int correct)
        {
            return new QuizQuestion
            {
                situation = situation,
                question = question,
                options = new[]
                {
                    new QuizOption { title = aTitle, description = aDesc },
                    new QuizOption { title = bTitle, description = bDesc },
                },
                correctIndex = correct,
            };
        }

        public static void FillFisio(FisioModuleData f)
        {
            f.programTitle = "PROGRAMA FISIO-BALÓN Y MÉTODOS NO FARMACOLÓGICOS DEL DOLOR";
            f.studentName = "Estudiante";
            f.sections = new[]
            {
                new FisioSection { menuLabel = "Anatomía", iconId = "cube", kind = FisioSectionKind.Anatomy, activity = "Anatomía pélvica" },
                new FisioSection { menuLabel = "Fisiología", iconId = "pulse", kind = FisioSectionKind.Video360,
                    video = new Video360Source { fileOrUrl = "Videos360/fisiologia.mp4" } },
                new FisioSection { menuLabel = "Trabajo de parto", iconId = "ball", kind = FisioSectionKind.Video360,
                    video = new Video360Source { fileOrUrl = "Videos360/trabajo_de_parto.mp4" } },
                new FisioSection { menuLabel = "Mecanismos del parto", iconId = "target", kind = FisioSectionKind.Mechanisms, activity = "Mecanismos del parto" },
                new FisioSection { menuLabel = "Evaluación", iconId = "clipboard", kind = FisioSectionKind.Evaluation },
            };
        }
    }
}
#endif
