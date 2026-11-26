import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // public/unity contiene la build WebGL de Unity (archivos grandes que Unity reescribe al recompilar).
    // Sin esto, el servidor de desarrollo se cae con EBUSY mientras Unity genera la build.
    watch: { ignored: ['**/public/unity/**'] },
  },
})
