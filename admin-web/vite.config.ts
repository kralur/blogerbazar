import react from "@vitejs/plugin-react";
import { fileURLToPath } from "node:url";
import type { Connect, Plugin } from "vite";
import { defineConfig } from "vitest/config";

// The sign-in page is its own document (login.html): the Telegram widget script runs only there, never next to
// an open session. In production nginx serves it at /login; this does the same for the dev and preview servers.
function loginPage(): Plugin {
  const rewrite: Connect.NextHandleFunction = (request, _response, next) => {
    if (request.url === "/login" || request.url?.startsWith("/login?")) {
      request.url = request.url.replace("/login", "/login.html");
    }
    next();
  };
  return {
    name: "admin-login-page",
    configureServer(server) {
      server.middlewares.use(rewrite);
    },
    configurePreviewServer(server) {
      server.middlewares.use(rewrite);
    }
  };
}

export default defineConfig({
  plugins: [react(), loginPage()],
  server: {
    port: 5174,
    strictPort: true,
    // Same origin as in production: the API answers behind /api, so the session cookie stays first-party.
    proxy: {
      "/api": {
        target: process.env.VITE_DEV_API_URL ?? "http://localhost:8080",
        changeOrigin: true
      }
    }
  },
  build: {
    rollupOptions: {
      input: {
        main: fileURLToPath(new URL("./index.html", import.meta.url)),
        login: fileURLToPath(new URL("./login.html", import.meta.url))
      }
    }
  },
  test: {
    environment: "jsdom",
    setupFiles: "./tests/setup.ts",
    include: ["tests/**/*.test.ts", "tests/**/*.test.tsx"]
  }
});
