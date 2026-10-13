/* BodyOps — the landing page and the sign-in / create-account form (one form, two tabs). */
'use strict';

(() => {
  const { esc, icons } = BO;
  const HEADLINE = 'Plan the week. Log every set. Watch the scale.';

  // The illustration on the landing page: what a logged exercise, a weigh-in and the meal list look like.
  const sampleExercise = `<article class="ex a1" aria-hidden="true">
      <div class="ex-head"><div><div class="ex-name">Incline chest press <span class="pr">PR</span></div><div class="ex-meta">Last: 100 kg · 3×10 · 21 Jul</div></div><span class="plan-tag">3 × 12</span></div>
      <div class="ex-log"><div class="load"><button type="button" tabindex="-1">−</button><input class="num" value="102.5" readonly tabindex="-1"><small>kg</small><button type="button" tabindex="-1">+</button></div>
        <div class="sets"><span class="set filled"><b>12</b><small>set 1</small></span><span class="set filled"><b>11</b><small>set 2</small></span><span class="set filled"><b>10</b><small>set 3</small></span></div></div>
    </article>`;

  BO.screens.landing = function landing(root) {
    const canRegister = !BO.health || BO.health.registration;
    root.innerHTML = `<div class="land">
      <header class="land-nav">
        ${BO.brandHTML()}
        <div class="row"><a class="btn btn-ghost" href="/signin" data-link>Sign in</a>${canRegister ? '<a class="btn btn-primary" href="/signup" data-link>Create account</a>' : ''}</div>
      </header>
      <section class="land-hero">
        <div>
          <h1 class="hero-title">${HEADLINE}</h1>
          <p class="hero-lede">BodyOps keeps your training plan, your meals and your weigh-ins in one place. Each account has its own plan, and it syncs between your phone at the gym and your computer at home.</p>
          <div class="hero-cta">
            ${canRegister ? '<a class="btn btn-primary btn-lg" href="/signup" data-link>Create your account</a>' : ''}
            <a class="btn btn-lg" href="/signin" data-link>${canRegister ? 'I already have one' : 'Sign in'}</a>
          </div>
        </div>
        <div class="hero-art">
          ${sampleExercise}
          <div class="wcard a2" aria-hidden="true">
            <span class="sub">Weight</span>
            <span class="big num">98.0<span style="font-size:22px;color:var(--mute)"> kg</span></span>
            <span class="sub">−2.4 kg since 14 Jul · goal 90</span>
            <div class="trip-bar" style="margin-top:6px"><i style="width:23%"></i></div>
          </div>
          <div class="meals a3" aria-hidden="true">
            <span class="meal" aria-checked="true"><span class="tick">${icons.check}</span><span><span class="meal-name">Breakfast</span><span class="meal-foods">Whey 25 g · Rice flour 50 g · Banana 1 pc</span></span></span>
            <span class="meal" aria-checked="false"><span class="tick">${icons.check}</span><span><span class="meal-name">Lunch</span><span class="meal-foods">Chicken breast 200 g · Pasta / rice 150 g</span></span></span>
          </div>
        </div>
      </section>
      <section class="land-feats">
        <div class="feat"><h3>Your plan, day by day</h3><p>Set exercises with sets × reps and meals with grams for each weekday. Copy a day to the others in one go.</p></div>
        <div class="feat"><h3>Log it at the gym</h3><p>Tap each set as you finish it. Last session's load is on every exercise, and a new personal record gets flagged.</p></div>
        <div class="feat"><h3>See how the week went</h3><p>Meals eaten, days trained, volume lifted, muscles worked and your weight against your goal.</p></div>
      </section>
      <footer class="land-foot"><span>Runs on your own server. Your data stays there.</span><span>BodyOps</span></footer>
    </div>`;
  };

  BO.screens.auth = function auth(root, { mode }) {
    const health = BO.health || { registration: true, firstRun: false };
    const signup = mode === 'signup';
    if (signup && !health.registration) {
      BO.go('/signin', { replace: true });
      return;
    }

    root.innerHTML = `<div class="auth">
      <aside class="auth-side">
        ${BO.brandHTML()}
        <h1 class="hero-title">${HEADLINE}</h1>
        <div class="today-sum" style="max-width:420px" aria-hidden="true">
          <div class="sum"><div class="ring" style="--p:60"><span>3/5</span></div><div><b>Meals</b><span>today</span></div></div>
          <div class="sum"><div class="ring" style="--p:100"><span>18</span></div><div><b>Sets</b><span>all logged</span></div></div>
        </div>
      </aside>
      <main class="auth-main">
        <form class="auth-card" id="authForm" novalidate>
          <a class="phone-brand" href="/" data-link style="text-decoration:none;color:inherit">${BO.brandHTML()}</a>
          <div>
            <h1 class="title-xl">${signup ? 'Create your account' : 'Welcome back'}</h1>
            <span class="sub">${signup ? 'You start with an empty plan of your own.' : 'Sign in to see your plan.'}</span>
          </div>
          ${health.registration ? `<div class="auth-tabs" role="tablist">
            <a href="/signin" data-link role="tab" aria-selected="${!signup}">Sign in</a>
            <a href="/signup" data-link role="tab" aria-selected="${signup}">Create account</a>
          </div>` : ''}
          <div class="form-err" id="authErr" role="alert" hidden></div>
          <label class="lbl-f" id="userField">Username
            <input class="field" id="authUser" name="username" autocomplete="username" autocapitalize="off" spellcheck="false" required>
            ${signup ? '<small>3–24 letters, numbers, dots, dashes or underscores.</small>' : ''}
          </label>
          <label class="lbl-f" id="passField">Password
            <input class="field" id="authPass" name="password" type="password" autocomplete="${signup ? 'new-password' : 'current-password'}" required>
            ${signup ? '<small>At least 8 characters.</small>' : ''}
          </label>
          ${signup ? `<label class="lbl-f"><span>Weight goal <span class="sub" style="font-weight:500">(optional)</span></span>
            <input class="field" id="authGoal" inputmode="decimal" placeholder="kg"></label>` : ''}
          <label class="check"><input type="checkbox" id="authRemember" checked>Keep me signed in on this device</label>
          <button type="submit" class="btn btn-primary" id="authSubmit">${signup ? 'Create account' : 'Sign in'}</button>
          <p class="fine">${signup
            ? (health.firstRun ? 'This is a new server: the first account becomes its admin.' : 'The admin can reset your password if you forget it.')
            : 'Forgot your password? Ask the server admin to reset it from Settings › Users.'}</p>
        </form>
      </main>
    </div>`;

    const form = root.querySelector('#authForm');
    const err = root.querySelector('#authErr');
    root.querySelector('#authUser').focus();

    form.addEventListener('submit', async event => {
      event.preventDefault();
      err.hidden = true;
      root.querySelectorAll('.lbl-f.err').forEach(el => el.classList.remove('err'));
      const button = root.querySelector('#authSubmit');
      button.disabled = true;

      const body = {
        username: root.querySelector('#authUser').value.trim(),
        password: root.querySelector('#authPass').value,
        remember: root.querySelector('#authRemember').checked,
      };
      try {
        BO.me = await BO.api('POST', signup ? '/api/auth/register' : '/api/auth/login', body);
        if (signup) {
          const goal = BO.readNumber(root.querySelector('#authGoal').value);
          if (goal != null) {
            try { await BO.api('PUT', '/api/settings', { weightGoalKg: goal }); } catch (e) { BO.toast(e.message, { error: true }); }
          }
          BO.health = Object.assign({}, BO.health, { firstRun: false });
        }
        BO.go(signup ? '/plan' : '/today', { replace: true });
        if (signup) BO.toast('Account created. Start by planning your week.');
      } catch (e) {
        const name = e.field || (e.status === 409 ? 'username' : null);
        const field = name === 'username' ? '#userField' : name === 'password' ? '#passField' : null;
        if (field) {
          const label = root.querySelector(field);
          label.classList.add('err');
          let small = label.querySelector('small');
          if (!small) { small = document.createElement('small'); label.appendChild(small); }
          small.textContent = e.message;
        } else {
          err.textContent = e.message;
          err.hidden = false;
        }
        button.disabled = false;
      }
    });
  };
})();
