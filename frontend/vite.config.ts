import { defineConfig, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'
import { fileURLToPath, URL } from 'node:url'

const pwaIcons = [
  { src: '/pwa-64x64.png', sizes: '64x64', type: 'image/png', purpose: 'any' },
  { src: '/pwa-192x192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
  { src: '/pwa-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
  { src: '/maskable-icon-192x192.png', sizes: '192x192', type: 'image/png', purpose: 'maskable' },
  { src: '/maskable-icon-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
] as const

const SECURITY_HEADERS: Record<string, string> = {
  'X-Content-Type-Options': 'nosniff',
  'X-Frame-Options': 'DENY',
  'Referrer-Policy': 'no-referrer',
  'Permissions-Policy': 'camera=(), microphone=(), geolocation=()',
}

const SECURITY_CSP = [
  "default-src 'self'",
  "base-uri 'self'",
  "form-action 'self'",
  "frame-ancestors 'none'",
  "object-src 'none'",
  "script-src 'self'",
  "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
  "font-src 'self' https://fonts.gstatic.com",
  "img-src 'self' data: blob: https://*.tile.openstreetmap.org https://tile.openstreetmap.org",
  "connect-src 'self'",
  "worker-src 'self' blob:",
  "manifest-src 'self'",
].join('; ')

function securityHeadersPlugin(): Plugin {
  const applyBase = (req: { url?: string }, res: { setHeader: (k: string, v: string) => void }, next: () => void) => {
    for (const [key, value] of Object.entries(SECURITY_HEADERS)) res.setHeader(key, value)
    next()
  }
  return {
    name: 'security-headers',
    configureServer(server) {
      server.middlewares.use(applyBase)
    },
    configurePreviewServer(server) {
      server.middlewares.use((req, res, next) => {
        applyBase(req, res, () => undefined)
        res.setHeader('Content-Security-Policy', SECURITY_CSP)
        next()
      })
    },
    transformIndexHtml: {
      order: 'pre',
      handler(html, ctx) {
        if (ctx.server) return html
        if (html.includes('Content-Security-Policy')) return html
        return html.replace(
          '<head>',
          `<head>\n    <meta http-equiv="Content-Security-Policy" content="${SECURITY_CSP}" />`,
        )
      },
    },
  }
}

// Plesk'te build çıktısı (dist/) statik olarak yayınlanır.
export default defineConfig({
  plugins: [
    react(),
    securityHeadersPlugin(),
    {
      name: 'geo-static-cache',
      configureServer(server) {
        server.middlewares.use((req, res, next) => {
          if (req.url?.startsWith('/geo/')) {
            res.setHeader('Cache-Control', 'public, max-age=86400')
          }
          next()
        })
      },
      configurePreviewServer(server) {
        server.middlewares.use((req, res, next) => {
          const url = req.url ?? ''
          if (url.startsWith('/assets/')) {
            res.setHeader('Cache-Control', 'public, max-age=31536000, immutable')
          } else if (url.startsWith('/geo/')) {
            res.setHeader('Cache-Control', 'public, max-age=2592000')
          } else if (/\.(png|svg|ico|webp|woff2?)(\?|$)/.test(url)) {
            res.setHeader('Cache-Control', 'public, max-age=604800')
          }
          next()
        })
      },
    },
    VitePWA({
      registerType: 'autoUpdate',
      injectRegister: 'auto',
      manifest: {
        id: '/',
        name: 'Yönetim Bilgi Sistemi',
        short_name: 'YBS',
        description: 'Mahalle, etkinlik ve tesis yönetimi',
        lang: 'tr',
        dir: 'ltr',
        start_url: '/',
        scope: '/',
        display: 'standalone',
        display_override: ['standalone', 'minimal-ui', 'browser'],
        orientation: 'any',
        background_color: '#F7FAFB',
        theme_color: '#208B5F',
        categories: ['business', 'productivity'],
        prefer_related_applications: false,
        icons: [...pwaIcons],
      },
      workbox: {
        clientsClaim: true,
        skipWaiting: true,
        cleanupOutdatedCaches: true,
        navigateFallback: 'index.html',
        navigateFallbackDenylist: [/^\/api\//, /^\/geo\//],
        globPatterns: ['**/*.{js,css,html,ico,png,svg,webmanifest,woff,woff2,json}'],
        globIgnores: ['**/pwa-source.svg', '**/.htaccess', '**/pdf-*.js', '**/purify.es-*.js'],
        maximumFileSizeToCacheInBytes: 8 * 1024 * 1024,
        runtimeCaching: [
          {
            urlPattern: ({ url }) => url.pathname.startsWith('/api/'),
            handler: 'NetworkOnly',
          },
          {
            urlPattern: ({ url }) => url.pathname.startsWith('/geo/'),
            handler: 'CacheFirst',
            options: {
              cacheName: 'geo-static',
              expiration: {
                maxEntries: 30,
                maxAgeSeconds: 60 * 60 * 24 * 30,
              },
              cacheableResponse: { statuses: [0, 200] },
            },
          },
          {
            urlPattern: ({ url }) => url.hostname.endsWith('tile.openstreetmap.org'),
            handler: 'CacheFirst',
            options: {
              cacheName: 'osm-tiles',
              expiration: {
                maxEntries: 400,
                maxAgeSeconds: 60 * 60 * 24 * 14,
              },
              cacheableResponse: { statuses: [0, 200] },
            },
          },
          {
            urlPattern: /^https:\/\/fonts\.googleapis\.com\/.*/i,
            handler: 'StaleWhileRevalidate',
            options: {
              cacheName: 'google-fonts-stylesheets',
              expiration: {
                maxEntries: 8,
                maxAgeSeconds: 60 * 60 * 24 * 365,
              },
            },
          },
          {
            urlPattern: /^https:\/\/fonts\.gstatic\.com\/.*/i,
            handler: 'CacheFirst',
            options: {
              cacheName: 'google-fonts-webfonts',
              expiration: {
                maxEntries: 20,
                maxAgeSeconds: 60 * 60 * 24 * 365,
              },
              cacheableResponse: { statuses: [0, 200] },
            },
          },
        ],
      },
      devOptions: {
        enabled: false,
        suppressWarnings: true,
      },
    }),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5232',
        changeOrigin: true,
      },
    },
  },
  preview: {
    port: 4173,
    proxy: {
      '/api': {
        target: 'http://localhost:5232',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: false,
    cssCodeSplit: true,
    target: 'es2020',
    modulePreload: {
      polyfill: false,
      resolveDependencies: (_filename, deps) =>
        deps.filter((dep) => !/leaflet|pdf-|html2canvas|purify/i.test(dep)),
    },
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('node_modules/jspdf')) return 'pdf-jspdf'
          if (id.includes('node_modules/html2canvas')) return 'pdf-html2canvas'
        },
      },
    },
  },
})
