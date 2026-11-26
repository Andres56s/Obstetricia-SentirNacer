# Sentir Nacer — Plataforma educativa de Obstetricia

React + Vite + Tailwind, con **Firebase** (Authentication + Cloud Firestore) y tres roles: alumno, profesor y administrador.

## Inicio rápido

```bash
npm install
cp .env.example .env.local   # y pega tus claves de Firebase
npm run dev
```

La configuración completa de Firebase, el mapa del código y la **Guía de Personalización**
están en [GUIA_PERSONALIZACION.md](GUIA_PERSONALIZACION.md).
Las reglas de seguridad de la base de datos están en [firestore.rules](firestore.rules).

La **realidad inmersiva** (VR con Meta Quest o modo escritorio en el navegador) viene del proyecto Unity
[lNubet/Obstetricia-VR](https://github.com/lNubet/Obstetricia-VR) (rama `modo-escritorio-web`; en el proyecto
unificado es la carpeta `../unity`). Su versión web se genera desde Unity con
**Tools > Obstetricia > Construir versión Web (WebGL)** y queda en `public/unity/` (ver
[public/unity/README.md](public/unity/README.md)). Este repo debe estar en una carpeta llamada `web`, al lado del
proyecto Unity.
