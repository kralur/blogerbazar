import designSystem from "@bloggerbazar/shared/tailwind-preset";

/** @type {import('tailwindcss').Config} */
export default {
  // Design System v2 from the shared preset, the same tokens as the Mini App and the admin site.
  presets: [designSystem],
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  plugins: []
};
