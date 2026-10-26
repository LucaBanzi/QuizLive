import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// In sviluppo: `npm run dev` + backend su http://localhost:8080 (dotnet run con ASPNETCORE_URLS)
const target = process.env.VITE_BACKEND ?? 'http://localhost:8080';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target, changeOrigin: true },
      '/media': { target, changeOrigin: true },
      '/healthz': { target, changeOrigin: true },
      '/hub': { target, changeOrigin: true, ws: true },
    },
  },
});
