/* BodyOps — Today: the day score, the streak, what's next and the day as a timeline you tick off.
   Opens on today; ?date= opens another day of that week. */
'use strict';

(() => {
  const { esc, icons } = BO;

  /** Minutes since midnight for "HH:mm". */
  const minutes = time => { const [h, m] = time.split(':').map(Number); return h * 60 + m; };
  const WORKOUT_AT = 18 * 60;   // where the workout sits in the timeline (the plan has no time for it)

  /** What the session looks like: not started, in progress, done, skipped or a rest day. */
  BO.sessionState = function sessionState(day) {
    if (!day.exercises.length) return 'rest';
    if (day.trainingState === 'missed') return 'missed';
    const started = day.exercises.some(ex => ex.sets.some(s => s != null) || (ex.cardioMinutes || 0) > 0 || (ex.cardioKm || 0) > 0);
    if (day.setsPlanned && day.setsDone >= day.setsPlanned) return 'done';
    // Marked trained without logging, or a past day that was started: it's over.
    if (day.trainingState === 'done' && (!started || day.date < BO.localToday())) return 'done';
    return started ? 'live' : 'todo';
  };

  /** The exercise to do next: the first one with planned sets still open. */
  BO.nextExercise = function nextExercise(day) {
    return day.exercises.find(ex => ex.type === 'cardio'
      ? !((ex.cardioMinutes || 0) > 0 || (ex.cardioKm || 0) > 0)
      : ex.sets.filter(s => s != null).length < Math.max(1, ex.targets.length)) || null;
  };

  /** Rough length of a session: two and a half minutes per set, or the minutes a cardio plan names. */
  BO.sessionMinutes = function sessionMinutes(day) {
    return Math.round(day.exercises.reduce((n, ex) => {
      if (ex.type === 'cardio') { const m = /(\d+)\s*min/i.exec(ex.detail || ''); return n + (m ? Number(m[1]) : 30); }
      return n + Math.max(1, ex.targets.length) * 2.5;
    }, 0) / 5) * 5;
  };

  /** The session's name: the plan's name for the day, or a fallback. */
  BO.sessionName = day => day.name || (day.exercises.length === 1 ? day.exercises[0].name : 'Workout');

  BO.screens.today = async function today(root, query) {
    const [first, streak, weights, prefs] = await Promise.all([
      BO.api('GET', '/api/days/week' + (query.date ? `?date=${encodeURIComponent(query.date)}` : '')),
      BO.api('GET', '/api/days/streak').catch(() => null),
      BO.api('GET', '/api/weights').catch(() => ({ entries: [], goalKg: null })),
      BO.settings().catch(() => ({ weighEveryDays: 1 })),
    ]);
    let watch = null;   // Apple Watch workouts, loaded after the first paint
    BO.watchLoad().then(w => { watch = w; if (root.isConnected && w.workouts.length) render(); }).catch(() => { });
    const every = prefs.weighEveryDays || 1;
    let week = first;
    let date = query.date && week.days.some(d => d.date === query.date) ? query.date : (week.days.some(d => d.date === week.today) ? week.today : week.from);
    let shownScore = 0;
    let firstPaint = true;
    week.days.forEach(BO.scoreDay);

    const day = () => week.days.find(d => d.date === date);
    const todayDay = () => week.days.find(d => d.date === week.today);

    async function refresh() {
      if (BO.busy()) return;
      week = await BO.api('GET', `/api/days/week?date=${date}`);
      week.days.forEach(BO.scoreDay);
      watch = await BO.watchLoad(true).catch(() => watch);
      if (root.isConnected) render();
    }
    BO.onResume = () => { if (root.isConnected) refresh().catch(() => { }); };

    async function save(method, path, body) {
      const updated = await BO.saving(BO.api(method, path, body));
      if (updated && updated.days && !BO.busy()) {
        updated.days.forEach(BO.scoreDay);
        week = updated;
        if (root.isConnected) render();
      }
    }

    function streakChip() {
      const n = BO.streakNow(streak, todayDay());
      document.querySelectorAll('[data-streak-label]').forEach(el => { el.textContent = n ? `${n}-day streak` : 'Start a streak today'; });
      return `<span class="chip" title="Days in a row with a day score of ${BO.STREAK_AT} or more"><span class="flame">${icons.flame}</span>${n ? `${n}-day streak` : 'Start a streak'}</span>`;
    }

    function weighIn(d) {
      const w = BO.weighIn(weights.entries, every, d.date);
      const before = w.last;
      if (w.entry) return { done: true, sub: `${BO.kg(w.entry.weightKg)} kg${before ? ` · ${BO.signed(w.entry.weightKg - before.weightKg)} vs ${every > 1 ? BO.shortDate(before.date) : BO.DAYS[BO.weekday(before.date) - 1]}` : ''}` };
      if (every > 1 && !w.due) {
        return { done: false, later: true, next: w.next, sub: `Next in ${BO.plural(w.inDays, 'day')}${before ? ` · last ${BO.kg(before.weightKg)} kg on ${BO.shortDate(before.date)}` : ''}` };
      }
      if (every > 1 && before) return { done: false, due: true, sub: `${BO.daysBetween(before.date, d.date)} days since ${BO.kg(before.weightKg)} kg · before breakfast` };
      return { done: false, due: every > 1, sub: before ? `Last ${BO.kg(before.weightKg)} kg · ${BO.shortDate(before.date)}` : 'Step on the scale' };
    }

    /** Weigh-in, meals and the workout, in the order of the day. */
    function items(d) {
      const list = [];
      const w = weighIn(d);
      list.push({
        kind: 'weigh', key: 6 * 60, time: w.later ? BO.DAYS_SHORT[BO.weekday(w.next) - 1] : 'AM', name: 'Weigh-in', sub: w.sub,
        done: w.done || d.weightKg != null, later: w.later, due: w.due,
      });
      let last = 7 * 60;
      const timed = d.meals.some(m => m.time);
      d.meals.forEach((m, i) => {
        const key = m.time ? minutes(m.time) : (timed ? last + 1 : 7 * 60 + i * 120);
        last = key;
        const sub = m.swap ? `${m.swap.name}${m.foods ? `, instead of ${m.foods}` : ''}` : (m.foods || 'No foods listed');
        const mac = m.swap ? BO.macroChips({ kcal: m.swap.kcal, proteinG: m.swap.proteinG }) : BO.macroChips(m.macros);
        list.push({ kind: 'eat', key, time: m.time || '', name: m.name, sub, mac, swap: !!m.swap, done: m.eaten, meal: m.planMealId });
      });
      if (d.exercises.length) {
        const state = BO.sessionState(d);
        const w = BO.watchFor(watch, d.date).session;
        const onWatch = w ? ` · ${BO.watchMinutes(w)} min on Apple Watch` : '';
        list.push({
          kind: 'train', key: timed ? WORKOUT_AT : 99 * 60, time: state === 'live' ? 'Now' : '',
          name: BO.sessionName(d), done: state === 'done', missed: state === 'missed', live: state === 'live',
          sub: state === 'done' ? `${d.setsDone} of ${d.setsPlanned} sets · done${onWatch}` : state === 'live' ? `In progress · ${d.setsDone} of ${d.setsPlanned} sets` : state === 'missed' ? 'Skipped' : `${BO.plural(d.exercises.length, 'exercise')} · ${d.setsPlanned} sets · about ${BO.sessionMinutes(d)} min`,
        });
      }
      list.sort((a, b) => a.key - b.key);
      const next = list.findIndex(x => !x.done && !x.missed && !x.live && !x.later);
      if (next >= 0) list[next].next = true;
      return list;
    }

    function timeline(d) {
      return `<div class="tl">${items(d).map(x => {
        const cls = `ti ${x.kind}${x.done ? ' done' : ''}${x.live ? ' live' : ''}${x.next ? ' next' : ''}${x.missed ? ' missed' : ''}${x.later ? ' later' : ''}${x.due && !x.done ? ' due' : ''}`;
        const pill = x.live ? '<span class="pill train">Live</span>'
          : x.later ? `<span class="pill mute">every ${every} days</span>`
            : x.due && !x.done ? '<span class="pill weigh">Due today</span>'
              : x.next ? `<span class="pill ${x.kind}">Next</span>` : x.swap ? '<span class="pill eat">Something else</span>' : '';
        const inner = `<span class="time num">${esc(x.time)}</span>
          <span class="rail"><span class="dot">${x.done ? icons.check : ''}</span><span class="ln"></span></span>
          <span class="body"><b>${esc(x.name)} ${pill}</b><span>${esc(x.sub)}</span>${x.mac ? `<span class="mac">${x.mac}</span>` : ''}</span>`;
        if (x.kind === 'eat') return `<button type="button" class="${cls}" role="checkbox" aria-checked="${x.done}" data-meal="${x.meal}">${inner}</button>`;
        if (x.kind === 'weigh') return `<button type="button" class="${cls}" data-weigh>${inner}</button>`;
        return `<a class="${cls}" href="/session?date=${d.date}" data-link>${inner}</a>`;
      }).join('')}</div>`;
    }

    function hero(d) {
      const state = BO.sessionState(d);
      const name = BO.sessionName(d);
      const segs = Math.min(d.setsPlanned, 24);
      const on = d.setsPlanned ? Math.round(d.setsDone / d.setsPlanned * segs) : 0;
      const prog = segs > 1 ? `<div class="prog" aria-label="${d.setsDone} of ${d.setsPlanned} sets">${Array.from({ length: segs }, (_, i) => `<i class="${i < on ? 'on' : ''}"></i>`).join('')}</div>` : '';
      const go = (label, href = `/session?date=${d.date}`) => `<a class="go" href="${href}" data-link>${label} ${icons.arrow}</a>`;
      if (state === 'rest') {
        return `<div class="hero rest"><div><div class="tag">${esc(d.name || 'Rest day')}</div><h3>Recover today</h3><div class="meta">Nothing planned to lift. Eat well, sleep, and weigh in.</div></div>
          <button type="button" class="go" data-quick>Quick log ${icons.arrow}</button></div>`;
      }
      if (state === 'done') {
        const vol = d.exercises.reduce((n, ex) => n + (ex.volumeKg || 0), 0);
        const prs = d.exercises.filter(ex => ex.isPr).length;
        const w = BO.watchFor(watch, d.date).session;
        return `<div class="hero done"><div><div class="tag">${esc(name)} · done</div><h3>Session done</h3><div class="meta">${d.setsDone} sets${vol ? ` · ${BO.volume(vol)} lifted` : ''}${prs ? ` · ${BO.plural(prs, 'record')}` : ''}</div></div>${w ? BO.watchStats(w) : ''}${go('See the session')}</div>`;
      }
      if (state === 'missed') {
        return `<div class="hero" style="background:var(--mute)"><div><div class="tag">${esc(name)}</div><h3>Skipped</h3><div class="meta">Changed your mind? You can still log it.</div></div>${go('Log it anyway')}</div>`;
      }
      const next = BO.nextExercise(d);
      if (state === 'live') {
        return `<div class="hero"><div><div class="tag">In progress · ${esc(name)}</div><h3>Back to it</h3><div class="meta">${next ? `${esc(next.name)} is next · ` : ''}${d.setsPlanned - d.setsDone} sets left</div></div>${prog}${go('Resume session')}</div>`;
      }
      return `<div class="hero"><div><div class="tag">${date === week.today ? 'Up next' : BO.DAYS[d.weekday - 1]} · ${esc(name)}</div><h3>${date === week.today ? 'Time to train' : esc(name)}</h3><div class="meta">${BO.plural(d.exercises.length, 'exercise')} · ${d.setsPlanned} sets · about ${BO.sessionMinutes(d)} min</div></div>${prog}${go('Start session')}</div>`;
    }

    function scoreCard(d) {
      const eaten = d.meals.filter(m => m.eaten).length;
      const bar = (label, p, color, v) => `<div class="bar"><span>${label}</span><span class="t"><i style="width:${Math.round(p * 100)}%;background:${color}"></i></span><span class="v num">${v}</span></div>`;
      return `<div class="card score">
        <div><div class="big num" data-score>${shownScore}<small>/100</small></div><div class="sub" style="margin-top:8px">Day score</div></div>
        <div class="bars">
          ${d.setsPlanned ? bar('Training', d.setsDone / d.setsPlanned, 'var(--train)', `${d.setsDone}/${d.setsPlanned}`) : bar('Training', 0, 'var(--train)', 'rest')}
          ${d.meals.length ? bar('Meals', eaten / d.meals.length, 'var(--eat)', `${eaten}/${d.meals.length}`) : bar('Meals', 0, 'var(--eat)', '–')}
          ${(() => { const w = weighIn(d); return bar('Weigh-in', d.weightKg != null ? 1 : 0, 'var(--weigh)', d.weightKg != null ? 'done' : w.later ? `in ${BO.daysBetween(d.date, w.next)} d` : w.due ? 'today' : '–'); })()}
        </div></div>`;
    }

    function pips() {
      return `<div class="wk" role="group" aria-label="This week">${week.days.map(x => {
        const isToday = x.date === week.today;
        const future = x.date > week.today;
        const good = !future && !isToday && BO.dayCounts(x) && x.score >= BO.STREAK_AT;
        const part = !future && !isToday && !good && x.score > 0;
        const label = isToday ? x.score : good ? icons.check : part ? x.score : '';
        return `<button type="button" class="wd${isToday ? ' today' : ''}${good ? ' good' : ''}${part ? ' part' : ''}" data-date="${x.date}" aria-pressed="${x.date === date}" aria-label="${BO.DAYS[x.weekday - 1]}: score ${x.score}">
          <span>${BO.DAYS_SHORT[x.weekday - 1][0]}</span><span class="pip num">${label}</span></button>`;
      }).join('')}</div>`;
    }

    function sessionList(d) {
      if (!d.exercises.length) return '<p class="sub" style="margin:0">Rest day. Nothing to lift.</p>';
      const next = BO.nextExercise(d);
      const fromWatch = BO.watchFor(watch, d.date).cardio;
      return `<div class="slist">${d.exercises.map(ex => {
        let right;
        if (ex.type === 'cardio') right = [ex.cardioMinutes ? `${BO.load(ex.cardioMinutes)} min` : '', ex.cardioKm ? `${BO.load(ex.cardioKm)} km` : ''].filter(Boolean).join(' · ') || (ex === next ? 'next' : esc(ex.detail));
        else right = ex.sets.some(s => s != null) ? ex.sets.map(s => s == null ? '–' : s).join(' · ') : ex === next ? 'next' : esc(ex.detail);
        const w = fromWatch[ex.planExerciseId];
        const load = ex.type === 'cardio' ? (w ? `<span class="wsub">from Apple Watch</span>${w.activeKcal != null ? ` · ${BO.kcalText(w.activeKcal)} kcal` : ''}` : esc(ex.detail))
          : ex.isPr && ex.bestBeforeKg != null ? `${BO.load(ex.bestBeforeKg)} → ${BO.load(ex.loadKg)} kg`
            : ex.loadKg != null ? `${BO.load(ex.loadKg)} kg` : ex.last && ex.last.loadKg != null ? `last ${BO.load(ex.last.loadKg)} kg` : 'no load yet';
        const up = ex.earned && ex.stepKg != null && ex.loadKg != null ? ` · <span class="up-next">↑ ${BO.load(ex.loadKg + ex.stepKg)} next time</span>` : '';
        return `<a class="srow" href="/session?date=${d.date}&ex=${ex.planExerciseId}" data-link><div><b>${esc(ex.name)}</b><div class="sub">${ex.isPr || ex.repRecord ? '<span class="pill gold">PR</span> ' : ''}${load}${up}</div></div><span class="r num">${right}</span></a>`;
      }).join('')}</div>`;
    }

    /** What was eaten against what the day's plan adds up to (the target), with what the meals still to eat bring. */
    function foodCard(d) {
      const f = d.food;
      if (!f || !(BO.macrosKnown(f.planned))) return '';
      const eaten = d.meals.filter(m => m.eaten).length;
      const part = (label, key, unit, color) => {
        const plan = f.planned[key];
        if (plan == null || plan <= 0) return '';
        const done = f.eaten[key] || 0, left = f.left[key] || 0;
        const w = v => Math.max(0, Math.min(100, v / plan * 100)).toFixed(1);
        const val = key === 'kcal' ? `${BO.kcalText(done)}<small> / ${BO.kcalText(plan)}</small>` : `${Math.round(done)}<small> / ${Math.round(plan)} ${unit}</small>`;
        return `<div><div class="k">${label}</div><div class="v num">${val}</div><div class="t"><i style="width:${w(done)}%;background:${color}"></i><i style="width:${w(Math.min(left, Math.max(0, plan - done)))}%;background:${color}"></i></div></div>`;
      };
      const next = d.meals.find(m => !m.eaten && m.macros && m.macros.proteinG != null);
      let note = 'The target is what today’s plan adds up to; the faded part is still to eat.';
      if (f.planned.proteinG != null) {
        const done = f.eaten.proteinG || 0;
        if (next) {
          const after = done + (f.left.proteinG || 0);
          const diff = Math.round(after - f.planned.proteinG);
          note = `${esc(next.name)} brings <b>${Math.round(next.macros.proteinG)} g</b>. Eating the rest of the plan: <b>${Math.round(after)} g</b>${diff < -2 ? `, ${-diff} g under the plan` : diff > 2 ? `, ${diff} g over the plan` : ', right on the plan'}.`;
        } else if (eaten === d.meals.length && d.meals.length) {
          const diff = Math.round(done - f.planned.proteinG);
          note = `All meals done: <b>${Math.round(done)} g</b> protein${diff < -2 ? `, ${-diff} g under the plan` : diff > 2 ? `, ${diff} g over the plan` : ', right on the plan'}.`;
        }
      }
      return `<section class="card fuel" aria-labelledby="h-fuel">
        <div class="card-head" style="margin:0"><h2 class="h-l" id="h-fuel">Food today</h2><span class="sub">${eaten} of ${d.meals.length} meals</span></div>
        <div class="two">${part('Protein', 'proteinG', 'g', 'var(--eat)')}${part('Calories', 'kcal', '', 'var(--ink)')}</div>
        <p class="note">${note}</p>
      </section>`;
    }

    function weightCard() {
      const entries = weights.entries.slice(-12);
      const latest = weights.entries.at(-1);
      if (!latest) return `<div class="card"><div class="card-head"><span class="pill weigh">Body weight</span></div><div class="empty"><b>No weigh-ins yet.</b><button type="button" class="btn" data-weigh>Log your weight</button></div></div>`;
      return `<a class="card" href="/progress" data-link style="text-decoration:none;color:inherit;display:block">
        <div class="card-head"><span class="pill weigh">Body weight</span>${weights.goalKg != null ? `<span class="sub">goal ${BO.kg(weights.goalKg)}</span>` : ''}</div>
        <div class="wbig"><b class="num">${BO.kg(latest.weightKg)}</b><span>kg</span></div>
        <div style="--c:var(--weigh);margin-top:10px">${BO.lineChart(entries.map(e => ({ date: e.date, value: e.weightKg })), { goal: weights.goalKg, goalLabel: '', height: 120, width: 300, label: v => BO.kg(v) })}</div></a>`;
    }

    function render() {
      const d = day();
      const isToday = date === week.today;
      const r = i => firstPaint ? ` rise" style="--i:${i}` : '';
      const head = `<div class="page-head${r(0)}">
          <div><div class="hello">${isToday ? `${BO.greeting()}, ${esc(BO.me.username)}` : `<a href="/today" data-link>Back to today</a>`}</div>
            <h1 class="h-xl">${BO.DAYS[d.weekday - 1]} ${BO.dayNum(d.date)}<span class="desk-only"> ${BO.MONTHS[BO.parseDate(d.date).getMonth()]}</span></h1></div>
          <div class="acts">${streakChip()}<button type="button" class="btn ink desk-only" data-quick>${icons.plus} Quick log</button><a class="icon-btn phone-only" href="/settings" data-link aria-label="Settings">${icons.settings}</a></div>
        </div>`;

      root.innerHTML = `${head}
        <div class="today-grid">
          <div class="col">
            <div class="${firstPaint ? 'rise' : ''}" style="--i:1">${scoreCard(d)}</div>
            <div class="${firstPaint ? 'rise' : ''}" style="--i:2">${pips()}</div>
            <div class="phone-hero ${firstPaint ? 'rise' : ''}" style="--i:3">${hero(d)}</div>
            <div class="phone-hero ${firstPaint ? 'rise' : ''}" style="--i:3">${foodCard(d)}</div>
            <section class="${firstPaint ? 'rise' : ''}" style="--i:4" aria-labelledby="h-day">
              <div class="spread" style="margin:6px 0 4px"><h2 class="h-l" id="h-day">${isToday ? 'Your day' : 'The day'}</h2><span class="sub">tap to tick</span></div>
              ${timeline(d)}
            </section>
          </div>
          <div class="col side-col">
            <div class="desk-hero ${firstPaint ? 'rise' : ''}" style="--i:2">${hero(d)}</div>
            <section class="card ${firstPaint ? 'rise' : ''}" style="--i:3" aria-labelledby="h-session">
              <div class="card-head"><h2 class="h-l" id="h-session">${isToday ? "Today's session" : 'Session'}</h2><span class="sub">${d.exercises.length ? 'tap to log' : ''}</span></div>
              ${sessionList(d)}
            </section>
          </div>
          <div class="col side-col">
            <div class="desk-hero ${firstPaint ? 'rise' : ''}" style="--i:3">${foodCard(d)}</div>
            <div class="${firstPaint ? 'rise' : ''}" style="--i:4">${weightCard()}</div>
            <section class="card ${firstPaint ? 'rise' : ''}" style="--i:5" aria-labelledby="h-week">
              <div class="card-head"><h2 class="h-l" id="h-week">This week</h2><span class="sub">day scores</span></div>
              ${BO.weekBars(week, { picked: date, height: 120 })}
            </section>
          </div>
        </div>`;

      const target = d.score;
      const el = root.querySelector('[data-score]');
      if (firstPaint) BO.countUp(el, target, { suffix: '<small>/100</small>', duration: 800 });
      else if (shownScore !== target) BO.countUp(el, target, { from: shownScore, suffix: '<small>/100</small>', duration: 450 });
      shownScore = target;
      firstPaint = false;
    }

    /** Celebrates when today's score crosses the streak bar or reaches 100. */
    function celebrate(before) {
      const d = todayDay();
      if (!d || date !== week.today) return;
      if (before < 100 && d.score === 100) {
        BO.confetti();
        BO.buzz([20, 40, 20]);
        BO.toast('Perfect day. Everything ticked off.');
      } else if (before < BO.STREAK_AT && d.score >= BO.STREAK_AT && BO.dayCounts(d)) {
        BO.buzz(20);
        BO.toast(`Streak kept: ${BO.streakNow(streak, d)} ${BO.streakNow(streak, d) === 1 ? 'day' : 'days'} in a row`);
      }
    }

    /** Ticks, unticks or swaps a meal; the score and the timeline update at once, the server catches up. */
    function setMeal(meal, eaten, swap = null) {
      const d = day();
      const before = d.score;
      meal.eaten = eaten;
      meal.swap = eaten ? swap : null;
      if (eaten) BO.buzz(10);
      BO.scoreDay(d);
      render();
      celebrate(before);
      const body = { eaten };
      if (eaten && swap) Object.assign(body, { swapName: swap.name, swapKcal: swap.kcal, swapProteinG: swap.proteinG });
      save('PUT', `/api/days/${date}/meals/${meal.planMealId}`, body).catch(() => refresh());
    }

    /** Tap a meal: as planned, something else (what, with its calories and protein), or not eaten. */
    function mealSheet(meal) {
      BO.sheet(async (body, close) => {
        const foods = await BO.loadFoods().catch(() => ({ list: [], byKey: new Map() }));
        let other = !!meal.swap;
        let picked = null;
        const swap = Object.assign({ name: '', kcal: null, proteinG: null }, meal.swap || {});
        const planned = [meal.foods, meal.macros && meal.macros.kcal != null ? `${BO.kcalText(meal.macros.kcal)} kcal` : '', meal.macros && meal.macros.proteinG != null ? `${BO.gramsText(meal.macros.proteinG)} protein` : ''].filter(Boolean).join(' · ');
        const paint = () => {
          const matches = foods.list.filter(f => !swap.name || BO.foodKey(f.name).includes(BO.foodKey(swap.name))).slice(0, 6);
          body.innerHTML = `<div><h3>${esc(meal.name)}</h3><span class="sub">${meal.time ? `${esc(meal.time)} · ` : ''}${planned ? `planned: ${esc(planned)}` : 'nothing listed for it'}</span></div>
            <div class="ql two"><button type="button" class="e" data-plan aria-pressed="${meal.eaten && !meal.swap && !other}">${icons.check}As planned</button><button type="button" class="e" data-other aria-pressed="${other}">${icons.swap}Something else</button></div>
            ${other ? `<label class="lbl-f">What did you eat?<input class="field" data-sw-name value="${esc(swap.name)}" maxlength="80" placeholder="e.g. Chicken wrap" autocomplete="off"></label>
              ${matches.length ? `<div class="pick-chips">${matches.map(f => `<button type="button" data-sw-food="${f.id}" aria-pressed="${picked && picked.id === f.id}">${esc(f.name)}</button>`).join('')}</div>` : ''}
              ${picked ? `<label class="lbl-f">How much<span class="row" style="gap:8px"><input class="field small num" inputmode="decimal" data-sw-amount value="${BO.load(picked.amount)}" style="max-width:110px"><span class="sub">${picked.basis === 'perPiece' ? esc(picked.pieceName || 'pieces') : 'g'}</span></span></label>` : ''}
              <div class="mac-in">
                <label>Calories<span><input class="num" inputmode="decimal" data-sw="kcal" value="${swap.kcal == null ? '' : BO.load(Math.round(swap.kcal))}" placeholder="–"><em>kcal</em></span></label>
                <label class="p">Protein<span><input class="num" inputmode="decimal" data-sw="proteinG" value="${swap.proteinG == null ? '' : BO.load(Math.round(swap.proteinG * 10) / 10)}" placeholder="–"><em>g</em></span></label>
              </div>
              <p class="fine">${picked ? `Worked out from “${esc(picked.name)}” in your foods. ` : 'Calories and protein are optional. '}It ticks the meal for the day score; next week’s plan stays the same.</p>
              <button type="button" class="btn lg block" style="background:var(--eat);color:#fff;box-shadow:none" data-sw-save>Save ${esc(meal.name.toLowerCase())}</button>` : ''}
            ${meal.eaten ? '<button type="button" class="btn ghost" data-uneat>Not eaten</button>' : ''}`;
        };
        const fill = () => {
          if (!picked) return;
          const m = BO.macrosFor(picked, picked.amount, picked.basis === 'perPiece' ? 'pieces' : 'grams');
          swap.kcal = m ? m.kcal : null;
          swap.proteinG = m ? m.proteinG : null;
        };
        body.addEventListener('click', e => {
          if (e.target.closest('[data-plan]')) { close(); setMeal(meal, true, null); return; }
          if (e.target.closest('[data-other]')) { other = true; paint(); const i = body.querySelector('[data-sw-name]'); if (i) i.focus(); return; }
          if (e.target.closest('[data-uneat]')) { close(); setMeal(meal, false); return; }
          const pf = e.target.closest('[data-sw-food]');
          if (pf) {
            const f = foods.list.find(x => String(x.id) === pf.dataset.swFood);
            picked = Object.assign({}, f, { amount: f.basis === 'perPiece' ? 1 : 100 });
            swap.name = f.name;
            fill();
            paint();
            return;
          }
          if (e.target.closest('[data-sw-save]')) {
            if (!swap.name.trim()) { BO.toast('Say what you ate.', { error: true }); return; }
            close();
            setMeal(meal, true, { name: swap.name.trim(), kcal: swap.kcal == null ? null : Math.round(swap.kcal * 10) / 10, proteinG: swap.proteinG == null ? null : Math.round(swap.proteinG * 10) / 10 });
          }
        });
        body.addEventListener('input', e => {
          const t = e.target;
          if (t.matches('[data-sw-name]')) {
            swap.name = t.value;
            if (picked && BO.foodKey(picked.name) !== BO.foodKey(t.value)) picked = null;
            const chips = body.querySelector('.pick-chips');
            const matches = foods.list.filter(f => !swap.name || BO.foodKey(f.name).includes(BO.foodKey(swap.name))).slice(0, 6);
            if (chips) chips.innerHTML = matches.map(f => `<button type="button" data-sw-food="${f.id}">${esc(f.name)}</button>`).join('');
          } else if (t.matches('[data-sw-amount]') && picked) {
            picked.amount = BO.readNumber(t.value);
            fill();
            body.querySelector('[data-sw="kcal"]').value = swap.kcal == null ? '' : BO.load(Math.round(swap.kcal));
            body.querySelector('[data-sw="proteinG"]').value = swap.proteinG == null ? '' : BO.load(Math.round(swap.proteinG * 10) / 10);
          } else if (t.matches('[data-sw]')) {
            swap[t.dataset.sw] = BO.readNumber(t.value);
          }
        });
        paint();
      }, { label: meal.name });
    }

    root.addEventListener('click', event => {
      const t = event.target;
      const pip = t.closest('[data-date]');
      if (pip) {
        date = pip.dataset.date;
        BO.setQuery({ date: date === week.today ? null : date });
        shownScore = day().score;
        render();
        return;
      }
      if (t.closest('[data-weigh]')) {
        BO.quickLog({ tab: 'weigh', date, onDone: () => BO.reload() });
        return;
      }
      const mealBtn = t.closest('[data-meal]');
      if (mealBtn) {
        const meal = day().meals.find(m => String(m.planMealId) === mealBtn.dataset.meal);
        if (meal) mealSheet(meal);
      }
    });

    render();

    // Opened from a reminder: ?meal= opens that meal of today, ?weigh=1 the weigh-in.
    const q = BO.query();
    if (q.meal || q.weigh) {
      BO.setQuery({ meal: null, weigh: null });
      const meal = q.meal && todayDay() ? todayDay().meals.find(m => String(m.planMealId) === q.meal) : null;
      if (meal) { date = week.today; render(); mealSheet(meal); }
      else if (q.weigh) BO.quickLog({ tab: 'weigh', date: week.today, onDone: () => BO.reload() });
    }
  };
})();
