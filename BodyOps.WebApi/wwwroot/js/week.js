/* BodyOps — Week: stacked day bars, the week's totals, the muscles worked, a recap and each day in a list.
   ?date= opens the week holding that date. */
'use strict';

(() => {
  const { esc, icons } = BO;

  /** ISO week number. */
  function weekNumber(iso) {
    const d = BO.parseDate(iso);
    d.setDate(d.getDate() + 3 - ((d.getDay() + 6) % 7));
    const jan4 = new Date(d.getFullYear(), 0, 4);
    return 1 + Math.round(((d - jan4) / 86400000 - 3 + ((jan4.getDay() + 6) % 7)) / 7);
  }

  BO.screens.week = async function weekScreen(root, query) {
    const load = async date => {
      const w = await BO.api('GET', '/api/days/week' + (date ? `?date=${encodeURIComponent(date)}` : ''));
      const prev = await BO.api('GET', `/api/days/week?date=${BO.addDays(w.from, -7)}`).catch(() => null);
      w.days.forEach(BO.scoreDay);
      if (prev) prev.days.forEach(BO.scoreDay);
      return [w, prev];
    };
    let [week, previous] = await load(query.date);
    const streak = await BO.api('GET', '/api/days/streak').catch(() => null);
    let firstPaint = true;

    async function go(date) {
      [week, previous] = await load(date);
      BO.setQuery({ date: week.from === BO.monday(week.today) ? null : week.from });
      firstPaint = true;
      render();
    }
    BO.onResume = () => { if (root.isConnected && !BO.busy()) go(week.from).catch(() => { }); };

    function label() {
      const thisMonday = BO.monday(week.today);
      if (week.from === thisMonday) return `Week ${weekNumber(week.from)} · this week`;
      if (week.from === BO.addDays(thisMonday, -7)) return `Week ${weekNumber(week.from)} · last week`;
      return `Week ${weekNumber(week.from)}`;
    }

    function range() {
      const a = BO.parseDate(week.from), b = BO.parseDate(week.to);
      return a.getMonth() === b.getMonth() ? `${a.getDate()}–${b.getDate()} ${BO.MONTHS_SHORT[b.getMonth()]}` : `${a.getDate()} ${BO.MONTHS_SHORT[a.getMonth()]} – ${b.getDate()} ${BO.MONTHS_SHORT[b.getMonth()]}`;
    }

    const volumeOf = w => w ? w.days.reduce((n, d) => n + d.exercises.reduce((m, ex) => m + (ex.volumeKg || 0), 0), 0) : 0;
    const trainedDays = w => w.days.filter(d => d.exercises.length && d.setsDone > 0 && d.trainingState !== 'missed');

    function tiles() {
      const planned = week.days.filter(d => d.exercises.length);
      const trained = trainedDays(week);
      const eaten = week.days.reduce((n, d) => n + d.meals.filter(m => m.eaten).length, 0);
      const meals = week.days.reduce((n, d) => n + d.meals.length, 0);
      const fullDays = week.days.filter(d => d.meals.length && d.meals.every(m => m.eaten));
      const vol = volumeOf(week), prevVol = volumeOf(previous);
      const prs = week.days.flatMap(d => d.exercises.filter(ex => ex.isPr));
      const t = (i, color, k, value, of, sub) => `<div class="tile ${firstPaint ? 'rise' : ''}" style="--i:${i}"><span class="k"><i style="background:${color}"></i>${k}</span><b class="num">${value}${of != null ? `<small>/${of}</small>` : ''}</b><span>${sub}</span></div>`;
      return `<div class="tiles">
        ${t(2, 'var(--train)', 'Sessions', trained.length, planned.length, trained.length ? esc(trained.map(d => d.name || BO.DAYS_SHORT[d.weekday - 1]).join(', ')) : 'none yet')}
        ${t(3, 'var(--eat)', 'Meals', eaten, meals, fullDays.length ? `all of ${esc(fullDays.map(d => BO.DAYS[d.weekday - 1]).slice(0, 2).join(', '))}${fullDays.length > 2 ? ' …' : ''}` : 'tick them on Today')}
        ${t(4, 'var(--ink)', 'Volume', vol >= 1000 ? (vol / 1000).toFixed(1) : Math.round(vol), null, `${vol >= 1000 ? 'tonnes' : 'kg'} lifted${prevVol ? ` · ${BO.signed((vol - prevVol) / prevVol * 100, 0)}% vs last week` : ''}`)}
        ${t(5, 'var(--gold)', 'Records', prs.length, null, prs.length ? esc(prs.map(p => p.name).slice(0, 2).join(', ')) : 'beat a best load')}
      </div>`;
    }

    function recap() {
      const over = week.to < week.today;
      const counted = week.days.filter(d => d.date <= week.today && BO.dayCounts(d));
      const good = counted.filter(d => d.score >= BO.STREAK_AT);
      const best = counted.slice().sort((a, b) => b.score - a.score)[0];
      const weighed = week.days.filter(d => d.weightKg != null).length;
      const trained = trainedDays(week).length;
      const planned = week.days.filter(d => d.exercises.length).length;
      const lines = [];
      lines.push(`${trained} of ${planned} sessions, ${BO.plural(weighed, 'weigh-in')}.`);
      if (best && best.score > 0) lines.push(`Best day: ${BO.DAYS[best.weekday - 1]} with ${best.score}.`);
      if (counted.length) lines.push(`${good.length} of ${counted.length} days scored ${BO.STREAK_AT}+.`);
      const title = over ? 'Week recap' : week.from === BO.monday(week.today) ? 'So far this week' : 'Coming up';
      return `<div class="recap ${firstPaint ? 'rise' : ''}" style="--i:6"><span class="pill gold" style="align-self:flex-start">${over ? 'Recap' : 'Live'}</span><h3>${title}</h3><p>${esc(lines.join(' '))}</p>
        ${streak && streak.best ? `<p>Longest streak this year: <b style="color:#fff">${streak.best} days</b>.</p>` : ''}</div>`;
    }

    function dayRows() {
      return `<div class="dlist">${week.days.map((d, i) => {
        const isToday = d.date === week.today;
        const future = d.date > week.today;
        const what = d.exercises.length ? (d.name || BO.plural(d.exercises.length, 'exercise')) : (d.name || 'Rest day');
        const bits = [];
        if (d.exercises.length) bits.push(d.trainingState === 'missed' ? 'skipped' : `${d.setsDone}/${d.setsPlanned} sets`);
        if (d.meals.length) bits.push(`${d.meals.filter(m => m.eaten).length}/${d.meals.length} meals`);
        if (d.weightKg != null) bits.push(`${BO.kg(d.weightKg)} kg`);
        const color = d.score >= BO.STREAK_AT ? 'var(--good)' : d.score > 0 ? 'var(--eat)' : 'var(--line)';
        return `<button type="button" class="drow${isToday ? ' today' : ''} ${firstPaint ? 'rise' : ''}" style="--i:${i + 3}" data-day="${d.date}">
          <span class="d"><small>${BO.DAYS_SHORT[d.weekday - 1]}</small><b class="num">${BO.dayNum(d.date)}</b></span>
          <span class="what"><b>${esc(what)}</b><span>${future ? 'planned' : esc(bits.join(' · ') || 'nothing planned')}</span></span>
          ${future ? '<span></span>' : BO.ring(d.score / 100, String(d.score), { size: 44, stroke: 7, color, track: 'var(--card2)', text: 'var(--ink)', cls: 'ring-s' })}
        </button>`;
      }).join('')}</div>`;
    }

    function render() {
      const muscles = week.totals.muscles;
      root.innerHTML = `
        <div class="page-head ${firstPaint ? 'rise' : ''}">
          <div><div class="hello">${label()}</div><h1 class="h-xl">${range()}</h1></div>
          <div class="acts">
            <button type="button" class="icon-btn" id="prevWeek" aria-label="Previous week">${icons.left}</button>
            ${week.from !== BO.monday(week.today) ? '<button type="button" class="btn" id="thisWeek">This week</button>' : ''}
            <button type="button" class="icon-btn" id="nextWeek" aria-label="Next week">${icons.right}</button>
          </div>
        </div>
        <div class="week">
          <div class="col">
            <section class="card ${firstPaint ? 'rise' : ''}" style="--i:1" aria-label="Day scores">
              ${BO.weekBars(week, { buttons: true, height: 170 })}
              <div class="legend"><span class="pill train">Training</span><span class="pill eat">Meals</span><span class="pill weigh">Weigh-in</span></div>
            </section>
            ${tiles()}
            <section aria-labelledby="h-days"><div class="spread" style="margin:4px 0 8px"><h2 class="h-l" id="h-days">Day by day</h2><span class="sub">tap a day</span></div>${dayRows()}</section>
          </div>
          <div class="col">
            ${recap()}
            <section class="card ${firstPaint ? 'rise' : ''}" style="--i:7" aria-labelledby="h-muscles">
              <div class="card-head"><h2 class="h-l" id="h-muscles">Muscles hit</h2><span class="sub">sets this week</span></div>
              ${muscles.length ? `<div class="bm">${BO.bodyMap(muscles)}${BO.muscleRank(muscles, 7)}</div>` : '<p class="sub" style="margin:0">Log sets and the map fills in.</p>'}
            </section>
            <div class="row" style="justify-content:flex-end"><button type="button" class="btn ghost" id="resetWeek">Reset this week</button></div>
          </div>
        </div>`;
      firstPaint = false;
    }

    /** A day's details in a sheet: tick meals, mark the session, jump to it. */
    function openDay(date) {
      BO.sheet((body, close) => {
        const paint = () => {
          const d = week.days.find(x => x.date === date);
          BO.scoreDay(d);
          const state = d.trainingState;
          body.innerHTML = `<div class="spread"><div><span class="sub">${BO.DAYS[d.weekday - 1]} ${BO.shortDate(d.date)}</span><h3>${esc(d.name || (d.exercises.length ? 'Training day' : 'Rest day'))}</h3></div>
              ${BO.ring(d.score / 100, String(d.score), { size: 56, stroke: 7, color: d.score >= BO.STREAK_AT ? 'var(--good)' : 'var(--eat)', track: 'var(--card2)', text: 'var(--ink)', cls: 'ring-s' })}</div>
            ${d.exercises.length ? `<div class="seg" role="group" aria-label="Session">
                <button type="button" data-state="pending" aria-pressed="${state === 'pending'}">Not yet</button>
                <button type="button" data-state="done" aria-pressed="${state === 'done'}">Trained</button>
                <button type="button" data-state="missed" aria-pressed="${state === 'missed'}">Skipped</button></div>
              <div class="slist">${d.exercises.map(ex => `<div class="srow"><div><b>${esc(ex.name)}</b>${ex.isPr ? ' <span class="pill gold">PR</span>' : ''}<div class="sub">${esc(ex.detail)}${ex.loadKg != null ? ` · ${BO.load(ex.loadKg)} kg` : ''}</div></div><span class="r num">${ex.type === 'cardio' ? [ex.cardioMinutes ? `${BO.load(ex.cardioMinutes)} min` : '', ex.cardioKm ? `${BO.load(ex.cardioKm)} km` : ''].filter(Boolean).join(' · ') || '–' : BO.setsLine(ex.sets, ex.setLoads) || '–'}</span></div>`).join('')}</div>
              <a class="btn primary block" href="/session?date=${d.date}" data-link data-close-sheet>${icons.plan} Log sets</a>` : ''}
            ${d.meals.length ? `<div class="qmeals">${d.meals.map(m => `<button type="button" class="qmeal" role="checkbox" aria-checked="${m.eaten}" data-meal="${m.planMealId}"><span class="tick">${icons.check}</span><span style="min-width:0"><b>${esc(m.name)}</b><span>${esc(m.foods || '')}</span></span>${m.time ? `<em>${m.time}</em>` : ''}</button>`).join('')}</div>` : ''}
            <a class="btn block" href="/today?date=${d.date}" data-link data-close-sheet>Open on Today</a>`;
        };
        paint();
        body.addEventListener('click', ev => {
          const t = ev.target;
          if (t.closest('[data-close-sheet]')) { close(); return; }
          const d = week.days.find(x => x.date === date);
          const stateBtn = t.closest('[data-state]');
          if (stateBtn) {
            d.trainingState = stateBtn.dataset.state;
            paint(); render();
            BO.saving(BO.api('PUT', `/api/days/${d.date}/training`, { state: d.trainingState })).then(w => { if (w && w.days && !BO.busy()) { w.days.forEach(BO.scoreDay); week = w; render(); } }).catch(() => go(week.from));
            return;
          }
          const mealBtn = t.closest('[data-meal]');
          if (mealBtn) {
            const meal = d.meals.find(m => String(m.planMealId) === mealBtn.dataset.meal);
            meal.eaten = !meal.eaten;
            if (meal.eaten) BO.buzz(10);
            paint(); render();
            BO.saving(BO.api('PUT', `/api/days/${d.date}/meals/${meal.planMealId}`, { eaten: meal.eaten })).catch(() => go(week.from));
          }
        });
      }, { label: 'Day' });
    }

    root.addEventListener('click', async event => {
      const t = event.target;
      if (t.closest('#prevWeek')) return go(BO.addDays(week.from, -7));
      if (t.closest('#nextWeek')) return go(BO.addDays(week.from, 7));
      if (t.closest('#thisWeek')) return go(week.today);
      const dayBtn = t.closest('[data-day]');
      if (dayBtn) { openDay(dayBtn.dataset.day); return; }
      if (t.closest('#resetWeek')) {
        const from = week.from;
        const removed = await BO.saving(BO.api('DELETE', `/api/days/week/${from}`));
        await go(from);
        BO.toast(removed.length ? `Week of ${range()} reset` : 'Nothing was logged this week', removed.length ? {
          action: 'Undo',
          onAction: async () => { await BO.saving(BO.api('POST', `/api/days/week/${from}/restore`, removed)); if (root.isConnected) go(from); },
        } : {});
      }
    });

    render();
  };
})();
