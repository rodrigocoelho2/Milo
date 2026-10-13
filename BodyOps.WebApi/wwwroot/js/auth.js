/* BodyOps — the landing page and the sign-in / create-account form (one form, two tabs). */
'use strict';

(() => {
  const { esc, icons } = BO;
  const WEIGHTS = [100.4, 100.1, 99.6, 99.8, 99.1, 98.9, 98.6, 98.7, 98.2, 98.0, 97.9, 97.6];

  /** Adds .in to .reveal elements as they scroll into view. */
  function revealOnScroll(root) {
    const items = root.querySelectorAll('.reveal');
    if (!('IntersectionObserver' in window)) { items.forEach(el => el.classList.add('in')); return; }
    const io = new IntersectionObserver(entries => entries.forEach(entry => {
      if (entry.isIntersecting) { entry.target.classList.add('in'); io.unobserve(entry.target); }
    }), { threshold: 0.15, rootMargin: '0px 0px -40px 0px' });
    items.forEach(el => io.observe(el));
  }

  /* ---------------- landing ---------------- */

  const STEPS = [
    { k: 'var(--weigh)', ic: 'scale', tm: '07:30', b: 'Weigh-in', s: '97.6 kg · −0.2 vs Sunday', pts: 15 },
    { k: 'var(--eat)', ic: 'bowl', tm: '08:00', b: 'Breakfast', s: 'Whey · Rice flour · Banana', pts: 10 },
    { k: 'var(--eat)', ic: 'bowl', tm: '13:00', b: 'Lunch', s: 'Chicken · Pasta / rice', pts: 10 },
    { k: 'var(--train)', ic: 'plan', tm: '18:20', b: 'Upper body', s: '6 exercises · 18 sets', pts: 45 },
    { k: 'var(--eat)', ic: 'bowl', tm: '20:30', b: 'Dinner', s: 'Chicken · Egg · Chocolate', pts: 10 },
    { k: 'var(--eat)', ic: 'moon', tm: '23:00', b: 'Supper', s: 'Greek yoghurt · Corn flakes', pts: 10 },
  ];
  const TICKER = [['Leg press 140 kg', 'var(--train)'], ['Incline chest press 102.5 kg · PR', 'var(--gold)'], ['6-day streak', 'var(--eat)'], ['97.6 kg · −2.8 since July', 'var(--weigh)'], ['Lat pulldown 3 × 12', 'var(--train)'], ['5 / 5 meals', 'var(--eat)'], ['Day score 100', 'var(--good)']];

  BO.screens.landing = function landing(root) {
    const canRegister = !BO.health || BO.health.registration;
    const tick = TICKER.map(([t, c]) => `<span><i style="background:${c}"></i>${t}</span>`).join('');
    const create = (label, cls = 'white') => canRegister ? `<a class="btn ${cls}" href="/signup" data-link>${label} ${icons.arrow}</a>` : '';
    root.innerHTML = `<div class="lp">
      <section class="lp-hero on-brand">
        <span class="ring-deco" style="width:520px;height:520px;right:-140px;top:-180px"></span>
        <span class="ring-deco" style="width:300px;height:300px;left:-120px;bottom:60px"></span>
        <nav class="lp-nav">${BO.brandHTML()}
          <div class="links"><a href="#how" data-jump="how">How it works</a><a href="#features" data-jump="features">Features</a><a href="#yours" data-jump="yours">Your data</a></div>
          <div class="acts"><a class="btn ghost-w" href="/signin" data-link style="padding:10px 15px">Sign in</a>${canRegister ? '<a class="btn white" href="/signup" data-link style="padding:10px 15px">Create account</a>' : ''}</div>
        </nav>
        <div class="lp-main">
          <div class="lp-copy">
            <span class="chip lp-eyebrow" style="--i:0"><span style="color:#FFC53D;display:inline-flex">${icons.flame}</span>Training, meals and weight in one place</span>
            <h1 class="lp-h1" style="--i:1"><span>Show up.</span><span>Log it.</span><span class="gold">Beat it.</span></h1>
            <p class="lp-lede" style="--i:2">Milo turns your plan into a day you can tick off: every set, every meal, every weigh-in. Then it shows you how to beat last week.</p>
            <div class="lp-cta" style="--i:3">${create('Create your account')}<a class="btn ghost-w" href="/signin" data-link>Sign in</a></div>
            <div class="lp-proof" style="--i:4"><span class="dots"><i style="background:#8A5CF6">W</i><i style="background:#FF7A2F">M</i><i style="background:#0D1015">S</i></span>One weigh-in, five meals, eighteen sets: that's a 100 day.</div>
          </div>
          <div class="lp-art" aria-hidden="true">
            <div class="lp-phone"><div class="lp-screen">
              <div><div class="hi">Good evening, Rodas</div><h4>Tuesday 13</h4></div>
              <div class="mini-score"><b class="num" data-count="64">0<small>/100</small></b>
                <div class="mini-bars"><div>Training<span><i data-w="28%" style="background:#3355FF"></i></span></div><div>Meals<span><i data-w="80%" style="background:#FF7A2F"></i></span></div><div>Weigh-in<span><i data-w="100%" style="background:#8A5CF6"></i></span></div></div></div>
              <div class="mini-hero"><small>Up next · Upper body</small><b>Back to it</b><div class="pg">${'<i class="on"></i>'.repeat(5)}${'<i></i>'.repeat(13)}</div></div>
              <div class="mini-tl">
                <div><em>07:30</em><i style="background:#8A5CF6">${icons.check}</i><b>Weigh-in · 97.6 kg</b></div>
                <div><em>08:00</em><i style="background:#FF7A2F">${icons.check}</i><b>Breakfast</b></div>
                <div><em>13:00</em><i style="background:#FF7A2F">${icons.check}</i><b>Lunch</b></div>
                <div><em>18:20</em><i style="background:#3355FF"></i><b>Upper body · live</b></div>
              </div>
            </div></div>
            <div class="float f-streak">${icons.flame}6-day streak</div>
            <div class="float f-pr"><div class="md">${icons.medal}</div><small>New personal record</small><b class="num">102.5<span> kg</span></b><small>Incline chest press</small></div>
            <div class="float f-rest"><small>Rest</small><span data-rest>${BO.ring(0.68, '1:02')}</span><small>then set 3</small></div>
            <div class="float f-weigh"><span class="tg">Body weight</span><b class="num">97.6<span> kg</span></b>${BO.sparkline(WEIGHTS, '#8A5CF6', { width: 200, height: 44, fill: true })}</div>
          </div>
        </div>
      </section>
      <div class="ticker" aria-hidden="true"><div class="track">${tick}${tick}</div></div>

      <section class="lp-sec" id="how">
        <div class="reveal"><div class="kick">How it works</div><h2 class="lp-h2">A day with Milo.</h2><p class="lede">Your plan becomes today's list. Tick things off as you live them and watch the day score climb. Tap the cards.</p></div>
        <div class="day-demo reveal">
          <div class="day-top"><div class="sc num" data-score>0<small>/100</small></div>
            <div class="meter"><div class="lbl"><span>Day score</span><span data-left>6 things to do</span></div><div class="t">${STEPS.map((s, i) => `<i data-seg="${i}" style="background:${s.k}"></i>`).join('')}</div></div></div>
          <div class="steps">${STEPS.map((s, i) => `<button type="button" class="step" style="--k:${s.k};--n:${i}" data-step="${i}" aria-pressed="false"><span class="tk">${icons.check}</span><span class="tm num">${s.tm}</span><span class="ic">${icons[s.ic]}</span><b>${s.b}</b><small>${s.s}</small></button>`).join('')}</div>
        </div>
      </section>

      <section class="lp-sec" id="features">
        <div class="reveal"><div class="kick">Features</div><h2 class="lp-h2">Built for the gym floor, not the spreadsheet.</h2></div>
        <div class="bento">
          <div class="btile t-sess reveal"><h3>Session mode</h3><p>One exercise at a time with big buttons, last time's numbers to beat and a rest timer that starts by itself.</p>
            <div class="sess-demo"><div class="s"><small>Set 1</small><b class="num">12</b><small>79 kg</small></div><div class="s"><small>Set 2</small><b class="num">12</b><small>79 kg</small></div><div class="s now"><small>Set 3 · now</small><b class="num">12</b><small>79 kg</small></div>
              <div class="rst"><span data-rest>${BO.ring(0.68, '1:02', { size: 62 })}</span><div><b>Rest</b><small>1:30 planned</small></div></div></div></div>
          <div class="btile t-pr reveal" style="--i:1"><span class="pr-badge">${icons.medal} Personal record</span><h3>Records that feel like records.</h3><div class="pr-num num">102.5<span> kg</span></div><p>+2.5 kg on your best incline chest press.</p></div>
          <div class="btile t-meal reveal"><h3>Meals in one tap</h3><p>Your diet plan, with times. Tick each meal as you eat it.</p>
            <div class="dmeals"><div class="on"><i>${icons.check}</i>Breakfast<em>08:00</em></div><div class="on"><i>${icons.check}</i>Lunch<em>13:00</em></div><div class="on"><i>${icons.check}</i>Snack<em>17:00</em></div><div><i></i>Dinner<em>20:30</em></div></div></div>
          <div class="btile t-weigh reveal" style="--i:1"><h3>Weight against your goal</h3><p>Weigh in, see the trend and when you'll get there.</p>
            <div class="dwgt"><b class="num">97.6<span> kg</span></b>${BO.sparkline(WEIGHTS, 'var(--weigh)', { width: 300, height: 70, fill: true })}</div></div>
          <div class="btile t-week reveal" style="--i:2"><h3>The week at a glance</h3><p>Training, meals and weigh-ins, day by day, with a recap on Sunday.</p>
            <div class="wk-bars">${[[34, 30, 30], [30, 28, 26], [26, 24, 0], [34, 30, 26], [30, 26, 26], [22, 26, 0], [0, 18, 0]].map((d, i) => `<div><span>${'MTWTFSS'[i]}</span>${d[2] ? `<i style="height:${d[2]}%;background:var(--weigh);animation-delay:${i * 60}ms"></i>` : ''}<i style="height:${d[1]}%;background:var(--eat);animation-delay:${i * 60}ms"></i>${d[0] ? `<i style="height:${d[0]}%;background:var(--train);animation-delay:${i * 60}ms"></i>` : ''}</div>`).join('')}</div></div>
          <div class="btile t-own reveal" id="yours">
            <div><h3>Yours, end to end.</h3></div>
            <div class="own"><span class="ic" style="background:var(--train-soft);color:var(--train)">${icons.server}</span><div><b>Your server</b><span>Runs on your own box. Your data stays there.</span></div></div>
            <div class="own"><span class="ic" style="background:var(--eat-soft);color:var(--eat)">${icons.phone}</span><div><b>Phone and computer</b><span>Log at the gym, plan at home. Always in sync.</span></div></div>
            <div class="own"><span class="ic" style="background:var(--weigh-soft);color:var(--weigh)">${icons.people}</span><div><b>One plan each</b><span>Every account has its own plan and history.</span></div></div>
          </div>
        </div>
      </section>

      <div class="lp-final-wrap"><section class="lp-final reveal">
        <div><h2>Your week starts Monday.</h2><p>Make an account, build your plan once, and let every day after that be a tick.</p></div>
        <div class="acts">${create('Create your account')}<a class="btn ghost-w" href="/signin" data-link>${canRegister ? 'I already have one' : 'Sign in'}</a></div>
      </section></div>
      <footer class="lp-foot"><span>Milo · self-hosted</span><span>Plan the week. Log every set. Watch the scale.</span></footer>
      <p class="whisper" data-milo>${BO.MILO}</p>
    </div>`;

    // The hero phone: score counts up, bars fill.
    BO.countUp(root.querySelector('[data-count]'), 64, { suffix: '<small>/100</small>', duration: 1400 });
    setTimeout(() => root.querySelectorAll('[data-w]').forEach(i => { i.style.width = i.dataset.w; }), 500);

    // The rest timers tick down.
    let rest = 62;
    const timer = setInterval(() => {
      if (!root.querySelector('.lp')) { clearInterval(timer); return; }
      rest = rest <= 0 ? 90 : rest - 1;
      root.querySelectorAll('[data-rest]').forEach(el => {
        const size = el.querySelector('svg').getAttribute('width');
        el.innerHTML = BO.ring(rest / 90, BO.clock(rest), { size: Number(size) });
      });
    }, 1000);

    // A day with Milo: tap to tick.
    const on = new Set([0, 1, 2]);
    const scoreEl = root.querySelector('[data-score]');
    const left = root.querySelector('[data-left]');
    let shown = 0;
    const paint = celebrate => {
      const total = [...on].reduce((a, i) => a + STEPS[i].pts, 0);
      root.querySelectorAll('[data-step]').forEach(b => { const i = Number(b.dataset.step); b.classList.toggle('on', on.has(i)); b.setAttribute('aria-pressed', String(on.has(i))); });
      root.querySelectorAll('[data-seg]').forEach(seg => { seg.style.width = on.has(Number(seg.dataset.seg)) ? STEPS[Number(seg.dataset.seg)].pts + '%' : '0'; });
      const rem = STEPS.length - on.size;
      left.textContent = rem ? `${rem} ${rem === 1 ? 'thing' : 'things'} to do` : 'Day complete';
      BO.countUp(scoreEl, total, { from: shown, suffix: '<small>/100</small>', duration: 400 });
      shown = total;
      if (celebrate && total === 100) BO.confetti();
    };
    paint(false);

    root.addEventListener('click', event => {
      const step = event.target.closest('[data-step]');
      if (step) {
        const i = Number(step.dataset.step);
        on.has(i) ? on.delete(i) : on.add(i);
        paint(true);
        return;
      }
      const jump = event.target.closest('[data-jump]');
      if (jump) {
        event.preventDefault();
        const target = root.querySelector('#' + jump.dataset.jump);
        if (target) target.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
    });
    revealOnScroll(root);
    parallax(root);
  };

  /** Depth in the hero while scrolling: the copy drifts and fades, the phone lags, the cards float up at their own speeds. */
  function parallax(root) {
    const hero = root.querySelector('.lp-hero');
    const copy = root.querySelector('.lp-copy');
    const phone = root.querySelector('.lp-phone');
    const floats = [...root.querySelectorAll('.float')];
    const rings = [...root.querySelectorAll('.ring-deco')];
    const speeds = [0.30, 0.18, 0.24, 0.12];
    let last = -1;
    BO.onScrollFrame(y => {
      if (!hero.isConnected) return false;
      const h = hero.offsetHeight;
      const p = Math.max(0, Math.min(y, h));
      if (p === last) return true;
      last = p;
      // On a phone the cards sit right under the buttons, so everything only drifts a little.
      const k = window.innerWidth < 760 ? 0.3 : 1;
      copy.style.transform = `translate3d(0, ${(p * 0.18 * k).toFixed(1)}px, 0)`;
      copy.style.opacity = String(Math.max(0, 1 - p / (h * 0.85)).toFixed(3));
      if (phone) phone.style.translate = `0 ${(p * 0.08).toFixed(1)}px`;
      floats.forEach((el, i) => { el.style.transform = `translate3d(0, ${(-p * speeds[i % speeds.length] * k).toFixed(1)}px, 0)`; });
      rings.forEach((el, i) => { el.style.transform = `translate3d(0, ${(p * (i ? 0.12 : 0.32)).toFixed(1)}px, 0) scale(${(1 + p / h * 0.15).toFixed(3)})`; });
      return true;
    });
  }

  /* ---------------- sign in / create account ---------------- */

  BO.screens.auth = function auth(root, { mode }) {
    const health = BO.health || { registration: true, firstRun: false };
    const signup = mode === 'signup';
    if (signup && !health.registration) {
      BO.go('/signin', { replace: true });
      return;
    }

    const pips = ['M', 'T', 'W', 'T', 'F', 'S', 'S'];
    root.innerHTML = `<div class="au">
      <aside class="au-side on-brand">
        <span class="ring-deco" style="width:460px;height:460px;right:-170px;top:-150px;border-width:40px"></span>
        <span class="ring-deco" style="width:260px;height:260px;left:-110px;bottom:-60px;border-width:40px"></span>
        <a href="/" data-link style="text-decoration:none">${BO.brandHTML()}</a>
        <div><h2>${signup ? 'Start your <span class="gold">first streak.</span>' : 'Welcome back. <span class="gold">Keep it going.</span>'}</h2></div>
        <p>${signup ? 'Build your plan once: exercises, meals, a weight goal. Then every day is a list you tick off.' : 'Your plan for today is waiting: the session, the meals and a weigh-in.'}</p>
        <div class="phone-chips">${signup
          ? `<span class="chip w"><span class="flame">${icons.flame}</span>Day 1</span><span class="chip">Plan · Log · Beat it</span>`
          : `<span class="chip w"><span class="flame">${icons.flame}</span>Keep the streak</span><span class="chip">Today's plan is ready</span>`}</div>
        <div class="au-stack">${signup ? `
          <div class="au-card" style="--i:0"><span class="ic" style="background:#E5EAFF;color:#3355FF">${icons.plan}</span><div><b>Plan your week</b><span>Exercises with sets × reps for each day</span></div></div>
          <div class="au-card" style="--i:1"><span class="ic" style="background:#FFEBDD;color:#FF7A2F">${icons.bowl}</span><div><b>Add your meals</b><span>Foods, grams and the time you eat</span></div></div>
          <div class="au-card" style="--i:2"><span class="ic" style="background:#EFE8FF;color:#8A5CF6">${icons.target}</span><div><b>Set a weight goal</b><span>See the trend and when you'll get there</span></div></div>` : `
          <div class="au-card" style="--i:0"><span class="ic" style="background:#FFEBDD;color:#FF7A2F">${icons.flame}</span><div><b>Your streak</b><span>Score 60 or more today to keep it</span></div></div>
          <div class="au-card" style="--i:1"><span class="ic" style="background:#E5EAFF;color:#3355FF">${icons.plan}</span><div><b>Today's session</b><span>Last time's numbers, ready to beat</span></div></div>
          <div class="au-card" style="--i:2"><span class="ic" style="background:#FFF3D1;color:#B07A00">${icons.medal}</span><div><b>Your records</b><span>Every best load, and when you set it</span></div></div>`}
        </div>
        <div class="au-pips">${pips.map((d, i) => signup ? `<i>${d}</i>` : i < 6 ? `<i class="on">${icons.check}</i>` : `<i class="now">${d}</i>`).join('')}</div>
      </aside>
      <main class="au-main">
        <form class="au-form" id="authForm" novalidate>
          <div><h1>${signup ? 'Create your account' : 'Sign in'}</h1><p class="lead">${signup ? 'You start with an empty plan of your own.' : 'Good to see you again.'}</p></div>
          ${health.registration ? `<div class="seg" role="tablist"><a href="/signin" data-link role="tab" aria-selected="${!signup}">Sign in</a><a href="/signup" data-link role="tab" aria-selected="${signup}">Create account</a></div>` : ''}
          ${signup && health.firstRun ? `<div class="first-run">${icons.info}<span>This is a new server: the first account becomes its admin.</span></div>` : ''}
          <div class="form-err" id="authErr" role="alert" hidden></div>
          <label class="fld" id="userField">Username
            <span class="in">${icons.user}<input id="authUser" name="username" autocomplete="username" autocapitalize="off" spellcheck="false" required placeholder="${signup ? 'Pick a username' : ''}"></span>
            ${signup ? '<small>3–24 letters, numbers, dots, dashes or underscores.</small>' : ''}
          </label>
          <label class="fld" id="passField">Password
            <span class="in">${icons.lock}<input id="authPass" name="password" type="password" autocomplete="${signup ? 'new-password' : 'current-password'}" required placeholder="${signup ? 'At least 8 characters' : ''}"><button type="button" class="eye" data-eye aria-label="Show password">${icons.eye}</button></span>
            ${signup ? '<span class="strength" data-meter><i></i><i></i><i></i><i></i></span><small data-meter-lbl>At least 8 characters.</small>' : ''}
          </label>
          ${signup ? `<div class="row" style="align-items:flex-start;gap:10px">
              <label class="fld" style="flex:1"><span>Weight today <small>(optional)</small></span><span class="in">${icons.scale}<input id="authWeight" inputmode="decimal" placeholder="e.g. 97.6"><span class="unit-s">kg</span></span></label>
              <label class="fld" style="flex:1"><span>Goal <small>(optional)</small></span><span class="in">${icons.target}<input id="authGoal" inputmode="decimal" placeholder="e.g. 90"><span class="unit-s">kg</span></span></label>
            </div>
            <div class="goal-row" data-goals hidden>${['−5 kg', '−10 kg', 'Keep it'].map(g => `<button type="button" data-goal="${g}" aria-pressed="false">${g}</button>`).join('')}</div>` : ''}
          <label class="chk"><input type="checkbox" id="authRemember" checked><i>${icons.check}</i>Keep me signed in on this device</label>
          <button type="submit" class="btn primary" id="authSubmit">${signup ? 'Create account' : 'Sign in'} ${icons.arrow}</button>
          <p class="fine">${signup ? 'Your password is stored hashed, never in plain text.' : 'Forgot it? The server admin can reset it from Settings › Users.'}</p>
        </form>
      </main>
    </div>`;

    const form = root.querySelector('#authForm');
    const err = root.querySelector('#authErr');
    const pass = root.querySelector('#authPass');
    if (matchMedia('(min-width: 861px)').matches) root.querySelector('#authUser').focus();

    root.querySelector('[data-eye]').addEventListener('click', () => { pass.type = pass.type === 'password' ? 'text' : 'password'; });

    if (signup) {
      const meter = root.querySelector('[data-meter]');
      const lbl = root.querySelector('[data-meter-lbl]');
      const colors = ['var(--bad)', 'var(--eat)', 'var(--gold)', 'var(--good)'];
      const names = ['Too short', 'Weak', 'Good', 'Strong'];
      pass.addEventListener('input', () => {
        const v = pass.value;
        const s = v.length < 8 ? (v ? 0 : -1) : Math.min(3, 1 + (/\d/.test(v) ? 1 : 0) + (/[^A-Za-z0-9]/.test(v) || v.length > 13 ? 1 : 0));
        meter.querySelectorAll('i').forEach((i, k) => { i.style.background = k <= s ? colors[s] : ''; });
        lbl.textContent = s < 0 ? 'At least 8 characters.' : names[s];
      });
      const weight = root.querySelector('#authWeight');
      const goals = root.querySelector('[data-goals]');
      const goal = root.querySelector('#authGoal');
      weight.addEventListener('input', () => { goals.hidden = BO.readNumber(weight.value) == null; });
      goals.addEventListener('click', e => {
        const b = e.target.closest('[data-goal]');
        const w = BO.readNumber(weight.value);
        if (!b || w == null) return;
        goals.querySelectorAll('[data-goal]').forEach(x => x.setAttribute('aria-pressed', String(x === b)));
        goal.value = BO.kg(w + ({ '−5 kg': -5, '−10 kg': -10, 'Keep it': 0 })[b.dataset.goal]);
      });
    }

    form.addEventListener('submit', async event => {
      event.preventDefault();
      err.hidden = true;
      root.querySelectorAll('.fld.err').forEach(el => el.classList.remove('err'));
      const button = root.querySelector('#authSubmit');
      button.disabled = true;

      const body = {
        username: root.querySelector('#authUser').value.trim(),
        password: pass.value,
        remember: root.querySelector('#authRemember').checked,
      };
      try {
        BO.me = await BO.api('POST', signup ? '/api/auth/register' : '/api/auth/login', body);
        BO.cache = {};
        if (signup) {
          const goal = BO.readNumber(root.querySelector('#authGoal').value);
          const weight = BO.readNumber(root.querySelector('#authWeight').value);
          try {
            if (goal != null) await BO.api('PUT', '/api/settings', { weightGoalKg: goal });
            if (weight != null) await BO.api('PUT', `/api/weights/${BO.localToday()}`, { weightKg: weight });
          } catch (e) { BO.toast(e.message, { error: true }); }
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
          let small = label.querySelector('small:not([data-meter-lbl])') || label.querySelector('small');
          if (!small) { small = document.createElement('small'); label.appendChild(small); }
          small.textContent = e.message;
        } else if (e.status === 401 || e.status === 400) {
          root.querySelector('#passField').classList.add('err');
          let small = root.querySelector('#passField small');
          if (!small) { small = document.createElement('small'); root.querySelector('#passField').appendChild(small); }
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
