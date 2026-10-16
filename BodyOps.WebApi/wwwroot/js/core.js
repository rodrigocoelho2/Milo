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
  const waiting = timers.get(key);
  if (waiting) clearTimeout(waiting.id);
  timers.set(key, { fn, id: setTimeout(() => { timers.delete(key); fn(); }, wait) });
  BO.setSync('saving');
};
/** Runs every waiting autosave now (before leaving a screen), so nothing typed is lost. */
BO.flush = function flush() {
  const waiting = [...timers.values()];
  timers.clear();
  return Promise.all(waiting.map(w => { clearTimeout(w.id); try { return Promise.resolve(w.fn()).catch(() => { }); } catch (e) { return null; } }));
};
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
/** 1st, 2nd, 3rd, 4th … 11th, 12th, 13th … 21st, 22nd. */
BO.ordinal = n => n + ((n % 100 >= 11 && n % 100 <= 13) ? 'th' : ({ 1: 'st', 2: 'nd', 3: 'rd' }[n % 10] || 'th'));
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

/** "Good morning" … "Good evening" for the hour now. */
BO.greeting = () => { const h = new Date().getHours(); return h < 5 ? 'Good night' : h < 12 ? 'Good morning' : h < 18 ? 'Good afternoon' : 'Good evening'; };
/** The time now as "HH:mm". */
BO.nowTime = () => { const d = new Date(); return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`; };
/** Seconds as "1:05". */
BO.clock = secs => { const s = Math.max(0, Math.round(secs)); return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`; };

/* ---------- day score (same rules as WeekBuilder.DayScore on the server, so ticks update it at once) ---------- */

BO.STREAK_AT = 60;
/** Fills setsDone, setsPlanned and score on a day from what it holds. */
BO.scoreDay = function scoreDay(day) {
  let done = 0, planned = 0;
  day.exercises.forEach(ex => {
    if (ex.type === 'cardio') { planned++; if ((ex.cardioMinutes || 0) > 0 || (ex.cardioKm || 0) > 0) done++; return; }
    const target = Math.max(1, ex.targets.length);
    planned += target;
    done += Math.min(target, ex.sets.filter(s => s != null).length);
  });
  if (day.trainingState === 'done' && done === 0) done = planned;
  // Training 60, meals 40. The weigh-in is on the timeline but not in the score (people weigh in every week or two).
  let total = 0, weight = 0;
  if (planned > 0) { weight += 60; total += day.trainingState === 'missed' ? 0 : 60 * done / planned; }
  if (day.meals.length) { weight += 40; total += 40 * day.meals.filter(m => m.eaten).length / day.meals.length; }
  day.setsDone = done;
  day.setsPlanned = planned;
  day.score = weight ? Math.round(100 * total / weight) : 0;
  return day;
};
/** Whether a day takes part in the streak (training or meals planned; a weigh-in alone doesn't count). */
BO.dayCounts = day => day.setsPlanned > 0 || day.meals.length > 0;
/** The streak shown now: the run up to yesterday, plus today once it reaches the bar. */
BO.streakNow = (streak, today) => streak ? streak.throughYesterday + (today && BO.dayCounts(today) && today.score >= BO.STREAK_AT ? 1 : 0) : 0;

/* ---------- shared data (loaded once, refreshed by the screens that change it) ---------- */

BO.cache = {};
BO.settings = async function settings(fresh) {
  if (fresh || !BO.cache.settings) BO.cache.settings = await BO.api('GET', '/api/settings');
  return BO.cache.settings;
};
BO.saveSettings = async function saveSettings(changes) {
  const current = await BO.settings();
  BO.cache.settings = await BO.saving(BO.api('PUT', '/api/settings', Object.assign({}, current, changes)));
  return BO.cache.settings;
};

/* ---------- muscles ---------- */

/** The sets of an exercise on one line: "12 · 10 · 8", or "60×12 · 65×10 · 70×8" when the loads differ. */
BO.setsLine = (reps, loads) => {
  const ls = (loads || []).filter((v, i) => v != null && reps[i] != null);
  const varied = ls.length > 1 && ls.some(v => v !== ls[0]);
  return reps.map((r, i) => r == null ? '–' : varied && loads[i] != null ? `${BO.load(loads[i])}×${r}` : r).join(' · ');
};

/** Kinds of cardio (CardioKind on the server). Treadmill, elliptical and stairs are logged by time only. */
BO.CARDIO = { treadmill: 'Treadmill', bike: 'Bike', run: 'Run', walk: 'Walk', elliptical: 'Elliptical', rowing: 'Rowing', stairs: 'Stairs', swim: 'Swim', other: 'Other' };
BO.cardioDistance = kind => !['treadmill', 'elliptical', 'stairs'].includes(kind);

BO.MUSCLES = {
  chest: 'Chest', shoulders: 'Shoulders', biceps: 'Biceps', triceps: 'Triceps', forearms: 'Forearms', abs: 'Abs',
  lats: 'Lats', traps: 'Traps', lowerBack: 'Lower back', glutes: 'Glutes', quads: 'Quads', hamstrings: 'Hamstrings', calves: 'Calves',
};

/* ---------- icons ---------- */

const svg = (body, view = '0 0 24 24', extra = '') => `<svg viewBox="${view}" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" ${extra}>${body}</svg>`;
BO.icons = {
  today: svg('<circle cx="12" cy="12" r="8.5"/><path d="M12 7.5V12l3 2"/>'),
  week: svg('<rect x="3.5" y="4.5" width="17" height="16" rx="4"/><path d="M8 3v3M16 3v3M8 14v3M12 11v6M16 13v4"/>'),
  progress: svg('<path d="M3.5 17 9 11.5l3.5 3.5L20.5 7"/><path d="M15 7h5.5v5.5"/>'),
  plan: svg('<path d="M2.5 12h2M19.5 12h2M6.5 7.5v9M17.5 7.5v9M4.5 9.5v5M19.5 9.5v5M6.5 12h11"/>', '0 0 24 24', 'stroke-width="2.2"'),
  settings: svg('<circle cx="12" cy="12" r="3"/><path d="M12 3v2.5M12 18.5V21M3 12h2.5M18.5 12H21M5.6 5.6l1.8 1.8M16.6 16.6l1.8 1.8M5.6 18.4l1.8-1.8M16.6 7.4l1.8-1.8"/>'),
  plus: svg('<path d="M12 5v14M5 12h14"/>', '0 0 24 24', 'stroke-width="2.4"'),
  check: svg('<path d="M5 12.5 10 17l9-10"/>', '0 0 24 24', 'stroke-width="3"'),
  x: svg('<path d="M6 6l12 12M18 6 6 18"/>', '0 0 24 24', 'stroke-width="2.2"'),
  left: svg('<path d="m15 5-7 7 7 7"/>', '0 0 24 24', 'stroke-width="2.4"'),
  right: svg('<path d="m9 5 7 7-7 7"/>', '0 0 24 24', 'stroke-width="2.4"'),
  down: svg('<path d="m6 9 6 6 6-6"/>', '0 0 24 24', 'stroke-width="2.4"'),
  arrow: svg('<path d="M5 12h14M13 6l6 6-6 6"/>', '0 0 24 24', 'stroke-width="2.4"'),
  trend: svg('<path d="M3.5 17 9 11.5l3.5 3.5L20.5 7"/>', '0 0 24 24', 'stroke-width="2.4"'),
  flame: '<svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M12.6 2.2c.4 3.1-1.3 4.6-2.9 6.3C8 10.3 6.5 12 6.5 14.8A5.5 5.5 0 0 0 12 20.5a5.5 5.5 0 0 0 5.5-5.6c0-2.6-1.4-4.1-2.3-5.1-.2 1.4-.9 2.3-1.8 2.7.6-3.4-.1-7.3-.8-10.3Z"/></svg>',
  medal: '<svg viewBox="0 0 24 24" fill="none" stroke="#2B1D00" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M8 3h8l-2 6h-4z"/><circle cx="12" cy="15" r="6"/><path d="m12 12.2.9 1.8 2 .3-1.5 1.4.4 2-1.8-1-1.8 1 .4-2-1.5-1.4 2-.3z" fill="#2B1D00" stroke-width="1"/></svg>',
  scale: svg('<rect x="3.5" y="3.5" width="17" height="17" rx="4.5"/><path d="M8 10a4 4 0 0 1 8 0M12 10l1.6-1.8"/>'),
  bowl: svg('<path d="M3.5 11h17a8.5 8.5 0 0 1-17 0ZM8 7c0-1.5 1-1.5 1-3M12 7c0-1.5 1-1.5 1-3M16 7c0-1.5 1-1.5 1-3"/>'),
  moon: svg('<path d="M20 14.5A8 8 0 1 1 9.5 4a6.5 6.5 0 0 0 10.5 10.5Z"/>'),
  user: svg('<circle cx="12" cy="8.5" r="4"/><path d="M4.5 20a7.5 7.5 0 0 1 15 0"/>'),
  lock: svg('<rect x="4.5" y="10.5" width="15" height="10" rx="3"/><path d="M8 10.5V8a4 4 0 0 1 8 0v2.5"/>'),
  eye: svg('<path d="M2.5 12S6 5.5 12 5.5 21.5 12 21.5 12 18 18.5 12 18.5 2.5 12 2.5 12Z"/><circle cx="12" cy="12" r="3"/>'),
  target: svg('<circle cx="12" cy="12" r="8.5"/><circle cx="12" cy="12" r="4.5"/><circle cx="12" cy="12" r=".8" fill="currentColor"/>'),
  info: svg('<circle cx="12" cy="12" r="9"/><path d="M12 11v5M12 7.6v.1"/>', '0 0 24 24', 'stroke-width="2.2"'),
  server: svg('<rect x="3.5" y="4" width="17" height="7" rx="2.5"/><rect x="3.5" y="13" width="17" height="7" rx="2.5"/><path d="M7.5 7.5h.01M7.5 16.5h.01"/>'),
  phone: svg('<rect x="6.5" y="2.5" width="11" height="19" rx="3"/><path d="M11 18.5h2"/>'),
  people: svg('<circle cx="9" cy="9" r="3.5"/><path d="M2.5 19.5a6.5 6.5 0 0 1 13 0M16 5.8a3.5 3.5 0 0 1 0 6.4M18.5 19.5a6.5 6.5 0 0 0-2.5-5.1"/>'),
  copy: svg('<rect x="8" y="8" width="12" height="12" rx="3"/><path d="M16 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2"/>'),
  grip: svg('<path d="M9 6h.01M9 12h.01M9 18h.01M15 6h.01M15 12h.01M15 18h.01"/>', '0 0 24 24', 'stroke-width="3"'),
  bolt: svg('<path d="M13 2.5 4.5 13.5H11l-1 8 8.5-11H12z"/>'),
};

/** The logo: a bumper plate with three grip holes (in the current text colour). Each copy gets its own mask id,
    because a mask inside a hidden copy would hide every other copy that points to it. */
let logoSeq = 0;
BO.logoSVG = function logoSVG() {
  const id = 'bo-plate-' + (++logoSeq);
  return `<svg viewBox="0 0 32 32" aria-hidden="true"><mask id="${id}"><rect width="32" height="32" fill="#fff"/><circle cx="16" cy="16" r="3.2" fill="#000"/><circle cx="16" cy="16" r="10.6" fill="none" stroke="#000" stroke-width=".9"/><g fill="#000"><rect x="13.4" y="5.2" width="5.2" height="3" rx="1.5"/><rect x="13.4" y="5.2" width="5.2" height="3" rx="1.5" transform="rotate(120 16 16)"/><rect x="13.4" y="5.2" width="5.2" height="3" rx="1.5" transform="rotate(240 16 16)"/></g></mask><circle cx="16" cy="16" r="14" fill="currentColor" mask="url(#${id})"/></svg>`;
};
/** Why the app is called Milo (shown, faintly, at the foot of the landing page and of Settings). */
BO.MILO = 'Milo of Croton lifted a newborn calf onto his shoulders and carried it every day. The calf grew, and so did he; when it was a bull, he was still carrying it.';

/** The easter egg behind that line: the drawing and the whole story. */
BO.miloStory = function miloStory() {
  BO.sheet((body, close) => {
    body.innerHTML = `<div class="milo-drawing" role="img" aria-label="Milo of Croton carrying a calf, and later the same calf grown into a bull"></div>
      <div class="milo-calf">1 calf <i>→</i> 1 bull</div>
      <blockquote>Milo of Croton lifted a newborn calf onto his shoulders and carried it every day. The calf grew, and so did he. When it was a bull, he was still carrying it.</blockquote>
      <span class="who">A little more every week. That's the whole idea.</span>
      <button type="button" class="btn ink lg block" data-close-story>Keep going</button>`;
    body.querySelector('[data-close-story]').addEventListener('click', close);
  }, { label: 'Milo of Croton', cls: 'milo-sheet' });
};

BO.brandHTML = () => `<span class="brand"><span class="brand-mark">${BO.logoSVG()}</span>Milo</span>`;

/* ---------- theme ---------- */

/** 'light', 'dark' or 'system' (follow the device). */
BO.setTheme = function setTheme(theme) {
  document.documentElement.dataset.theme = theme;
  try { localStorage.setItem('bodyops.theme', theme); } catch (e) { }
  BO.paintThemeColor();
};
BO.theme = () => document.documentElement.dataset.theme || 'system';
BO.isDark = () => BO.theme() === 'dark' || (BO.theme() === 'system' && matchMedia('(prefers-color-scheme: dark)').matches);
BO.paintThemeColor = function paintThemeColor(color) {
  const meta = document.querySelector('meta[name="theme-color"]');
  if (meta) meta.setAttribute('content', color || (BO.isDark() ? '#0E1020' : '#EEF0F4'));
};
matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => BO.paintThemeColor());

/** The light/dark switch on the landing and sign-in pages: sun while dark, moon while light (CSS picks the icon). */
BO.themeToggleHTML = () => `<button type="button" class="theme-toggle" data-theme-toggle aria-label="Switch light or dark mode" title="Light / dark">
  <svg class="tt-sun" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="4.2"/><path d="M12 2.5v2.2M12 19.3v2.2M4.6 4.6l1.6 1.6M17.8 17.8l1.6 1.6M2.5 12h2.2M19.3 12h2.2M4.6 19.4l1.6-1.6M17.8 6.2l1.6-1.6"/></svg>
  <svg class="tt-moon" viewBox="0 0 24 24" aria-hidden="true"><path d="M20 14.6A8.3 8.3 0 1 1 9.4 4a6.6 6.6 0 0 0 10.6 10.6Z"/></svg></button>`;
/** Flips to the other mode from what is on screen now (also when following the device), and keeps the choice. */
BO.toggleTheme = function toggleTheme() {
  const flip = () => BO.setTheme(BO.isDark() ? 'light' : 'dark');
  if (document.startViewTransition && !matchMedia('(prefers-reduced-motion: reduce)').matches) document.startViewTransition(flip);
  else flip();
};

/* ---------- small drawings ---------- */

/** A progress ring (0–1) with a label in the middle. */
BO.ring = function ring(p, label, { size = 76, stroke = 8, color = '#fff', track = 'rgba(255,255,255,.2)', text = '#fff', cls = 'ring' } = {}) {
  const r = 38 - stroke / 2, c = 2 * Math.PI * r;
  const off = c * (1 - Math.max(0, Math.min(1, p)));
  return `<svg class="${cls}" viewBox="0 0 76 76" width="${size}" height="${size}" aria-hidden="true"><circle cx="38" cy="38" r="${r}" fill="none" stroke="${track}" stroke-width="${stroke}"/><circle class="arc" cx="38" cy="38" r="${r}" fill="none" stroke="${color}" stroke-width="${stroke}" stroke-linecap="round" stroke-dasharray="${c.toFixed(1)}" stroke-dashoffset="${off.toFixed(1)}" transform="rotate(-90 38 38)" style="transition:stroke-dashoffset .6s var(--ease)"/><text x="38" y="43" text-anchor="middle" fill="${text}" style="font:800 15px var(--font)">${BO.esc(label)}</text></svg>`;
};

/** A small trend line. values: numbers, oldest first. */
BO.sparkline = function sparkline(values, color = 'var(--train)', { width = 84, height = 30, fill = false } = {}) {
  if (!values.length) return '';
  const pts = values.length === 1 ? [values[0], values[0]] : values;
  const lo = Math.min(...pts), hi = Math.max(...pts);
  const xs = pts.map((_, i) => 3 + i * (width - 6) / (pts.length - 1));
  const ys = pts.map(v => hi === lo ? height / 2 : 4 + (hi - v) / (hi - lo) * (height - 8));
  const d = xs.map((x, i) => `${i ? 'L' : 'M'}${x.toFixed(1)} ${ys[i].toFixed(1)}`).join(' ');
  return `<svg viewBox="0 0 ${width} ${height}" preserveAspectRatio="none" aria-hidden="true">${fill ? `<path d="${d} L${xs.at(-1)} ${height} L${xs[0]} ${height}Z" fill="${color}" opacity=".15"/>` : ''}<path d="${d}" fill="none" stroke="${color}" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" vector-effect="non-scaling-stroke"/><circle cx="${xs.at(-1)}" cy="${ys.at(-1)}" r="3.5" fill="${color}"/></svg>`;
};

/** Confetti over the whole page for a moment (records, a full day). */
BO.confetti = function confetti() {
  if (matchMedia('(prefers-reduced-motion: reduce)').matches) return;
  const c = document.createElement('canvas');
  c.className = 'confetti';
  document.body.appendChild(c);
  const dpr = Math.min(2, window.devicePixelRatio || 1);
  c.width = innerWidth * dpr; c.height = innerHeight * dpr;
  const ctx = c.getContext('2d');
  const cols = ['#3355FF', '#FF7A2F', '#8A5CF6', '#FFC53D', '#10A765'];
  const bits = Array.from({ length: 160 }, () => ({
    x: c.width / 2 + (Math.random() - .5) * c.width * .3, y: c.height * .42,
    vx: (Math.random() - .5) * 30 * dpr, vy: (-Math.random() * 26 - 8) * dpr,
    s: (6 + Math.random() * 8) * dpr, a: Math.random() * 6, va: (Math.random() - .5) * .4, col: cols[Math.random() * cols.length | 0],
  }));
  let f = 0;
  (function draw() {
    ctx.clearRect(0, 0, c.width, c.height);
    bits.forEach(b => { b.x += b.vx; b.y += b.vy; b.vy += .9 * dpr; b.vx *= .985; b.a += b.va; ctx.save(); ctx.translate(b.x, b.y); ctx.rotate(b.a); ctx.fillStyle = b.col; ctx.fillRect(-b.s / 2, -b.s / 4, b.s, b.s / 2); ctx.restore(); });
    if (++f < 150) requestAnimationFrame(draw); else c.remove();
  })();
};

/** A short buzz on phones that support it. */
BO.buzz = pattern => { try { if (navigator.vibrate) navigator.vibrate(pattern || 12); } catch (e) { } };

/** Counts a number up inside an element (the day score). */
BO.countUp = function countUp(el, to, { from = 0, suffix = '', duration = 600 } = {}) {
  if (!el) return;
  if (matchMedia('(prefers-reduced-motion: reduce)').matches || from === to) { el.innerHTML = `${to}${suffix}`; return; }
  const t0 = performance.now();
  const step = now => {
    const k = Math.min(1, (now - t0) / duration);
    const e = 1 - Math.pow(1 - k, 3);
    el.innerHTML = `${Math.round(from + (to - from) * e)}${suffix}`;
    if (k < 1) requestAnimationFrame(step);
  };
  requestAnimationFrame(step);
};

/* ---------- motion on scroll ---------- */

BO.reducedMotion = () => matchMedia('(prefers-reduced-motion: reduce)').matches;

/** The blocks of a screen that slide in as they scroll into view (only the outermost of nested ones). */
const REVEAL = '.page-head, .card, .hero, .tile, .ti, .wk, .set-card, .drow, .empty, .lift, .dchips';

/** Fades and lifts a freshly drawn screen's blocks in as they are scrolled to. What is already on screen keeps its
    own first-paint animation (.rise). Once in, an element drops the classes, so re-renders never animate again. */
BO.revealIn = function revealIn(root) {
  if (!root || BO.reducedMotion() || !('IntersectionObserver' in window)) return;
  const fold = window.innerHeight * 0.94;
  const all = [...root.querySelectorAll(REVEAL)];
  const items = all
    .filter(el => !all.some(other => other !== el && other.contains(el)))
    .filter(el => el.getBoundingClientRect().top > fold);
  if (!items.length) return;
  items.forEach(el => el.classList.remove('rise'));

  const settle = el => { el.classList.remove('rv', 'in'); el.style.removeProperty('--d'); };
  const show = (el, delay) => {
    el.style.setProperty('--d', `${delay}ms`);
    el.classList.add('in');
    const end = event => {
      if (event.target !== el || event.propertyName !== 'transform') return;
      el.removeEventListener('transitionend', end);
      settle(el);
    };
    el.addEventListener('transitionend', end);
    setTimeout(() => settle(el), 1200 + delay);
  };

  const io = new IntersectionObserver(entries => {
    let n = 0;
    entries.forEach(entry => {
      if (!entry.isIntersecting) return;
      io.unobserve(entry.target);
      show(entry.target, Math.min(n++, 8) * 55);
    });
  }, { rootMargin: '0px 0px -6% 0px', threshold: 0.06 });

  items.forEach(el => { el.classList.add('rv'); io.observe(el); });
};

/** Calls fn(scrollY) on every animation frame the page scrolls, until fn returns false. */
BO.onScrollFrame = function onScrollFrame(fn) {
  if (BO.reducedMotion()) return;
  let queued = false;
  const tick = () => {
    queued = false;
    if (fn(window.scrollY) === false) window.removeEventListener('scroll', onScroll);
  };
  const onScroll = () => { if (!queued) { queued = true; requestAnimationFrame(tick); } };
  window.addEventListener('scroll', onScroll, { passive: true });
  tick();
};

/* ---------- sheets ---------- */

/** Opens a bottom sheet (a centred card on a computer). render(el, close) fills it; returns close(). */
BO.sheet = function sheet(render, { label = 'Dialog', cls = '' } = {}) {
  const back = document.createElement('div');
  back.className = 'sheet-back';
  back.innerHTML = `<div class="sheet ${cls}" role="dialog" aria-modal="true" aria-label="${BO.esc(label)}"><span class="grab"></span><div class="sheet-body" style="display:contents"></div></div>`;
  document.body.appendChild(back);
  document.body.style.overflow = 'hidden';
  const box = back.querySelector('.sheet');
  const body = back.querySelector('.sheet-body');
  let closed = false;
  const close = () => {
    if (closed) return;
    closed = true;
    back.classList.add('closing');
    document.body.style.overflow = '';
    document.removeEventListener('keydown', onKey);
    setTimeout(() => back.remove(), 220);
  };
  const onKey = e => { if (e.key === 'Escape') close(); };
  document.addEventListener('keydown', onKey);
  back.addEventListener('click', e => { if (e.target === back) close(); });
  // Drag the grab handle down to close.
  let startY = null;
  box.addEventListener('touchstart', e => { if (box.scrollTop <= 0) startY = e.touches[0].clientY; }, { passive: true });
  box.addEventListener('touchmove', e => {
    if (startY == null) return;
    const dy = e.touches[0].clientY - startY;
    if (dy > 0) box.style.transform = `translateY(${dy}px)`;
  }, { passive: true });
  box.addEventListener('touchend', e => {
    if (startY == null) return;
    const dy = (e.changedTouches[0].clientY - startY);
    startY = null;
    if (dy > 90) close(); else box.style.transform = '';
  });
  render(body, close, box);
  return close;
};

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
