import designSystem from "@bloggerbazar/shared/tailwind-preset";

/** @type {import('tailwindcss').Config} */
export default {
  // Design System v2 from the shared preset, the same tokens as the Mini App.
  presets: [designSystem],
  content: ["./index.html", "./login.html", "./src/**/*.{ts,tsx}"],
  plugins: []
};
