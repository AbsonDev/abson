import { defineConfig } from 'astro/config';

export default defineConfig({
  site: 'https://docs.fisga.abson.tech',
  output: 'static',
  trailingSlash: 'never',
  build: { format: 'file' },
  devToolbar: { enabled: false },
  vite: { build: { assetsInlineLimit: 0 } }
});
