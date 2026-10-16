/* BodyOps — Apple Watch and Apple Health. A web app can't read Health, so an iOS Shortcut on the iPhone sends Milo the
   workout when it ends on the Watch, and the latest weight every morning, with a personal key. This file loads what
   arrived, shows it on Today and the session summary, and has the Settings card and its set-up sheet. */
'use strict';

(() => {
  const { esc, icons } = BO;
  let cached = null, cachedAt = 0;

  /** The key (hint only) and the workouts of the last four weeks; cached for a short while. */
  BO.watchLoad = async function watchLoad(fresh) {
    if (!fresh && cached && Date.now() - cachedAt < 30000) return cached;
    cached = await BO.api('GET', '/api/watch');
    cachedAt = Date.now();
    return cached;
  };
  BO.watchSet = state => { cached = state; cachedAt = Date.now(); };

  /** A day's Watch workouts: the one that goes with the session (not cardio it filled in), and cardio by planned exercise. */
  BO.watchFor = function watchFor(watch, date) {
    const list = watch && watch.workouts ? watch.workouts.filter(w => w.date === date) : [];
    const cardio = {};
    list.forEach(w => { if (w.planExerciseId != null) cardio[w.planExerciseId] = w; });
    const session = list.filter(w => w.planExerciseId == null && w.category !== 'cardio').sort((a, b) => b.durationSeconds - a.durationSeconds)[0] || null;
    return { session, cardio, all: list };
  };

  const mins = w => Math.max(1, Math.round(w.durationSeconds / 60));
  BO.watchMinutes = mins;

  /** "58 min · 412 kcal · 128 avg · 164 max" as small stats with icons. */
  BO.watchStats = function watchStats(w) {
    return `<div class="wstats"><span>${icons.watch}${mins(w)} min</span>${w.activeKcal != null ? `<span>${icons.bolt}${BO.kcalText(w.activeKcal)} kcal</span>` : ''}${w.avgBpm != null ? `<span>${icons.heart}${w.avgBpm} avg${w.maxBpm != null ? ` · ${w.maxBpm} max` : ''}</span>` : ''}</div>`;
  };

  /** The heart-rate curve as a line. */
  function curve(points) {
    if (!points || points.length < 2) return '';
    const lo = Math.min(...points) - 5, hi = Math.max(...points) + 5;
    const xy = points.map((v, i) => `${(i / (points.length - 1) * 340).toFixed(1)},${(50 - (v - lo) / (hi - lo) * 46).toFixed(1)}`).join(' ');
    return `<svg class="hr" viewBox="0 0 340 54" preserveAspectRatio="none" aria-hidden="true"><polyline points="${xy}" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round" stroke-linecap="round" vector-effect="non-scaling-stroke"/></svg>`;
  }

  /** The card on the session summary: the session's Watch numbers and the cardio it filled in. */
  BO.watchCard = function watchCard(day, watch) {
    const f = BO.watchFor(watch, day.date);
    const filled = day.exercises.filter(ex => f.cardio[ex.planExerciseId]).map(ex => {
      const w = f.cardio[ex.planExerciseId];
      return `<div class="line">${icons.check}<span>${esc(ex.name)} filled in: ${esc(w.kind)}, ${mins(w)} min${w.activeKcal != null ? ` · ${BO.kcalText(w.activeKcal)} kcal` : ''}</span></div>`;
    }).join('');
    const w = f.session;
    if (!w && !filled) return '';
    const num = (v, label) => `<div><b class="num">${v}</b><span>${label}</span></div>`;
    return `<div class="watch-card">
        ${w ? `<div class="hd"><span class="row" style="gap:6px">${icons.watch}From Apple Watch · ${esc(w.kind)}</span><span class="num">${esc(w.endedAt.slice(11, 16))}</span></div>
        <div class="nums">${num(mins(w), 'min')}${num(w.activeKcal != null ? BO.kcalText(w.activeKcal) : '–', 'active kcal')}${num(w.avgBpm ?? '–', 'avg bpm')}${num(w.maxBpm ?? '–', 'max bpm')}</div>
        ${curve(w.heartRates)}` : ''}
        ${filled}
      </div>`;
  };

  /** "today 19:12", "yesterday 08:01", "5 Oct". */
  function when(iso) {
    if (!iso) return '';
    const d = new Date(iso.endsWith('Z') ? iso : iso + 'Z');
    const day = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    const time = `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
    const today = BO.localToday();
    return day === today ? `today ${time}` : BO.daysBetween(day, today) === 1 ? `yesterday ${time}` : BO.shortDate(day);
  }

  /** The Settings row: connected or not, the last sync and what came in. */
  BO.watchRow = function watchRow(watch, weights) {
    if (!watch) return '<div class="set-row"><span class="sub">Checking…</span></div>';
    const last = watch.workouts.at(-1);
    const weight = weights && weights.entries ? weights.entries.at(-1) : null;
    let sub;
    if (!watch.connected) sub = 'Your workouts, calories and heart rate from the Watch, and your weight, sent by an iOS Shortcut. Set up once.';
    else if (watch.lastError) sub = `<span class="warn-t">The last sync didn't work:</span> ${esc(watch.lastError)}`;
    else if (!watch.lastSyncAt) sub = `Key ${esc(watch.keyHint)} made. Nothing has arrived yet: finish the set-up on your iPhone.`;
    else sub = [last ? `Last: ${esc(last.kind)}, ${mins(last)} min${last.activeKcal != null ? `, ${BO.kcalText(last.activeKcal)} kcal` : ''}.` : '', weight ? `Weight ${BO.kg(weight.weightKg)} kg on ${BO.shortDate(weight.date)}.` : ''].filter(Boolean).join(' ') || 'Connected.';
    const pill = !watch.connected ? '' : watch.lastSyncAt ? ` <span class="pill ${watch.lastError ? 'mute' : 'good'}">Synced ${when(watch.lastSyncAt)}</span>` : ' <span class="pill mute">Waiting</span>';
    return `<div class="set-row"><div class="row" style="align-items:flex-start;flex:1 1 200px;min-width:0"><span class="hk-icon">${icons.heart}</span><div style="min-width:0"><b>${watch.connected ? 'Connected' : 'Not connected'}${pill}</b><span class="sub">${sub}</span></div></div>
        <button type="button" class="btn${watch.connected ? '' : ' primary'}" data-watch-setup>Set up</button></div>`;
  };

  /** The fields the Shortcut sends, for step 2. */
  const FIELDS = [
    ['type', 'Workout › Type'],
    ['start', 'Workout › Start Date'],
    ['end', 'Workout › End Date'],
    ['activeEnergy', 'Workout › Active Energy (or Total Energy)'],
    ['distance', 'Workout › Distance (walks, runs, rides)'],
    ['heartRate', 'the Heart Rate samples (action 2), if you added it'],
  ];

  /**
   * The set-up sheet: make (or replace) the key, build the Shortcut, turn on the automations, revoke.
   * @param {{onChange: function}} options called with the new state when the key changes.
   */
  BO.watchSetup = function watchSetup({ onChange } = {}) {
    BO.sheet(async (body, close) => {
      let watch = await BO.watchLoad(true).catch(() => null);
      let fresh = null;   // the key, right after it was made (Milo keeps only its hash)
      let confirmRevoke = false;
      const url = `${location.origin}${(watch && watch.ingestPath) || '/api/watch/ingest'}`;

      const copy = async (text, what) => {
        try { await navigator.clipboard.writeText(text); BO.toast(`${what} copied`); } catch (e) { BO.toast(`Couldn't copy. Select the ${what.toLowerCase()} and copy it by hand.`, { error: true }); }
      };

      function draw() {
        const key = fresh || (watch && watch.connected ? watch.keyHint : null);
        body.innerHTML = `
          <div class="row" style="align-items:flex-start"><span class="hk-icon">${icons.heart}</span><div><h3 class="h-l" style="margin:0">Apple Watch &amp; Health</h3><span class="sub">Your iPhone sends it; Milo never reads Health by itself.</span></div></div>
          <div class="hk-steps">
            <div class="hk-step"><span class="n">1</span><div><b>${fresh ? 'Copy your key now' : 'Your personal key'}</b>
              <span class="sub">${fresh ? 'It is shown only this once. It lets the Shortcut send to your account and nothing else.' : key ? 'Milo keeps only a fingerprint of it. Lost it? Make a new one: the old one stops working.' : 'It lets the Shortcut send to your account and nothing else.'}</span>
              <div class="row"><span class="keybox${fresh ? ' fresh' : ''}">${key ? esc(key) : 'No key yet'}</span>${fresh ? `<button type="button" class="btn primary" data-copy-key>${icons.copy}Copy</button>` : `<button type="button" class="btn${key ? '' : ' primary'}" data-new-key>${key ? 'New key' : 'Make a key'}</button>`}</div></div></div>
            <div class="hk-step"><span class="n">2</span><div><b>Make the Milo Shortcut</b>
              <span class="sub">In the Shortcuts app, a new shortcut called <b class="inl">Milo workout</b> with these actions:</span>
              <ol class="hk-list">
                <li><b class="inl">Find Health Samples</b> where Type is <i>Workouts</i>, sorted by End Date, latest first, limit 1.</li>
                <li>Optional, for the heart-rate curve: <b class="inl">Find Health Samples</b> where Type is <i>Heart Rate</i>, Start Date is after the workout's Start Date and End Date is before its End Date.</li>
                <li><b class="inl">Get Contents of URL</b>: the address below, Method <i>POST</i>, a Header <i>Authorization</i> = <i>Bearer</i> + your key, Request Body <i>JSON</i> with these fields:</li>
              </ol>
              <div class="row"><span class="keybox">${esc(url)}</span><button type="button" class="btn" data-copy-url>${icons.copy}Copy</button></div>
              <div class="hk-fields">${FIELDS.map(([k, v]) => `<div><code>${k}</code><span>${v}</span></div>`).join('')}</div>
              <span class="sub" style="margin-top:8px">A second one, <b class="inl">Milo weight</b>: Find Health Samples where Type is <i>Weight</i>, latest first, limit 1, then the same Get Contents of URL with <code>weight</code> = its Value and <code>date</code> = its Start Date. Numbers can keep their units and dates any format: Milo reads “412 kcal”, “82,4 kg” and “16 Oct 2026 at 19:12”.</span></div></div>
            <div class="hk-step"><span class="n">3</span><div><b>Turn on two automations</b><span class="sub">Shortcuts › Automation › New, choose Run Immediately.</span>
              <div class="auto">${icons.watch}<span>Apple Watch Workout, when it ends <i>→ Milo workout</i></span></div>
              <div class="auto">${icons.today}<span>Time of Day, every day at 09:00 <i>→ Milo weight</i></span></div></div></div>
          </div>
          ${watch && watch.connected ? `<div class="row" style="flex-wrap:wrap">${confirmRevoke
            ? '<button type="button" class="btn" data-revoke-cancel>Keep it</button><button type="button" class="btn danger" data-revoke>Revoke for good</button>'
            : '<button type="button" class="btn danger" data-revoke-ask>Revoke key</button>'}
            <span class="fine" style="flex:1">${watch.lastSyncAt ? `Last sync ${when(watch.lastSyncAt)}` : 'Nothing sent yet'} · ${BO.plural(watch.workouts.filter(w => BO.daysBetween(w.date, BO.localToday()) < 7).length, 'workout')} this week</span></div>` : ''}
          <p class="fine">Sets, reps and loads are still logged in Milo: the Watch doesn't know them. An indoor walk or run fills in the planned treadmill minutes when you haven't logged them. Calories burned don't touch the day score.</p>`;
      }

      body.addEventListener('click', async event => {
        const t = event.target;
        if (t.closest('[data-copy-key]')) { copy(fresh, 'Key'); return; }
        if (t.closest('[data-copy-url]')) { copy(url, 'Address'); return; }
        if (t.closest('[data-new-key]')) {
          try {
            const made = await BO.saving(BO.api('POST', '/api/watch/key'));
            fresh = made.key; watch = made.watch; BO.watchSet(watch);
            draw();
            if (onChange) onChange(watch);
          } catch (e) { }
          return;
        }
        if (t.closest('[data-revoke-ask]')) { confirmRevoke = true; draw(); return; }
        if (t.closest('[data-revoke-cancel]')) { confirmRevoke = false; draw(); return; }
        if (t.closest('[data-revoke]')) {
          try {
            watch = await BO.saving(BO.api('DELETE', '/api/watch/key'));
            BO.watchSet(watch);
            fresh = null; confirmRevoke = false;
            draw();
            BO.toast('Key revoked. The Shortcut can no longer send.');
            if (onChange) onChange(watch);
          } catch (e) { }
        }
      });
      draw();
    });
  };
})();
