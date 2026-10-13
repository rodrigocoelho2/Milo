/* BodyOps — Week: the seven days at a glance, the picked day's session and meals, muscles, weight and consistency. */
'use strict';

(() => {
  const { esc, icons } = BO;

  BO.screens.week = async function weekScreen(root, query) {
    const [first, weights, consistency] = await Promise.all([
      BO.api('GET', '/api/days/week' + (query.date ? `?date=${encodeURIComponent(query.date)}` : '')),
      BO.api('GET', '/api/weights'),
      BO.api('GET', '/api/days/consistency?weeks=21'),
    ]);
    let week = first;
    let picked = week.days.some(d => d.date === query.day) ? query.day : (week.days.some(d => d.date === week.today) ? week.today : week.from);

    async function load(date) {
      week = await BO.api('GET', `/api/days/week?date=${date}`);
      picked = week.days.some(d => d.date === week.today) ? week.today : week.from;
      BO.setQuery({ date: week.from === BO.monday(week.today) ? null : week.from, day: null });
      render();
    }
    BO.onResume = () => { if (root.isConnected && !BO.busy()) load(week.from).catch(() => { }); };

    async function save(method, path, body) {
      const updated = await BO.saving(BO.api(method, path, body));
      if (updated && updated.days) {
        week = updated;
        if (root.isConnected) render();
      }
    }

    function label() {
      const thisMonday = BO.monday(week.today);
      if (week.from === thisMonday) return 'This week';
      if (week.from === BO.addDays(thisMonday, -7)) return 'Last week';
      if (week.from === BO.addDays(thisMonday, 7)) return 'Next week';
      return week.from < thisMonday ? 'Past week' : 'Coming week';
    }

    function ribbon() {
      return week.days.map(d => {
        const eaten = d.meals.filter(m => m.eaten).length;
        const bars = d.meals.map((m, i) => `<i class="${i < eaten ? 'on' : ''}"></i>`).join('');
        const chip = !d.exercises.length && d.trainingState === 'pending' ? '<span class="chip rest">Rest</span>'
          : d.trainingState === 'done' ? `<span class="chip done">${icons.check}Trained</span>`
            : d.trainingState === 'missed' ? '<span class="chip miss">Skipped</span>'
              : '<span class="chip">Planned</span>';
        const what = d.exercises.length
          ? (d.exercises.length === 1 && d.exercises[0].type === 'cardio' ? `${d.exercises[0].name} ${d.exercises[0].detail}` : BO.plural(d.exercises.length, 'exercise'))
          : 'Rest day';
        const isToday = d.date === week.today;
        return `<button type="button" class="rday${isToday ? ' today' : ''}" data-day="${d.date}" aria-pressed="${d.date === picked}">
          <span class="top"><small>${isToday ? 'Today' : BO.DAYS_SHORT[d.weekday - 1]}</small><b>${BO.dayNum(d.date)}</b></span>
          <span class="mealbar" aria-label="${eaten} of ${d.meals.length} meals">${bars}</span>
          <span class="dot ${d.trainingState === 'done' ? 'done' : d.trainingState === 'missed' ? 'miss' : ''}"></span>
          ${chip}<span class="what">${esc(what)}</span></button>`;
      }).join('');
    }

    function session(d) {
      if (!d.exercises.length) return `<div class="empty"><b>Nothing planned.</b><a class="btn" href="/plan?day=${d.weekday}" data-link>Open the plan</a></div>`;
      const rows = d.exercises.map(ex => {
        if (ex.type === 'cardio') {
          const done = [ex.cardioMinutes != null ? `${BO.load(ex.cardioMinutes)} min` : '', ex.cardioKm != null ? `${BO.load(ex.cardioKm)} km` : ''].filter(Boolean).join(' · ');
          return `<tr><td><b style="font-weight:600">${esc(ex.name)}</b></td><td class="sub">${esc(ex.detail)}</td><td class="r" colspan="2">${done || '<span class="sub">–</span>'}</td><td class="r"><span class="sub">cardio</span></td><td></td></tr>`;
        }
        const count = Math.max(ex.targets.length, ex.sets.length);
        const sets = Array.from({ length: count }, (_, i) => ex.sets[i] != null ? `<i>${ex.sets[i]}</i>` : '<i class="e">–</i>').join('');
        const last = ex.last ? `${ex.last.loadKg != null ? BO.load(ex.last.loadKg) + ' kg' : ''}${ex.last.reps.length ? ' · ' + ex.last.reps.join(',') : ''}` : '—';
        return `<tr><td><b style="font-weight:600">${esc(ex.name)}</b>${ex.isPr ? ' <span class="pr">PR</span>' : ''}</td>
          <td class="sub">${esc(ex.detail)}</td>
          <td class="r">${ex.loadKg != null ? `<span class="kg num">${BO.load(ex.loadKg)}</span> <span class="sub">kg</span>` : '<span class="sub">–</span>'}</td>
          <td><span class="mini-sets">${sets}</span></td>
          <td class="r num">${ex.volumeKg ? BO.volume(ex.volumeKg) : '<span class="sub">–</span>'}</td>
          <td class="r sub num">${esc(last)}</td></tr>`;
      }).join('');
      const list = d.exercises.map(ex => {
        if (ex.type === 'cardio') {
          const done = [ex.cardioMinutes != null ? `${BO.load(ex.cardioMinutes)} min` : '', ex.cardioKm != null ? `${BO.load(ex.cardioKm)} km` : ''].filter(Boolean).join(' · ');
          return `<div class="srow2"><div><b style="font-weight:600">${esc(ex.name)}</b><div class="sub">${esc(ex.detail)}</div></div><span class="sub">${done || 'not logged'}</span></div>`;
        }
        const count = Math.max(ex.targets.length, ex.sets.length);
        const sets = Array.from({ length: count }, (_, i) => ex.sets[i] != null ? `<i>${ex.sets[i]}</i>` : '<i class="e">–</i>').join('');
        return `<div class="srow2"><div><b style="font-weight:600">${esc(ex.name)}</b>${ex.isPr ? ' <span class="pr">PR</span>' : ''}
            <div class="sub">${esc(ex.detail)}${ex.loadKg != null ? ` · ${BO.load(ex.loadKg)} kg` : ''}${ex.volumeKg ? ` · ${BO.volume(ex.volumeKg)}` : ''}</div></div>
          <span class="mini-sets">${sets}</span></div>`;
      }).join('');
      return `<div class="session-list">${list}</div><div class="table-wrap session-table"><table class="log"><thead><tr><th>Exercise</th><th>Plan</th><th class="r">Load</th><th>Sets</th><th class="r">Volume</th><th class="r">Last time</th></tr></thead><tbody>${rows}</tbody></table></div>`;
    }

    function weightPanel() {
      const entries = weights.entries.slice(-12);
      if (!entries.length) return `<div class="empty"><b>No weigh-ins yet.</b><a class="btn" href="/weight" data-link>Log your weight</a></div>`;
      const latest = entries[entries.length - 1];
      const goal = weights.goalKg;
      return BO.lineChart(entries.map(e => ({ date: e.date, value: e.weightKg })), {
        goal, goalLabel: goal != null ? `goal ${BO.kg(goal)} kg` : '', height: 170, label: v => BO.kg(v),
      }) + `<span class="sub">Latest ${BO.kg(latest.weightKg)} kg on ${BO.shortDate(latest.date)}${goal != null ? ` · ${BO.kg(Math.abs(latest.weightKg - goal))} kg to goal` : ''}</span>`;
    }

    function render() {
      const d = week.days.find(x => x.date === picked);
      const t = week.totals;
      const latest = weights.entries[weights.entries.length - 1];
      const notes = [];
      if (t.unknownSetCount) notes.push(`${BO.plural(t.unknownSetCount, 'exercise')} with no sets planned or logged counted as one set.`);

      root.innerHTML = `
        <div class="page-head">
          <div><span class="sub">${label()}</span><h1 class="title-xl">${BO.weekRange(week.from)}</h1></div>
          <div class="week-nav">
            <button type="button" class="icon-btn" id="prevWeek" aria-label="Previous week">${icons.left}</button>
            <button type="button" class="btn" id="thisWeek">Today</button>
            <button type="button" class="icon-btn" id="nextWeek" aria-label="Next week">${icons.right}</button>
            <label class="icon-btn" style="position:relative;cursor:pointer" aria-label="Pick a date">${icons.calendar}
              <input type="date" id="pickDate" value="${picked}" style="position:absolute;inset:0;opacity:0;cursor:pointer;width:100%"></label>
          </div>
        </div>
        <div class="week-totals">
          <div class="wt"><b class="num">${t.mealsEaten} / ${t.mealsPlanned}</b><span>meals eaten</span></div>
          <div class="wt"><b class="num">${t.trainedDays} / ${t.trainingDaysPlanned}</b><span>training days done${t.missedDays ? ` · ${t.missedDays} skipped` : ''}</span></div>
          <div class="wt"><b class="num">${BO.volume(t.volumeKg)}</b><span>volume lifted</span></div>
          <div class="wt"><b class="num">${latest ? BO.kg(latest.weightKg) + ' kg' : '—'}</b><span>latest weigh-in</span></div>
        </div>
        <div class="ribbon" role="group" aria-label="Days">${ribbon()}</div>
        <div class="week-grid">
          <div class="stack">
            <section class="panel" aria-labelledby="h-session">
              <div class="panel-head">
                <div><h2 class="title-l" id="h-session">${BO.DAYS[d.weekday - 1]} ${BO.shortDate(d.date)}</h2>
                  <span class="sub">${BO.plural(d.exercises.length, 'exercise')}${d.logged ? '' : ' · nothing logged yet'}</span></div>
                <div class="row" style="flex-wrap:wrap">
                  <div class="seg" role="group" aria-label="Session">
                    <button type="button" data-state="pending" aria-pressed="${d.trainingState === 'pending'}">Not yet</button>
                    <button type="button" class="is-done" data-state="done" aria-pressed="${d.trainingState === 'done'}">Trained</button>
                    <button type="button" class="is-miss" data-state="missed" aria-pressed="${d.trainingState === 'missed'}">Skipped</button>
                  </div>
                  <a class="btn btn-primary" href="/today?date=${d.date}" data-link>Log sets</a>
                </div>
              </div>
              ${session(d)}
            </section>
            <section class="panel" aria-labelledby="h-meals">
              <div class="panel-head"><div><h2 class="title-l" id="h-meals">Meals</h2><span class="sub">${d.meals.filter(m => m.eaten).length} of ${d.meals.length} eaten</span></div>
                ${d.meals.length ? `<button type="button" class="link-btn" id="markAll">${d.meals.every(m => m.eaten) ? 'Clear all' : 'Mark all eaten'}</button>` : ''}</div>
              ${d.meals.length ? `<div class="desk-meals">${d.meals.map(m => `<button type="button" class="dm" role="checkbox" aria-checked="${m.eaten}" data-meal="${m.planMealId}">
                  <span class="spread"><b>${esc(m.name)}</b><span class="tick">${icons.check}</span></span>
                  <span class="dm-foods">${esc(m.foods).split(' · ').join('<br>')}</span></button>`).join('')}</div>`
                : `<div class="empty"><b>No meals planned.</b><a class="btn" href="/plan?day=${d.weekday}&tab=diet" data-link>Plan meals</a></div>`}
            </section>
          </div>
          <div class="stack">
            <section class="panel" aria-labelledby="h-muscles">
              <div class="panel-head"><h2 class="title-l" id="h-muscles">Muscles this week</h2><span class="sub">sets on trained days</span></div>
              ${t.muscles.length
                ? `<div class="muscles-wrap">${BO.bodyMap(t.muscles)}${BO.muscleRank(t.muscles)}</div>${notes.length ? `<p class="fine">${esc(notes.join(' '))}</p>` : ''}`
                : '<p class="sub" style="margin:0">Mark a day as Trained, or log its sets, and the map fills in.</p>'}
            </section>
            <section class="panel" aria-labelledby="h-weight">
              <div class="panel-head"><h2 class="title-l" id="h-weight">Weight</h2><a class="link-btn" href="/weight" data-link>All weigh-ins</a></div>
              ${weightPanel()}
            </section>
            <section class="panel" aria-labelledby="h-consistency">
              <div class="panel-head"><h2 class="title-l" id="h-consistency">Consistency</h2><span class="sub">last 21 weeks</span></div>
              ${BO.heatmap(consistency, week.today)}
            </section>
            <div class="row" style="justify-content:flex-end"><button type="button" class="btn btn-danger" id="resetWeek">Reset this week</button></div>
          </div>
        </div>`;
    }

    root.addEventListener('click', async event => {
      const t = event.target;
      if (t.closest('#prevWeek')) return load(BO.addDays(week.from, -7));
      if (t.closest('#nextWeek')) return load(BO.addDays(week.from, 7));
      if (t.closest('#thisWeek')) return load(week.today);

      const dayBtn = t.closest('[data-day]');
      if (dayBtn) {
        picked = dayBtn.dataset.day;
        BO.setQuery({ day: picked });
        render();
        return;
      }

      const d = week.days.find(x => x.date === picked);
      const stateBtn = t.closest('[data-state]');
      if (stateBtn) {
        d.trainingState = stateBtn.dataset.state;
        render();
        return save('PUT', `/api/days/${d.date}/training`, { state: stateBtn.dataset.state }).catch(() => load(week.from));
      }
      const mealBtn = t.closest('[data-meal]');
      if (mealBtn) {
        const meal = d.meals.find(m => String(m.planMealId) === mealBtn.dataset.meal);
        meal.eaten = !meal.eaten;
        render();
        return save('PUT', `/api/days/${d.date}/meals/${meal.planMealId}`, { eaten: meal.eaten }).catch(() => load(week.from));
      }
      if (t.closest('#markAll')) {
        const all = d.meals.every(m => m.eaten);
        d.meals.forEach(m => { m.eaten = !all; });
        render();
        return save('PUT', `/api/days/${d.date}/meals`, { eaten: !all }).catch(() => load(week.from));
      }
      if (t.closest('#resetWeek')) {
        const from = week.from;
        const removed = await BO.saving(BO.api('DELETE', `/api/days/week/${from}`));
        await load(from);
        BO.toast(removed.length ? `Week of ${BO.weekRange(from)} reset` : 'Nothing was logged this week', removed.length ? {
          action: 'Undo',
          onAction: async () => {
            week = await BO.saving(BO.api('POST', `/api/days/week/${from}/restore`, removed));
            if (root.isConnected) render();
          },
        } : {});
      }
    });

    root.addEventListener('change', event => {
      if (event.target.id === 'pickDate' && event.target.value) {
        const date = event.target.value;
        load(date).then(() => { picked = date; BO.setQuery({ day: date }); render(); });
      }
    });

    render();
  };
})();
