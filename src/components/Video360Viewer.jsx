import React, { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { Play, Pause, RotateCcw, RotateCw, Glasses, Maximize, AlertTriangle } from 'lucide-react';

// =====================================================================
// VISOR DE VIDEO 360° (con opción de verlo con un visor VR)
// =====================================================================
// - En el computador o celular: el video se ve dentro de una esfera; se mira arrastrando (o con el dedo)
//   y la rueda acerca/aleja.
// - "Ver con visor VR": aparece solo si el navegador soporta WebXR (p. ej. el navegador de las Meta Quest).
//   Dentro del visor, el video rodea al usuario y el gatillo del control pausa/reanuda.
// - Guía: puntos con tiempo y texto para guiar la práctica; un punto con "pausa" detiene el video
//   para que el docente explique o pregunte.
//
// El video debe ser un .mp4 equirectangular (2:1) con enlace DIRECTO y permiso CORS
// (p. ej. Firebase Storage con CORS configurado). Un enlace de Google Drive o de YouTube no sirve aquí.
// =====================================================================

/**
 * Convierte el texto de la guía en una lista ordenada de puntos.
 * Una línea por punto: "minuto:segundo | texto | pausa"  (el "| pausa" es opcional).
 *   0:00 | Observa la postura sobre el balón
 *   1:25 | ¿Qué músculo se está activando? | pausa
 */
const parseGuia = (texto) =>
  (texto || '')
    .split('\n')
    .map((linea) => linea.split('|').map((p) => p.trim()))
    .filter(([tiempo, contenido]) => tiempo && contenido && /^\d+(:\d{1,2})?$/.test(tiempo))
    .map(([tiempo, contenido, marca]) => {
      const [a, b] = tiempo.split(':').map(Number);
      return { t: b === undefined ? a : a * 60 + b, texto: contenido, pausa: /paus/i.test(marca || '') };
    })
    .sort((x, y) => x.t - y.t);

const reloj = (s) => {
  const n = Math.max(0, Math.floor(s || 0));
  return `${Math.floor(n / 60)}:${String(n % 60).padStart(2, '0')}`;
};

// Índice del último punto de la guía cuyo tiempo ya pasó (-1 si ninguno).
const indiceGuia = (guia, t) => {
  let i = -1;
  guia.forEach((p, k) => {
    if (p.t <= t) i = k;
  });
  return i;
};

// Panel con el texto de la guía para mostrarlo dentro del visor VR (el HTML no se ve en VR).
const crearPanelVR = () => {
  const canvas = document.createElement('canvas');
  canvas.width = 1024;
  canvas.height = 256;
  const textura = new THREE.CanvasTexture(canvas);
  textura.colorSpace = THREE.SRGBColorSpace;
  const panel = new THREE.Mesh(
    new THREE.PlaneGeometry(1.6, 0.4),
    new THREE.MeshBasicMaterial({ map: textura, transparent: true, depthTest: false }),
  );
  panel.renderOrder = 10;
  panel.visible = false;
  const dibujar = (texto) => {
    const ctx = canvas.getContext('2d');
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    if (!texto) {
      panel.visible = false;
      return;
    }
    ctx.fillStyle = 'rgba(20, 8, 40, 0.82)';
    ctx.beginPath();
    ctx.roundRect(8, 8, canvas.width - 16, canvas.height - 16, 40);
    ctx.fill();
    ctx.fillStyle = '#ffffff';
    ctx.font = '600 44px system-ui, sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    // Ajuste simple en hasta 3 líneas.
    const palabras = texto.split(' ');
    const lineas = [];
    let actual = '';
    palabras.forEach((p) => {
      const prueba = actual ? `${actual} ${p}` : p;
      if (ctx.measureText(prueba).width > canvas.width - 90 && actual) {
        lineas.push(actual);
        actual = p;
      } else actual = prueba;
    });
    if (actual) lineas.push(actual);
    const visibles = lineas.slice(0, 3);
    visibles.forEach((l, i) => ctx.fillText(l, canvas.width / 2, canvas.height / 2 + (i - (visibles.length - 1) / 2) * 56));
    textura.needsUpdate = true;
    panel.visible = true;
  };
  return { panel, dibujar };
};

// `modoVR`: en vez de "Reproducir" muestra "Entrar al visor VR" (la sesión VR debe abrirse con un clic del usuario).
const Video360Viewer = ({ url, guiaTexto, title, modoVR = false }) => {
  const contenedorRef = useRef(null);
  const lienzoRef = useRef(null);
  const videoRef = useRef(null);
  const motorRef = useRef(null); // { renderer, session, ... }
  const guiaRef = useRef([]);
  const guiaActualRef = useRef(-1);

  const [iniciado, setIniciado] = useState(false);
  const [reproduciendo, setReproduciendo] = useState(false);
  const [tiempo, setTiempo] = useState(0);
  const [duracion, setDuracion] = useState(0);
  const [textoGuia, setTextoGuia] = useState('');
  const [vrDisponible, setVrDisponible] = useState(false);
  const [enVR, setEnVR] = useState(false);
  const [error, setError] = useState('');

  // La guía se vuelve a leer si el docente la edita.
  useEffect(() => {
    guiaRef.current = parseGuia(guiaTexto);
    guiaActualRef.current = -1;
  }, [guiaTexto]);

  // ¿El navegador permite abrir el video en un visor VR (WebXR)?
  useEffect(() => {
    let vivo = true;
    navigator.xr?.isSessionSupported?.('immersive-vr').then((ok) => vivo && setVrDisponible(ok)).catch(() => {});
    return () => {
      vivo = false;
    };
  }, []);

  // Escena: esfera con el video por dentro, cámara al centro.
  useEffect(() => {
    const contenedor = lienzoRef.current;
    if (!contenedor) return undefined;

    const video = document.createElement('video');
    video.crossOrigin = 'anonymous';
    video.playsInline = true;
    video.preload = 'metadata';
    video.src = url;
    videoRef.current = video;

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.xr.enabled = true;
    renderer.xr.setReferenceSpaceType('local');
    contenedor.appendChild(renderer.domElement);
    renderer.domElement.style.width = '100%';
    renderer.domElement.style.height = '100%';
    renderer.domElement.style.touchAction = 'none';

    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(75, 16 / 9, 0.1, 1100);
    const textura = new THREE.VideoTexture(video);
    textura.colorSpace = THREE.SRGBColorSpace;
    const geometria = new THREE.SphereGeometry(500, 64, 32);
    geometria.scale(-1, 1, 1); // se ve por dentro
    const esfera = new THREE.Mesh(geometria, new THREE.MeshBasicMaterial({ map: textura }));
    scene.add(esfera);

    const { panel, dibujar } = crearPanelVR();
    scene.add(panel);

    // Gatillo de cualquier control: pausa / reanuda (dentro del visor).
    const alternar = () => (video.paused ? video.play().catch(() => {}) : video.pause());
    [0, 1].forEach((i) => renderer.xr.getController(i).addEventListener('select', alternar));

    // Mirar arrastrando (fuera del visor).
    let lon = 0;
    let lat = 0;
    let arrastre = null;
    const alPresionar = (e) => {
      arrastre = { x: e.clientX, y: e.clientY, lon, lat };
      renderer.domElement.setPointerCapture(e.pointerId);
    };
    const alMover = (e) => {
      if (!arrastre) return;
      lon = arrastre.lon - (e.clientX - arrastre.x) * 0.15;
      lat = Math.max(-85, Math.min(85, arrastre.lat + (e.clientY - arrastre.y) * 0.15));
    };
    const alSoltar = () => {
      arrastre = null;
    };
    const alRueda = (e) => {
      e.preventDefault();
      camera.fov = Math.max(35, Math.min(95, camera.fov + e.deltaY * 0.04));
      camera.updateProjectionMatrix();
    };
    renderer.domElement.addEventListener('pointerdown', alPresionar);
    renderer.domElement.addEventListener('pointermove', alMover);
    renderer.domElement.addEventListener('pointerup', alSoltar);
    renderer.domElement.addEventListener('pointercancel', alSoltar);
    renderer.domElement.addEventListener('wheel', alRueda, { passive: false });

    const ajustar = () => {
      const { width, height } = contenedor.getBoundingClientRect();
      if (!width || !height || renderer.xr.isPresenting) return;
      renderer.setSize(width, height, false);
      camera.aspect = width / height;
      camera.updateProjectionMatrix();
    };
    const observador = new ResizeObserver(ajustar);
    observador.observe(contenedor);
    ajustar();

    const objetivo = new THREE.Vector3();
    const adelante = new THREE.Vector3();
    renderer.setAnimationLoop(() => {
      if (!renderer.xr.isPresenting) {
        const phi = THREE.MathUtils.degToRad(90 - lat);
        const theta = THREE.MathUtils.degToRad(lon);
        objetivo.setFromSphericalCoords(1, phi, theta);
        camera.lookAt(objetivo);
      } else {
        // En el visor, el texto de la guía flota frente a la vista, un poco abajo.
        const xrCam = renderer.xr.getCamera();
        xrCam.getWorldDirection(adelante);
        adelante.y = 0;
        adelante.normalize();
        panel.position.copy(xrCam.position).addScaledVector(adelante, 1.8);
        panel.position.y -= 0.45;
        panel.lookAt(xrCam.position.x, panel.position.y, xrCam.position.z);
      }

      // Guía: al llegar a un punto nuevo se muestra su texto y, si corresponde, se pausa.
      const guia = guiaRef.current;
      const i = indiceGuia(guia, video.currentTime);
      if (i !== guiaActualRef.current) {
        const avanzando = i > guiaActualRef.current;
        guiaActualRef.current = i;
        const texto = i >= 0 ? guia[i].texto : '';
        setTextoGuia(texto);
        dibujar(renderer.xr.isPresenting ? texto : '');
        if (avanzando && i >= 0 && guia[i].pausa && !video.paused) video.pause();
      }
      renderer.render(scene, camera);
    });

    const alActualizar = () => setTiempo(video.currentTime);
    const alMetadatos = () => setDuracion(video.duration || 0);
    const alReproducir = () => setReproduciendo(true);
    const alPausar = () => setReproduciendo(false);
    const alError = () =>
      setError('No se pudo cargar el video 360°. El enlace debe ser un .mp4 directo con permiso CORS (no Drive ni YouTube).');
    video.addEventListener('timeupdate', alActualizar);
    video.addEventListener('loadedmetadata', alMetadatos);
    video.addEventListener('play', alReproducir);
    video.addEventListener('pause', alPausar);
    video.addEventListener('error', alError);

    motorRef.current = { renderer, dibujar };

    return () => {
      renderer.setAnimationLoop(null);
      renderer.xr.getSession()?.end().catch(() => {});
      observador.disconnect();
      video.pause();
      video.removeAttribute('src');
      video.load();
      textura.dispose();
      geometria.dispose();
      esfera.material.dispose();
      panel.geometry.dispose();
      panel.material.map.dispose();
      panel.material.dispose();
      renderer.dispose();
      renderer.domElement.remove();
      motorRef.current = null;
    };
  }, [url]);

  const reproducir = () => {
    setIniciado(true);
    videoRef.current?.play().catch(() => setError('El navegador no permitió reproducir el video. Intenta de nuevo.'));
  };
  const alternarPausa = () => {
    const v = videoRef.current;
    if (!v) return;
    if (v.paused) reproducir();
    else v.pause();
  };
  const saltar = (segundos) => {
    const v = videoRef.current;
    if (!v) return;
    v.currentTime = Math.max(0, Math.min((v.duration || 0) - 0.5, v.currentTime + segundos));
    // Al saltar no se vuelve a pausar en el punto donde se cae.
    guiaActualRef.current = indiceGuia(guiaRef.current, v.currentTime);
    setTextoGuia(guiaActualRef.current >= 0 ? guiaRef.current[guiaActualRef.current].texto : '');
  };
  const buscar = (e) => saltar(Number(e.target.value) - (videoRef.current?.currentTime || 0));

  const verEnVR = async () => {
    const motor = motorRef.current;
    if (!motor || !navigator.xr) return;
    try {
      const session = await navigator.xr.requestSession('immersive-vr', { optionalFeatures: ['local-floor'] });
      await motor.renderer.xr.setSession(session);
      setEnVR(true);
      motor.dibujar(textoGuia);
      session.addEventListener('end', () => {
        setEnVR(false);
        motor.dibujar('');
      });
      reproducir();
    } catch {
      setError('No se pudo abrir el visor VR. Abre esta página desde el navegador de las Meta Quest.');
    }
  };

  const pantallaCompleta = () => contenedorRef.current?.requestFullscreen?.().catch(() => {});

  return (
    <div ref={contenedorRef} className="mb-4 rounded-lg overflow-hidden bg-black">
      <div className="relative aspect-video">
        <div ref={lienzoRef} className="absolute inset-0 cursor-grab active:cursor-grabbing" />
        {!iniciado && !error && modoVR && (
          <div className="absolute inset-0 flex flex-col items-center justify-center gap-3 bg-black/60 text-white text-center px-6">
            {vrDisponible ? (
              <>
                <button
                  onClick={() => {
                    setIniciado(true);
                    verEnVR();
                  }}
                  className="flex items-center gap-2 bg-primary hover:bg-primary-dark px-6 py-3 rounded-full font-semibold"
                >
                  <Glasses size={22} /> Entrar al visor VR
                </button>
                <span className="text-xs text-white/75">Dentro del visor, el gatillo del control pausa y reanuda el video.</span>
              </>
            ) : (
              <>
                <Glasses size={32} className="text-primary-light" />
                <span className="text-sm max-w-md">
                  No se detecta un visor VR. Conecta las <b>Meta Quest</b> al computador con <b>Quest Link</b> y abre esta
                  página en <b>Chrome</b> o <b>Edge</b> (o ábrela desde el navegador de las Quest), y vuelve a elegir
                  «Ver en VR».
                </span>
                <button onClick={reproducir} className="text-xs underline text-white/80 hover:text-white">
                  Mientras tanto, verlo en la pantalla
                </button>
              </>
            )}
          </div>
        )}
        {!iniciado && !error && !modoVR && (
          <button
            onClick={reproducir}
            className="absolute inset-0 flex flex-col items-center justify-center gap-2 bg-black/50 text-white hover:bg-black/40 transition-colors"
          >
            <span className="w-16 h-16 rounded-full bg-primary flex items-center justify-center">
              <Play size={30} className="ml-1" />
            </span>
            <span className="text-sm font-medium">Reproducir video 360°{title ? `: ${title}` : ''}</span>
            <span className="text-xs text-white/70">Arrastra para mirar alrededor · rueda para acercar</span>
          </button>
        )}
        {textoGuia && iniciado && (
          <div className="absolute inset-x-4 bottom-4 pointer-events-none flex justify-center">
            <p className="bg-[#14082a]/85 text-white text-sm md:text-base px-4 py-2 rounded-lg text-center max-w-xl">{textoGuia}</p>
          </div>
        )}
        {enVR && (
          <div className="absolute inset-0 flex items-center justify-center bg-black/70 text-white text-sm">
            Reproduciendo en el visor VR · gatillo para pausar
          </div>
        )}
        {error && (
          <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 bg-black/80 text-white text-sm text-center px-6">
            <AlertTriangle className="text-amber-400" />
            {error}
          </div>
        )}
      </div>

      <div className="flex flex-wrap items-center gap-2 px-3 py-2 bg-[#1b1028] text-white">
        <button onClick={() => saltar(-10)} aria-label="Retroceder 10 segundos" className="p-1.5 rounded hover:bg-white/10">
          <RotateCcw size={18} />
        </button>
        <button onClick={alternarPausa} aria-label={reproduciendo ? 'Pausar' : 'Reproducir'} className="p-1.5 rounded hover:bg-white/10">
          {reproduciendo ? <Pause size={18} /> : <Play size={18} />}
        </button>
        <button onClick={() => saltar(10)} aria-label="Adelantar 10 segundos" className="p-1.5 rounded hover:bg-white/10">
          <RotateCw size={18} />
        </button>
        <input
          type="range"
          min={0}
          max={duracion || 0}
          step={0.1}
          value={Math.min(tiempo, duracion || 0)}
          onChange={buscar}
          aria-label="Avance del video"
          className="flex-1 min-w-24 accent-primary"
        />
        <span className="text-xs tabular-nums text-white/80">
          {reloj(tiempo)} / {reloj(duracion)}
        </span>
        {vrDisponible && (
          <button
            onClick={verEnVR}
            className="flex items-center gap-1.5 text-xs font-semibold bg-primary hover:bg-primary-dark px-3 py-1.5 rounded-md"
          >
            <Glasses size={16} /> Ver con visor VR
          </button>
        )}
        <button onClick={pantallaCompleta} aria-label="Pantalla completa" className="p-1.5 rounded hover:bg-white/10">
          <Maximize size={18} />
        </button>
      </div>
    </div>
  );
};

export default Video360Viewer;
