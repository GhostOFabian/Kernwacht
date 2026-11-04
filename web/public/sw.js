// Service Worker: speichert die App-Shell für Offline-Nutzung (PWA).
// Der Cache-Name enthält den Build-Hash; alte Caches werden beim Aktivieren entfernt.
const CACHE = 'kernwacht-%HASH%';
const SHELL = ['./', './index.html', './assets/main.js?v=%HASH%', './assets/styles.css?v=%HASH%', './manifest.webmanifest', './icon.svg'];

self.addEventListener('install', (e) => {
  e.waitUntil(caches.open(CACHE).then((c) => c.addAll(SHELL)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', (e) => {
  e.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k.startsWith('kernwacht-') && k !== CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim()),
  );
});

self.addEventListener('fetch', (e) => {
  if (e.request.method !== 'GET') return;
  // Fremde Ursprünge (z. B. Schriftarten) nicht abfangen – ohne Netz greifen die Ersatzschriften.
  if (new URL(e.request.url).origin !== self.location.origin) return;
  e.respondWith(
    caches.match(e.request).then((hit) => hit || fetch(e.request).catch(() => caches.match('./index.html'))),
  );
});
