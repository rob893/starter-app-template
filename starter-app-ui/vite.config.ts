import { defineConfig, loadEnv } from 'vite';
import type { Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { buildContentSecurityPolicy } from './src/utils/csp';

// Injects a baseline Content-Security-Policy <meta> tag into the production build only.
// It is intentionally skipped in dev so Vite's HMR/inline bootstrap scripts keep working.
// The tag is prepended to <head> so it precedes (and therefore governs) the bundle's
// script/style tags. Note: <meta>-delivered CSP cannot enforce frame-ancestors; add that
// (and other security headers) at the hosting layer.
function cspMetaPlugin(apiBaseUrl: string | undefined): Plugin {
  return {
    name: 'inject-csp-meta',
    apply: 'build',
    transformIndexHtml: {
      order: 'pre',
      handler() {
        return [
          {
            tag: 'meta',
            attrs: {
              'http-equiv': 'Content-Security-Policy',
              content: buildContentSecurityPolicy(apiBaseUrl)
            },
            injectTo: 'head-prepend'
          }
        ];
      }
    }
  };
}

// https://vite.dev/config/
// Set VITE_BASE_PATH env var for GitHub Pages deployment (e.g. '/my-repo/')
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');

  return {
    plugins: [react(), tailwindcss(), cspMetaPlugin(env.VITE_API_BASE_URL)],
    base: env.VITE_BASE_PATH || '/',
    publicDir: 'public'
  };
});
