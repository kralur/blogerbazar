import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5175,
    strictPort: true,
    // Same path as in production, where the site's nginx passes it to the API.
    proxy: { "/api/public": { target: process.env.VITE_DEV_API_URL ?? "http://localhost:8080", changeOrigin: true } }
  },
  test: {
    environment: "jsdom",
    setupFiles: "./tests/setup.ts",
    include: ["tests/**/*.test.ts", "tests/**/*.test.tsx"]
  }
});
