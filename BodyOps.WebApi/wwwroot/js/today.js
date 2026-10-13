/* BodyOps — Today: log the day's sets and tick its meals. Opens on today; ?date= opens another day. */
'use strict';

(() => {
  const { esc, icons } = BO;

  BO.screens.today = async function today(root, query) {
    let week = await BO.api('GET', '/api/days/week' + (query.date ? `?date=${encodeURIComponent(query.date)}` : ''));
    let date = query.date && week.days.some(d => d.date === query.date) ? query.date : (week.days.some(d => d.date === week.today) ? week.today : week.from);
    let editing = null;   // { pid, set } while a set is being typed

    const day = () => week.days.find(d => d.date === date);

    async function refresh() {
      if (BO.busy()) return;
      week = await BO.api('GET', `/api/days/week?date=${date}`);
      BO.rerender(render);
    }
    BO.onResume = () => { if (root.isConnected) refresh().catch(() => { }); };

    /** Saves a change and redraws with the server's answer, unless more changes are already waiting. */
    async function save(method, path, body) {
      const updated = await BO.saving(BO.api(method, path, body));
      if (updated && updated.days && !BO.busy()) {
        week = updated;
        if (root.isConnected) BO.rerender(render);
      }
    }

    function scheduleLog(ex) {
      BO.debounce(`log:${date}:${ex.planExerciseId}`, 500, () => {
        const body = ex.type === 'cardio'
          ? { cardioMinutes: ex.cardioMinutes, cardioKm: ex.cardioKm }
          : { loadKg: ex.loadKg, sets: ex.sets };
        save('PUT', `/api/days/${date}/exercises/${ex.planExerciseId}`, body).catch(() => { });
      });
    }

    function setCount(ex) {
      return Math.max(ex.targets.length, ex.sets.length, ex.type === 'strength' ? 1 : 0);
    }

    function exerciseCard(ex) {
      const pid = ex.planExerciseId;
      const last = ex.last;
      if (ex.type === 'cardio') {
        return `<article class="ex" data-pid="${pid}">
          <div class="ex-head"><div><div class="ex-name">${esc(ex.name)}</div><div class="ex-meta">${esc(ex.notes || 'Cardio: log time and distance')}</div></div><span class="plan-tag">${esc(ex.detail)}</span></div>
          <div class="cardio-line">
            <label><input id="min-${pid}" class="num" inputmode="decimal" value="${ex.cardioMinutes ?? ''}" placeholder="0" data-cardio="cardioMinutes" aria-label="Minutes"><span>min</span></label>
            <label><input id="km-${pid}" class="num" inputmode="decimal" value="${ex.cardioKm ?? ''}" placeholder="0" data-cardio="cardioKm" aria-label="Kilometres"><span>km</span></label>
          </div>
        </article>`;
      }

      const count = setCount(ex);
      let cells = '';
      for (let i = 0; i < count; i++) {
        const value = ex.sets[i];
        const target = ex.targets[i] || 0;
        if (editing && editing.pid === pid && editing.set === i) {
          cells += `<span class="set filled"><input id="set-${pid}-${i}" class="num" inputmode="numeric" value="${value ?? ''}" data-set-input="${i}" aria-label="Reps in set ${i + 1}"></span>`;
        } else if (value != null) {
          cells += `<button type="button" class="set filled" data-set="${i}" aria-label="Set ${i + 1}: ${value} reps. Tap to change."><b class="num">${value}</b><small>set ${i + 1}</small></button>`;
        } else {
          cells += `<button type="button" class="set" data-set="${i}" aria-label="Log set ${i + 1}${target ? `, ${target} reps planned` : ''}"><b class="num">${target || '–'}</b><small>set ${i + 1}</small></button>`;
        }
      }
      if (count < 20) cells += '<button type="button" class="set add" data-add-set aria-label="Add a set">+</button>';

      const lastText = last
        ? `Last: ${last.loadKg != null ? BO.load(last.loadKg) + ' kg' : 'no load'}${last.reps.length ? ' · ' + repsText(last.reps) : ''} · ${BO.shortDate(last.date)}`
        : 'First time logged';
      const foot = [];
      if (ex.volumeKg) foot.push(`<span>Volume ${BO.volume(ex.volumeKg)}</span>`);
      if (ex.loadKg != null && last && last.loadKg != null) {
        const diff = ex.loadKg - last.loadKg;
        foot.push(diff > 0 ? `<span class="up">+${BO.load(diff)} kg vs last time</span>` : diff < 0 ? `<span>${BO.load(diff)} kg vs last time</span>` : '<span>same load as last time</span>');
      }
      const active = ex.sets.some(s => s != null) && ex.sets.slice(0, count).some(s => s == null);

      return `<article class="ex${active ? ' active' : ''}" data-pid="${pid}">
        <div class="ex-head"><div>
          <div class="ex-name">${esc(ex.name)}${ex.isPr ? ' <span class="pr">PR</span>' : ''}</div>
          <div class="ex-meta">${esc(lastText)}${ex.notes ? ' · ' + esc(ex.notes) : ''}</div>
        </div><span class="plan-tag">${esc(ex.detail)}</span></div>
        <div class="ex-log">
          <div class="load">
            <button type="button" data-step="-1" aria-label="Less load">−</button>
            <input id="load-${pid}" class="num" inputmode="decimal" value="${BO.load(ex.loadKg)}" placeholder="${last && last.loadKg != null ? BO.load(last.loadKg) : 'kg'}" data-load aria-label="Load in kg">
            <small>kg</small>
            <button type="button" data-step="1" aria-label="More load">+</button>
          </div>
          <div class="sets">${cells}</div>
        </div>
        ${foot.length ? `<div class="ex-foot">${foot.join('')}</div>` : ''}
      </article>`;
    }

    function repsText(reps) {
      return reps.length && reps.every(r => r === reps[0]) ? `${reps.length}×${reps[0]}` : reps.join(', ');
    }

    function render() {
      const d = day();
      const isToday = date === week.today;
      const eaten = d.meals.filter(m => m.eaten).length;
      const strength = d.exercises.filter(e => e.type === 'strength');
      const setsDone = strength.reduce((n, e) => n + e.sets.filter(s => s != null).length, 0);
      const setsPlanned = strength.reduce((n, e) => n + Math.max(e.targets.length, e.sets.filter(s => s != null).length), 0);
      const mealPct = d.meals.length ? Math.round(eaten / d.meals.length * 100) : 0;
      const setPct = setsPlanned ? Math.min(100, Math.round(setsDone / setsPlanned * 100)) : 0;
      const state = d.trainingState;

      const strip = week.days.map(x => {
        const meals = x.meals.length;
        const done = x.meals.filter(m => m.eaten).length;
        const marks = (meals ? `<i class="${done === meals ? 'm-full' : done ? 'm-part' : ''}"></i>` : '')
          + (x.trainingState === 'done' ? '<i class="t-done"></i>' : x.trainingState === 'missed' ? '<i class="t-miss"></i>' : '');
        return `<button type="button" class="day${x.date === week.today ? ' today' : ''}" data-date="${x.date}" aria-pressed="${x.date === date}">
          <small>${BO.DAYS_SHORT[x.weekday - 1]}</small><b>${BO.dayNum(x.date)}</b><span class="marks">${marks}</span></button>`;
      }).join('');

      const training = d.exercises.length
        ? d.exercises.map(exerciseCard).join('')
        : `<div class="empty"><b>Nothing planned for ${BO.DAYS[d.weekday - 1]}.</b><span>Rest day, or add exercises to this day in your plan.</span><a class="btn" href="/plan?day=${d.weekday}" data-link>Open the plan</a></div>`;

      const meals = d.meals.length
        ? `<div class="meals">${d.meals.map(m => `<button type="button" class="meal" role="checkbox" aria-checked="${m.eaten}" data-meal="${m.planMealId}">
            <span class="tick">${icons.check}</span><span style="min-width:0"><span class="meal-name">${esc(m.name)}</span><span class="meal-foods">${esc(m.foods || 'No foods listed')}</span></span></button>`).join('')}</div>`
        : `<div class="empty"><b>No meals planned for ${BO.DAYS[d.weekday - 1]}.</b><a class="btn" href="/plan?day=${d.weekday}&tab=diet" data-link>Plan meals</a></div>`;

      root.innerHTML = `
        <div class="page-head">
          <div>
            <h1 class="title-xl">${isToday ? 'Today' : BO.DAYS[d.weekday - 1]}</h1>
            <span class="sub">${isToday ? BO.DAYS[d.weekday - 1] + ', ' : ''}${BO.longDate(date)} · week of ${BO.weekRange(week.from)}${!isToday ? ` · <a href="/today" data-link>back to today</a>` : ''}</span>
          </div>
          <div class="row">${BO.syncHTML()}<a class="icon-btn phone-only" href="/settings" data-link aria-label="Settings">${icons.settings}</a></div>
        </div>
        <div class="days" role="group" aria-label="Day of the week">${strip}</div>
        <div class="today-sum">
          <div class="sum"><div class="ring" style="--p:${mealPct}"><span class="num">${eaten}/${d.meals.length}</span></div><div><b>Meals</b><span>${d.meals.length ? (eaten === d.meals.length ? 'All eaten' : `${d.meals.length - eaten} left`) : 'None planned'}</span></div></div>
          <div class="sum"><div class="ring" style="--p:${setPct}"><span class="num">${setsDone}/${setsPlanned}</span></div><div><b>Sets</b><span>${state === 'done' ? 'Trained' : state === 'missed' ? 'Skipped' : setsDone ? 'Session started' : strength.length ? 'Not started' : 'No sets planned'}</span></div></div>
        </div>
        <div class="today-grid">
          <section class="stack" aria-labelledby="h-training">
            <div class="section-head">
              <h2 class="title-l" id="h-training">Training</h2>
              <div class="seg" role="group" aria-label="Session">
                <button type="button" data-state="pending" aria-pressed="${state === 'pending'}">Not yet</button>
                <button type="button" class="is-done" data-state="done" aria-pressed="${state === 'done'}">Trained</button>
                <button type="button" class="is-miss" data-state="missed" aria-pressed="${state === 'missed'}">Skipped</button>
              </div>
            </div>
            <div class="stack" style="gap:10px">${training}</div>
          </section>
          <section class="stack" aria-labelledby="h-meals">
            <div class="section-head">
              <h2 class="title-l" id="h-meals">Meals</h2>
              ${d.meals.length ? `<button type="button" class="link-btn" id="markAll">${eaten === d.meals.length ? 'Clear all' : 'Mark all eaten'}</button>` : ''}
            </div>
            ${meals}
          </section>
        </div>`;

      if (editing) {
        const input = root.querySelector(`#set-${editing.pid}-${editing.set}`);
        if (input) { input.focus(); input.select(); }
      }
    }

    function findExercise(el) {
      const card = el.closest('[data-pid]');
      return card ? day().exercises.find(e => String(e.planExerciseId) === card.dataset.pid) : null;
    }

    function commitEdit(input) {
      if (!editing) return;
      const ex = day().exercises.find(e => e.planExerciseId === editing.pid);
      const value = BO.readNumber(input.value);
      const reps = value == null ? null : Math.max(0, Math.min(999, Math.round(value)));
      while (ex.sets.length <= editing.set) ex.sets.push(null);
      ex.sets[editing.set] = reps;
      editing = null;
      scheduleLog(ex);
      BO.rerender(render);
    }

    root.addEventListener('click', event => {
      const t = event.target;
      const dayBtn = t.closest('[data-date]');
      if (dayBtn) {
        date = dayBtn.dataset.date;
        editing = null;
        BO.setQuery({ date: date === week.today ? null : date });
        render();
        return;
      }

      const stateBtn = t.closest('[data-state]');
      if (stateBtn) {
        day().trainingState = stateBtn.dataset.state;
        render();
        save('PUT', `/api/days/${date}/training`, { state: stateBtn.dataset.state }).catch(() => refresh());
        return;
      }

      const mealBtn = t.closest('[data-meal]');
      if (mealBtn) {
        const meal = day().meals.find(m => String(m.planMealId) === mealBtn.dataset.meal);
        meal.eaten = !meal.eaten;
        render();
        save('PUT', `/api/days/${date}/meals/${meal.planMealId}`, { eaten: meal.eaten }).catch(() => refresh());
        return;
      }

      if (t.closest('#markAll')) {
        const all = day().meals.every(m => m.eaten);
        day().meals.forEach(m => { m.eaten = !all; });
        render();
        save('PUT', `/api/days/${date}/meals`, { eaten: !all }).catch(() => refresh());
        return;
      }

      const ex = findExercise(t);
      if (!ex) return;

      const step = t.closest('[data-step]');
      if (step) {
        const base = ex.loadKg ?? (ex.last && ex.last.loadKg) ?? 0;
        ex.loadKg = Math.max(0, Math.round((base + Number(step.dataset.step) * 2.5) * 100) / 100);
        scheduleLog(ex);
        BO.rerender(render);
        return;
      }

      if (t.closest('[data-add-set]')) {
        ex.sets.length = setCount(ex);
        ex.sets.push(null);
        for (let i = 0; i < ex.sets.length; i++) if (ex.sets[i] === undefined) ex.sets[i] = null;
        editing = { pid: ex.planExerciseId, set: ex.sets.length - 1 };
        render();
        return;
      }

      const setBtn = t.closest('[data-set]');
      if (setBtn) {
        const i = Number(setBtn.dataset.set);
        while (ex.sets.length < setCount(ex)) ex.sets.push(null);
        if (ex.sets[i] == null) {
          const previous = ex.sets.slice(0, i).reverse().find(s => s != null);
          ex.sets[i] = ex.targets[i] || (ex.last && ex.last.reps[i]) || previous || 10;
          if (ex.loadKg == null && ex.last && ex.last.loadKg != null) ex.loadKg = ex.last.loadKg;
          scheduleLog(ex);
          render();
        } else {
          editing = { pid: ex.planExerciseId, set: i };
          render();
        }
      }
    });

    root.addEventListener('input', event => {
      const t = event.target;
      const ex = findExercise(t);
      if (!ex) return;
      if (t.matches('[data-load]')) {
        const value = BO.readNumber(t.value);
        ex.loadKg = value == null ? null : Math.max(0, value);
        scheduleLog(ex);
      } else if (t.matches('[data-cardio]')) {
        const value = BO.readNumber(t.value);
        ex[t.dataset.cardio] = value == null ? null : Math.max(0, value);
        scheduleLog(ex);
      }
    });

    root.addEventListener('focusout', event => {
      if (event.target.matches('[data-set-input]')) commitEdit(event.target);
    });
    root.addEventListener('keydown', event => {
      if (event.target.matches('[data-set-input]') && (event.key === 'Enter' || event.key === 'Tab')) {
        event.preventDefault();
        event.target.blur();
      } else if (event.target.matches('[data-set-input]') && event.key === 'Escape') {
        editing = null;
        render();
      }
    });

    render();
  };
})();
