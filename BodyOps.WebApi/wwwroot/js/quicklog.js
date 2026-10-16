/* BodyOps — Quick log: the + button. A weigh-in, a meal or the workout in two taps, from any screen. */
'use strict';

(() => {
  const { esc, icons } = BO;

  /** Opens the quick log. options: tab ('weigh' | 'meals'), date (yyyy-MM-dd, default today), onDone (after a change). */
  BO.quickLog = function quickLog({ tab = null, date = null, onDone = null } = {}) {
    let changed = false;
    BO.sheet(async (body, close) => {
      body.innerHTML = '<div class="loading" style="min-height:220px"><div class="spinner"></div></div>';
      let week, weights;
      try {
        [week, weights] = await Promise.all([
          BO.api('GET', '/api/days/week' + (date ? `?date=${date}` : '')),
          BO.api('GET', '/api/weights'),
        ]);
      } catch (e) {
        body.innerHTML = `<div class="empty"><b>Couldn't load.</b><span>${esc(e.message)}</span></div>`;
        return;
      }
      const day = week.days.find(d => d.date === (date || week.today)) || week.days[0];
      const entry = weights.entries.find(e => e.date === day.date);
      const before = weights.entries.filter(e => e.date < day.date).at(-1);
      let value = entry ? entry.weightKg : before ? before.weightKg : 80;
      tab = tab || (entry ? 'meals' : 'weigh');
      const nextMeal = day.meals.find(m => !m.eaten);
      const isToday = day.date === week.today;

      function paint() {
        const workout = day.exercises.length
          ? `<button type="button" class="t" data-train>${icons.plan}${esc(BO.sessionName(day))}</button>`
          : `<button type="button" class="t" disabled>${icons.moon}Rest day</button>`;
        const weigh = `<div class="stepper">
            <button type="button" data-w="-0.1" aria-label="100 g less">−</button>
            <div class="v"><input class="num" inputmode="decimal" value="${BO.kg(value)}" data-wv aria-label="Weight in kg"><small>kg${before ? ` · ${BO.DAYS[BO.weekday(before.date) - 1].toLowerCase()} ${BO.kg(before.weightKg)}` : ''}</small></div>
            <button type="button" data-w="0.1" aria-label="100 g more">+</button>
          </div>
          <button type="button" class="btn ink lg block" data-save-w>${entry ? 'Update weigh-in' : 'Save weigh-in'}</button>
          <p class="fine" style="text-align:center">Saves to ${isToday ? 'today' : BO.fullDate(day.date)}. Hold − or + to move faster.</p>`;
        const meals = day.meals.length
          ? `<div class="qmeals">${day.meals.map(m => `<button type="button" class="qmeal" role="checkbox" aria-checked="${m.eaten}" data-m="${m.planMealId}"><span class="tick">${icons.check}</span><span style="min-width:0"><b>${esc(m.name)}</b><span>${esc(m.swap ? `${m.swap.name} (instead)` : m.foods || '')}</span></span>${m.time ? `<em>${m.time}</em>` : ''}</button>`).join('')}</div>`
          : `<div class="empty"><b>No meals planned for ${BO.DAYS[day.weekday - 1]}.</b><a class="btn" href="/plan?day=${day.weekday}&tab=meals" data-link>Plan meals</a></div>`;
        body.innerHTML = `
          <div class="spread"><h3>Quick log</h3><span class="sub">${BO.DAYS_SHORT[day.weekday - 1]} ${BO.shortDate(day.date)}${isToday ? ` · ${BO.nowTime()}` : ''}</span></div>
          <div class="ql">
            <button type="button" class="w" data-tab="weigh" aria-pressed="${tab === 'weigh'}">${icons.scale}Weigh-in</button>
            <button type="button" class="e" data-tab="meals" aria-pressed="${tab === 'meals'}">${icons.bowl}${esc(nextMeal ? nextMeal.name : 'Meals')}</button>
            ${workout}
          </div>
          ${tab === 'weigh' ? weigh : meals}`;
      }
      paint();

      const finish = () => { changed = true; };
      let hold = null;
      const nudge = d => {
        const input = body.querySelector('[data-wv]');
        value = Math.round(((BO.readNumber(input.value) ?? value) + d) * 10) / 10;
        input.value = BO.kg(value);
      };
      body.addEventListener('pointerdown', ev => {
        const b = ev.target.closest('[data-w]');
        if (!b) return;
        const d = Number(b.dataset.w);
        nudge(d);
        let speed = 260;
        const again = () => { nudge(d); speed = Math.max(50, speed * 0.82); hold = setTimeout(again, speed); };
        hold = setTimeout(again, 420);
      });
      ['pointerup', 'pointerleave', 'pointercancel'].forEach(name => body.addEventListener(name, () => { clearTimeout(hold); hold = null; }));

      body.addEventListener('click', async ev => {
        const t = ev.target;
        const tabBtn = t.closest('[data-tab]');
        if (tabBtn) { tab = tabBtn.dataset.tab; paint(); return; }
        if (t.closest('[data-train]')) { close(); BO.go(`/session?date=${day.date}`); return; }
        if (t.closest('[data-save-w]')) {
          const v = BO.readNumber(body.querySelector('[data-wv]').value);
          if (v == null) return;
          try {
            await BO.saving(BO.api('PUT', `/api/weights/${day.date}`, { weightKg: v }));
            BO.buzz(15);
            finish();
            close();
            BO.toast(`Weigh-in saved: ${BO.kg(v)} kg`);
          } catch (e) { }
          return;
        }
        const m = t.closest('[data-m]');
        if (m) {
          const meal = day.meals.find(x => String(x.planMealId) === m.dataset.m);
          meal.eaten = !meal.eaten;
          m.setAttribute('aria-checked', String(meal.eaten));
          if (meal.eaten) BO.buzz(10);
          finish();
          BO.saving(BO.api('PUT', `/api/days/${day.date}/meals/${meal.planMealId}`, { eaten: meal.eaten })).catch(() => { });
        }
      });
    }, { label: 'Quick log' });

    // Redraw the screen behind once the sheet is gone.
    const watch = setInterval(() => {
      if (document.querySelector('.sheet-back')) return;
      clearInterval(watch);
      if (changed) (onDone || BO.reload)();
    }, 200);
  };
})();
