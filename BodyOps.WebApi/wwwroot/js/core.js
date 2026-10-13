/* BodyOps — shared helpers: the API client, dates, formatting, icons, the toast and the save indicator.
   Every screen script adds a render function to the global BO object; main.js routes between them. */
'use strict';

const BO = window.BO = {
  me: null,
  health: null,
  screens: {},
};

/* ---------- API ---------- */

BO.ApiError = class ApiError extends Error {
  constructor(status, message, field) {
    super(message);
    this.status = status;
    this.field = field;
  }
};

/** Calls the WebApi. Writes carry the X-BodyOps header (the API's CSRF guard). Throws BO.ApiError with the server's message. */
BO.api = async function api(method, path, body) {
  const options = { method, headers: { Accept: 'application/json' }, credentials: 'same-origin' };
  if (method !== 'GET') options.headers['X-BodyOps'] = '1';
  if (body !== undefined) {
    options.headers['Content-Type'] = 'application/json';
    options.body = typeof body === 'string' ? body : JSON.stringify(body);
  }

  let response;
  try {
    response = await fetch(path, options);
  } catch (e) {
    throw new BO.ApiError(0, 'Can’t reach the server. Check your connection and try again.');
  }

  if (response.status === 204) return null;
  const text = await response.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch (e) { data = null; }

  if (!response.ok) {
    if (response.status === 401 && BO.me && !path.startsWith('/api/auth/')) {
      BO.me = null;
      BO.go('/signin');
    }
    const message = (data && (data.detail || data.title)) || (response.status === 429 ? 'Too many attempts. Wait a minute and try again.' : `Something went wrong (${response.status}).`);
    const field = data && data.errors && data.errors[0] ? data.errors[0].field : undefined;
    throw new BO.ApiError(response.status, message, field);
  }
  return data;
};

/* ---------- save indicator ---------- */

let pending = 0;
BO.saving = async function saving(promise) {
  pending++;
  BO.setSync('saving');
  try {
    const result = await promise;
    pending--;
    if (pending === 0) BO.setSync('saved');
    return result;
  } catch (e) {
    pending--;
    BO.setSync('error');
    BO.toast(e.message, { error: true });
    throw e;
  }
};

BO.syncState = 'saved';
BO.setSync = function setSync(state) {
  BO.syncState = state;
  const label = { saving: 'Saving…', saved: 'Saved', error: 'Not saved' }[state];
  document.querySelectorAll('[data-sync]').forEach(el => {
    el.className = 'sync ' + state;
    el.innerHTML = `<i></i>${label}`;
  });
};
BO.syncHTML = () => `<span class="sync ${BO.syncState}" data-sync><i></i>${{ saving: 'Saving…', saved: 'Saved', error: 'Not saved' }[BO.syncState]}</span>`;

/** Runs fn after `wait` ms of quiet for the given key (autosave). */
const timers = new Map();
BO.debounce = function debounce(key, wait, fn) {
  clearTimeout(timers.get(key));
  timers.set(key, setTimeout(() => { timers.delete(key); fn(); }, wait));
  BO.setSync('saving');
};
BO.flush = function flush() { timers.forEach((id) => clearTimeout(id)); timers.clear(); };
/** True while an autosave is waiting to run or a save is on its way. */
BO.busy = () => timers.size > 0 || pending > 0;

/** Re-renders without losing the focused field: focus and caret go back to the element with the same id. */
BO.rerender = function rerender(render) {
  const active = document.activeElement;
  const id = active && active.id;
  const start = active && 'selectionStart' in active ? active.selectionStart : null;
  render();
  if (id) {
    const again = document.getElementById(id);
    if (again && again !== active) {
      again.focus({ preventScroll: true });
      try { if (start != null) again.setSelectionRange(start, start); } catch (e) { }
    }
  }
};

/* ---------- toast ---------- */

let toastTimer = null;
BO.toast = function toast(message, { action, onAction, error = false, duration } = {}) {
  const el = document.getElementById('toast');
  el.className = 'toast' + (error ? ' err' : '');
  el.innerHTML = `<span>${BO.esc(message)}</span>${action ? `<button type="button">${BO.esc(action)}</button>` : ''}`;
  el.hidden = false;
  if (action) {
    el.querySelector('button').onclick = () => { el.hidden = true; onAction(); };
  }
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { el.hidden = true; }, duration || (action ? 7000 : error ? 6000 : 3000));
};

/* ---------- text ---------- */

BO.esc = function esc(value) {
  return String(value == null ? '' : value).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
};

/** Body weight with one decimal ("98.0"). */
BO.kg = value => value == null ? '—' : Number(value).toFixed(1);
/** A load as typed: no trailing zeros ("102.5", "80"). */
BO.load = value => value == null ? '' : String(Math.round(Number(value) * 100) / 100);
BO.signed = (value, digits = 1) => (value > 0 ? '+' : value < 0 ? '−' : '±') + Math.abs(value).toFixed(digits);
BO.volume = function volume(kg) {
  if (!kg) return '0 kg';
  return kg >= 1000 ? (kg / 1000).toFixed(1) + ' t' : Math.round(kg).toLocaleString('en-GB') + ' kg';
};
BO.plural = (n, one, many) => `${n} ${n === 1 ? one : (many || one + 's')}`;

/* ---------- dates (yyyy-MM-dd strings everywhere) ---------- */

BO.DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
BO.DAYS_SHORT = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
BO.MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
BO.MONTHS_SHORT = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

BO.parseDate = iso => { const [y, m, d] = iso.split('-').map(Number); return new Date(y, m - 1, d); };
BO.iso = date => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
BO.addDays = (iso, n) => { const d = BO.parseDate(iso); d.setDate(d.getDate() + n); return BO.iso(d); };
BO.weekday = iso => { const day = BO.parseDate(iso).getDay(); return day === 0 ? 7 : day; };
BO.monday = iso => BO.addDays(iso, 1 - BO.weekday(iso));
BO.localToday = () => BO.iso(new Date());
BO.dayNum = iso => BO.parseDate(iso).getDate();
BO.shortDate = iso => { const d = BO.parseDate(iso); return `${d.getDate()} ${BO.MONTHS_SHORT[d.getMonth()]}`; };
BO.longDate = iso => { const d = BO.parseDate(iso); return `${d.getDate()} ${BO.MONTHS[d.getMonth()]}`; };
BO.fullDate = iso => { const d = BO.parseDate(iso); return `${BO.DAYS_SHORT[BO.weekday(iso) - 1]} ${d.getDate()} ${BO.MONTHS_SHORT[d.getMonth()]} ${d.getFullYear()}`; };
BO.weekRange = monday => {
  const a = BO.parseDate(monday), b = BO.parseDate(BO.addDays(monday, 6));
  return a.getMonth() === b.getMonth()
    ? `${a.getDate()} – ${b.getDate()} ${BO.MONTHS[b.getMonth()]}`
    : `${a.getDate()} ${BO.MONTHS_SHORT[a.getMonth()]} – ${b.getDate()} ${BO.MONTHS_SHORT[b.getMonth()]}`;
};

/* ---------- muscles ---------- */

BO.MUSCLES = {
  chest: 'Chest', shoulders: 'Shoulders', biceps: 'Biceps', triceps: 'Triceps', forearms: 'Forearms', abs: 'Abs',
  lats: 'Lats', traps: 'Traps', lowerBack: 'Lower back', glutes: 'Glutes', quads: 'Quads', hamstrings: 'Hamstrings', calves: 'Calves',
};

/* ---------- icons ---------- */

const svg = (body, view = '0 0 24 24', extra = '') => `<svg viewBox="${view}" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" ${extra}>${body}</svg>`;
BO.icons = {
  today: svg('<circle cx="12" cy="12" r="8.5"/><path d="M12 7.5V12l3 2"/>'),
  week: svg('<rect x="3.5" y="5" width="17" height="15" rx="3"/><path d="M3.5 10h17M8 3v4M16 3v4"/>'),
  plan: svg('<path d="M2.5 12h2M19.5 12h2M6.5 7.5v9M17.5 7.5v9M4.5 9.5v5M19.5 9.5v5M6.5 12h11"/>'),
  weight: svg('<rect x="3.5" y="3.5" width="17" height="17" rx="4"/><path d="M8 10a4 4 0 0 1 8 0M12 10l1.6-1.8"/>'),
  settings: svg('<circle cx="12" cy="12" r="3"/><path d="M12 3v2.5M12 18.5V21M3 12h2.5M18.5 12H21M5.6 5.6l1.8 1.8M16.6 16.6l1.8 1.8M5.6 18.4l1.8-1.8M16.6 7.4l1.8-1.8"/>'),
  check: '<svg viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 7.5 6 10l5-6"/></svg>',
  x: svg('<path d="M6 6l12 12M18 6 6 18"/>', '0 0 24 24', 'stroke-width="2.2"'),
  left: svg('<path d="M10 3 5 8l5 5"/>', '0 0 16 16', 'stroke-width="2"'),
  right: svg('<path d="m6 3 5 5-5 5"/>', '0 0 16 16', 'stroke-width="2"'),
  calendar: svg('<rect x="2" y="3" width="12" height="11" rx="2"/><path d="M2 6.5h12"/>', '0 0 16 16', 'stroke-width="1.6"'),
  up: svg('<path d="m4 10 4-4 4 4"/>', '0 0 16 16', 'stroke-width="2"'),
  down: svg('<path d="m4 6 4 4 4-4"/>', '0 0 16 16', 'stroke-width="2"'),
};

/** The logo: a bumper plate with three grip holes (in the current text colour). Each copy gets its own mask id,
    because a mask inside a hidden copy (the rail on a phone) would hide every other copy that points to it. */
let logoSeq = 0;
BO.logoSVG = function logoSVG() {
  const id = 'bo-plate-' + (++logoSeq);
  return `<svg viewBox="0 0 32 32" aria-hidden="true"><mask id="${id}"><rect width="32" height="32" fill="#fff"/><circle cx="16" cy="16" r="3.2" fill="#000"/><circle cx="16" cy="16" r="10.6" fill="none" stroke="#000" stroke-width=".9"/><g fill="#000"><rect x="13.4" y="5.2" width="5.2" height="3" rx="1.5"/><rect x="13.4" y="5.2" width="5.2" height="3" rx="1.5" transform="rotate(120 16 16)"/><rect x="13.4" y="5.2" width="5.2" height="3" rx="1.5" transform="rotate(240 16 16)"/></g></mask><circle cx="16" cy="16" r="14" fill="currentColor" mask="url(#${id})"/></svg>`;
};
BO.brandHTML = () => `<span class="brand"><span class="brand-mark">${BO.logoSVG()}</span><strong>BODYOPS</strong></span>`;

/* ---------- theme ---------- */

BO.setTheme = function setTheme(theme) {
  document.documentElement.dataset.theme = theme;
  try { localStorage.setItem('bodyops.theme', theme); } catch (e) { }
  const dark = theme === 'dark' || (theme === 'system' && !matchMedia('(prefers-color-scheme: light)').matches);
  document.querySelector('meta[name="theme-color"]').setAttribute('content', dark ? '#0B0F14' : '#F2F5F7');
};
BO.theme = () => document.documentElement.dataset.theme || 'dark';

/* ---------- download ---------- */

BO.download = function download(name, text) {
  const url = URL.createObjectURL(new Blob([text], { type: 'application/json' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
};

/** Reads a number typed with a comma or a dot; null when empty or not a number. */
BO.readNumber = function readNumber(value) {
  const text = String(value == null ? '' : value).trim().replace(',', '.');
  if (text === '') return null;
  const n = Number(text);
  return Number.isFinite(n) ? n : null;
};
