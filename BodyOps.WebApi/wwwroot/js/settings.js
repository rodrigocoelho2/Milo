/* BodyOps — Settings: account, training (rest timer), appearance, backups, and (for the admin) the other accounts. */
'use strict';

(() => {
  const { esc } = BO;

  BO.screens.settings = async function settings(root) {
    let users = BO.me.isAdmin ? await BO.api('GET', '/api/admin/users') : [];
    let prefs = await BO.settings(true);
    let firstPaint = true;
    let panel = null;          // 'password' | 'delete' while one of those forms is open
    let confirmUser = null;    // user id waiting for "Delete" confirmation
    const tempPasswords = {};  // user id → temporary password just set

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
              <div class="set-row"><div><b>Weight goal</b><span class="sub">${prefs.weightGoalKg != null ? `${BO.kg(prefs.weightGoalKg)} kg` : 'None set'} · shown on Progress.</span></div><a class="btn" href="/progress" data-link>Edit</a></div>
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
            <section class="set-card ${firstPaint ? 'rise' : ''}" style="--i:4" aria-labelledby="h-data">
              <h3 id="h-data">Your data</h3>
              <div class="set-row"><div><b>Export backup</b><span class="sub">Downloads your plan, days and weigh-ins as JSON.</span></div><button type="button" class="btn" id="export">Export</button></div>
              <div class="set-row"><div><b>Import backup</b><span class="sub">Milo (BodyOps) or old CorpoOps file. Replaces your data, with Undo.</span></div>
                <label class="btn" style="cursor:pointer">Import<input type="file" id="importFile" accept="application/json,.json" hidden></label></div>
              <div class="set-row"><div><b>Reset this week</b><span class="sub">Clears this week's ticks, sets and sessions.</span></div><button type="button" class="btn danger" id="resetWeek">Reset</button></div>
            </section>

            ${BO.me.isAdmin ? `<section class="set-card ${firstPaint ? 'rise' : ''}" style="--i:5" aria-labelledby="h-users">
              <h3 id="h-users">Users</h3>
              ${userRows || '<div class="set-row"><span class="sub">Nobody else has an account yet.</span></div>'}
              <div class="set-row"><span class="sub">${BO.health && BO.health.registration ? 'Anyone who can open this site can create an account. Set BodyOps__AllowRegistration (BODYOPS_ALLOW_REGISTRATION) to false to stop that.' : 'New accounts are turned off on this server.'}</span></div>
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
