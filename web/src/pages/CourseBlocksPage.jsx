import React, { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, Plus, Glasses } from 'lucide-react';
import { useCourse } from '../hooks/useFirestore';
import { useAuth } from '../context/AuthContext';
import { updateCourse } from '../services/db';
import ContentBlock from '../components/ContentBlock';
import BlockFormModal from '../components/BlockFormModal';
import Practica360 from '../components/Practica360';
import { videosIncluidos } from '../data/videos360';

// =====================================================================
// APARTADOS "TEÓRICO" y "PRÁCTICO"
// =====================================================================
// Una sola página sirve para los dos (App.jsx le pasa `section`):
//   section="teoria"   -> guarda en  courses/{id}.teoria
//   section="practica" -> guarda en  courses/{id}.practica  (se muestra como galería de videos 360°)
// Todos pueden LEER. Solo profesor/admin ven los botones de agregar, editar y borrar.
// =====================================================================

// Campos del formulario de cada apartado. Para agregar un campo nuevo, añade una línea aquí.
const FIELDS = {
  teoria: [
    { name: 'title', label: 'Título', type: 'text', required: true },
    { name: 'text', label: 'Contenido', type: 'textarea' },
    { name: 'imageUrl', label: 'Imagen (enlace URL, opcional)', type: 'url' },
    { name: 'videoUrl', label: 'Video de YouTube (opcional)', type: 'url' },
  ],
  // Práctico = videos 360° para practicar con el visor puesto (ver components/Practica360.jsx).
  practica: [
    { name: 'title', label: 'Título del video', type: 'text', required: true },
    { name: 'text', label: 'Qué se practica (descripción corta)', type: 'textarea' },
    { name: 'videoUrl', label: 'Video 360° de YouTube', type: 'url' },
    {
      name: 'video360Url',
      label: 'O enlace directo al .mp4 360° (opcional): permite verlo en VR dentro de la página, con guía',
      type: 'url',
    },
    {
      name: 'guia360',
      label: 'Guía del video (opcional, solo con .mp4). Una línea por punto: minuto:segundo | texto | pausa',
      type: 'textarea',
    },
  ],
};

const tieneVideo = (b) => Boolean(b.videoUrl || b.video360Url);

const CourseBlocksPage = ({ section, title, emptyText }) => {
  const { id } = useParams();
  const { data: course, loading } = useCourse(id);
  const { isStaff } = useAuth();
  // `modal` = null (cerrado) | { block } (editar uno existente) | {} (crear nuevo)
  const [modal, setModal] = useState(null);

  if (loading) return <div className="text-center p-10">Cargando...</div>;
  if (!course) return <div className="text-center p-10">Curso no encontrado.</div>;

  const blocks = course[section] || [];
  // Práctico: los videos incluidos en la página (data/videos360.js) + los que agregan los profesores.
  const videosPractica =
    section === 'practica' ? [...videosIncluidos(id).map((v) => ({ ...v, incluido: true })), ...blocks.filter(tieneVideo)] : [];

  // Guarda la lista COMPLETA de bloques del apartado en el curso.
  const saveBlocks = (next) => updateCourse(id, { [section]: next });

  const handleSave = (values) => {
    if (modal.block) return saveBlocks(blocks.map((b) => (b.id === modal.block.id ? { ...b, ...values } : b)));
    return saveBlocks([...blocks, { id: crypto.randomUUID(), ...values }]);
  };

  const handleDelete = (block) => {
    if (window.confirm(`¿Eliminar "${block.title}"?`)) saveBlocks(blocks.filter((b) => b.id !== block.id));
  };

  return (
    <div className="min-h-screen bg-[#faf8fb] pb-20">
      <div className="max-w-3xl mx-auto px-4 py-12">
        <Link to={`/curso/${id}`} className="inline-flex items-center gap-1.5 text-sm text-primary hover:underline mb-6">
          <ArrowLeft size={16} /> Volver al curso
        </Link>

        <p className="text-sm text-gray-500 uppercase tracking-wider">{course.title}</p>
        <div className="flex flex-wrap items-center justify-between gap-4 mb-2">
          <h1 className="text-3xl md:text-4xl font-display font-bold text-[#a236df] uppercase">{title}</h1>
          {isStaff && (
            <button
              onClick={() => setModal({})}
              className="flex items-center gap-2 bg-primary hover:bg-primary-dark text-white px-4 py-2 rounded-md transition-colors"
            >
              <Plus size={18} /> {section === 'practica' ? 'Agregar video 360°' : 'Agregar bloque'}
            </button>
          )}
        </div>
        <div className="w-20 h-1 bg-primary mb-8 rounded-full"></div>

        {section === 'practica' ? (
          <>
            {/* PRÁCTICO: simulación con videos 360°. Se mira el video con el visor y se hace lo mismo en la vida real. */}
            <div className="flex items-start gap-3 bg-white border border-primary/20 rounded-xl p-4 mb-6 text-sm text-gray-600">
              <Glasses size={22} className="text-primary shrink-0 mt-0.5" />
              <p>
                <b className="text-primary-dark">Práctica con simulación en video 360°.</b> Elige un video y ponte el visor
                VR: quedarás dentro de la escena para hacer en la vida real lo que muestra el video. También puedes verlo en
                la pantalla, arrastrando para mirar alrededor.
              </p>
            </div>
            {videosPractica.length === 0 ? (
              <p className="text-gray-500 bg-white rounded-xl border border-gray-100 p-8 text-center">{emptyText}</p>
            ) : (
              <Practica360
                videos={videosPractica}
                onEdit={isStaff ? (block) => setModal({ block }) : undefined}
                onDelete={isStaff ? handleDelete : undefined}
              />
            )}
          </>
        ) : blocks.length === 0 ? (
          <p className="text-gray-500 bg-white rounded-xl border border-gray-100 p-8 text-center">{emptyText}</p>
        ) : (
          <div className="space-y-6">
            {blocks.map((block) => (
              <ContentBlock
                key={block.id}
                block={block}
                onEdit={isStaff ? () => setModal({ block }) : undefined}
                onDelete={isStaff ? () => handleDelete(block) : undefined}
              />
            ))}
          </div>
        )}
      </div>

      {modal && (
        <BlockFormModal
          heading={modal.block ? `Editar bloque (${title})` : `Nuevo bloque (${title})`}
          fields={FIELDS[section]}
          initial={modal.block}
          onSave={handleSave}
          onClose={() => setModal(null)}
        />
      )}
    </div>
  );
};

export default CourseBlocksPage;
