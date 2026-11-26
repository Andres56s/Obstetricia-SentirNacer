# Sentir Nacer · Obstetricia con Realidad Inmersiva (UCEN)

Proyecto unificado de la plataforma educativa **Sentir Nacer** y su experiencia de **realidad inmersiva**.

```
Obstetricia-SentirNacer/
├─ web/     Página web (React + Vite + Tailwind + Firebase)
└─ unity/   Experiencia inmersiva (Unity 6000.3.27f1): VR para Meta Quest + modo escritorio (WebGL)
```

Este es **el repositorio principal de trabajo**: reúne el proyecto completo (las dos partes con todo su historial)
en un solo lugar. Se clona, se trabaja y se sube todo desde aquí (rama `main`).
Los modelos, texturas y audio de `unity/` se guardan con **Git LFS** (`git lfs install` antes de clonar).

Para armarlo en otro computador y el trabajo del día a día: [web/CONFIGURAR_OTRO_PC.md](web/CONFIGURAR_OTRO_PC.md).
`unity/` también se comparte con el compañero en <https://github.com/lNubet/Obstetricia-VR> (rama `modo-escritorio-web`).

## Dos formas de vivir la experiencia inmersiva

Cada curso marcado como **Realidad Inmersiva** tiene el botón **«Realidad inmersiva»**, que lleva a
`/curso/:id/inmersivo`. Desde ahí, el alumno elige:

| | Con visor VR | Sin visor (modo escritorio) |
|---|---|---|
| Dispositivo | Meta Quest 3 / 3S | Computador con teclado y mouse |
| Dónde corre | App Android instalada en el visor (realidad mixta) | En el navegador, dentro de la página (Unity WebGL) |
| Cómo se interactúa | Manos o controles | Mouse para mirar, WASD para moverse, clic para tocar y tomar objetos (ayuda con C) |
| Balón kinésico | Detectado con la cámara del visor (IA) | Simulado (la pelvis aparece directamente) |
| Resultados | `resultados_obstetricia.csv` en el visor | Firestore (`immersiveResults`), visibles en Panel > Resultados |

Es **el mismo proyecto Unity**: el código detecta si hay visor (`PlatformMode.IsDesktop`) y, si no lo
hay, activa el modo escritorio (`unity/Assets/Scripts/Obstetricia/Desktop/`).

## Puesta en marcha

Instalación, clonado, claves de Firebase, versión web y trabajo diario: [web/CONFIGURAR_OTRO_PC.md](web/CONFIGURAR_OTRO_PC.md).
Resumen:

- **Página:** `cd web`, `npm install`, `npm run dev` (necesita `web/.env.local` con las claves de Firebase).
- **Versión VR (Quest):** abre `unity/` con Unity Hub (6000.3.27f1) > File > Build Profiles > Android > Build.
- **Versión web (sin visor):** menú **Tools > Obstetricia > Construir versión Web (WebGL)**: la build queda en
  `web/public/unity/` y la página la muestra. Después revisa `git -C unity status`: Unity borra unos ajustes de
  XR al compilar para web y hay que descartarlos (ver la guía).
- **Modo escritorio en el Editor:** **Tools > Obstetricia > Modo escritorio en el Editor** y Play (también se
  activa solo si das Play sin visor conectado).

## Cómo se comunican la web y Unity

```
Página (ImmersivePlayer.jsx)                          Unity WebGL (iframe)
  /unity/index.html?modulo=fisio&alumno=Ana   ──────▶  WebBridge.RequestedModule / StudentName
                                                       abre el programa pedido
  window "message" (mismo origen)             ◀──────  WebBridge.SendResult(...) al terminar una evaluación
  saveImmersiveResult() → Firestore
```

- Programa que abre cada curso: Panel > Cursos > Editar > «Programa de la experiencia inmersiva»
  (`immersiveModule`: vacío = mesa UCEN, `fisio`, `legal`, `sala`).
- Código web: `web/src/pages/ImmersivePage.jsx`, `web/src/components/ImmersivePlayer.jsx`.
- Código Unity: `unity/Assets/Scripts/Obstetricia/Desktop/` (`PlatformMode`, `DesktopRig`, `WebBridge`),
  `unity/Assets/Plugins/WebGL/ObstetriciaWebBridge.jslib`, `unity/Assets/WebGLTemplates/SentirNacer/`.

## Pendiente

- Probar la rama `modo-escritorio-web` en las Quest (el compañero) y juntarla con su `main`.
- Publicar `web/firestore.rules` (colección `immersiveResults`) en Firebase cuando el equipo lo decida.
- Videos 360° de `unity/Assets/StreamingAssets/Videos360/`: no están en el repo (los tiene el compañero).
