/// <reference types="vite/client" />

interface ImportMetaEnv {
  // The bot's username without "@" (a public name, not a secret), given at build time.
  readonly VITE_BOT_USERNAME?: string;
}
