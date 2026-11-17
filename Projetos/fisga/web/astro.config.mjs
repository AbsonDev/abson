import { defineConfig } from 'astro/config';
import node from '@astrojs/node';

export default defineConfig({
  output: 'server',
  adapter: node({ mode: 'standalone' }),
  site: process.env.PUBLIC_SITE_URL ?? 'http://localhost:4323',
  server: {
    host: true,
    port: 4323
  },
  devToolbar: {
    enabled: false
  }
});
