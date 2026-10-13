/* BodyOps service worker.
   One job: when the server can't be reached, show offline.html instead of the browser's error page.
   Everything else goes straight to the network, so a new deploy shows up on the next launch. */
const CACHE = 'bodyops-shell-v1';
const SHELL = ['/offline.html', '/icons/icon.svg'];

self.addEventListener('install', e => {
  e.waitUntil(caches.open(CACHE).then(c => c.addAll(SHELL)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', e => {
  e.waitUntil(caches.keys()
    .then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
    .then(() => self.clients.claim()));
});

self.addEventListener('fetch', e => {
  if (e.request.mode !== 'navigate') return;
  e.respondWith(fetch(e.request).catch(() => caches.match('/offline.html')));
});
