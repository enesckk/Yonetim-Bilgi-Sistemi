const rawOrigin = process.env.API_ORIGIN ?? process.env.RENDER_API_ORIGIN
if (!rawOrigin) {
  throw new Error('API_ORIGIN must be set to the HTTPS API origin')
}

const apiOrigin = new URL(rawOrigin)
if (apiOrigin.protocol !== 'https:' || apiOrigin.username || apiOrigin.password ||
    apiOrigin.pathname !== '/' || apiOrigin.search || apiOrigin.hash) {
  throw new Error('API_ORIGIN must be an HTTPS origin without a path')
}

const csp = [
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

export const config = {
  framework: 'vite',
  buildCommand: 'npm run build',
  outputDirectory: 'dist',
  rewrites: [
    { source: '/api/:path*', destination: `${apiOrigin.origin}/api/:path*` },
    { source: '/(.*)', destination: '/index.html' },
  ],
  headers: [
    {
      source: '/:path((?!api/).*)',
      headers: [
        { key: 'Content-Security-Policy', value: csp },
        { key: 'X-Content-Type-Options', value: 'nosniff' },
        { key: 'X-Frame-Options', value: 'DENY' },
        { key: 'Referrer-Policy', value: 'no-referrer' },
        { key: 'Permissions-Policy', value: 'camera=(), microphone=(), geolocation=()' },
      ],
    },
  ],
}
