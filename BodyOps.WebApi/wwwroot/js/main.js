/* BodyOps — routing and the app shell: a side bar on a computer, a tab bar with the + button on a phone. Loaded last. */
'use strict';

(() => {
  const app = document.getElementById('app');
  const NAV = [
    ['/today', 'Today', 'today'],
    ['/week', 'Week', 'week'],
    ['/progress', 'Progress', 'progress'],
    ['/plan', 'Plan', 'plan'],
    ['/settings', 'Settings', 'settings'],
  ];
  const SIGNED_IN = { '/today': 'today', '/week': 'week', '/progress': 'progress', '/plan': 'plan', '/settings': 'settings', '/session': 'session' };
  const MOVED = { '/weight': '/progress' };

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

  /** Re-runs the current screen (after a quick log changed its data). */
  BO.reload = () => route({ keepScroll: true });

  let current = null;     // the path the shell is drawn for

  function shell(path) {
    const link = ([href, label, icon]) => `<a href="${href}" data-link${path === href ? ' aria-current="page"' : ''}>${BO.icons[icon]}<span>${label}</span></a>`;
    if (current && app.querySelector('.shell')) {
      // Keep the shell (no flash), just move the highlight.
      app.querySelectorAll('.side nav a, .tabs a').forEach(a => {
        if (a.getAttribute('href') === path) a.setAttribute('aria-current', 'page'); else a.removeAttribute('aria-current');
      });
      current = path;
      // A fresh element each time, so listeners from the previous screen go with the old one.
      const fresh = document.createElement('main');
      fresh.className = 'main';
      fresh.id = 'screen';
      app.querySelector('#screen').replaceWith(fresh);
      return fresh;
    }
    current = path;
    const initial = (BO.me.username || '?').slice(0, 1).toUpperCase();
    app.innerHTML = `<div class="shell">
      <aside class="side">
        <a href="/today" data-link style="text-decoration:none">${BO.brandHTML()}</a>
        <nav aria-label="Main">${NAV.map(link).join('')}</nav>
        <a class="me" href="/settings" data-link><span class="avatar">${BO.esc(initial)}</span><span><b style="font-size:14px">${BO.esc(BO.me.username)}</b><span class="sub" style="display:block" data-streak-label>BodyOps</span></span></a>
      </aside>
      <main class="main" id="screen"></main>
      <nav class="tabs" aria-label="Main">
        ${link(NAV[0])}${link(NAV[1])}
        <button type="button" class="plus" data-quick aria-label="Quick log">${BO.icons.plus}</button>
        ${link(NAV[2])}${link(NAV[3])}
      </nav>
    </div>`;
    return app.querySelector('#screen');
  }

  async function route({ keepScroll = false } = {}) {
    let path = location.pathname.replace(/\/+$/, '') || '/';
    if (MOVED[path]) { history.replaceState(null, '', MOVED[path]); path = MOVED[path]; }
    if (!keepScroll) window.scrollTo(0, 0);
    BO.onResume = null;

    if (path === '/' || path === '/signin' || path === '/signup') {
      if (BO.me) {
        BO.go('/today', { replace: true });
        return;
      }
      current = null;
      BO.paintThemeColor('#3355FF');
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
    BO.paintThemeColor();

    if (screen === 'session') {
      // Session mode covers the whole screen on top of Today.
      if (!app.querySelector('.shell')) {
        const root = shell('/today');
        BO.screens.today(root, {}).catch(() => { });
      }
      document.title = 'Session · BodyOps';
      try { await BO.screens.session(BO.query()); } catch (e) { BO.toast(e.message, { error: true }); BO.go('/today', { replace: true }); }
      return;
    }
    if (document.querySelector('.session')) { document.querySelectorAll('.session').forEach(el => el.remove()); document.body.style.overflow = ''; }

    const before = keepScroll && app.querySelector('#screen') ? app.querySelector('#screen').innerHTML : null;
    const root = shell(path);
    document.title = `${NAV.find(n => n[0] === path)[1]} · BodyOps`;
    root.innerHTML = before != null ? before : '<div class="loading"><div class="spinner" aria-label="Loading"></div></div>';
    if (before != null) root.classList.add('still');
    try {
      await BO.screens[screen](root, BO.query());
    } catch (e) {
      root.innerHTML = `<div class="empty"><b>This page didn't load.</b><span>${BO.esc(e.message)}</span><button type="button" class="btn" id="retry">Try again</button></div>`;
      root.querySelector('#retry').onclick = () => route();
    }
  }

  // Links marked data-link stay inside the page.
  document.addEventListener('click', event => {
    if (event.target.closest('[data-quick]')) {
      BO.quickLog();
      return;
    }
    const link = event.target.closest('a[data-link]');
    if (!link || event.ctrlKey || event.metaKey || event.shiftKey || event.button !== 0) return;
    event.preventDefault();
    const url = new URL(link.href);
    if (url.pathname + url.search !== location.pathname + location.search) BO.go(url.pathname + url.search);
  });
  window.addEventListener('popstate', () => route());

  // Re-read the data when the app comes back to the front (phone unlocked at the gym).
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
