# Versión web de la experiencia inmersiva (Unity WebGL)

Esta carpeta la llena Unity con **Tools > Obstetricia > Construir versión Web (WebGL)** (el proyecto Unity, que va al lado de la carpeta `web`: `Obstetricia-VR`, o `unity` en el proyecto unificado).
Contendrá `index.html`, `Build/`, `TemplateData/` y, si los hay, `StreamingAssets/` (videos 360°).

La página la muestra en `/curso/:id/inmersivo` → «Sin visor (modo escritorio)».

Los archivos generados **no se suben a git** (son pesados y se regeneran). Para publicar la página,
genera la build WebGL y luego ejecuta `npm run build` en `web/`: todo queda dentro de `web/dist/`.
