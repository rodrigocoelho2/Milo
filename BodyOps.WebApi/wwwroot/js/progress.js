/* BodyOps — Progress: body weight against the goal (with the pace), every lift's best and trend, and the records. */
'use strict';

(() => {
  const { esc, icons } = BO;
  const RANGES = { '1M': 31, '3M': 92, 'All': 100000 };

  /** kg per week from a least-squares line through the weigh-ins of the last 5 weeks (null with too few). */
  function pace(entries) {
    if (entries.length < 3) return null;
    const lastDate = BO.parseDate(entries.at(-1).date).getTime();
    const recent = entries.filter(e => lastDate - BO.parseDate(e.date).getTime() <= 35 * 86400000);
    if (recent.length < 3) return null;
    const xs = recent.map(e => BO.parseDate(e.date).getTime() / 86400000), ys = recent.map(e => e.weightKg);
    const mx = xs.reduce((a, b) => a + b) / xs.length, my = ys.reduce((a, b) => a + b) / ys.length;
    let num = 0, den = 0;
    xs.forEach((x, i) => { num += (x - mx) * (ys[i] - my); den += (x - mx) * (x - mx); });
    return den ? num / den * 7 : null;
  }

  function howLong(weeks) {
    if (weeks < 1.5) return 'about a week';
    if (weeks < 10) return `${Math.round(weeks)} weeks`;
    const months = weeks / 4.35;
    return months < 1.5 ? 'about a month' : `roughly ${Math.round(months)} months`;
  }

  BO.screens.progress = async function progress(root, query) {
    let [weights, lifts, records] = await Promise.all([
      BO.api('GET', '/api/weights'),
      BO.api('GET', '/api/exercises'),
      BO.api('GET', '/api/exercises/records?limit=12'),
    ]);
    let range = RANGES[query.range] ? query.range : '3M';
    let editingGoal = false;
    let showAll = false;
    let firstPaint = true;
    const today = BO.localToday();
    const r = i => firstPaint ? ` rise" style="--i:${i}` : '';

    async function reloadWeights() { weights = await BO.api('GET', '/api/weights'); render(); }
    BO.onResume = () => { if (root.isConnected) reloadWeights().catch(() => { }); };

    function weightCard() {
      const entries = weights.entries;
      const latest = entries.at(-1);
      const goal = weights.goalKg;
      if (!latest) {
        return `<section class="card${r(1)}"><div class="card-head"><span class="pill weigh">Body weight</span></div>
          <div class="empty"><b>No weigh-ins yet.</b><span>Weigh yourself at the same time each morning for a fair trend.</span><button type="button" class="btn primary" data-weigh>Log your weight</button></div></section>`;
      }
      const cutoff = BO.addDays(latest.date, -RANGES[range]);
      const shown = entries.filter(e => e.date >= cutoff);
      const first = shown[0];
      const pills = [];
      if (shown.length > 1) {
        const diff = latest.weightKg - first.weightKg;
        const towardGoal = goal == null || Math.sign(diff) === Math.sign(goal - first.weightKg);
        pills.push(`<span class="pill ${towardGoal ? 'good' : 'eat'}">${BO.signed(diff)} kg since ${BO.MONTHS[BO.parseDate(first.date).getMonth()]}</span>`);
      }
      if (goal != null) {
        const left = Math.abs(latest.weightKg - goal);
        pills.push(`<span class="pill weigh">${left < 0.05 ? 'Goal reached' : `${BO.kg(left)} kg to go`}</span>`);
      }
      let paceText = '';
      const p = pace(entries);
      if (goal != null && p != null && Math.abs(latest.weightKg - goal) >= 0.05) {
        const need = goal - latest.weightKg;
        if (Math.sign(p) === Math.sign(need) && Math.abs(p) >= 0.05) {
          const step = need < 0 ? -0.5 : 0.5;
          paceText = `At about ${BO.signed(p)} kg a week, ${BO.kg(goal)} kg is ${howLong(Math.abs(need / p))} away.${Math.abs(p) < Math.abs(step) ? ` At ${BO.signed(step)} kg a week it is ${howLong(Math.abs(need / step))}.` : ''}`;
        } else {
          paceText = `Lately ${p > 0 ? 'up' : 'down'} ${BO.kg(Math.abs(p))} kg a week, away from the goal.`;
        }
      } else if (p != null) {
        paceText = `Lately ${Math.abs(p) < 0.05 ? 'steady' : `${p > 0 ? 'up' : 'down'} ${BO.kg(Math.abs(p))} kg a week`}.`;
      }
      const goalRow = editingGoal
        ? `<form class="row" id="goalForm" style="flex-wrap:wrap"><input class="field small" id="goalInput" inputmode="decimal" value="${goal ?? ''}" placeholder="kg" aria-label="Goal in kg" style="max-width:110px"><button class="btn primary" type="submit">Save goal</button><button class="btn ghost" type="button" id="goalCancel">Cancel</button>${goal != null ? '<button class="link-btn" type="button" id="goalClear">Remove</button>' : ''}</form>`
        : `<button type="button" class="link-btn" id="goalEdit">${goal != null ? `goal ${BO.kg(goal)} kg · change` : 'Set a goal'}</button>`;
      return `<section class="card${r(1)}">
        <div class="card-head"><span class="pill weigh">Body weight</span>${goalRow}</div>
        <div class="wbig"><b class="num">${BO.kg(latest.weightKg)}</b><span>kg</span></div>
        <div class="row" style="flex-wrap:wrap;margin:10px 0 6px">${pills.join('')}</div>
        ${shown.length ? BO.lineChart(shown.map(e => ({ date: e.date, value: e.weightKg })), { goal, goalLabel: goal != null ? `goal ${BO.kg(goal)}` : '', height: matchMedia('(min-width: 1000px)').matches ? 260 : 190, width: matchMedia('(min-width: 1000px)').matches ? 560 : 340, label: v => BO.kg(v) }) : ''}
        ${paceText ? `<p class="sub" style="margin:10px 0 0;font-size:13.5px">${esc(paceText)}</p>` : ''}
      </section>`;
    }

    function logCard() {
      const entries = weights.entries.slice().reverse();
      const list = (showAll ? entries : entries.slice(0, 6)).map((e, i) => {
        const before = entries[i + 1];
        const diff = before ? e.weightKg - before.weightKg : null;
        return `<div class="wrow"><div><b class="num">${BO.kg(e.weightKg)} kg</b><div class="sub">${BO.fullDate(e.date)}</div></div>
          <span class="delta num ${diff == null ? '' : diff < 0 ? 'down' : diff > 0 ? 'up' : ''}">${diff == null ? '' : BO.signed(diff)}</span>
          <button type="button" class="del" data-del="${e.date}" aria-label="Remove the weigh-in of ${BO.fullDate(e.date)}">${icons.x}</button></div>`;
      }).join('');
      return `<section class="card${r(2)}" aria-labelledby="h-log">
        <div class="card-head"><h2 class="h-l" id="h-log">Weigh-ins</h2><button type="button" class="btn primary" data-weigh style="padding:9px 13px">${icons.plus} Log</button></div>
        <form class="row" id="pastForm" style="margin-bottom:8px"><input class="field small" type="date" id="wDate" value="${today}" max="${today}" aria-label="Date" style="max-width:160px"><input class="field small" id="wValue" inputmode="decimal" placeholder="kg" aria-label="Weight in kg"><button type="submit" class="btn">Add</button></form>
        ${entries.length ? list : ''}
        ${entries.length > 6 ? `<button type="button" class="link-btn" id="showAll" style="margin-top:8px">${showAll ? 'Show fewer' : `Show all ${entries.length}`}</button>` : ''}
      </section>`;
    }

    function liftsCard() {
      const withLoad = lifts.filter(l => l.bestKg != null).sort((a, b) => (b.lastDate || '').localeCompare(a.lastDate || ''));
      const recent = new Set(records.filter(x => x.date >= BO.monday(today)).map(x => x.name.toLowerCase()));
      const body = withLoad.length
        ? `<div class="lifts">${withLoad.map(l => `<button type="button" class="lift" data-lift="${esc(l.name)}">
              <div><b>${esc(l.name)}</b><span class="sub">${recent.has(l.name.toLowerCase()) ? '<span class="pill gold">PR</span> record this week' : `best ${l.bestDate ? BO.shortDate(l.bestDate) : ''} · ${BO.plural(l.sessions, 'session')}`}</span></div>
              ${BO.sparkline(l.trend.map(p => p.loadKg), recent.has(l.name.toLowerCase()) ? 'var(--gold)' : 'var(--train)')}
              <span class="val num">${BO.load(l.bestKg)}</span></button>`).join('')}</div>`
        : '<div class="empty"><b>No lifts logged yet.</b><span>Log a session and every exercise shows its best load and trend here.</span></div>';
      return `<section class="card${r(3)}" aria-labelledby="h-lifts"><div class="card-head"><h2 class="h-l" id="h-lifts">Lifts</h2><span class="sub">best load, kg</span></div>${body}</section>`;
    }

    function recordsCard() {
      if (!records.length) return '';
      return `<section class="card${r(4)}" aria-labelledby="h-rec"><div class="card-head"><h2 class="h-l" id="h-rec">Records</h2><span class="sub">newest first</span></div>
        ${records.map(x => `<div class="rec"><span class="m">${icons.medal}</span><div><b>${esc(x.name)}</b><span class="sub">${BO.fullDate(x.date)}</span></div><span class="r num">${BO.load(x.previousKg)} → ${BO.load(x.loadKg)} kg</span></div>`).join('')}</section>`;
    }

    function render() {
      root.innerHTML = `
        <div class="page-head${r(0)}">
          <div><div class="hello">Weight, lifts and records</div><h1 class="h-xl">Progress</h1></div>
          <div class="acts"><div class="seg" role="group" aria-label="Range">${Object.keys(RANGES).map(k => `<button type="button" data-range="${k}" aria-pressed="${k === range}">${k}</button>`).join('')}</div></div>
        </div>
        <div class="prog-grid">
          <div class="stack">${weightCard()}${logCard()}</div>
          <div class="stack">${liftsCard()}${recordsCard()}</div>
        </div>`;
      firstPaint = false;
    }

    async function openLift(name) {
      BO.sheet(async body => {
        body.innerHTML = '<div class="loading" style="min-height:200px"><div class="spinner"></div></div>';
        try {
          const h = await BO.api('GET', `/api/exercises/history?name=${encodeURIComponent(name)}`);
          const sessions = h.sessions.filter(s => s.loadKg != null);
          const best = sessions.length ? Math.max(...sessions.map(s => s.loadKg)) : null;
          const bestIndex = sessions.map(s => s.loadKg).lastIndexOf(best);
          body.innerHTML = `<div><span class="sub">${BO.plural(h.sessions.length, 'session')}</span><h3>${esc(h.name)}</h3></div>
            ${best != null ? `<div class="row"><span class="pill gold">Best ${BO.load(best)} kg</span>${sessions.length > 1 ? `<span class="pill train">${BO.signed(sessions.at(-1).loadKg - sessions[0].loadKg, 1)} kg since ${BO.shortDate(sessions[0].date)}</span>` : ''}</div>` : ''}
            ${sessions.length ? `<div style="--c:var(--train)">${BO.lineChart(sessions.map(s => ({ date: s.date, value: s.loadKg })), { highlight: sessions.length > 1 ? bestIndex : -1, height: 170, label: v => BO.load(v) })}</div>` : '<p class="sub">No loads logged yet.</p>'}
            <div class="slist">${h.sessions.slice().reverse().slice(0, 12).map(s => `<div class="srow"><div><b>${BO.fullDate(s.date)}</b><div class="sub">${s.reps.length ? s.reps.join(' · ') + ' reps' : 'no sets'}${s.volumeKg ? ` · ${BO.volume(s.volumeKg)}` : ''}</div></div><span class="r num">${s.loadKg != null ? BO.load(s.loadKg) + ' kg' : '–'}</span></div>`).join('')}</div>`;
        } catch (e) {
          body.innerHTML = `<div class="empty"><b>Couldn't load.</b><span>${esc(e.message)}</span></div>`;
        }
      }, { label: name });
    }

    async function put(date, weightKg) {
      weights = await BO.saving(BO.api('PUT', `/api/weights/${date}`, { weightKg }));
      render();
    }

    root.addEventListener('click', async event => {
      const t = event.target;
      const rangeBtn = t.closest('[data-range]');
      if (rangeBtn) { range = rangeBtn.dataset.range; BO.setQuery({ range: range === '3M' ? null : range }); render(); return; }
      if (t.closest('[data-weigh]')) { BO.quickLog({ tab: 'weigh', onDone: () => reloadWeights() }); return; }
      if (t.closest('#showAll')) { showAll = !showAll; render(); return; }
      if (t.closest('#goalEdit')) { editingGoal = true; render(); root.querySelector('#goalInput').focus(); return; }
      if (t.closest('#goalCancel')) { editingGoal = false; render(); return; }
      if (t.closest('#goalClear')) {
        try { await BO.saveSettings({ weightGoalKg: null }); weights.goalKg = null; editingGoal = false; render(); } catch (e) { }
        return;
      }
      const lift = t.closest('[data-lift]');
      if (lift) { openLift(lift.dataset.lift); return; }
      const del = t.closest('[data-del]');
      if (del) {
        const date = del.dataset.del;
        const entry = weights.entries.find(e => e.date === date);
        try {
          weights = await BO.saving(BO.api('DELETE', `/api/weights/${date}`));
          render();
          BO.toast(`Weigh-in of ${BO.shortDate(date)} removed`, { action: 'Undo', onAction: () => put(date, entry.weightKg).catch(() => { }) });
        } catch (e) { }
      }
    });

    root.addEventListener('submit', async event => {
      event.preventDefault();
      if (event.target.id === 'goalForm') {
        const value = BO.readNumber(root.querySelector('#goalInput').value);
        try { await BO.saveSettings({ weightGoalKg: value }); weights.goalKg = value; editingGoal = false; render(); } catch (e) { }
      } else if (event.target.id === 'pastForm') {
        const value = BO.readNumber(root.querySelector('#wValue').value);
        const date = root.querySelector('#wDate').value || today;
        if (value == null) { BO.toast('Type the weight in kg first.', { error: true }); return; }
        try { await put(date, value); BO.toast(`Logged ${BO.kg(value)} kg for ${BO.shortDate(date)}`); } catch (e) { }
      }
    });

    render();
  };
})();
