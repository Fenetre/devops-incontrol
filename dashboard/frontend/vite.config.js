import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'
import fs from 'fs'

const docsDir = path.resolve(__dirname, '../../Documentation')

/** Serve /Documentation/* as static files from the repo root Documentation/ folder */
function serveDocsPlugin() {
  return {
    name: 'serve-documentation',
    configureServer(server) {
      server.middlewares.use('/Documentation', (req, res, next) => {
        const safePath = path.normalize(req.url).replace(/^(\.\.[/\\])+/, '')
        const filePath = path.join(docsDir, safePath)
        if (!filePath.startsWith(docsDir)) return next()
        if (fs.existsSync(filePath) && fs.statSync(filePath).isFile()) {
          const ext = path.extname(filePath).toLowerCase()
          const types = { '.html': 'text/html', '.css': 'text/css', '.png': 'image/png', '.jpg': 'image/jpeg', '.svg': 'image/svg+xml' }
          res.setHeader('Content-Type', types[ext] || 'application/octet-stream')
          fs.createReadStream(filePath).pipe(res)
        } else {
          next()
        }
      })
    },
  }
}

export default defineConfig({
  plugins: [vue(), serveDocsPlugin()],
  resolve: {
    preserveSymlinks: true,
  },
  test: {
    environment: 'jsdom',
    globals: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: `http://127.0.0.1:${process.env.VITE_BACKEND_PORT || '5172'}`,
        changeOrigin: true,
        timeout: 300000,       // 5 min — audit can be slow
        proxyTimeout: 300000,
        configure: (proxy) => {
          // Return a JSON error instead of Vite's default HTML error page
          // when the backend is unreachable (network drive hiccup, restart, etc.)
          proxy.on('error', (err, _req, res) => {
            if (!res.headersSent) {
              res.writeHead(502, { 'Content-Type': 'application/json' })
              res.end(JSON.stringify({ detail: `Backend unreachable: ${err.message}` }))
            }
          })
        },
      },
    },
    watch: {
      usePolling: true,
      interval: 1000,
    },
  },
})
