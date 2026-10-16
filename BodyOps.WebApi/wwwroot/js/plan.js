/* BodyOps — Plan: the week that repeats. Each day gets a name ("Upper body"), its exercises with sets × reps and the
   muscles they work, and its meals with a time and foods. Every change saves itself a moment after you stop typing. */
'use strict';

(() => {
  const { esc, icons } = BO;
  let keySeq = 0;
  const key = () => 'k' + (++keySeq);

  BO.screens.plan = async function plan(root, query) {
    const [loaded, firstFoods] = await Promise.all([BO.api('GET', '/api/plan'), BO.loadFoods().catch(() => ({ list: [], byKey: new Map() }))]);
    let foods = firstFoods;
    let weekday = Math.min(7, Math.max(1, Number(query.day) || BO.weekday(BO.localToday())));
    let tab = query.tab === 'meals' || query.tab === 'diet' ? 'meals' : 'training';
    let openMuscles = null;     // key of the exercise whose muscle picker is open
    let openKind = null;        // key of the cardio exercise whose kind picker is open
    let openStep = null;        // key of the exercise whose progression panel is open
    let copyOpen = false;
    let copyTargets = new Set();
    let firstPaint = true;
    const todayWeekday = BO.weekday(BO.localToday());

    // Local editable copy of the plan, with stable keys so focus survives re-renders.
    const days = loaded.days.map(fromServer);
    function fromServer(day) {
      return {
        weekday: day.weekday,
        name: day.name || '',
        muscles: day.muscles,
        exercises: day.exercises.map(e => ({ _k: key(), id: e.id, type: e.type, name: e.name, detail: e.detail, notes: e.notes, muscles: e.muscles, musclesAuto: e.musclesAuto, targets: e.targets, cardioKind: e.cardioKind, cardioKindAuto: e.cardioKindAuto !== false, progressStepKg: e.progressStepKg ?? null })),
        meals: day.meals.map(m => ({ _k: key(), id: m.id, name: m.name, time: m.time || '', foods: m.foods.map(f => ({ _k: key(), name: f.name, amount: f.amount, unit: f.unit })) })),
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
            cardioKind: e.type === 'cardio' && !e.cardioKindAuto ? e.cardioKind : null,
            progressStepKg: e.type === 'strength' ? e.progressStepKg : null,
          }))));
          sent.forEach((e, i) => {
            const saved = result.exercises[i];
            if (!saved) return;
            e.id = saved.id;
            e.targets = saved.targets;
            if (e.musclesAuto) e.muscles = saved.muscles;
            if (e.cardioKindAuto) e.cardioKind = saved.cardioKind;
          });
          day.muscles = result.muscles;
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
            time: m.time || null,
            foods: m.foods.filter(f => f.name.trim()).map(f => ({ name: f.name, amount: f.amount || 0, unit: f.unit })),
          }))));
          sent.forEach((m, i) => { if (result.meals[i]) m.id = result.meals[i].id; });
          if (!BO.busy() && root.isConnected) BO.rerender(render);
        } catch (e) { /* the toast already explains */ }
      });
    }

    function saveName(n) {
      BO.debounce(`plan-name:${n}`, 700, async () => {
        try { await BO.saving(BO.api('PUT', `/api/plan/${n}/name`, { name: dayOf(n).name })); } catch (e) { }
        if (!BO.busy() && root.isConnected) BO.rerender(render);
      });
    }

    /* ---------- rendering ---------- */

    function chipText(day) {
      if (day.name.trim()) return day.name.trim();
      if (day.exercises.length) return day.exercises.length === 1 ? day.exercises[0].name : BO.plural(day.exercises.length, 'exercise');
      return day.meals.length ? 'Rest day' : 'Empty';
    }

    function summary(day) {
      const sets = day.exercises.reduce((n, e) => n + (e.type === 'cardio' ? 0 : Math.max(1, e.targets.length)), 0);
      const fake = { exercises: day.exercises.map(e => ({ type: e.type, detail: e.detail, targets: e.targets })) };
      const parts = [BO.DAYS[day.weekday - 1]];
      if (tab === 'training') {
        parts.push(BO.plural(day.exercises.length, 'exercise'));
        if (sets) parts.push(`${sets} sets`);
        if (day.exercises.length) parts.push(`about ${BO.sessionMinutes(fake)} min`);
      } else {
        parts.push(BO.plural(day.meals.length, 'meal'));
      }
      return parts.join(' · ');
    }

    function exerciseRow(e, index) {
      const k = e._k;
      const tags = e.muscles.length
        ? e.muscles.map(m => `<span class="mtag${e.musclesAuto ? '' : ' on'}">${BO.MUSCLES[m]}</span>`).join('')
        : `<span class="mtag">${e.type === 'cardio' ? 'Cardio' : e.name.trim() ? 'No muscles found' : 'Muscles appear when you name it'}</span>`;
      const picker = openMuscles === k ? `<div class="mpicker">
          <span class="sub">Pick the muscles this exercise works. ${e.musclesAuto ? 'Now detected from the name.' : 'Picked by hand.'}</span>
          <div class="chips">${Object.entries(BO.MUSCLES).map(([id, label]) => `<button type="button" data-muscle="${id}" aria-pressed="${e.muscles.includes(id)}">${label}</button>`).join('')}</div>
          <div class="spread">${e.musclesAuto ? '<span></span>' : '<button type="button" class="link-btn" data-muscles-auto>Detect from the name again</button>'}<button type="button" class="btn" data-muscles-close>Done</button></div>
        </div>` : openStep === k && e.type === 'strength' && BO.isRange(e.detail) ? progressPanel(e) : openKind === k && e.type === 'cardio' ? `<div class="mpicker">
          <span class="sub">What kind of cardio? Treadmill, elliptical and stairs are logged by time only; the others by time and distance. ${e.cardioKindAuto ? 'Now worked out from the name.' : 'Picked by hand.'}</span>
          <div class="chips">${Object.entries(BO.CARDIO).map(([id, label]) => `<button type="button" data-kind="${id}" aria-pressed="${e.cardioKind === id}">${label}</button>`).join('')}</div>
          <div class="spread">${e.cardioKindAuto ? '<span></span>' : '<button type="button" class="link-btn" data-kind-auto>Work it out from the name again</button>'}<button type="button" class="btn" data-kind-close>Done</button></div>
        </div>` : '';
      const range = e.type === 'strength' ? BO.parseSets(e.detail).find(s => s.min < s.max) : null;
      const stepTag = range ? `<button type="button" class="mtag up" data-step-open title="Automatic progression">↑ ${BO.load(e.progressStepKg ?? 2.5)} kg at ${range.max}</button>` : '';
      const kindTag = e.type === 'cardio'
        ? `<button type="button" class="mtag${e.cardioKindAuto ? '' : ' on'}" data-kind-open title="Kind of cardio">${BO.CARDIO[e.cardioKind] || 'Kind'} · ${BO.cardioDistance(e.cardioKind || 'other') ? 'time + km' : 'time'}</button>`
        : '';
      return `<div class="pex${e.type === 'cardio' ? ' cardio' : ''}" data-k="${k}">
        <span class="n" data-grip title="Drag to reorder" aria-label="Exercise ${index + 1}, drag to reorder">${index + 1}</span>
        <div class="main-in">
          <input class="nm" id="name-${k}" data-f="name" value="${esc(e.name)}" placeholder="${e.type === 'cardio' ? 'Cardio, e.g. Treadmill' : 'Exercise, e.g. Pec deck'}" aria-label="Exercise name" maxlength="100">
          <div class="tags"><button type="button" class="mtag ${e.type === 'cardio' ? '' : 'on'}" data-type title="Switch between strength and cardio">${e.type === 'cardio' ? 'Cardio' : 'Strength'}</button>${kindTag}${e.type === 'strength' ? tags + stepTag : ''}${e.type === 'strength' ? `<button type="button" class="link-btn" style="font-size:12px" data-muscles-open>${openMuscles === k ? 'Close' : 'Edit muscles'}</button>` : ''}</div>
          <input class="nt" id="notes-${k}" data-f="notes" value="${esc(e.notes)}" placeholder="Add a note" aria-label="Notes" maxlength="200">
        </div>
        <div class="side-in">
          <input class="det num" id="detail-${k}" data-f="detail" value="${esc(e.detail)}" placeholder="${e.type === 'cardio' ? '30 min' : '3x12'}" aria-label="${e.type === 'cardio' ? 'Plan' : 'Sets × reps'}" maxlength="60">
          <div class="acts"><button type="button" data-del-ex aria-label="Remove ${esc(e.name || 'exercise')}">${icons.x}</button></div>
        </div>
        ${picker}
      </div>`;
    }

    /** The rep-range rule of one exercise: how much weight to add once every set reaches the top. */
    function progressPanel(e) {
      const range = BO.parseSets(e.detail).find(s => s.min < s.max);
      const step = e.progressStepKg ?? 2.5;
      const steps = [1, 2.5, 5];
      if (!steps.includes(step)) steps.push(step);
      return `<div class="mpicker">
          <span class="sub">A range: Milo pushes reps until every set reaches <b>${range.max}</b>, then adds weight for next time and you start again at <b>${range.min}</b>.</span>
          <div class="spread"><span class="sub" style="font-weight:700;color:var(--ink)">Add each time</span>
            <div class="seg" role="group" aria-label="Weight to add">${steps.sort((a, b) => a - b).map(v => `<button type="button" data-stepkg="${v}" aria-pressed="${v === step}">${BO.load(v)} kg</button>`).join('')}</div></div>
          <div class="spread"><button type="button" class="link-btn" data-step-fixed>Use fixed reps (${range.max})</button><button type="button" class="btn" data-step-close>Done</button></div>
        </div>`;
    }

    /* ---------- food values ---------- */

    /** The line under a food: its calories and protein, or a way to add its values. */
    function foodValues(f) {
      if (!f.name.trim()) return '';
      const food = foods.byKey.get(BO.foodKey(f.name));
      if (!food) return `<span class="miss">+ add values</span>`;
      const m = BO.macrosFor(food, f.amount, f.unit);
      if (!m) return f.amount == null ? `per ${food.basis === 'perPiece' ? 'piece' : '100 g'}: <b>${BO.kcalText(food.kcal)} kcal</b>` : `${f.unit === 'pieces' ? 'pieces need' : 'grams need'} a piece weight · <b>set it</b>`;
      return `<b>${BO.kcalText(m.kcal)} kcal</b>${m.proteinG != null ? ` · ${BO.gramsText(m.proteinG)} protein` : ''}`;
    }
    const foodsLib = () => foods;
    const mealTotal = m => { const t = BO.mealMacros(m, foods); return BO.macrosKnown(t) ? BO.macroChips(t, { full: true }) : ''; };
    function dayTotal(day) {
      const t = BO.sumMacros(day.meals.map(m => BO.mealMacros(m, foods)).filter(BO.macrosKnown));
      if (!BO.macrosKnown(t)) return '';
      return `<div class="daytot"><span class="sub">${BO.DAYS[day.weekday - 1]}, all meals: the day’s target</span><b class="num">${BO.kcalText(t.kcal)} kcal</b></div><div class="mtot">${BO.macroChips(t, { full: true, kcal: false })}</div>`;
    }
    /** Updates the values on screen while typing (no re-render, so the field keeps its focus). */
    function paintValues(meal) {
      meal.foods.forEach(f => { const el = root.querySelector(`[data-fv="${f._k}"]`); if (el) { el.innerHTML = foodValues(f); el.classList.toggle('miss', !!f.name.trim() && !foods.byKey.get(BO.foodKey(f.name))); } });
      const mt = root.querySelector(`[data-mt="${meal._k}"]`);
      if (mt) { const html = mealTotal(meal); mt.innerHTML = html; mt.hidden = !html; }
      const dt = root.querySelector('[data-daytot]');
      if (dt) { const html = dayTotal(dayOf(weekday)); dt.innerHTML = html; dt.hidden = !html; }
    }
    async function reloadFoods() {
      foods = await BO.loadFoods(true).catch(() => foods);
      if (root.isConnected) BO.rerender(render);
    }

    function myFoods() {
      const rows = foods.list.map(f => `<button type="button" data-food-edit="${f.id}"><b>${esc(f.name)} ${BO.foodSourceTag(f)}</b><span class="num">${BO.kcalText(f.kcal)}</span><span class="num p">${BO.gramsText(f.proteinG)}</span><span>${f.basis === 'perPiece' ? esc(f.pieceName || 'piece') : '100 g'}</span></button>`).join('');
      return `<section class="card ${firstPaint ? 'rise' : ''}" style="--i:4" aria-labelledby="h-foods">
        <div class="card-head"><h2 class="h-l" id="h-foods">My foods</h2><button type="button" class="btn primary" style="padding:9px 13px" data-food-new>${icons.plus} Add</button></div>
        ${rows ? `<div class="ftable"><div class="fh"><span style="text-align:left">Food</span><span>kcal</span><span>protein</span><span>per</span></div>${rows}</div>
          <p class="fine" style="margin-top:12px">A food is matched to the plan by its name. Change its values and every meal that uses it updates.</p>`
          : '<div class="empty"><b>No foods yet.</b><span>Add the foods you eat (from Milo’s list, a barcode or the label) and every meal shows its calories and protein.</span></div>'}
      </section>`;
    }

    function mealCard(m, i) {
      const k = m._k;
      const foods = m.foods.map(f => `<div class="foodrow" data-f-k="${f._k}">
          <input class="field small" id="food-${f._k}" data-food="name" value="${esc(f.name)}" placeholder="Food" aria-label="Food" maxlength="80">
          <input class="field small num" id="amount-${f._k}" data-food="amount" inputmode="decimal" value="${f.amount ?? ''}" placeholder="0" aria-label="Amount">
          <span class="unit"><button type="button" data-unit="grams" aria-pressed="${f.unit === 'grams'}">g</button><button type="button" data-unit="pieces" aria-pressed="${f.unit === 'pieces'}">pc</button></span>
          <button type="button" class="del" data-del-food aria-label="Remove ${esc(f.name || 'food')}">${icons.x}</button>
          <button type="button" class="fmac${f.name.trim() && !foodsLib().byKey.get(BO.foodKey(f.name)) ? ' miss' : ''}" data-fv="${f._k}" data-food-values>${foodValues(f)}</button>
        </div>`).join('');
      const total = mealTotal(m);
      return `<div class="mealcard ${firstPaint ? 'rise' : ''}" style="--i:${i + 2}" data-k="${k}">
        <div class="mh"><input class="time num" type="time" id="time-${k}" data-meal-time value="${esc(m.time)}" aria-label="Time"><input class="mname" id="meal-${k}" data-meal-name value="${esc(m.name)}" aria-label="Meal name" maxlength="80"><button type="button" class="del" data-del-meal aria-label="Remove ${esc(m.name)}">${icons.x}</button></div>
        ${foods || '<span class="sub">No foods yet.</span>'}
        <div class="mtot" data-mt="${k}"${total ? '' : ' hidden'}>${total}</div>
        <button type="button" class="link-btn" style="align-self:flex-start" data-add-food>+ Add food</button>
      </div>`;
    }

    function copyBox() {
      if (!copyOpen) return '';
      const part = tab === 'meals' ? 'meals' : 'exercises';
      const others = [1, 2, 3, 4, 5, 6, 7].filter(n => n !== weekday);
      return `<div class="copybox">
        <span class="sub">Replace these days' ${part}${part === 'exercises' ? ' (and name)' : ''} with ${BO.DAYS[weekday - 1]}'s:</span>
        <div class="copyto">${others.map(n => `<button type="button" data-copy-day="${n}" aria-pressed="${copyTargets.has(n)}">${BO.DAYS_SHORT[n - 1]}</button>`).join('')}
          <button type="button" data-copy-all>All</button></div>
        <div class="row" style="justify-content:flex-end"><button type="button" class="btn ghost" data-copy-cancel>Cancel</button><button type="button" class="btn primary" data-copy-go${copyTargets.size ? '' : ' disabled'}>Copy to ${BO.plural(copyTargets.size, 'day')}</button></div>
      </div>`;
    }

    function render() {
      const day = dayOf(weekday);
      const chips = days.map(d => `<button type="button" class="dchip${d.weekday === todayWeekday ? ' today' : ''}" data-weekday="${d.weekday}" aria-pressed="${d.weekday === weekday}"><b>${BO.DAYS_SHORT[d.weekday - 1]}</b><span>${esc(chipText(d))}</span></button>`).join('');
      const head = `<div class="spread" style="align-items:flex-start">
          <div style="min-width:0;flex:1"><input class="dayname" id="dayname-${weekday}" data-day-name value="${esc(day.name)}" placeholder="Name this day" aria-label="Name of ${BO.DAYS[weekday - 1]}" maxlength="40">
            <div class="sub" style="margin-top:4px">${esc(summary(day))}</div></div>
          ${(tab === 'training' ? day.exercises.length : day.meals.length) ? `<button type="button" class="icon-btn" data-copy-open aria-label="Copy to other days" title="Copy to other days">${icons.copy}</button>` : ''}
        </div>`;

      const training = `<div class="plan-grid">
        <section class="card ${firstPaint ? 'rise' : ''}" style="--i:2" aria-label="Exercises">
          ${head}
          ${copyBox()}
          <div style="margin-top:10px" id="rows">${day.exercises.map(exerciseRow).join('') || `<div class="empty" style="margin-top:6px"><b>Nothing planned for ${BO.DAYS[weekday - 1]}.</b><span>Add exercises, or leave it as a rest day.</span></div>`}</div>
          <button type="button" class="add-row" id="addExercise">${icons.plus} Add exercise</button>
        </section>
        <section class="card ${firstPaint ? 'rise' : ''}" style="--i:3" aria-labelledby="h-day-muscles">
          <div class="card-head"><h2 class="h-l" id="h-day-muscles">${esc(day.name.trim() || BO.DAYS[weekday - 1])} works</h2><span class="sub">planned sets</span></div>
          ${day.muscles.length ? `<div class="bm">${BO.bodyMap(day.muscles)}${BO.muscleRank(day.muscles, 7)}</div>` : '<p class="sub" style="margin:0">No strength exercises with known muscles on this day.</p>'}
        </section>
      </div>`;

      const totalHtml = dayTotal(day);
      const meals = `<div class="plan-grid"><div class="stack">
        <section class="card ${firstPaint ? 'rise' : ''}" style="--i:2" aria-label="Meals">${head}${copyBox()}<div data-daytot style="display:flex;flex-direction:column;gap:10px;margin-top:12px"${totalHtml ? '' : ' hidden'}>${totalHtml}</div></section>
        ${day.meals.length ? `<div class="meal-list">${day.meals.map(mealCard).join('')}</div>` : `<div class="empty"><b>No meals planned for ${BO.DAYS[weekday - 1]}.</b><span>Add the meals you plan to eat, with a time, foods and amounts.</span></div>`}
        <button type="button" class="add-row" id="addMeal" style="margin-top:0">${icons.plus} Add meal</button>
        </div>${myFoods()}</div>`;

      root.innerHTML = `
        <div class="page-head ${firstPaint ? 'rise' : ''}">
          <div><div class="hello">Repeats every week</div><h1 class="h-xl">Plan</h1></div>
          <div class="acts">${BO.syncHTML()}
            <div class="seg" role="group" aria-label="Plan section">
              <button type="button" data-tab="training" aria-pressed="${tab === 'training'}">Training</button>
              <button type="button" data-tab="meals" aria-pressed="${tab === 'meals'}">Meals</button>
            </div></div>
        </div>
        <div class="dchips ${firstPaint ? 'rise' : ''}" style="--i:1" role="group" aria-label="Weekday">${chips}</div>
        ${tab === 'training' ? training : meals}
        <p class="fine">Changes apply to this week and the weeks ahead. Past weeks keep the plan they had.</p>`;

      if (firstPaint) {
        const picked = root.querySelector('.dchip[aria-pressed="true"]');
        if (picked) picked.scrollIntoView({ inline: 'center', block: 'nearest' });
      }
      firstPaint = false;
    }

    /* ---------- events ---------- */

    const exFrom = el => { const row = el.closest('.pex[data-k]'); return row ? dayOf(weekday).exercises.find(e => e._k === row.dataset.k) : null; };
    const mealFrom = el => { const card = el.closest('.mealcard[data-k]'); return card ? dayOf(weekday).meals.find(m => m._k === card.dataset.k) : null; };
    const foodFrom = (meal, el) => { const row = el.closest('[data-f-k]'); return row ? meal.foods.find(f => f._k === row.dataset.fK) : null; };

    root.addEventListener('click', event => {
      const t = event.target;
      const day = dayOf(weekday);

      const dayChip = t.closest('[data-weekday]');
      if (dayChip) {
        weekday = Number(dayChip.dataset.weekday);
        openMuscles = null; copyOpen = false; copyTargets = new Set();
        BO.setQuery({ day: weekday });
        render();
        return;
      }
      const tabBtn = t.closest('[data-tab]');
      if (tabBtn) {
        tab = tabBtn.dataset.tab;
        copyOpen = false; copyTargets = new Set();
        BO.setQuery({ tab: tab === 'meals' ? 'meals' : null });
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
        const part = tab === 'meals' ? 'meals' : 'exercises';
        const targets = [...copyTargets];
        BO.flush().then(() => BO.saving(BO.api('POST', `/api/plan/${weekday}/copy`, { part, targets }))).then(result => {
          result.days.forEach(d => { days[d.weekday - 1] = fromServer(d); });
          copyOpen = false; copyTargets = new Set();
          render();
          BO.toast(`Copied ${BO.DAYS[weekday - 1]}'s ${part} to ${BO.plural(targets.length, 'day')}`);
        }).catch(() => { });
        return;
      }

      // Exercises
      if (t.closest('#addExercise')) {
        const e = { _k: key(), id: null, type: 'strength', name: '', detail: '', notes: '', muscles: [], musclesAuto: true, targets: [], cardioKind: null, cardioKindAuto: true };
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
        if (t.closest('[data-type]')) { ex.type = ex.type === 'cardio' ? 'strength' : 'cardio'; if (ex.type === 'cardio' && !ex.cardioKind) ex.cardioKind = 'other'; saveExercises(weekday); render(); return; }
        if (t.closest('[data-kind-open]')) { openKind = openKind === ex._k ? null : ex._k; render(); return; }
        if (t.closest('[data-step-open]')) { openStep = openStep === ex._k ? null : ex._k; openMuscles = null; render(); return; }
        if (t.closest('[data-step-close]')) { openStep = null; render(); return; }
        const stepKg = t.closest('[data-stepkg]');
        if (stepKg) { const v = Number(stepKg.dataset.stepkg); ex.progressStepKg = v === 2.5 ? null : v; saveExercises(weekday); render(); return; }
        if (t.closest('[data-step-fixed]')) {
          // "3x8-12" → "3x12": the top of each range.
          ex.detail = ex.detail.replace(/[–—]/g, '-').replace(/(\d{1,3})\s*-\s*(\d{1,3})(?!\d)(?!\s*kg)/gi, (_, a, b) => String(Math.max(Number(a), Number(b))));
          openStep = null;
          saveExercises(weekday);
          render();
          return;
        }
        if (t.closest('[data-kind-close]')) { openKind = null; render(); return; }
        if (t.closest('[data-kind-auto]')) { ex.cardioKindAuto = true; saveExercises(weekday); render(); return; }
        const kind = t.closest('[data-kind]');
        if (kind) { ex.cardioKind = kind.dataset.kind; ex.cardioKindAuto = false; saveExercises(weekday); render(); return; }
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
      }

      // My foods
      const editFood = t.closest('[data-food-edit]');
      if (editFood) {
        const food = foods.list.find(f => String(f.id) === editFood.dataset.foodEdit);
        if (food) BO.foodEditor({ food, onSaved: reloadFoods, onDeleted: reloadFoods });
        return;
      }
      if (t.closest('[data-food-new]')) { BO.foodPicker({ title: 'Add a food', onPick: reloadFoods }); return; }

      // Meals
      if (t.closest('#addMeal')) {
        const lastTime = day.meals.map(m => m.time).filter(Boolean).sort().at(-1);
        const time = lastTime ? `${String(Math.min(23, Number(lastTime.slice(0, 2)) + 3)).padStart(2, '0')}:00` : '08:00';
        const m = { _k: key(), id: null, name: `Meal ${day.meals.length + 1}`, time, foods: [{ _k: key(), name: '', amount: null, unit: 'grams' }] };
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
          const n = weekday;
          const add = (name, food) => {
            const perPiece = food && (food.basis === 'perPiece' || (food.pieceGrams && food.pieceName && food.source === 'milo'));
            const f = { _k: key(), name, amount: food ? (perPiece ? 1 : 100) : null, unit: perPiece ? 'pieces' : 'grams' };
            // A blank row left by "Add meal" is used first.
            const blank = meal.foods.find(x => !x.name.trim());
            if (blank) Object.assign(blank, { name: f.name, amount: f.amount, unit: f.unit }); else meal.foods.push(f);
            saveMeals(n);
            return blank || f;
          };
          BO.foodPicker({
            title: `Add to ${meal.name || 'this meal'}`,
            onPick: async food => { const f = add(food.name, food); await reloadFoods(); const input = root.querySelector(`#amount-${f._k}`); if (input) { input.focus(); input.select(); } },
            onPlain: name => { const f = add(name, null); render(); const input = root.querySelector(`#amount-${f._k}`); if (input) input.focus(); },
          });
          return;
        }
        const food = foodFrom(meal, t);
        if (food && t.closest('[data-food-values]')) {
          if (!food.name.trim()) return;
          const known = foods.byKey.get(BO.foodKey(food.name));
          if (known) BO.foodEditor({ food: known, onSaved: reloadFoods, onDeleted: reloadFoods });
          else BO.foodPicker({ title: `Values for ${food.name}`, hint: 'Pick the closest match: its values are saved under your name for it.', query: food.name, saveAs: food.name, onPick: reloadFoods });
          return;
        }
        if (food && t.closest('[data-del-food]')) {
          meal.foods.splice(meal.foods.indexOf(food), 1);
          saveMeals(weekday);
          render();
          return;
        }
        const unit = t.closest('[data-unit]');
        if (food && unit) { food.unit = unit.dataset.unit; saveMeals(weekday); render(); }
        return;
      }
    });

    root.addEventListener('input', event => {
      const t = event.target;
      if (t.matches('[data-day-name]')) {
        dayOf(weekday).name = t.value;
        saveName(weekday);
        const chip = root.querySelector(`.dchip[data-weekday="${weekday}"] span`);
        if (chip) chip.textContent = chipText(dayOf(weekday));
        return;
      }
      const ex = exFrom(t);
      if (ex && t.dataset.f) {
        const wasRange = BO.isRange(ex.detail);
        ex[t.dataset.f] = t.value;
        saveExercises(weekday);
        // Typing a range ("3x8-12") shows the progression tag; removing it hides the tag.
        if (t.dataset.f === 'detail' && ex.type === 'strength' && wasRange !== BO.isRange(ex.detail)) BO.rerender(render);
        return;
      }
      const meal = mealFrom(t);
      if (meal) {
        if (t.matches('[data-meal-name]')) { meal.name = t.value; saveMeals(weekday); return; }
        if (t.matches('[data-meal-time]')) { meal.time = t.value; saveMeals(weekday); return; }
        const food = foodFrom(meal, t);
        if (food && t.dataset.food === 'name') { food.name = t.value; saveMeals(weekday); paintValues(meal); }
        if (food && t.dataset.food === 'amount') { food.amount = Math.max(0, BO.readNumber(t.value) ?? 0); saveMeals(weekday); paintValues(meal); }
      }
    });

    // Drag an exercise by its number to reorder (works with a finger and a mouse).
    let drag = null;
    root.addEventListener('pointerdown', event => {
      const grip = event.target.closest('[data-grip]');
      if (!grip) return;
      const row = grip.closest('.pex');
      drag = { k: row.dataset.k, over: null };
      row.classList.add('dragging');
      grip.setPointerCapture(event.pointerId);
      event.preventDefault();
    });
    root.addEventListener('pointermove', event => {
      if (!drag) return;
      const under = document.elementFromPoint(event.clientX, event.clientY);
      const row = under && under.closest('.pex[data-k]');
      root.querySelectorAll('.pex.over').forEach(r => r.classList.remove('over'));
      if (row && row.dataset.k !== drag.k) { row.classList.add('over'); drag.over = row.dataset.k; } else drag.over = null;
    });
    const endDrag = () => {
      if (!drag) return;
      const list = dayOf(weekday).exercises;
      const from = list.findIndex(e => e._k === drag.k);
      const to = drag.over ? list.findIndex(e => e._k === drag.over) : -1;
      drag = null;
      if (from >= 0 && to >= 0 && from !== to) {
        const [moved] = list.splice(from, 1);
        list.splice(to, 0, moved);
        saveExercises(weekday);
      }
      render();
    };
    root.addEventListener('pointerup', endDrag);
    root.addEventListener('pointercancel', endDrag);

    render();
  };
})();
