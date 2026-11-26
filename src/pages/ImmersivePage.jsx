import React, { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, Glasses, Monitor, Download, Keyboard, Mouse, MousePointerClick, Move, RotateCcw } from 'lucide-react';
import { useCourse } from '../hooks/useFirestore';
import { useAuth } from '../context/AuthContext';
import ImmersivePlayer from '../components/ImmersivePlayer';
import { QUEST_APP_URL, immersiveModuleLabel } from '../constants';

// =====================================================================
// APARTADO "REALIDAD INMERSIVA" de un curso  (/curso/:id/inmersivo)
// =====================================================================
// Dos formas de vivir la misma experiencia (proyecto Unity de la carpeta ../unity):
//   1) Con visor VR (Meta Quest 3 / 3S): app instalada en el visor (realidad mixta).
//   2) Sin visor (modo escritorio "tipo juego"): en el navegador, con teclado y mouse.
// Para cambiar los textos de las instrucciones, edita las listas de abajo.
// =====================================================================

const VR_STEPS = [
  'Ponte el visor Meta Quest 3 / 3S en un espacio despejado.',
  'Abre la app «Obstetricia UCEN» (Biblioteca > Orígenes desconocidos).',
  'Sigue el tutorial y elige el programa sobre la mesa UCEN.',
  'Tus resultados quedan registrados en el visor; tu docente puede descargarlos.',
];

const DESKTOP_CONTROLS = [
  { Icon: Move, keys: 'W A S D / flechas', text: 'Moverse (Shift para correr)' },
  { Icon: MousePointerClick, keys: 'Mouse', text: 'Mirar alrededor (Esc suelta el cursor)' },
  { Icon: MousePointerClick, keys: 'Clic izquierdo', text: 'Presionar botones · mantener para tomar objetos' },
  { Icon: Mouse, keys: 'Rueda / Tab + mouse', text: 'Acercar o rotar el objeto que tomaste' },
  { Icon: Keyboard, keys: 'H', text: 'Devolver el objeto a su lugar' },
  { Icon: Keyboard, keys: 'E', text: 'Abrir o cerrar el menú de la sala virtual' },
  { Icon: RotateCcw, keys: 'I J K L', text: 'Girar la pelvis 3D' },
  { Icon: Keyboard, keys: 'R (mantener)', text: 'Traer la mesa frente a ti' },
];

const ImmersivePage = () => {
  const { id } = useParams();
  const { data: course, loading } = useCourse(id);
  const { user, isStaff } = useAuth();
  const [playing, setPlaying] = useState(false);

  if (loading) return <div className="text-center p-10">Cargando...</div>;
  if (!course) return <div className="text-center p-10">Curso no encontrado.</div>;

  if (!course.isImmersive) {
    return (
      <div className="max-w-xl mx-auto text-center py-24 px-4">
        <p className="text-gray-600 mb-6">Este curso no tiene una experiencia de realidad inmersiva.</p>
        <Link to={`/curso/${id}`} className="text-primary font-medium hover:underline">Volver al curso</Link>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-[#faf8fb] pb-20">
      <div className="max-w-5xl mx-auto px-4 py-12">
        <Link to={`/curso/${id}`} className="inline-flex items-center gap-1.5 text-sm text-primary hover:underline mb-6">
          <ArrowLeft size={16} /> Volver al curso
        </Link>

        <p className="text-sm text-gray-500 uppercase tracking-wider">{course.title}</p>
        <h1 className="text-3xl md:text-4xl font-display font-bold text-[#a236df] uppercase mb-2">Realidad inmersiva</h1>
        <div className="w-20 h-1 bg-primary mb-3 rounded-full"></div>
        <p className="text-gray-600 mb-8">
          Experiencia: <strong>{immersiveModuleLabel(course.immersiveModule)}</strong>. Elige cómo quieres vivirla.
        </p>

        {!playing && (
          <div className="grid md:grid-cols-2 gap-6">
            {/* Opción 1: con visor */}
            <section className="bg-white rounded-xl shadow-sm border border-gray-100 p-6 flex flex-col">
              <div className="flex items-center gap-3 mb-4">
                <div className="bg-fuchsia-50 text-primary rounded-full p-3"><Glasses size={26} /></div>
                <div>
                  <h2 className="text-xl font-semibold text-primary-dark">Con visor VR</h2>
                  <p className="text-xs text-gray-500">Meta Quest 3 / 3S · realidad mixta</p>
                </div>
              </div>
              <ol className="list-decimal list-inside space-y-2 text-sm text-gray-700 flex-grow">
                {VR_STEPS.map((s) => <li key={s}>{s}</li>)}
              </ol>
              {QUEST_APP_URL ? (
                <a
                  href={QUEST_APP_URL}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="mt-6 inline-flex items-center justify-center gap-2 border border-primary text-primary hover:bg-fuchsia-50 px-4 py-2.5 rounded-md transition-colors"
                >
                  <Download size={18} /> Descargar app para el visor (APK)
                </a>
              ) : (
                <p className="mt-6 text-xs text-gray-400">
                  ¿No tienes la app en el visor? Pídesela a tu docente.
                </p>
              )}
            </section>

            {/* Opción 2: sin visor */}
            <section className="bg-white rounded-xl shadow-sm border border-gray-100 p-6 flex flex-col">
              <div className="flex items-center gap-3 mb-4">
                <div className="bg-fuchsia-50 text-primary rounded-full p-3"><Monitor size={26} /></div>
                <div>
                  <h2 className="text-xl font-semibold text-primary-dark">Sin visor (modo escritorio)</h2>
                  <p className="text-xs text-gray-500">En el navegador · teclado y mouse</p>
                </div>
              </div>
              <ul className="space-y-2 text-sm text-gray-700 flex-grow">
                {DESKTOP_CONTROLS.map(({ Icon, keys, text }) => (
                  <li key={keys} className="flex items-start gap-2">
                    <Icon size={16} className="text-primary mt-0.5 shrink-0" />
                    <span><strong>{keys}</strong> — {text}</span>
                  </li>
                ))}
              </ul>
              <button
                onClick={() => setPlaying(true)}
                className="mt-6 inline-flex items-center justify-center gap-2 bg-primary hover:bg-primary-dark text-white px-4 py-2.5 rounded-md transition-colors"
              >
                <Monitor size={18} /> Comenzar en el navegador
              </button>
              <p className="mt-2 text-xs text-gray-400">Recomendado en computador (no en celular).</p>
            </section>
          </div>
        )}

        {playing && (
          <>
            <ImmersivePlayer course={course} user={user} isStaff={isStaff} />
            <button onClick={() => setPlaying(false)} className="mt-6 text-sm text-gray-500 hover:text-primary hover:underline">
              Salir de la experiencia
            </button>
          </>
        )}
      </div>
    </div>
  );
};

export default ImmersivePage;
