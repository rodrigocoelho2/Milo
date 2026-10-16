/* BodyOps — Session mode: one exercise at a time on the whole screen, at the gym.
   Big load buttons for the next set (each set keeps its own load, for progressive loading), last time's numbers to
   beat, set tiles, a rest timer that starts by itself and the record moment.
   /session?date=yyyy-MM-dd&ex=<planExerciseId>. */
'use strict';

(() => {
  const { esc, icons } = BO;
  const store = {
    get(key) { try { return JSON.parse(localStorage.getItem(key)); } catch (e) { return null; } },
    set(key, value) { try { value == null ? localStorage.removeItem(key) : localStorage.setItem(key, JSON.stringify(value)); } catch (e) { } },
  };

  BO.screens.session = async function session(query) {
    document.querySelectorAll('.session').forEach(el => el.remove());
    const [first, settings] = await Promise.all([
      BO.api('GET', '/api/days/week' + (query.date ? `?date=${encodeURIComponent(query.date)}` : '')),
      BO.settings().catch(() => ({ restSeconds: 90 })),
    ]);
    let week = first;
    const date = query.date && week.days.some(d => d.date === query.date) ? query.date : week.today;
    const day = () => week.days.find(d => d.date === date);
    if (!day() || !day().exercises.length) { BO.go('/today', { replace: true }); return; }

    const restDefault = settings.restSeconds || 90;
    const startKey = `bodyops.session.${date}`;
    const celebrated = new Set(store.get(`bodyops.prs.${date}`) || []);
    let idx = Math.max(0, day().exercises.findIndex(ex => String(ex.planExerciseId) === String(query.ex)));
    if (!query.ex) { const next = BO.nextExercise(day()); idx = next ? day().exercises.indexOf(next) : 0; }
    let reps = null;          // reps for the next set, chosen with − / +
    let nextLoad = null;      // load for the next set, chosen with the big − / + (null = suggest one)
    let finished = false;
    let wakeLock = null;

    const el = document.createElement('div');
    el.className = 'session';
    el.setAttribute('role', 'dialog');
    el.setAttribute('aria-label', 'Session');
    document.body.appendChild(el);
    document.body.style.overflow = 'hidden';
    BO.paintThemeColor(getComputedStyle(el).backgroundColor);
    try { if ('wakeLock' in navigator) wakeLock = await navigator.wakeLock.request('screen'); } catch (e) { }

    const ex = () => day().exercises[idx];
    const isCardio = e => e.type === 'cardio';
    const count = e => Math.max(e.targets.length, e.sets.length, 1);
    const doneSets = e => e.sets.filter(s => s != null).length;
    const complete = e => isCardio(e) ? (e.cardioMinutes || 0) > 0 || (e.cardioKm || 0) > 0 : doneSets(e) >= Math.max(1, e.targets.length);
    const nextOpen = e => { for (let i = 0; i < count(e); i++) if (e.sets[i] == null) return i; return -1; };
    /** A rep range ("3x8-12") turns on progression: its bottom and top for set i, or null for fixed reps. */
    const rangeOf = (e, i) => {
      if (e.stepKg == null) return null;
      const max = e.targets[i] || Math.max(0, ...e.targets);
      const min = (e.targetsMin || [])[i] ?? max;
      return min < max ? { min, max } : null;
    };
    const earnedNow = e => e.stepKg != null && e.targets.length > 0 && e.targets.every((t, i) => e.sets[i] != null && e.sets[i] >= t);
    const stepUp = e => e.last && e.last.earned && e.stepKg != null;
    const defaultReps = (e, i) => {
      const range = rangeOf(e, i);
      if (range) {
        // After a step up, start at the bottom; otherwise try one more rep than last time, up to the top.
        if (stepUp(e)) return range.min;
        const before = e.last && e.last.reps[i];
        return before ? Math.max(range.min, Math.min(range.max, before + 1)) : range.max;
      }
      return e.targets[i] || (e.last && e.last.reps[i]) || e.sets.slice(0, i).reverse().find(s => s != null) || 10;
    };
    const loadsOf = e => { if (!Array.isArray(e.setLoads)) e.setLoads = []; while (e.setLoads.length < e.sets.length) e.setLoads.push(null); return e.setLoads; };
    /** The load to suggest for set i: that set last time, else the set before it today, else last time's, else today's.
        When last time completed the rep range, last time's loads go up by the exercise's step. */
    const suggestLoad = (e, i) => {
      const up = stepUp(e) ? e.stepKg : 0;
      const lastSet = e.last && e.last.loads ? e.last.loads[i] : null;
      if (lastSet != null) return lastSet + up;
      const before = loadsOf(e).slice(0, i).reverse().find(v => v != null);
      if (before != null) return before;
      return e.last && e.last.loadKg != null ? e.last.loadKg + up : e.loadKg;
    };
    const topLoad = e => { const ls = loadsOf(e).filter((v, i) => v != null && e.sets[i] != null); return ls.length ? Math.max(...ls) : e.loadKg; };
    const kgText = v => v != null ? ` · ${BO.load(v)} kg` : '';

    function close() {
      clearInterval(tick);
      try { if (wakeLock) wakeLock.release(); } catch (e) { }
      document.body.style.overflow = '';
      el.remove();
      BO.paintThemeColor();
      if (location.pathname === '/session') BO.go('/today' + (date !== week.today ? `?date=${date}` : ''), { replace: true });
    }

    /* ---------- saving ---------- */

    function save(e) {
      if (!store.get(startKey)) store.set(startKey, Date.now());
      BO.debounce(`session:${date}:${e.planExerciseId}`, 400, async () => {
        const body = isCardio(e) ? { cardioMinutes: e.cardioMinutes, cardioKm: e.cardioKm } : { loadKg: e.loadKg, sets: e.sets, setLoads: loadsOf(e) };
        try {
          const updated = await BO.saving(BO.api('PUT', `/api/days/${date}/exercises/${e.planExerciseId}`, body));
          if (updated && updated.days && !BO.busy()) {
            updated.days.forEach(BO.scoreDay);
            week = updated;
            if (el.isConnected) render();
          }
        } catch (err) { /* the toast explains */ }
      });
    }

    /* ---------- rest timer (kept in storage so it survives locking the phone) ---------- */

    const restLeft = () => { const r = store.get('bodyops.rest'); return r && r.date === date ? (r.end - Date.now()) / 1000 : null; };
    function startRest(afterSet) {
      store.set('bodyops.rest', { date, end: Date.now() + restDefault * 1000, total: restDefault, afterSet, name: ex().name });
    }
    let buzzed = false;
    const tick = setInterval(() => {
      if (!el.isConnected) { clearInterval(tick); return; }
      const start = store.get(startKey);
      const clockEl = el.querySelector('[data-clock]');
      if (clockEl && start) clockEl.textContent = BO.clock((Date.now() - start) / 1000);
      const r = store.get('bodyops.rest');
      const restEl = el.querySelector('[data-rest]');
      if (!r || r.date !== date || !restEl) return;
      const left = (r.end - Date.now()) / 1000;
      if (left <= 0) {
        if (!buzzed) { buzzed = true; BO.buzz([60, 80, 60]); render(); }
        return;
      }
      buzzed = false;
      const arc = restEl.querySelector('.arc');
      if (arc) {
        const c = Number(arc.getAttribute('stroke-dasharray'));
        arc.setAttribute('stroke-dashoffset', (c * (1 - left / r.total)).toFixed(1));
      }
      const text = restEl.querySelector('text');
      if (text) text.textContent = BO.clock(left);
    }, 250);

    /* ---------- pieces ---------- */

    function beatLine(e) {
      if (isCardio(e)) return e.last ? '' : '';
      const range = rangeOf(e, 0);
      if (range) {
        if (stepUp(e)) return `Up ${BO.load(e.stepKg)} kg: you earned it. Aim for ${range.min}+ reps`;
        return `${range.min}–${range.max} reps · ${range.max} on every set moves you up`;
      }
      const target = Math.max(0, ...e.targets);
      if (!e.last) return `First time: find a load you can do ${target || 10} reps with`;
      const hitAll = e.last.reps.length && e.targets.length && e.targets.every((t, i) => (e.last.reps[i] || 0) >= t);
      const varied = (e.last.loads || []).filter(v => v != null).some((v, _, all) => v !== all[0]);
      if (hitAll && varied) return 'Beat it: add 2.5 kg to each set';
      if (hitAll && e.last.loadKg != null) return `Beat it: try ${BO.load(e.last.loadKg + 2.5)} kg`;
      return `Beat it: ${target || Math.max(...e.last.reps, 0) + 1} reps on every set`;
    }

    function lastLine(e) {
      if (isCardio(e)) return `${BO.CARDIO[e.cardioKind] || 'Cardio'} · ${e.cardioDistance ? 'time and distance' : 'time only'}`;
      if (!e.last) return e.notes ? esc(e.notes) : 'No earlier session logged';
      const loads = (e.last.loads || []).filter(v => v != null);
      if (loads.length > 1 && loads.some(v => v !== loads[0])) {
        // Progressive sets: each set's load × reps.
        return `Last time · ${BO.shortDate(e.last.date)} · ${e.last.reps.map((r, i) => `${e.last.loads[i] != null ? BO.load(e.last.loads[i]) : '–'}×${r}`).join(' · ')}`;
      }
      const reps = e.last.reps.length ? e.last.reps.join(', ') : '';
      return `Last time · ${BO.shortDate(e.last.date)}${e.last.loadKg != null ? ` · ${BO.load(e.last.loadKg)} kg` : ''}${reps ? ` · ${reps}` : ''}`;
    }

    function restCard(e) {
      const r = store.get('bodyops.rest');
      if (!r || r.date !== date) return '';
      const left = restLeft();
      if (left <= 0) {
        const next = nextOpen(e);
        return `<div class="s-rest over" data-rest>${BO.ring(1, 'Go', { color: '#fff' })}<div><b>${next >= 0 ? `Set ${next + 1}: go` : 'Rested'}</b><div style="font-size:13px;opacity:.9">Rest is over</div>
          <div class="acts"><button type="button" class="s-btn" data-rest-skip>Hide</button></div></div></div>`;
      }
      return `<div class="s-rest" data-rest>${BO.ring(left / r.total, BO.clock(left))}<div><b>Rest</b><div style="font-size:13px;opacity:.85">After set ${r.afterSet} · ${BO.clock(r.total)} planned</div>
        <div class="acts"><button type="button" class="s-btn" data-rest-add>+30 s</button><button type="button" class="s-btn" data-rest-skip>Skip rest</button></div></div></div>`;
    }

    function strengthBody(e) {
      const n = count(e);
      const open = nextOpen(e);
      const ls = loadsOf(e);
      if (open >= 0 && nextLoad == null) nextLoad = suggestLoad(e, open);
      const loadValue = nextLoad != null ? BO.load(nextLoad) : '';
      const tiles = Array.from({ length: n }, (_, i) => {
        const v = e.sets[i];
        if (v != null) return `<button type="button" class="s-set done" data-set="${i}" aria-label="Set ${i + 1}: ${v} reps${ls[i] != null ? ` with ${BO.load(ls[i])} kg` : ''}. Tap to change."><small>Set ${i + 1} · done</small><b class="num">${v}</b><small>reps${kgText(ls[i])}</small></button>`;
        const kg = i === open ? nextLoad : suggestLoad(e, i);
        const range = rangeOf(e, i);
        const goal = range ? `<b class="num rng">${range.min}–${range.max}</b>` : `<b class="num">${e.targets[i] || defaultReps(e, i)}</b>`;
        return `<button type="button" class="s-set${i === open ? ' now' : ''}" data-set="${i}" aria-label="Log set ${i + 1}"><small>Set ${i + 1}${i === open ? ' · now' : ''}</small>${goal}<small>reps${kgText(kg)}</small></button>`;
      }).join('') + (n < 20 ? '<button type="button" class="s-set add" data-add-set aria-label="Add a set">+</button>' : '');
      if (open >= 0 && reps == null) reps = defaultReps(e, open);
      const action = open >= 0
        ? `<div class="log-set"><div class="reps"><button type="button" data-reps="-1" aria-label="One rep less">−</button><span class="num" aria-live="polite">${reps}<small>reps</small></span><button type="button" data-reps="1" aria-label="One rep more">+</button></div><button type="button" class="log-btn" data-log>Log set ${open + 1}</button></div>`
        : `<button type="button" class="log-set" data-next><b>${day().exercises.some((x, j) => j !== idx && !complete(x)) ? 'Next exercise' : 'Finish session'}</b><span class="log-btn" style="display:inline-flex;gap:6px;align-items:center">${icons.arrow}</span></button>`;
      const loadPad = open >= 0 ? `
        <div class="load-big">
          <button type="button" data-step="-1" aria-label="2.5 kg less">−</button>
          <label class="v"><input class="num" inputmode="decimal" value="${loadValue}" placeholder="kg" data-load aria-label="Load for set ${open + 1} in kg"><small>kg for set ${open + 1} · steps of 2.5</small></label>
          <button type="button" data-step="1" aria-label="2.5 kg more">+</button>
        </div>` : '';
      return `${loadPad}
        <div class="s-sets">${tiles}</div>
        ${restCard(e)}
        ${action}`;
    }

    function cardioBody(e) {
      // Time only for a treadmill, elliptical or stairs; time and distance for the rest (kept if one was typed before).
      const km = e.cardioDistance || (e.cardioKm || 0) > 0;
      return `<div class="cardio-in${km ? '' : ' one'}">
          <label>Minutes<input class="num" inputmode="decimal" value="${e.cardioMinutes ?? ''}" placeholder="0" data-cardio="cardioMinutes"></label>
          ${km ? `<label>Kilometres<input class="num" inputmode="decimal" value="${e.cardioKm ?? ''}" placeholder="0" data-cardio="cardioKm"></label>` : ''}
        </div>
        <button type="button" class="log-set" data-next><b>${complete(e) ? (day().exercises.some((x, j) => j !== idx && !complete(x)) ? 'Next exercise' : 'Finish session') : cardioPrompt(e)}</b><span class="log-btn" style="display:inline-flex">${icons.arrow}</span></button>`;
    }

    const cardioPrompt = e => e.cardioDistance || (e.cardioKm || 0) > 0 ? 'Type the time or distance' : 'Type the time';

    function summary() {
      const d = day();
      const vol = d.exercises.reduce((n, x) => n + (x.volumeKg || 0), 0);
      const prs = d.exercises.filter(x => x.isPr || x.repRecord).length;
      const start = store.get(startKey);
      return `<div class="s-top"><span></span><span class="s-clock">${esc(BO.sessionName(d))}</span><button type="button" class="s-btn" data-close>${icons.x} Close</button></div>
        <div class="s-scroll" style="justify-content:center;text-align:center;align-items:center">
          <div class="medal" style="background:#fff;transform:none">${icons.check.replace('<svg', '<svg style="color:var(--train);width:48px;height:48px"')}</div>
          <h2 class="s-name">Session done</h2>
          <div class="s-last">${BO.DAYS[d.weekday - 1]} ${BO.shortDate(d.date)}${start ? ` · ${BO.clock((Date.now() - start) / 1000)}` : ''}</div>
          <div class="compare" style="max-width:420px">
            <div style="background:rgba(255,255,255,.12)"><b class="num">${d.setsDone}</b><span style="color:inherit;opacity:.8">sets</span></div>
            <div style="background:rgba(255,255,255,.12)"><b class="num">${vol ? BO.volume(vol) : '–'}</b><span style="color:inherit;opacity:.8">lifted</span></div>
            <div style="background:rgba(255,255,255,.12)"><b class="num">${prs}</b><span style="color:inherit;opacity:.8">${prs === 1 ? 'record' : 'records'}</span></div>
          </div>
          <button type="button" class="log-set" style="max-width:420px;justify-content:center" data-close><b>Back to today</b></button>
        </div>`;
    }

    function render() {
      if (finished) { el.innerHTML = summary(); return; }
      const d = day();
      const e = ex();
      const upnext = d.exercises.map((x, j) => ({ x, j })).filter(({ j }) => j !== idx);
      el.innerHTML = `
        <div class="s-top">
          <button type="button" class="s-btn" data-close>${icons.x} End</button>
          <span class="s-clock num" data-clock>${esc(BO.sessionName(d))}</span>
          <button type="button" class="s-btn" data-pick>${idx + 1} of ${d.exercises.length} ${icons.down}</button>
        </div>
        <div class="s-scroll">
          <div class="s-prog">${d.exercises.map((x, j) => `<i class="${complete(x) ? 'on' : j === idx ? 'now' : ''}"></i>`).join('')}</div>
          <div><h2 class="s-name">${esc(e.name)}</h2><div class="s-last" style="margin-top:6px">${lastLine(e)}</div></div>
          ${beatLine(e) ? `<span class="beat${stepUp(e) ? ' up' : ''}">${stepUp(e) ? icons.up : icons.trend}${esc(beatLine(e))}</span>` : ''}
          ${isCardio(e) ? cardioBody(e) : strengthBody(e)}
          ${upnext.length ? `<div class="upnext"><span class="lbl">The rest of the session</span>${upnext.map(({ x, j }) => `<button type="button" data-goto="${j}" class="${complete(x) ? 'done' : ''}">${esc(x.name)}<span>${complete(x) ? 'done' : isCardio(x) ? esc(x.detail) : `${x.loadKg != null ? BO.load(x.loadKg) + ' kg · ' : x.last && x.last.loadKg != null ? BO.load(x.last.loadKg) + ' kg · ' : ''}${esc(x.detail || '')}`}</span></button>`).join('')}</div>` : ''}
        </div>`;
      const start = store.get(startKey);
      if (start) el.querySelector('[data-clock]').textContent = BO.clock((Date.now() - start) / 1000);
    }

    /* ---------- the record moment ---------- */

    function celebrate(key) {
      celebrated.add(key);
      store.set(`bodyops.prs.${date}`, [...celebrated]);
    }

    /** The highest rep count whose best this session beats, from the bests before today (a first time isn't a record). */
    function repRecordOf(e) {
      const before = new Map((e.repBests || []).map(b => [b.reps, b.loadKg]));
      const ls = loadsOf(e);
      for (const n of [...BO.REP_COUNTS].reverse()) {
        const best = Math.max(-1, ...e.sets.map((r, i) => r != null && r >= n && ls[i] != null ? ls[i] : -1));
        if (best > 0 && before.has(n) && best > before.get(n)) return { reps: n, previousKg: before.get(n), loadKg: best };
      }
      return null;
    }

    const newRecBox = rec => `<div class="newrec"><span class="m">${icons.medal}</span><div><b>New ${rec.reps}-rep record</b><span class="sub">${BO.load(rec.loadKg)} kg × ${rec.reps} · was ${BO.load(rec.previousKg)} kg</span></div></div>`;

    /** Every set at the top of the rep range: the weight goes up next time. */
    function movingUp(e, rec, pr) {
      celebrate(`up:${e.planExerciseId}`);
      if (rec) celebrate(`rep:${e.planExerciseId}`);
      if (pr) celebrate(e.planExerciseId);
      BO.confetti();
      BO.buzz([30, 50, 30, 50, 60]);
      const ls = loadsOf(e);
      const done = e.sets.filter(s => s != null);
      const loads = ls.filter((v, i) => v != null && e.sets[i] != null);
      const same = loads.length && loads.every(v => v === loads[0]);
      const next = topLoad(e) + e.stepKg;
      const e1 = BO.bestE1rm(e.sets, ls);
      const gain = e1 != null && e.bestE1rmBeforeKg != null ? e1 - e.bestE1rmBeforeKg : null;
      const min = (rangeOf(e, 0) || {}).min;
      BO.sheet((body, closeSheet) => {
        body.innerHTML = `<div class="medal up">${icons.up}</div>
          <h3>Moving up</h3>
          <div class="pr-big num">${BO.load(next)}<small> kg</small></div>
          <span class="sub" style="font-size:14px">${esc(e.name)}, next time. You hit ${done.join(' · ')}${same ? ` at&nbsp;${BO.load(loads[0])}&nbsp;kg` : ''}.</span>
          <div class="compare">
            <div><b class="num">+${BO.load(e.stepKg)} kg</b><span>next session</span></div>
            <div><b class="num">${e1 != null ? BO.load(Math.round(e1)) + ' kg' : '–'}</b><span>est. 1RM${gain != null && gain > 0 ? `, +${BO.load(Math.round(gain * 10) / 10)}` : ''}</span></div>
            <div><b class="num">${min != null ? min + '+' : '–'}</b><span>reps to aim for</span></div>
          </div>
          ${pr ? `<div class="newrec"><span class="m">${icons.medal}</span><div><b>New personal record</b><span class="sub">${BO.load(topLoad(e))} kg · was ${BO.load(e.bestBeforeKg)} kg</span></div></div>` : rec ? newRecBox(rec) : ''}
          <button type="button" class="btn ink lg block" data-close-pr>Keep going</button>`;
        body.querySelector('[data-close-pr]').onclick = closeSheet;
      }, { label: 'Moving up', cls: 'pr-sheet' });
    }

    async function recordMoment(e, setIndex, rec) {
      celebrate(e.planExerciseId);
      if (rec) celebrate(`rep:${e.planExerciseId}`);
      BO.confetti();
      BO.buzz([30, 50, 30, 50, 60]);
      const kg = rec ? rec.loadKg : loadsOf(e)[setIndex] ?? e.loadKg;
      const gain = kg - (rec ? rec.previousKg : e.bestBeforeKg);
      BO.sheet((body, closeSheet) => {
        body.innerHTML = `<div class="medal">${icons.medal}</div>
          <h3>${rec ? `New ${rec.reps}-rep record` : 'New personal record'}</h3>
          <div class="pr-big num">${BO.load(kg)}<small> kg</small></div>
          <span class="sub" style="font-size:14px">${esc(e.name)} · ${e.sets[setIndex]} reps on set ${setIndex + 1}</span>
          <div class="compare">
            <div><b class="num">+${BO.load(gain)} kg</b><span>${rec ? `vs your best for ${rec.reps}` : 'vs your best'}</span></div>
            <div><b class="num" data-since>…</b><span data-since-lbl>since you started</span></div>
            <div><b class="num" data-month>…</b><span>record this month</span></div>
          </div>
          <button type="button" class="btn ink lg block" data-close-pr>Keep going</button>`;
        body.querySelector('[data-close-pr]').onclick = closeSheet;
        // Fill in how far it has come, once the log knows about it.
        setTimeout(async () => {
          try {
            const [list, records] = await Promise.all([BO.api('GET', '/api/exercises'), BO.api('GET', '/api/exercises/records?limit=200')]);
            const item = list.find(x => x.name.toLowerCase() === e.name.toLowerCase());
            const firstPoint = item && item.trend.length ? item.trend[0] : null;
            const sinceEl = body.querySelector('[data-since]');
            if (firstPoint && firstPoint.loadKg > 0 && sinceEl) {
              sinceEl.textContent = `+${Math.round((kg - firstPoint.loadKg) / firstPoint.loadKg * 100)}%`;
              body.querySelector('[data-since-lbl]').textContent = `since ${BO.shortDate(firstPoint.date)}`;
            } else if (sinceEl) sinceEl.textContent = '–';
            const month = date.slice(0, 7);
            const n = records.filter(r => r.date.slice(0, 7) === month).length;
            const monthEl = body.querySelector('[data-month]');
            if (monthEl) monthEl.textContent = BO.ordinal(Math.max(1, n));
          } catch (err) { }
        }, 700);
      }, { label: 'New personal record', cls: 'pr-sheet' });
    }

    /* ---------- actions ---------- */

    function logSet(e, i, value) {
      while (e.sets.length <= i) e.sets.push(null);
      const ls = loadsOf(e);
      const isOpen = i === nextOpen(e);   // the set the load pad is for
      ls[i] = isOpen && nextLoad != null ? nextLoad : suggestLoad(e, i);
      e.sets[i] = Math.max(0, Math.min(999, Math.round(value)));
      e.loadKg = topLoad(e);
      BO.buzz(15);
      reps = null;
      nextLoad = null;   // the next set suggests its own load (same set last time, or this one)
      save(e);
      BO.scoreDay(day());
      const more = nextOpen(e) >= 0;
      if (more) startRest(i + 1); else store.set('bodyops.rest', null);
      render();
      const pr = e.bestBeforeKg != null && ls[i] != null && ls[i] > e.bestBeforeKg && !celebrated.has(e.planExerciseId);
      const rec = celebrated.has(`rep:${e.planExerciseId}`) ? null : repRecordOf(e);
      if (!more && earnedNow(e) && !celebrated.has(`up:${e.planExerciseId}`)) movingUp(e, rec, pr);
      else if (pr) recordMoment(e, i);
      else if (rec && rec.loadKg === ls[i]) recordMoment(e, i, rec);
    }

    function goNext() {
      const d = day();
      const order = d.exercises.map((_, j) => (idx + 1 + j) % d.exercises.length);
      const j = order.find(k => !complete(d.exercises[k]));
      store.set('bodyops.rest', null);
      reps = null;
      nextLoad = null;
      if (j == null || j === idx) {
        finished = true;
        BO.flush();
        BO.confetti();
        BO.buzz([30, 60, 30]);
      } else {
        idx = j;
        BO.setQuery({ ex: d.exercises[idx].planExerciseId });
      }
      render();
      el.querySelector('.s-scroll') && el.querySelector('.s-scroll').scrollTo({ top: 0, behavior: 'smooth' });
    }

    /** A set already logged: change its reps and its load, or remove it. */
    function editSet(e, i) {
      let value = e.sets[i];
      let kg = loadsOf(e)[i];
      BO.sheet((body, closeSheet) => {
        body.innerHTML = `<h3>Set ${i + 1}</h3><span class="sub">${esc(e.name)}</span>
            <div class="stepper" style="background:var(--train-soft)"><button type="button" data-d="-1" style="color:var(--train)" aria-label="One rep less">−</button><div class="v"><input class="num" inputmode="numeric" value="${value}" data-v aria-label="Reps"><small>reps</small></div><button type="button" data-d="1" style="color:var(--train)" aria-label="One rep more">+</button></div>
            <div class="stepper" style="background:var(--train-soft)"><button type="button" data-kg="-1" style="color:var(--train)" aria-label="2.5 kg less">−</button><div class="v"><input class="num" inputmode="decimal" value="${kg != null ? BO.load(kg) : ''}" placeholder="kg" data-k aria-label="Load in kg"><small>kg</small></div><button type="button" data-kg="1" style="color:var(--train)" aria-label="2.5 kg more">+</button></div>
            <div class="row"><button type="button" class="btn danger" data-remove>Remove set</button><button type="button" class="btn primary" style="flex:1" data-ok>Save</button></div>`;
        body.addEventListener('click', ev => {
          const d = ev.target.closest('[data-d]');
          if (d) { value = Math.max(0, (BO.readNumber(body.querySelector('[data-v]').value) ?? value) + Number(d.dataset.d)); body.querySelector('[data-v]').value = value; return; }
          const k = ev.target.closest('[data-kg]');
          if (k) { kg = Math.max(0, Math.round(((BO.readNumber(body.querySelector('[data-k]').value) ?? kg ?? 0) + Number(k.dataset.kg) * 2.5) * 100) / 100); body.querySelector('[data-k]').value = BO.load(kg); return; }
          if (ev.target.closest('[data-remove]')) {
            e.sets[i] = null;
            loadsOf(e)[i] = null;
            while (e.sets.length && e.sets[e.sets.length - 1] == null && e.sets.length > e.targets.length) { e.sets.pop(); e.setLoads.length = e.sets.length; }
            e.loadKg = topLoad(e);
            save(e); BO.scoreDay(day()); closeSheet(); render();
            return;
          }
          if (ev.target.closest('[data-ok]')) {
            const v = BO.readNumber(body.querySelector('[data-v]').value);
            const w = BO.readNumber(body.querySelector('[data-k]').value);
            if (v != null) e.sets[i] = Math.max(0, Math.min(999, Math.round(v)));
            loadsOf(e)[i] = w == null ? null : Math.max(0, w);
            e.loadKg = topLoad(e);
            save(e); BO.scoreDay(day());
            closeSheet(); render();
          }
        });
      }, { label: `Set ${i + 1}` });
    }

    function pick() {
      BO.sheet((body, closeSheet) => {
        const d = day();
        body.innerHTML = `<h3>${esc(BO.sessionName(d))}</h3><span class="sub">${d.setsDone} of ${d.setsPlanned} sets done</span>
          <div class="pick-list">${d.exercises.map((x, j) => `<button type="button" class="${complete(x) ? 'done' : ''}" data-j="${j}"${j === idx ? ' aria-current="true"' : ''}><span class="n">${complete(x) ? icons.check.replace('<svg', '<svg style="width:13px;height:13px"') : j + 1}</span>${esc(x.name)}<span class="sub">${isCardio(x) ? esc(x.detail) : `${doneSets(x)}/${Math.max(1, x.targets.length)} sets`}</span></button>`).join('')}</div>`;
        body.addEventListener('click', ev => {
          const b = ev.target.closest('[data-j]');
          if (!b) return;
          idx = Number(b.dataset.j);
          reps = null;
          nextLoad = null;
          BO.setQuery({ ex: day().exercises[idx].planExerciseId });
          closeSheet();
          render();
        });
      }, { label: 'Exercises' });
    }

    el.addEventListener('click', event => {
      const t = event.target;
      if (t.closest('[data-close]')) { close(); return; }
      if (t.closest('[data-pick]')) { pick(); return; }
      const e = ex();
      const step = t.closest('[data-step]');
      if (step) {
        // The load of the next set only: sets already done keep theirs.
        const base = nextLoad ?? suggestLoad(e, Math.max(0, nextOpen(e))) ?? 0;
        nextLoad = Math.max(0, Math.round((base + Number(step.dataset.step) * 2.5) * 100) / 100);
        BO.buzz(6);
        render();
        return;
      }
      const repsBtn = t.closest('[data-reps]');
      if (repsBtn) { reps = Math.max(0, Math.min(999, (reps || 0) + Number(repsBtn.dataset.reps))); el.querySelector('.reps span').innerHTML = `${reps}<small>reps</small>`; return; }
      if (t.closest('[data-log]')) { const i = nextOpen(e); if (i >= 0) logSet(e, i, reps ?? defaultReps(e, i)); return; }
      const setBtn = t.closest('[data-set]');
      if (setBtn) {
        const i = Number(setBtn.dataset.set);
        if (e.sets[i] != null) editSet(e, i); else logSet(e, i, i === nextOpen(e) && reps != null ? reps : defaultReps(e, i));
        return;
      }
      if (t.closest('[data-add-set]')) {
        while (e.sets.length < count(e)) e.sets.push(null);
        e.sets.push(null);
        loadsOf(e);
        reps = null;
        render();
        return;
      }
      if (t.closest('[data-next]')) { if (!isCardio(e) || complete(e)) goNext(); return; }
      if (t.closest('[data-rest-add]')) {
        const r = store.get('bodyops.rest');
        if (r) { r.end += 30000; r.total += 30; store.set('bodyops.rest', r); render(); }
        return;
      }
      if (t.closest('[data-rest-skip]')) { store.set('bodyops.rest', null); render(); return; }
      const go = t.closest('[data-goto]');
      if (go) {
        idx = Number(go.dataset.goto);
        reps = null;
        nextLoad = null;
        BO.setQuery({ ex: day().exercises[idx].planExerciseId });
        render();
        el.querySelector('.s-scroll').scrollTo({ top: 0, behavior: 'smooth' });
      }
    });

    el.addEventListener('input', event => {
      const t = event.target;
      const e = ex();
      if (t.matches('[data-load]')) {
        const v = BO.readNumber(t.value);
        nextLoad = v == null ? null : Math.max(0, v);
      } else if (t.matches('[data-cardio]')) {
        const v = BO.readNumber(t.value);
        const wasComplete = complete(e);
        e[t.dataset.cardio] = v == null ? null : Math.max(0, v);
        save(e);
        BO.scoreDay(day());
        if (wasComplete !== complete(e)) {
          const btn = el.querySelector('[data-next] b');
          if (btn) btn.textContent = complete(e) ? (day().exercises.some((x, j) => j !== idx && !complete(x)) ? 'Next exercise' : 'Finish session') : cardioPrompt(e);
        }
      }
    });
    el.addEventListener('keydown', event => {
      if (event.key === 'Escape' && !document.querySelector('.sheet-back')) close();
    });

    BO.onResume = () => { if (el.isConnected) render(); };
    render();
  };
})();
