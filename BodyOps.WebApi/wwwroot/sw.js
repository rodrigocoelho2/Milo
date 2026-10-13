/* Milo (BodyOps) service worker.
   One job: when the server can't be reached, show offline.html instead of the browser's error page.
   Everything else goes straight to the network, so a new deploy shows up on the next launch. */
const CACHE = 'bodyops-shell-v5';
// The offline page and what it shows (the font and the drawing), so it looks right with no connection.
const SHELL = ['/offline.html', '/icons/milo-icon.svg', '/fonts/fonts.css', '/fonts/geist-latin-wght-normal.woff2', '/images/milo-of-croton.webp'];

self.addEventListener('install', e => {
  e.waitUntil(caches.open(CACHE).then(c => c.addAll(SHELL)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', e => {
  e.waitUntil(caches.keys()
    .then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
    .then(() => self.clients.claim()));
});

self.addEventListener('fetch', e => {
  if (e.request.mode !== 'navigate') {
    // The offline page's own files: from the network, or the cached copy when the server is down.
    const path = new URL(e.request.url).pathname;
    if (SHELL.includes(path)) e.respondWith(fetch(e.request).catch(() => caches.match(path)));
    return;
  }
  e.respondWith(fetch(e.request).catch(() => caches.match('/offline.html')));
});
