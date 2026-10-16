/* BodyOps — Settings: account, training (rest timer), food, reminders, appearance, backups, and (for the admin) the other accounts. */
'use strict';

(() => {
  const { esc, icons } = BO;

  BO.screens.settings = async function settings(root) {
    let users = BO.me.isAdmin ? await BO.api('GET', '/api/admin/users') : [];
    let prefs = await BO.settings(true);
    const [plan, foods, weights] = await Promise.all([BO.api('GET', '/api/plan').catch(() => null), BO.loadFoods(true).catch(() => null), BO.api('GET', '/api/weights').catch(() => null)]);

    /** The plan's food per day, on average over the days with values: Settings shows it, the plan sets it. */
    function weekAverage() {
      if (!plan || !foods) return null;
      const perDay = plan.days.map(d => BO.sumMacros(d.meals.map(m => BO.mealMacros({ foods: m.foods.map(f => ({ name: f.name, amount: f.amount, unit: f.unit })) }, foods)).filter(BO.macrosKnown)))
        .filter(BO.macrosKnown);
      if (!perDay.length) return null;
      const avg = k => { const v = perDay.filter(t => t[k] != null); return v.length ? v.reduce((n, t) => n + t[k], 0) / v.length : null; };
      return { kcal: avg('kcal'), proteinG: avg('proteinG'), carbsG: avg('carbsG'), fatG: avg('fatG'), days: perDay.length };
    }
    const push = BO.push;
    push.load().then(() => { if (root.isConnected) render(); });
    let watch = null;
    BO.watchLoad(true).then(w => { watch = w; if (root.isConnected) render(); }).catch(() => { watch = { connected: false, workouts: [], failed: true }; if (root.isConnected) render(); });
    let firstPaint = true;
    let panel = null;          // 'password' | 'delete' while one of those forms is open
    let confirmUser = null;    // user id waiting for "Delete" confirmation
    const tempPasswords = {};  // user id → temporary password just set

    /** When the next weigh-in is, in words. */
    function weighText() {
      const every = prefs.weighEveryDays || 1;
      if (every <= 1) return 'Today shows it every morning.';
      const w = weights ? BO.weighIn(weights.entries, every, BO.localToday()) : null;
      const next = !w ? '' : w.entry ? `Next one ${BO.fullDate(w.next).replace(/ \d{4}$/, '')}. ` : w.due ? 'Due today. ' : `Next one ${BO.fullDate(w.next).replace(/ \d{4}$/, '')}. `;
      return `${next}On the other days Today shows the countdown, and the score never counts it.`;
    }

    /** "last push 2 h ago" / "added 3 Oct". */
    function deviceWhen(d) {
      const at = d.lastSentAt || d.createdAt;
      const day = at ? at.slice(0, 10) : null;
      return day ? `${d.lastSentAt ? 'last reminder' : 'added'} ${day === BO.localToday() ? 'today' : BO.shortDate(day)}` : '';
    }

    /** The Reminders card: this device on or off, the other devices, and which reminders to get. */
    function remindersCard() {
      const st = push.state;
      const r = prefs.reminders || {};
      const devices = st ? st.devices : [];
      const here = push.here;
      let sub, actions = '';
      if (!st) sub = 'Checking this device…';
      else if (!st.enabled) sub = 'Turned off on this server (MILO_PUSH_ENABLED in .env).';
      else if (here) sub = `${esc(push.deviceLabel())}. Each phone or computer turns it on once.`;
      else if (!push.supported()) sub = esc(push.problem() || "This browser can't get reminders here.");
      else sub = 'Each phone or computer turns it on once. Milo asks for permission first.';
      if (st && st.enabled) {
        if (here) actions = '<button type="button" class="btn" data-push-test>Send a test</button><button type="button" class="btn" data-push-off>Turn off</button>';
        else if (push.supported()) actions = `${devices.length ? '<button type="button" class="btn" data-push-test>Send a test</button>' : ''}<button type="button" class="btn primary" data-push-on>${icons.bell} Turn on</button>`;
      }
      const others = devices.filter(d => d.key !== here).map(d => `<div class="set-row dev"><div><b>${esc(d.device || 'Device')}</b>
          <span class="sub">${deviceWhen(d)}${d.lastError ? ` · <span class="warn-t">last one failed</span>` : ''}</span></div>
          <button type="button" class="btn" data-push-remove="${d.id}">Remove</button></div>`).join('');
      const row = (key, label, text, timeKey) => {
        const on = r[key] !== false;
        return `<div class="set-row rm"><div><b>${label}</b><span class="sub">${text}</span></div>
          <div class="row remind-ctl">${timeKey ? `<input class="tm num" type="time" value="${esc(r[timeKey] || '')}" data-rtime="${timeKey}" aria-label="${label} time" ${on ? '' : 'disabled'} required>` : ''}
          <button type="button" class="sw" role="switch" aria-checked="${on}" aria-label="${label}" data-remind="${key}"></button></div></div>`;
      };
      const every = prefs.weighEveryDays || 1;
      return `<section class="set-card ${firstPaint ? 'rise' : ''}" style="--i:4" aria-labelledby="h-remind">
          <h3 id="h-remind">Reminders</h3>
          <div class="set-row"><div><b>On this device <span class="pill ${here ? 'good' : 'mute'}">${here ? 'On' : 'Off'}</span></b><span class="sub">${sub}</span></div>${actions ? `<div class="row">${actions}</div>` : ''}</div>
          ${others ? `<div class="set-devs"><span class="sub">Also on</span>${others}</div>` : ''}
          ${row('meals', 'Meals', 'At each meal’s time, with what’s on the plate. “Ate it” ticks it from the notification.')}
          ${row('workout', 'Workout', 'Training days, if nothing is logged yet.', 'workoutTime')}
          ${row('weighIn', 'Weigh-in day', every <= 1 ? 'Every day, until you weigh in.' : 'On the day it’s due.', 'weighInTime')}
          ${row('streak', 'Streak at risk', `Only if today is still under ${BO.STREAK_AT}.`, 'streakTime')}
        </section>`;
    }

    function render() {
      const theme = BO.theme();
      const userRows = users.filter(u => u.id !== BO.me.id).map(u => `<div class="set-row">
          <div><b>${esc(u.username)}</b>${u.isAdmin ? ' <span class="badge">Admin</span>' : ''}
            <span class="sub">${BO.plural(u.plannedExercises, 'exercise')} · ${BO.plural(u.plannedMeals, 'meal')} planned${u.lastLoggedDay ? ` · last logged ${BO.shortDate(u.lastLoggedDay)}` : ''}</span>
            ${tempPasswords[u.id] ? `<span class="sub">Temporary password, give it to ${esc(u.username)}:</span><div class="temp-pass">${esc(tempPasswords[u.id])}</div>` : ''}</div>
          <div class="row">${confirmUser === u.id
            ? `<button type="button" class="btn" data-user-cancel>Keep</button><button type="button" class="btn danger" data-user-delete="${u.id}">Delete ${esc(u.username)}</button>`
            : `<button type="button" class="btn" data-user-reset="${u.id}">Reset password</button><button type="button" class="btn danger" data-user-confirm="${u.id}">Delete</button>`}</div>
        </div>`).join('');

      root.innerHTML = `
        <div class="page-head ${firstPaint ? 'rise' : ''}"><div><div class="hello">${esc(BO.me.username)}</div><h1 class="h-xl">Settings</h1></div>${BO.syncHTML()}</div>
        <div class="set-grid">
          <div class="stack">
            <section class="set-card ${firstPaint ? 'rise' : ''}" style="--i:1" aria-labelledby="h-account">
              <h3 id="h-account">Account</h3>
              <div class="set-row"><div><b>${esc(BO.me.username)}${BO.me.isAdmin ? '<span class="badge">Admin</span>' : ''}</b><span class="sub">Your plan, days and weigh-ins belong to this account.</span></div><button type="button" class="btn" id="signOut">Sign out</button></div>
              <div class="set-row"><div><b>Password</b><span class="sub">Changing it signs out your other devices.</span></div><button type="button" class="btn" data-panel="password">${panel === 'password' ? 'Cancel' : 'Change'}</button></div>
              ${panel === 'password' ? `<form class="set-form" id="passwordForm">
                <label class="lbl-f">Current password<input class="field" type="password" id="curPass" autocomplete="current-password" required></label>
                <label class="lbl-f">New password<input class="field" type="password" id="newPass" autocomplete="new-password" required><small>At least 8 characters.</small></label>
                <button class="btn primary" type="submit">Change password</button>
              </form>` : ''}
              <div class="set-row"><div><b>Delete account</b><span class="sub">Removes the account with its plan, days and weigh-ins.</span></div><button type="button" class="btn danger" data-panel="delete">${panel === 'delete' ? 'Cancel' : 'Delete'}</button></div>
              ${panel === 'delete' ? `<form class="set-form" id="deleteForm">
                <span class="sub">Export a backup first if you might want this data again.</span>
                <label class="lbl-f">Your password, to confirm<input class="field" type="password" id="delPass" autocomplete="current-password" required></label>
                <button class="btn danger" type="submit">Delete my account for good</button>
              </form>` : ''}
            </section>

            <section class="set-card ${firstPaint ? 'rise' : ''}" style="--i:2" aria-labelledby="h-train">
              <h3 id="h-train">Training</h3>
              <div class="set-row"><div><b>Rest between sets</b><span class="sub">The timer in session mode starts after each set.</span></div>
                <div class="seg" role="group" aria-label="Rest between sets">${[60, 90, 120, 180].map(v => `<button type="button" data-rest="${v}" aria-pressed="${prefs.restSeconds === v}">${BO.clock(v)}</button>`).join('')}</div></div>
              <div class="set-row"><div><b>Weigh-in every</b><span class="sub">${weighText()}</span></div>
                <div class="seg" role="group" aria-label="Weigh-in every">${[...new Set([1, 7, 15, 30, prefs.weighEveryDays || 1])].sort((a, b) => a - b).map(v => `<button type="button" data-weigh-every="${v}" aria-pressed="${(prefs.weighEveryDays || 1) === v}">${v === 1 ? 'Day' : `${v} d`}</button>`).join('')}</div></div>
              <div class="set-row"><div><b>Weight goal</b><span class="sub">${prefs.weightGoalKg != null ? `${BO.kg(prefs.weightGoalKg)} kg` : 'None set'} · shown on Progress.</span></div><a class="btn" href="/progress" data-link>Edit</a></div>
            </section>

            <section class="set-card ${firstPaint ? 'rise' : ''}" style="--i:3" aria-labelledby="h-food">
              <h3 id="h-food">Food</h3>
              <div class="set-row"><div><b>Targets come from your plan</b><span class="sub">Each day’s calories, protein, carbs and fat are what its planned meals add up to. Change the meals to change them.</span></div><a class="btn" href="/plan?tab=meals" data-link>Open Plan</a></div>
              ${(() => {
                const a = weekAverage();
                return a ? `<div class="set-row"><div><b>Your week, on average</b><span class="sub">${[a.kcal != null ? `${BO.kcalText(a.kcal)} kcal` : '', a.proteinG != null ? `${Math.round(a.proteinG)} g protein` : '', a.carbsG != null ? `${Math.round(a.carbsG)} g carbs` : '', a.fatG != null ? `${Math.round(a.fatG)} g fat` : ''].filter(Boolean).join(' · ')} a day${a.days < 7 ? ` (${BO.plural(a.days, 'day')} with values)` : ''}</span></div></div>`
                  : '<div class="set-row"><div><b>No values yet</b><span class="sub">Add your foods’ labels in Plan › Meals › My foods and the targets appear.</span></div></div>';
              })()}
            </section>

            <section class="set-card ${firstPaint ? 'rise' : ''}" style="--i:3" aria-labelledby="h-look">
              <h3 id="h-look">Appearance</h3>
              <div class="set-row"><div><b>Theme</b><span class="sub">Device follows your phone or computer.</span></div>
                <div class="seg" role="group" aria-label="Theme">
                  <button type="button" data-theme-set="light" aria-pressed="${theme === 'light'}">Light</button>
                  <button type="button" data-theme-set="dark" aria-pressed="${theme === 'dark'}">Dark</button>
                  <button type="button" data-theme-set="system" aria-pressed="${theme === 'system'}">Device</button>
                </div></div>
            </section>
          </div>
          <div class="stack">
            ${remindersCard()}
            <section class="set-card ${firstPaint ? 'rise' : ''}" style="--i:4" aria-labelledby="h-watch">
              <h3 id="h-watch">Apple Watch &amp; Health</h3>
              ${BO.watchRow(watch, weights)}
            </section>
            <section class="set-card ${firstPaint ? 'rise' : ''}" style="--i:4" aria-labelledby="h-data">
              <h3 id="h-data">Your data</h3>
              <div class="set-row"><div><b>Export backup</b><span class="sub">Downloads your plan, days, weigh-ins, foods and Watch workouts as JSON.</span></div><button type="button" class="btn" id="export">Export</button></div>
              <div class="set-row"><div><b>Import backup</b><span class="sub">Milo (BodyOps) or old CorpoOps file. Replaces your data, with Undo.</span></div>
                <label class="btn" style="cursor:pointer">Import<input type="file" id="importFile" accept="application/json,.json" hidden></label></div>
              <div class="set-row"><div><b>Reset this week</b><span class="sub">Clears this week's ticks, sets and sessions.</span></div><button type="button" class="btn danger" id="resetWeek">Reset</button></div>
            </section>

            ${BO.me.isAdmin ? `<section class="set-card ${firstPaint ? 'rise' : ''}" style="--i:5" aria-labelledby="h-users">
              <h3 id="h-users">Users</h3>
              ${userRows || '<div class="set-row"><span class="sub">Nobody else has an account yet.</span></div>'}
              <div class="set-row"><span class="sub">${BO.health && BO.health.registration ? 'Anyone who can open this site can create an account. Set MILO_ALLOW_REGISTRATION to false in .env to stop that.' : 'New accounts are turned off on this server.'}</span></div>
            </section>` : ''}
          </div>
        </div>
        <p class="whisper" data-milo style="padding-top:28px">${BO.MILO}</p>`;
      firstPaint = false;
    }

    root.addEventListener('click', async event => {
      const t = event.target;
      if (t.closest('#signOut')) {
        try { await BO.api('POST', '/api/auth/logout'); } catch (e) { }
        BO.me = null;
        BO.go('/', { replace: true });
        return;
      }
      const panelBtn = t.closest('[data-panel]');
      if (panelBtn) { panel = panel === panelBtn.dataset.panel ? null : panelBtn.dataset.panel; render(); return; }

      if (t.closest('[data-watch-setup]')) { BO.watchSetup({ onChange: w => { watch = w; render(); } }); return; }
      if (t.closest('[data-push-on]')) { await push.enable(); render(); return; }
      if (t.closest('[data-push-off]')) { await push.disable(); render(); return; }
      if (t.closest('[data-push-test]')) { await push.test(); render(); return; }
      const removeDev = t.closest('[data-push-remove]');
      if (removeDev) { await push.remove(Number(removeDev.dataset.pushRemove)); render(); return; }
      const remind = t.closest('[data-remind]');
      if (remind) {
        const key = remind.dataset.remind;
        const reminders = Object.assign({}, prefs.reminders, { [key]: !(prefs.reminders && prefs.reminders[key] !== false) });
        try { prefs = await BO.saveSettings({ reminders }); render(); } catch (e) { }
        return;
      }

      const everyBtn = t.closest('[data-weigh-every]');
      if (everyBtn) {
        try { prefs = await BO.saveSettings({ weighEveryDays: Number(everyBtn.dataset.weighEvery) }); render(); } catch (e) { }
        return;
      }
      const restBtn = t.closest('[data-rest]');
      if (restBtn) {
        try { prefs = await BO.saveSettings({ restSeconds: Number(restBtn.dataset.rest) }); render(); } catch (e) { }
        return;
      }

      const themeBtn = t.closest('[data-theme-set]');
      if (themeBtn) { BO.setTheme(themeBtn.dataset.themeSet); render(); return; }

      if (t.closest('#export')) {
        try {
          const backup = await BO.api('GET', '/api/backup');
          BO.download(`milo-backup-${BO.localToday()}.json`, JSON.stringify(backup, null, 2));
        } catch (e) { BO.toast(e.message, { error: true }); }
        return;
      }

      if (t.closest('#resetWeek')) {
        const monday = BO.monday(BO.localToday());
        try {
          const removed = await BO.saving(BO.api('DELETE', `/api/days/week/${monday}`));
          BO.toast(removed.length ? `Week of ${BO.weekRange(monday)} reset` : 'Nothing was logged this week', removed.length ? {
            action: 'Undo', onAction: () => BO.saving(BO.api('POST', `/api/days/week/${monday}/restore`, removed)).catch(() => { }),
          } : {});
        } catch (e) { }
        return;
      }

      const reset = t.closest('[data-user-reset]');
      if (reset) {
        try {
          const result = await BO.saving(BO.api('POST', `/api/admin/users/${reset.dataset.userReset}/reset-password`));
          tempPasswords[reset.dataset.userReset] = result.temporaryPassword;
          render();
        } catch (e) { }
        return;
      }
      const ask = t.closest('[data-user-confirm]');
      if (ask) { confirmUser = Number(ask.dataset.userConfirm); render(); return; }
      if (t.closest('[data-user-cancel]')) { confirmUser = null; render(); return; }
      const del = t.closest('[data-user-delete]');
      if (del) {
        try {
          await BO.saving(BO.api('DELETE', `/api/admin/users/${del.dataset.userDelete}`));
          users = await BO.api('GET', '/api/admin/users');
          confirmUser = null;
          render();
        } catch (e) { }
      }
    });

    root.addEventListener('change', async event => {
      const rtime = event.target.closest('[data-rtime]');
      if (rtime) {
        if (!/^\d{2}:\d{2}$/.test(rtime.value)) return;
        try { prefs = await BO.saveSettings({ reminders: Object.assign({}, prefs.reminders, { [rtime.dataset.rtime]: rtime.value }) }); } catch (e) { render(); }
        return;
      }
      if (event.target.id !== 'importFile' || !event.target.files.length) return;
      const file = event.target.files[0];
      let text;
      try { text = await file.text(); JSON.parse(text); } catch (e) { BO.toast('That file is not JSON. Pick a Milo or CorpoOps backup.', { error: true }); return; }
      try {
        const before = await BO.api('GET', '/api/backup');
        const result = await BO.saving(BO.api('POST', '/api/backup', text));
        BO.toast(`Imported ${BO.plural(result.exercises, 'exercise')}, ${BO.plural(result.meals, 'meal')}, ${BO.plural(result.days, 'day')} and ${BO.plural(result.weights, 'weigh-in')}`, {
          action: 'Undo',
          onAction: () => BO.saving(BO.api('POST', '/api/backup', before)).then(() => BO.toast('Back to how it was')).catch(() => { }),
          duration: 10000,
        });
      } catch (e) { }
      event.target.value = '';
    });

    root.addEventListener('submit', async event => {
      event.preventDefault();
      if (event.target.id === 'passwordForm') {
        try {
          await BO.saving(BO.api('POST', '/api/auth/password', { currentPassword: root.querySelector('#curPass').value, newPassword: root.querySelector('#newPass').value }));
          panel = null;
          render();
          BO.toast('Password changed. Your other devices are signed out.');
        } catch (e) { }
      } else if (event.target.id === 'deleteForm') {
        try {
          await BO.saving(BO.api('POST', '/api/auth/delete-account', { password: root.querySelector('#delPass').value }));
          BO.me = null;
          BO.go('/', { replace: true });
          BO.toast('Your account was deleted.');
        } catch (e) { }
      }
    });

    render();
  };
})();
