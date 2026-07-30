import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    allowedHosts: [".trycloudflare.com", "localhost", "127.0.0.1"],
    proxy: {
      "/api": {
        target: process.env.VITE_DEV_API_URL ?? "http://localhost:8080",
        changeOrigin: true
      },
      "/health": {
        target: process.env.VITE_DEV_API_URL ?? "http://localhost:8080",
        changeOrigin: true
      }
    }
  }
});
