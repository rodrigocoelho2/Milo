'use strict';

/**
 * CorpoOps sync server
 * -------------------------------------------------------------
 * A tiny single-user state store. The whole app state is one JSON
 * document; the client GETs it on load and POSTs it back on change.
 * Persistence is a single SQLite file, so backups are trivial (copy
 * the file). Auth is a shared token you set via the CORPOOPS_TOKEN
 * env var. Designed to sit behind Nginx Proxy Manager on nas-net.
 */

const http = require('http');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const Database = require('better-sqlite3');

const PORT = parseInt(process.env.PORT || '8971', 10);
const TOKEN = process.env.CORPOOPS_TOKEN || '';
const DATA_DIR = process.env.CORPOOPS_DATA_DIR || path.join(__dirname, 'data');
const PUBLIC_DIR = path.join(__dirname, 'public');
const DB_PATH = path.join(DATA_DIR, 'corpoops.db');
const MAX_BODY = 8 * 1024 * 1024; // 8 MB ceiling on a state payload

if (!TOKEN) {
  console.error('FATAL: CORPOOPS_TOKEN is not set. Refusing to start without a token.');
  process.exit(1);
}
if (TOKEN.length < 16) {
  console.error('FATAL: CORPOOPS_TOKEN is too short. Use at least 16 characters.');
  process.exit(1);
}

fs.mkdirSync(DATA_DIR, { recursive: true });

const db = new Database(DB_PATH);
db.pragma('journal_mode = WAL');
db.exec(`
  CREATE TABLE IF NOT EXISTS app_state (
    id         INTEGER PRIMARY KEY CHECK (id = 1),
    payload    TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    revision   INTEGER NOT NULL DEFAULT 1
  );
`);

const getStmt = db.prepare('SELECT payload, updated_at, revision FROM app_state WHERE id = 1');
const upsertStmt = db.prepare(`
  INSERT INTO app_state (id, payload, updated_at, revision)
  VALUES (1, @payload, @updated_at, 1)
  ON CONFLICT(id) DO UPDATE SET
    payload = @payload,
    updated_at = @updated_at,
    revision = revision + 1
`);

// ---- helpers ----------------------------------------------------

// constant-time token comparison so we don't leak length/prefix via timing
function tokenValid(provided) {
  if (!provided) return false;
  const a = Buffer.from(provided);
  const b = Buffer.from(TOKEN);
  if (a.length !== b.length) return false;
  return crypto.timingSafeEqual(a, b);
}

function authOK(req) {
  const header = req.headers['authorization'] || '';
  const bearer = header.startsWith('Bearer ') ? header.slice(7) : null;
  const alt = req.headers['x-corpoops-token'] || null;
  return tokenValid(bearer) || tokenValid(alt);
}

function sendJSON(res, status, obj) {
  const body = JSON.stringify(obj);
  res.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Cache-Control': 'no-store',
  });
  res.end(body);
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let size = 0;
    const chunks = [];
    req.on('data', (c) => {
      size += c.length;
      if (size > MAX_BODY) {
        reject(new Error('payload_too_large'));
        req.destroy();
        return;
      }
      chunks.push(c);
    });
    req.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')));
    req.on('error', reject);
  });
}

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
  '.webmanifest': 'application/manifest+json',
};

// serve static files, blocking any path traversal out of PUBLIC_DIR
function serveStatic(req, res) {
  let urlPath = decodeURIComponent(req.url.split('?')[0]);
  if (urlPath === '/') urlPath = '/index.html';
  const resolved = path.normalize(path.join(PUBLIC_DIR, urlPath));
  if (!resolved.startsWith(PUBLIC_DIR)) {
    res.writeHead(403); res.end('Forbidden'); return;
  }
  fs.readFile(resolved, (err, data) => {
    if (err) {
      // SPA fallback: unknown non-API path returns the app shell
      fs.readFile(path.join(PUBLIC_DIR, 'index.html'), (e2, html) => {
        if (e2) { res.writeHead(404); res.end('Not found'); return; }
        res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
        res.end(html);
      });
      return;
    }
    const ext = path.extname(resolved).toLowerCase();
    res.writeHead(200, { 'Content-Type': MIME[ext] || 'application/octet-stream' });
    res.end(data);
  });
}

// ---- request routing --------------------------------------------

const server = http.createServer(async (req, res) => {
  const url = req.url.split('?')[0];

  // health check — unauthenticated, so NPM / uptime probes can hit it
  if (url === '/api/health' && req.method === 'GET') {
    return sendJSON(res, 200, { ok: true, service: 'corpoops', time: new Date().toISOString() });
  }

  // everything else under /api requires the token
  if (url.startsWith('/api/')) {
    if (!authOK(req)) {
      return sendJSON(res, 401, { error: 'unauthorized' });
    }

    if (url === '/api/state' && req.method === 'GET') {
      const row = getStmt.get();
      if (!row) return sendJSON(res, 200, { state: null, revision: 0, updatedAt: null });
      let parsed;
      try { parsed = JSON.parse(row.payload); } catch (_) { parsed = null; }
      return sendJSON(res, 200, { state: parsed, revision: row.revision, updatedAt: row.updated_at });
    }

    if (url === '/api/state' && req.method === 'POST') {
      let raw;
      try {
        raw = await readBody(req);
      } catch (e) {
        if (e.message === 'payload_too_large') return sendJSON(res, 413, { error: 'payload_too_large' });
        return sendJSON(res, 400, { error: 'read_error' });
      }
      let body;
      try { body = JSON.parse(raw); } catch (_) { return sendJSON(res, 400, { error: 'invalid_json' }); }
      if (!body || typeof body.state !== 'object' || body.state === null) {
        return sendJSON(res, 400, { error: 'missing_state' });
      }
      const payload = JSON.stringify(body.state);
      const updatedAt = new Date().toISOString();
      const info = upsertStmt.run({ payload, updated_at: updatedAt });
      const row = getStmt.get();
      return sendJSON(res, 200, { ok: true, revision: row.revision, updatedAt });
    }

    return sendJSON(res, 404, { error: 'not_found' });
  }

  // static app
  if (req.method === 'GET') return serveStatic(req, res);
  res.writeHead(405); res.end('Method not allowed');
});

server.listen(PORT, () => {
  console.log(`CorpoOps sync server listening on :${PORT}`);
  console.log(`  data dir: ${DATA_DIR}`);
  console.log(`  db file : ${DB_PATH}`);
});

// graceful shutdown so SQLite closes cleanly under docker stop
function shutdown() {
  console.log('Shutting down…');
  try { db.close(); } catch (_) {}
  server.close(() => process.exit(0));
  setTimeout(() => process.exit(0), 3000).unref();
}
process.on('SIGTERM', shutdown);
process.on('SIGINT', shutdown);
