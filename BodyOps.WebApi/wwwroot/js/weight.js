/* BodyOps — Weight: weigh-ins, the trend and the goal. */
'use strict';

(() => {
  const { esc, icons } = BO;

  BO.screens.weight = async function weight(root) {
    let data = await BO.api('GET', '/api/weights');
    let editingGoal = false;
    const today = BO.localToday();

    function render() {
      const entries = data.entries;
      const latest = entries[entries.length - 1];
      const previous = entries[entries.length - 2];
      const first = entries[0];
      const goal = data.goalKg;

      let head;
      if (!latest) {
        head = `<div class="empty"><b>No weigh-ins yet.</b><span>Log your weight below. Weigh yourself at the same time of day for a fair trend.</span></div>`;
      } else {
        const sub = [`${BO.shortDate(latest.date)}`];
        if (previous) sub.push(`${BO.signed(latest.weightKg - previous.weightKg)} kg vs previous`);
        if (entries.length > 2) sub.push(`${BO.signed(latest.weightKg - first.weightKg)} kg since ${BO.shortDate(first.date)}`);
        head = `<div><div class="big-w"><b class="num">${BO.kg(latest.weightKg)}</b><span>kg</span></div><span class="sub">${esc(sub.join(' · '))}</span></div>`;
      }

      let trip = '';
      if (goal != null && latest) {
        const start = first.weightKg;
        const span = start - goal;
        const done = span === 0 ? 1 : (start - latest.weightKg) / span;
        const pct = Math.max(0, Math.min(1, done));
        const left = Math.abs(latest.weightKg - goal);
        trip = `<div class="trip">
          <div class="spread"><span style="font-weight:600">Goal ${BO.kg(goal)} kg</span><span class="sub">${left < 0.05 ? 'Reached' : `${BO.kg(left)} kg to go`}</span></div>
          <div class="trip-bar"><i style="width:${Math.round(pct * 100)}%"></i></div>
          <div class="trip-ends num"><span>Start ${BO.kg(start)}</span><span>${Math.round(pct * 100)}% of the way</span></div>
        </div>`;
      }

      const goalRow = editingGoal
        ? `<form class="row" id="goalForm"><input class="field" id="goalInput" inputmode="decimal" value="${goal ?? ''}" placeholder="kg" aria-label="Weight goal in kg" style="max-width:120px"><button class="btn btn-primary" type="submit">Save goal</button><button class="btn" type="button" id="goalCancel">Cancel</button>${goal != null ? '<button class="link-btn" type="button" id="goalClear">Remove goal</button>' : ''}</form>`
        : `<button type="button" class="link-btn" id="goalEdit">${goal != null ? 'Change goal' : 'Set a goal'}</button>`;

      const chart = entries.length
        ? BO.lineChart(entries.slice(-90).map(e => ({ date: e.date, value: e.weightKg })), { goal, goalLabel: goal != null ? `goal ${BO.kg(goal)} kg` : '', height: 200, label: v => BO.kg(v) })
        : '';

      const list = entries.slice().reverse().map((e, i, all) => {
        const before = all[i + 1];
        const diff = before ? e.weightKg - before.weightKg : null;
        return `<div class="wrow"><div><b class="num">${BO.kg(e.weightKg)} kg</b><div class="sub">${BO.fullDate(e.date)}</div></div>
          <span class="delta num ${diff == null ? '' : diff < 0 ? 'down' : diff > 0 ? 'up' : ''}">${diff == null ? '' : BO.signed(diff)}</span>
          <button type="button" class="del" data-del="${e.date}" aria-label="Remove the weigh-in of ${BO.fullDate(e.date)}">${icons.x}</button></div>`;
      }).join('');

      root.innerHTML = `
        <div class="page-head"><h1 class="title-xl">Weight</h1>${BO.syncHTML()}</div>
        <div class="weight-grid">
          <div class="stack">
            ${head}
            ${trip}
            <div>${goalRow}</div>
            ${chart ? `<section class="panel" aria-label="Trend">${chart}</section>` : ''}
          </div>
          <div class="stack">
            <form class="panel" id="addForm" aria-labelledby="h-add">
              <h2 class="title-l" id="h-add">Log a weigh-in</h2>
              <div class="addw">
                <input class="field" type="date" id="wDate" value="${today}" max="${today}" aria-label="Date">
                <input class="field" id="wValue" inputmode="decimal" placeholder="kg" aria-label="Weight in kg">
                <button type="submit" class="btn btn-primary">Log</button>
              </div>
              <p class="fine">Logging a date that already has a weigh-in replaces it.</p>
            </form>
            ${entries.length ? `<div class="wlist">${list}</div>` : ''}
          </div>
        </div>`;
    }

    async function put(date, weightKg) {
      data = await BO.saving(BO.api('PUT', `/api/weights/${date}`, { weightKg }));
      render();
    }

    root.addEventListener('submit', async event => {
      event.preventDefault();
      if (event.target.id === 'addForm') {
        const value = BO.readNumber(root.querySelector('#wValue').value);
        const date = root.querySelector('#wDate').value || today;
        if (value == null) { BO.toast('Type your weight in kg first.', { error: true }); return; }
        try { await put(date, value); BO.toast(`Logged ${BO.kg(value)} kg for ${BO.shortDate(date)}`); } catch (e) { }
      } else if (event.target.id === 'goalForm') {
        const value = BO.readNumber(root.querySelector('#goalInput').value);
        try {
          await BO.saving(BO.api('PUT', '/api/settings', { weightGoalKg: value }));
          data.goalKg = value;
          editingGoal = false;
          render();
        } catch (e) { }
      }
    });

    root.addEventListener('click', async event => {
      const t = event.target;
      if (t.closest('#goalEdit')) { editingGoal = true; render(); root.querySelector('#goalInput').focus(); return; }
      if (t.closest('#goalCancel')) { editingGoal = false; render(); return; }
      if (t.closest('#goalClear')) {
        try { await BO.saving(BO.api('PUT', '/api/settings', { weightGoalKg: null })); data.goalKg = null; editingGoal = false; render(); } catch (e) { }
        return;
      }
      const del = t.closest('[data-del]');
      if (del) {
        const date = del.dataset.del;
        const entry = data.entries.find(e => e.date === date);
        try {
          data = await BO.saving(BO.api('DELETE', `/api/weights/${date}`));
          render();
          BO.toast(`Weigh-in of ${BO.shortDate(date)} removed`, { action: 'Undo', onAction: () => put(date, entry.weightKg).catch(() => { }) });
        } catch (e) { }
      }
    });

    render();
  };
})();
