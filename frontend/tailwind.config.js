import designSystem from "@bloggerbazar/shared/tailwind-preset";

/** @type {import('tailwindcss').Config} */
export default {
  // Token colors, font and shadows come from the shared Design System v2 preset (also used by admin-web).
  presets: [designSystem],
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      borderRadius: {
        "4xl": "2rem",
        "5xl": "2.5rem"
      },
      backgroundImage: {
        "brand-gradient": "var(--bb-gradient-action)",
        "premium-gradient": "var(--bb-gradient-premium)",
        "soft-radial": "none"
      },
      keyframes: {
        "screen-in": {
          "0%": { opacity: "0", transform: "translateY(6px) scale(.995)" },
          "100%": { opacity: "1", transform: "translateY(0) scale(1)" }
        }
      },
      animation: {
        "screen-in": "screen-in 240ms cubic-bezier(.2,.8,.2,1) both"
      }
    }
  },
  plugins: []
};
