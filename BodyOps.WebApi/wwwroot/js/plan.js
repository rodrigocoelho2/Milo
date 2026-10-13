/* BodyOps — Plan: the weekly plan editor (Training and Diet), the day's muscle map and each exercise's progression.
   Every change saves itself a moment after you stop typing. */
'use strict';

(() => {
  const { esc, icons } = BO;
  let keySeq = 0;
  const key = () => 'k' + (++keySeq);

  BO.screens.plan = async function plan(root, query) {
    const [loaded, exerciseList] = await Promise.all([BO.api('GET', '/api/plan'), BO.api('GET', '/api/exercises')]);
    let exercisesKnown = exerciseList;
    let weekday = Math.min(7, Math.max(1, Number(query.day) || BO.weekday(BO.localToday())));
    let tab = query.tab === 'diet' ? 'diet' : 'training';
    let openMuscles = null;     // key of the exercise whose muscle picker is open
    let copyOpen = false;
    let copyTargets = new Set();
    let progressName = null;
    let progress = null;        // { name, bestKg, sessions }

    // Local editable copy of the plan, with stable keys so focus survives re-renders.
    const days = loaded.days.map(fromServer);
    function fromServer(day) {
      return {
        weekday: day.weekday,
        muscles: day.muscles,
        exercises: day.exercises.map(e => ({ _k: key(), id: e.id, type: e.type, name: e.name, detail: e.detail, notes: e.notes, muscles: e.muscles, musclesAuto: e.musclesAuto, targets: e.targets })),
        meals: day.meals.map(m => ({ _k: key(), id: m.id, name: m.name, foods: m.foods.map(f => ({ _k: key(), name: f.name, amount: f.amount, unit: f.unit })) })),
      };
    }
    const dayOf = n => days[n - 1];

    /* ---------- saving ---------- */

    function saveExercises(n) {
      BO.debounce(`plan-ex:${n}`, 700, async () => {
        const day = dayOf(n);
        const sent = day.exercises.filter(e => e.name.trim());
        try {
          const result = await BO.saving(BO.api('PUT', `/api/plan/${n}/exercises`, sent.map(e => ({
            id: e.id, type: e.type, name: e.name, detail: e.detail, notes: e.notes, muscles: e.musclesAuto ? null : e.muscles,
          }))));
          sent.forEach((e, i) => {
            const saved = result.exercises[i];
            if (!saved) return;
            e.id = saved.id;
            e.targets = saved.targets;
            if (e.musclesAuto) e.muscles = saved.muscles;
          });
          day.muscles = result.muscles;
          refreshExercises();
          if (!BO.busy() && root.isConnected) BO.rerender(render);
        } catch (e) { /* the toast already explains */ }
      });
    }

    function saveMeals(n) {
      BO.debounce(`plan-meals:${n}`, 700, async () => {
        const day = dayOf(n);
        const sent = day.meals.filter(m => m.name.trim());
        try {
          const result = await BO.saving(BO.api('PUT', `/api/plan/${n}/meals`, sent.map(m => ({
            id: m.id,
            name: m.name,
            foods: m.foods.filter(f => f.name.trim()).map(f => ({ name: f.name, amount: f.amount || 0, unit: f.unit })),
          }))));
          sent.forEach((m, i) => { if (result.meals[i]) m.id = result.meals[i].id; });
          if (!BO.busy() && root.isConnected) BO.rerender(render);
        } catch (e) { /* the toast already explains */ }
      });
    }

    async function refreshExercises() {
      try { exercisesKnown = await BO.api('GET', '/api/exercises'); } catch (e) { }
    }

    async function loadProgress(name) {
      progressName = name;
      progress = name ? await BO.api('GET', `/api/exercises/history?name=${encodeURIComponent(name)}`) : null;
      if (root.isConnected) BO.rerender(render);
    }

    /* ---------- rendering ---------- */

    function tabSummary(day) {
      const parts = [];
      if (day.exercises.length) {
        parts.push(day.exercises.length === 1 && day.exercises[0].type === 'cardio' ? `${day.exercises[0].name}` : BO.plural(day.exercises.length, 'exercise'));
      }
      if (day.meals.length) parts.push(BO.plural(day.meals.length, 'meal'));
      return parts.join(' · ') || 'Rest day';
    }

    function exerciseRow(e, index) {
      const k = e._k;
      const tags = e.muscles.length
        ? e.muscles.map(m => `<span class="mtag${e.musclesAuto ? ' auto' : ''}">${BO.MUSCLES[m]}</span>`).join('')
        : `<span class="mtag auto">${e.type === 'cardio' ? 'No muscles (cardio)' : e.name.trim() ? 'No muscles found' : 'Muscles appear when you name it'}</span>`;
      const picker = openMuscles === k ? `<div class="mpicker">
          <span class="sub">Pick the muscles this exercise trains. ${e.musclesAuto ? 'Now detected from the name.' : 'Picked by hand.'}</span>
          <div class="chips">${Object.entries(BO.MUSCLES).map(([id, label]) => `<button type="button" data-muscle="${id}" aria-pressed="${e.muscles.includes(id)}">${label}</button>`).join('')}</div>
          <div class="row" style="justify-content:space-between">${e.musclesAuto ? '<span></span>' : '<button type="button" class="link-btn" data-muscles-auto>Detect from the name again</button>'}<button type="button" class="btn" data-muscles-close>Done</button></div>
        </div>` : '';
      return `<div class="prow${progress && e.name.trim().toLowerCase() === (progressName || '').toLowerCase() ? ' picked' : ''}" data-k="${k}" data-index="${index}" draggable="false">
        <span class="grip" title="Drag to reorder" data-grip>⋮⋮</span>
        <select class="field p-type" id="type-${k}" data-f="type" aria-label="Type"><option value="strength"${e.type === 'strength' ? ' selected' : ''}>Strength</option><option value="cardio"${e.type === 'cardio' ? ' selected' : ''}>Cardio</option></select>
        <input class="field p-name" id="name-${k}" data-f="name" value="${esc(e.name)}" placeholder="Exercise, e.g. Pec deck" aria-label="Exercise name" maxlength="100">
        <input class="field small p-detail" id="detail-${k}" data-f="detail" value="${esc(e.detail)}" placeholder="${e.type === 'cardio' ? '20 km' : '3x12'}" aria-label="Sets × reps" maxlength="60">
        <input class="field p-notes" id="notes-${k}" data-f="notes" value="${esc(e.notes)}" placeholder="Notes" aria-label="Notes" maxlength="200">
        <button type="button" class="del" data-del-ex aria-label="Remove ${esc(e.name || 'exercise')}">${icons.x}</button>
        <span class="mtags">${tags}<button type="button" class="link-btn" style="font-size:12px;padding:0 4px" data-muscles-open>${openMuscles === k ? '' : 'Edit muscles'}</button>${e.name.trim() && e.type === 'strength' ? `<button type="button" class="link-btn" style="font-size:12px;padding:0 4px" data-progress>Progress</button>` : ''}</span>
        ${picker}
      </div>`;
    }

    function mealCard(m) {
      const k = m._k;
      const foods = m.foods.map(f => `<div class="foodrow" data-f-k="${f._k}">
          <input class="field" id="food-${f._k}" data-food="name" value="${esc(f.name)}" placeholder="Food" aria-label="Food" maxlength="80">
          <input class="field small" id="amount-${f._k}" data-food="amount" inputmode="decimal" value="${f.amount ?? ''}" placeholder="0" aria-label="Amount">
          <span class="unit"><button type="button" data-unit="grams" aria-pressed="${f.unit === 'grams'}">g</button><button type="button" data-unit="pieces" aria-pressed="${f.unit === 'pieces'}">pc</button></span>
          <button type="button" class="del" data-del-food aria-label="Remove ${esc(f.name || 'food')}">${icons.x}</button>
        </div>`).join('');
      return `<div class="mealcard" data-k="${k}">
        <div class="row"><input class="field" id="meal-${k}" data-meal-name value="${esc(m.name)}" style="font-weight:700" aria-label="Meal name" maxlength="80"><button type="button" class="del" data-del-meal aria-label="Remove ${esc(m.name)}">${icons.x}</button></div>
        ${foods || '<span class="sub">No foods yet.</span>'}
        <button type="button" class="link-btn" style="align-self:flex-start" data-add-food>Add food</button>
      </div>`;
    }

    function copyBox(part) {
      if (!copyOpen) return '';
      const others = [1, 2, 3, 4, 5, 6, 7].filter(n => n !== weekday);
      return `<div class="copybox">
        <span class="sub">Replace these days' ${part} with ${BO.DAYS[weekday - 1]}'s:</span>
        <div class="copyto">${others.map(n => `<button type="button" data-copy-day="${n}" aria-pressed="${copyTargets.has(n)}">${BO.DAYS_SHORT[n - 1]}</button>`).join('')}
          <button type="button" data-copy-all>All</button></div>
        <div class="row" style="justify-content:flex-end"><button type="button" class="btn" data-copy-cancel>Cancel</button><button type="button" class="btn btn-primary" data-copy-go${copyTargets.size ? '' : ' disabled'}>Copy to ${BO.plural(copyTargets.size, 'day')}</button></div>
      </div>`;
    }

    function progressPanel() {
      const names = exercisesKnown.map(x => x.name);
      if (!names.length) return '';
      if (progressName == null) {
        const withHistory = dayOf(weekday).exercises.map(e => exercisesKnown.find(x => x.name.toLowerCase() === e.name.trim().toLowerCase() && x.sessions > 0)).find(Boolean)
          || exercisesKnown.find(x => x.sessions > 0);
        if (withHistory) { loadProgress(withHistory.name); return ''; }
      }
      const sessions = progress ? progress.sessions.filter(s => s.loadKg != null) : [];
      let body;
      if (!progress) body = '<p class="sub" style="margin:0">Pick an exercise to see its loads over time.</p>';
      else if (!sessions.length) body = '<p class="chart-empty">No loads logged yet. Log a session on Today and it shows up here.</p>';
      else {
        const best = Math.max(...sessions.map(s => s.loadKg));
        const bestIndex = sessions.map(s => s.loadKg).lastIndexOf(best);
        const first = sessions[0].loadKg, last = sessions[sessions.length - 1].loadKg;
        body = BO.lineChart(sessions.map(s => ({ date: s.date, value: s.loadKg })), { highlight: sessions.length > 1 ? bestIndex : -1, height: 160, label: v => BO.load(v) })
          + `<span class="sub">${BO.plural(sessions.length, 'session')} · now ${BO.load(last)} kg · best ${BO.load(best)} kg${sessions.length > 1 ? ` · ${BO.signed(last - first, 1)} kg since ${BO.shortDate(sessions[0].date)}` : ''}</span>`;
      }
      return `<section class="panel" aria-labelledby="h-progress">
        <div class="panel-head"><div><h2 class="title-l" id="h-progress">${esc(progress ? progress.name : 'Progress')}</h2><span class="sub">heaviest load per session</span></div>
          ${progress && progress.bestKg != null ? `<span class="pr">PR ${BO.load(progress.bestKg)} kg</span>` : ''}</div>
        <select class="field prog-select" id="progressPick" aria-label="Exercise">${progress ? '' : '<option value="" selected disabled>Choose an exercise</option>'}${names.map(n => `<option${progress && n.toLowerCase() === progress.name.toLowerCase() ? ' selected' : ''}>${esc(n)}</option>`).join('')}</select>
        ${body}
      </section>`;
    }

    function render() {
      const day = dayOf(weekday);
      const tabs = days.map(d => `<button type="button" class="daytab" data-weekday="${d.weekday}" aria-pressed="${d.weekday === weekday}"><b>${BO.DAYS[d.weekday - 1]}</b><span>${esc(tabSummary(d))}</span></button>`).join('');
      const sets = day.exercises.reduce((n, e) => n + e.targets.length, 0);

      const training = `<div class="plan-grid">
        <section class="panel" aria-labelledby="h-plan">
          <div class="panel-head">
            <div><h2 class="title-l" id="h-plan">${BO.DAYS[weekday - 1]}</h2><span class="sub">${BO.plural(day.exercises.length, 'exercise')}${sets ? ` · ${sets} sets planned` : ''}</span></div>
            <div class="row" style="flex-wrap:wrap">${day.exercises.length ? '<button type="button" class="btn" data-copy-open>Copy to other days</button>' : ''}<button type="button" class="btn btn-primary" id="addExercise">Add exercise</button></div>
          </div>
          ${copyBox('exercises')}
          ${day.exercises.length ? `<div>
            <div class="prow head"><span></span><span>Type</span><span>Exercise</span><span>Sets × reps</span><span>Notes</span><span></span></div>
            <div id="rows">${day.exercises.map(exerciseRow).join('')}</div>
          </div>` : `<div class="empty"><b>Nothing planned for ${BO.DAYS[weekday - 1]}.</b><span>Add exercises, or leave it as a rest day.</span></div>`}
        </section>
        <div class="stack">
          <section class="panel" aria-labelledby="h-day-muscles">
            <div class="panel-head"><h2 class="title-l" id="h-day-muscles">${BO.DAYS[weekday - 1]} works</h2><span class="sub">planned sets</span></div>
            ${day.muscles.length ? `${BO.bodyMap(day.muscles)}${BO.muscleRank(day.muscles, 6)}` : '<p class="sub" style="margin:0">No strength exercises with known muscles on this day.</p>'}
          </section>
          ${progressPanel()}
        </div>
      </div>`;

      const diet = `<section class="panel" aria-labelledby="h-diet">
          <div class="panel-head">
            <div><h2 class="title-l" id="h-diet">${BO.DAYS[weekday - 1]}</h2><span class="sub">${BO.plural(day.meals.length, 'meal')}</span></div>
            <div class="row" style="flex-wrap:wrap">${day.meals.length ? '<button type="button" class="btn" data-copy-open>Copy to other days</button>' : ''}<button type="button" class="btn btn-primary" id="addMeal">Add meal</button></div>
          </div>
          ${copyBox('meals')}
          ${day.meals.length ? `<div class="meal-list">${day.meals.map(mealCard).join('')}</div>` : `<div class="empty"><b>No meals planned for ${BO.DAYS[weekday - 1]}.</b><span>Add the meals you plan to eat, with their foods and amounts.</span></div>`}
        </section>`;

      root.innerHTML = `
        <div class="page-head">
          <div><span class="sub">Repeats every week</span><h1 class="title-xl">Plan</h1></div>
          <div class="row">${BO.syncHTML()}
            <div class="seg" role="group" aria-label="Plan section">
              <button type="button" data-tab="training" aria-pressed="${tab === 'training'}">Training</button>
              <button type="button" data-tab="diet" aria-pressed="${tab === 'diet'}">Diet</button>
            </div></div>
        </div>
        <div class="daytabs" role="group" aria-label="Weekday">${tabs}</div>
        ${tab === 'training' ? training : diet}
        <p class="fine">Changes apply to this week and the weeks ahead. Past weeks keep the plan they had.</p>`;
    }

    /* ---------- events ---------- */

    const exFrom = el => { const row = el.closest('.prow[data-k]'); return row ? dayOf(weekday).exercises.find(e => e._k === row.dataset.k) : null; };
    const mealFrom = el => { const card = el.closest('.mealcard[data-k]'); return card ? dayOf(weekday).meals.find(m => m._k === card.dataset.k) : null; };
    const foodFrom = (meal, el) => { const row = el.closest('[data-f-k]'); return row ? meal.foods.find(f => f._k === row.dataset.fK) : null; };

    root.addEventListener('click', event => {
      const t = event.target;
      const day = dayOf(weekday);

      const dayTab = t.closest('[data-weekday]');
      if (dayTab) {
        weekday = Number(dayTab.dataset.weekday);
        openMuscles = null; copyOpen = false; copyTargets = new Set();
        BO.setQuery({ day: weekday });
        render();
        return;
      }
      const tabBtn = t.closest('[data-tab]');
      if (tabBtn) {
        tab = tabBtn.dataset.tab;
        copyOpen = false; copyTargets = new Set();
        BO.setQuery({ tab: tab === 'diet' ? 'diet' : null });
        render();
        return;
      }

      // Copy to other days
      if (t.closest('[data-copy-open]')) { copyOpen = !copyOpen; copyTargets = new Set(); render(); return; }
      if (t.closest('[data-copy-cancel]')) { copyOpen = false; render(); return; }
      const copyDay = t.closest('[data-copy-day]');
      if (copyDay) {
        const n = Number(copyDay.dataset.copyDay);
        copyTargets.has(n) ? copyTargets.delete(n) : copyTargets.add(n);
        render();
        return;
      }
      if (t.closest('[data-copy-all]')) { copyTargets = new Set([1, 2, 3, 4, 5, 6, 7].filter(n => n !== weekday)); render(); return; }
      if (t.closest('[data-copy-go]')) {
        const part = tab === 'diet' ? 'meals' : 'exercises';
        const targets = [...copyTargets];
        BO.flush();
        BO.saving(BO.api('POST', `/api/plan/${weekday}/copy`, { part, targets })).then(result => {
          result.days.forEach(d => { days[d.weekday - 1] = fromServer(d); });
          copyOpen = false; copyTargets = new Set();
          render();
          BO.toast(`Copied ${BO.DAYS[weekday - 1]}'s ${part} to ${BO.plural(targets.length, 'day')}`);
        }).catch(() => { });
        return;
      }

      // Exercises
      if (t.closest('#addExercise')) {
        const e = { _k: key(), id: null, type: 'strength', name: '', detail: '', notes: '', muscles: [], musclesAuto: true, targets: [] };
        day.exercises.push(e);
        render();
        root.querySelector(`#name-${e._k}`).focus();
        return;
      }
      const ex = exFrom(t);
      if (ex) {
        if (t.closest('[data-del-ex]')) {
          const index = day.exercises.indexOf(ex);
          day.exercises.splice(index, 1);
          saveExercises(weekday);
          render();
          const n = weekday;
          BO.toast(`${ex.name || 'Exercise'} removed`, { action: 'Undo', onAction: () => { dayOf(n).exercises.splice(index, 0, ex); saveExercises(n); render(); } });
          return;
        }
        if (t.closest('[data-muscles-open]')) { openMuscles = openMuscles === ex._k ? null : ex._k; render(); return; }
        if (t.closest('[data-muscles-close]')) { openMuscles = null; render(); return; }
        if (t.closest('[data-muscles-auto]')) { ex.musclesAuto = true; saveExercises(weekday); render(); return; }
        const muscle = t.closest('[data-muscle]');
        if (muscle) {
          const id = muscle.dataset.muscle;
          ex.muscles = ex.muscles.includes(id) ? ex.muscles.filter(m => m !== id) : [...ex.muscles, id];
          ex.musclesAuto = false;
          saveExercises(weekday);
          render();
          return;
        }
        if (t.closest('[data-progress]')) { loadProgress(ex.name.trim()).catch(() => { }); return; }
      }

      // Meals
      if (t.closest('#addMeal')) {
        const m = { _k: key(), id: null, name: `Meal ${day.meals.length + 1}`, foods: [{ _k: key(), name: '', amount: null, unit: 'grams' }] };
        day.meals.push(m);
        saveMeals(weekday);
        render();
        const input = root.querySelector(`#meal-${m._k}`);
        input.focus(); input.select();
        return;
      }
      const meal = mealFrom(t);
      if (meal) {
        if (t.closest('[data-del-meal]')) {
          const index = day.meals.indexOf(meal);
          day.meals.splice(index, 1);
          saveMeals(weekday);
          render();
          const n = weekday;
          BO.toast(`${meal.name} removed`, { action: 'Undo', onAction: () => { dayOf(n).meals.splice(index, 0, meal); saveMeals(n); render(); } });
          return;
        }
        if (t.closest('[data-add-food]')) {
          const f = { _k: key(), name: '', amount: null, unit: 'grams' };
          meal.foods.push(f);
          render();
          root.querySelector(`#food-${f._k}`).focus();
          return;
        }
        const food = foodFrom(meal, t);
        if (food && t.closest('[data-del-food]')) {
          const index = meal.foods.indexOf(food);
          meal.foods.splice(index, 1);
          saveMeals(weekday);
          render();
          return;
        }
        const unit = t.closest('[data-unit]');
        if (food && unit) { food.unit = unit.dataset.unit; saveMeals(weekday); render(); }
      }
    });

    root.addEventListener('input', event => {
      const t = event.target;
      const ex = exFrom(t);
      if (ex && t.dataset.f) {
        ex[t.dataset.f] = t.value;
        saveExercises(weekday);
        return;
      }
      const meal = mealFrom(t);
      if (meal) {
        if (t.matches('[data-meal-name]')) { meal.name = t.value; saveMeals(weekday); return; }
        const food = foodFrom(meal, t);
        if (food && t.dataset.food === 'name') { food.name = t.value; saveMeals(weekday); }
        if (food && t.dataset.food === 'amount') { food.amount = Math.max(0, BO.readNumber(t.value) ?? 0); saveMeals(weekday); }
      }
    });

    root.addEventListener('change', event => {
      const t = event.target;
      if (t.id === 'progressPick') { loadProgress(t.value).catch(() => { }); return; }
      const ex = exFrom(t);
      if (ex && t.dataset.f === 'type') { ex.type = t.value; saveExercises(weekday); render(); }
    });

    // Drag a row by its grip to reorder the day's exercises.
    let dragging = null;
    root.addEventListener('pointerdown', event => {
      const grip = event.target.closest('[data-grip]');
      if (grip) grip.closest('.prow').draggable = true;
    });
    root.addEventListener('dragstart', event => {
      const row = event.target.closest('.prow[data-k]');
      if (!row) return;
      dragging = row.dataset.k;
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData('text/plain', dragging);
    });
    root.addEventListener('dragover', event => { if (dragging && event.target.closest('.prow[data-k]')) event.preventDefault(); });
    root.addEventListener('drop', event => {
      const row = event.target.closest('.prow[data-k]');
      if (!dragging || !row) return;
      event.preventDefault();
      const list = dayOf(weekday).exercises;
      const from = list.findIndex(e => e._k === dragging);
      const to = list.findIndex(e => e._k === row.dataset.k);
      if (from >= 0 && to >= 0 && from !== to) {
        const [moved] = list.splice(from, 1);
        list.splice(to, 0, moved);
        saveExercises(weekday);
      }
      dragging = null;
      render();
    });
    root.addEventListener('dragend', () => { dragging = null; root.querySelectorAll('.prow[draggable="true"]').forEach(r => { r.draggable = false; }); });

    render();
  };
})();
