import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// Standalone Candidate Portal running on port 3001
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 3001,
    proxy: {
      '/api': {
        target: 'http://localhost:5160',
        changeOrigin: true,
        secure: false,
      }
    }
  }
});
