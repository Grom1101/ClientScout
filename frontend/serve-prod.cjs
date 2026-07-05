const http = require('http');
const fs = require('fs');
const path = require('path');

const dist = path.join(__dirname, 'dist');
const apiTarget = 'http://localhost:5000';
const port = Number(process.env.PORT || 5173);

const contentTypes = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.ico': 'image/x-icon',
};

const server = http.createServer((req, res) => {
  if (req.url && req.url.startsWith('/api/')) {
    const target = new URL(req.url, apiTarget);
    const proxy = http.request(
      target,
      {
        method: req.method,
        headers: { ...req.headers, host: target.host },
      },
      (proxyResponse) => {
        res.writeHead(proxyResponse.statusCode || 502, proxyResponse.headers);
        proxyResponse.pipe(res);
      },
    );

    proxy.on('error', (error) => {
      res.writeHead(502, { 'content-type': 'text/plain; charset=utf-8' });
      res.end(`API proxy error: ${error.message}`);
    });

    req.pipe(proxy);
    return;
  }

  const pathname = decodeURIComponent(new URL(req.url || '/', 'http://localhost').pathname);
  let filePath = path.join(dist, pathname === '/' ? 'index.html' : pathname);

  if (!filePath.startsWith(dist)) {
    res.writeHead(403, { 'content-type': 'text/plain; charset=utf-8' });
    res.end('Forbidden');
    return;
  }

  fs.stat(filePath, (error, stat) => {
    if (error || !stat.isFile()) {
      filePath = path.join(dist, 'index.html');
    }

    const extension = path.extname(filePath).toLowerCase();
    res.writeHead(200, {
      'content-type': contentTypes[extension] || 'application/octet-stream',
      'cache-control': extension === '.html' ? 'no-cache' : 'public, max-age=3600',
    });
    fs.createReadStream(filePath).pipe(res);
  });
});

server.listen(port, '0.0.0.0', () => {
  console.log(`ClientScout production frontend on http://0.0.0.0:${port}`);
});
