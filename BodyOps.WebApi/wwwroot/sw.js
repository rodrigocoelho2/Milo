/* Milo (BodyOps) service worker.
   Two jobs: when the server can't be reached, show offline.html instead of the browser's error page; and show the
   push reminders (and answer their buttons). Everything else goes straight to the network, so a new deploy shows up
   on the next launch. */
const CACHE = 'bodyops-shell-v6';
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

/* A reminder from the server: { title, body, url, tag, actions, data }. Same tag = the newer replaces the older. */
self.addEventListener('push', e => {
  let data = {};
  try { data = e.data ? e.data.json() : {}; } catch (err) { data = { body: e.data ? e.data.text() : '' }; }
  e.waitUntil(self.registration.showNotification(data.title || 'Milo', {
    body: data.body || '',
    tag: data.tag || undefined,
    renotify: Boolean(data.tag),
    icon: '/icons/milo-192.png',
    badge: '/icons/milo-192.png',
    actions: (data.actions || []).slice(0, 2),
    data: Object.assign({ url: data.url || '/today' }, data.data || {}),
  }));
});

/* Opens a path of the app, reusing an open Milo window. */
async function openApp(path) {
  const target = new URL(path || '/today', self.location.origin);
  const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
  const open = windows.find(w => new URL(w.url).origin === self.location.origin);
  if (open) {
    await open.focus();
    if (open.url !== target.href && 'navigate' in open) await open.navigate(target.href).catch(() => { });
    return;
  }
  await self.clients.openWindow(target.href);
}

/* A tap opens what the reminder is about. "Ate it" ticks the meal without opening Milo (it opens Milo if that fails,
   e.g. signed out); "Something else" opens the meal; "Log weight" opens the weigh-in. */
self.addEventListener('notificationclick', e => {
  const n = e.notification;
  const data = n.data || {};
  n.close();
  e.waitUntil((async () => {
    if (e.action === 'ate' && data.date && data.meal) {
      try {
        const response = await fetch(`/api/days/${data.date}/meals/${data.meal}`, {
          method: 'PUT', credentials: 'same-origin',
          headers: { 'Content-Type': 'application/json', 'X-BodyOps': '1' },
          body: JSON.stringify({ eaten: true }),
        });
        if (response.ok) {
          const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
          for (const w of windows) w.postMessage({ type: 'day-changed' });
          return;
        }
      } catch (err) { /* offline: open the meal instead */ }
      await openApp(`/today?meal=${data.meal}`);
      return;
    }
    if (e.action === 'weigh') { await openApp('/today?weigh=1'); return; }
    if (e.action === 'other' && data.meal) { await openApp(`/today?meal=${data.meal}`); return; }
    await openApp(data.url);
  })());
});

/* The browser replaced the subscription: an open Milo window sends the new one. */
self.addEventListener('pushsubscriptionchange', e => {
  e.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true })
    .then(windows => windows.forEach(w => w.postMessage({ type: 'push-resubscribe' }))));
});
