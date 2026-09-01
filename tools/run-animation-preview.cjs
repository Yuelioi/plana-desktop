const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..', 'src', 'Plana.Desktop', 'Renderer');
const port = Number(process.env.PLANA_PREVIEW_PORT || 4173);
const contentTypes = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.json': 'application/json', '.png': 'image/png', '.atlas': 'text/plain; charset=utf-8', '.skel': 'application/octet-stream' };

http.createServer((request, response) => {
  const requestPath = new URL(request.url, `http://${request.headers.host}`).pathname;
  const relative = requestPath === '/' ? 'prototype-animation-preview.html' : decodeURIComponent(requestPath.slice(1));
  const filePath = path.resolve(root, relative);
  if (!filePath.startsWith(`${root}${path.sep}`)) { response.writeHead(403).end(); return; }
  fs.readFile(filePath, (error, bytes) => {
    if (error) { response.writeHead(404).end('Not found'); return; }
    response.writeHead(200, { 'Content-Type': contentTypes[path.extname(filePath)] || 'application/octet-stream', 'Cache-Control': 'no-store' });
    response.end(bytes);
  });
}).listen(port, '127.0.0.1', () => console.log(`PROTOTYPE animation preview: http://127.0.0.1:${port}/?variant=A`));
