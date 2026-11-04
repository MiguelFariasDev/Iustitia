import type { NextConfig } from 'next'

const securityHeaders = [
  { key: 'X-Content-Type-Options', value: 'nosniff' },
  { key: 'X-Frame-Options', value: 'DENY' },
  { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
  { key: 'Permissions-Policy', value: 'camera=(), microphone=(), geolocation=()' },
]

const nextConfig: NextConfig = {
  // Diretório de build isolável: o E2E compila em .next-e2e para não invalidar o
  // .next de um `next dev` em andamento.
  distDir: process.env.NEXT_DIST_DIR || '.next',
  // React Compiler: memoização automática (ver docs/frontend/performance.md).
  reactCompiler: true,
  typedRoutes: true,
  poweredByHeader: false,
  experimental: {
    // Reescreve imports de barrel (lucide-react, radix-ui) para imports diretos.
    optimizePackageImports: ['lucide-react', 'radix-ui', 'date-fns'],
  },
  async headers() {
    return [{ source: '/:path*', headers: securityHeaders }]
  },
}

export default nextConfig
