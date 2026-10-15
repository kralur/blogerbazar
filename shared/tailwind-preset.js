/** Design System v2 aliases for Tailwind: utilities read the shared --bb-* tokens, so both themes work. */
/** @type {import('tailwindcss').Config} */
export default {
  theme: {
    extend: {
      colors: {
        brand: {
          blue: "var(--bb-action)",
          cyan: "var(--bb-accent)",
          premium: "var(--bb-premium)",
          ink: "var(--bb-text)",
          surface: "var(--bb-surface)",
          soft: "var(--bb-surface-secondary)",
          accent: "var(--bb-accent)",
          muted: "var(--bb-text-secondary)",
          bg: "var(--bb-background)",
          line: "var(--bb-border)",
          success: "var(--bb-success)",
          warning: "var(--bb-warning)",
          danger: "var(--bb-error)"
        }
      },
      fontFamily: {
        sans: ["Inter", "SF Pro Display", "ui-sans-serif", "system-ui", "sans-serif"]
      },
      boxShadow: {
        soft: "var(--bb-shadow-overlay)",
        card: "var(--bb-shadow-card)",
        glow: "var(--bb-shadow-action)"
      }
    }
  }
};
