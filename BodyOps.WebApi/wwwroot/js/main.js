/* BodyOps — routing and the app shell (side rail on a computer, tab bar on a phone). Loaded last. */
'use strict';

(() => {
  const app = document.getElementById('app');
  const NAV = [
    ['/today', 'Today', 'today'],
    ['/week', 'Week', 'week'],
    ['/plan', 'Plan', 'plan'],
    ['/weight', 'Weight', 'weight'],
    ['/settings', 'Settings', 'settings'],
  ];
  const SIGNED_IN = { '/today': 'today', '/week': 'week', '/plan': 'plan', '/weight': 'weight', '/settings': 'settings' };

  /** Navigates within the page. */
  BO.go = function go(path, { replace = false } = {}) {
    BO.flush();
    if (replace) history.replaceState(null, '', path);
    else history.pushState(null, '', path);
    route();
  };

  /** The current query string as an object. */
  BO.query = () => Object.fromEntries(new URLSearchParams(location.search));

  /** Updates the query string without re-rendering (remembered tab, picked day). */
  BO.setQuery = function setQuery(values) {
    const params = new URLSearchParams(location.search);
    Object.entries(values).forEach(([key, value]) => value == null || value === '' ? params.delete(key) : params.set(key, value));
    const search = params.toString();
    history.replaceState(null, '', location.pathname + (search ? '?' + search : ''));
  };

  function shell(current) {
    const navLinks = NAV.map(([path, label, icon]) =>
      `<a href="${path}" data-link${current === path ? ' aria-current="page"' : ''}>${BO.icons[icon]}${label}</a>`).join('');
    const tabLinks = NAV.slice(0, 4).map(([path, label, icon]) =>
      `<a href="${path}" data-link${current === path ? ' aria-current="page"' : ''}>${BO.icons[icon]}${label}</a>`).join('');
    app.innerHTML = `<div class="shell">
      <aside class="rail">
        <a href="/today" data-link style="text-decoration:none;color:inherit">${BO.brandHTML()}</a>
        <nav class="nav" aria-label="Main">${navLinks}</nav>
        <div class="rail-foot">${BO.syncHTML()}<span class="sub">Signed in as ${BO.esc(BO.me.username)}</span></div>
      </aside>
      <main class="main" id="screen"></main>
      <nav class="tabbar" aria-label="Main">${tabLinks}</nav>
    </div>`;
    return app.querySelector('#screen');
  }

  async function route() {
    const path = location.pathname.replace(/\/+$/, '') || '/';
    window.scrollTo(0, 0);

    if (path === '/' || path === '/signin' || path === '/signup') {
      if (BO.me) {
        BO.go('/today', { replace: true });
        return;
      }
      if (path === '/') BO.screens.landing(app);
      else BO.screens.auth(app, { mode: path === '/signup' ? 'signup' : 'signin' });
      document.title = path === '/' ? 'BodyOps' : (path === '/signup' ? 'Create account · BodyOps' : 'Sign in · BodyOps');
      return;
    }

    const screen = SIGNED_IN[path];
    if (!screen) {
      BO.go(BO.me ? '/today' : '/', { replace: true });
      return;
    }
    if (!BO.me) {
      BO.go('/signin', { replace: true });
      return;
    }

    const root = shell(path);
    document.title = `${NAV.find(n => n[0] === path)[1]} · BodyOps`;
    root.innerHTML = '<div class="loading"><div class="spinner" aria-label="Loading"></div></div>';
    try {
      await BO.screens[screen](root, BO.query());
    } catch (e) {
      root.innerHTML = `<div class="empty"><b>This page didn't load.</b><span>${BO.esc(e.message)}</span><button type="button" class="btn" id="retry">Try again</button></div>`;
      root.querySelector('#retry').onclick = route;
    }
  }

  // Links marked data-link stay inside the page.
  document.addEventListener('click', event => {
    const link = event.target.closest('a[data-link]');
    if (!link || event.ctrlKey || event.metaKey || event.shiftKey || event.button !== 0) return;
    event.preventDefault();
    const url = new URL(link.href);
    if (url.pathname + url.search !== location.pathname + location.search) BO.go(url.pathname + url.search);
  });
  window.addEventListener('popstate', route);

  // Re-read the week when the app comes back to the front (phone screen unlocked at the gym).
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible' && BO.me && BO.onResume) BO.onResume();
  });

  async function boot() {
    try { BO.health = await BO.api('GET', '/api/health'); } catch (e) { BO.health = { registration: true, firstRun: false }; }
    try { BO.me = await BO.api('GET', '/api/auth/me'); } catch (e) { BO.me = null; }
    route();
  }

  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/sw.js').catch(() => { });
  }
  boot();
})();
