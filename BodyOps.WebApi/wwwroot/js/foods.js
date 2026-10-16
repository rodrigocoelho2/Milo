/* BodyOps — foods: the account's food labels, calories and macros of the plan's foods, the food search (your foods,
   Milo's list, Open Food Facts), the label editor and the barcode scanner. Used by Plan, Today and Settings. */
'use strict';

(() => {
  const { esc, icons } = BO;

  /** The key a food is matched by (same as the server): lower case, no accents, spaces collapsed. */
  BO.foodKey = name => String(name || '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/\s+/g, ' ').trim();

  /** The account's foods, cached: { list, byKey }. */
  BO.loadFoods = async function loadFoods(fresh) {
    if (fresh || !BO.cache.foods) {
      const list = await BO.api('GET', '/api/foods');
      BO.cache.foods = { list, byKey: new Map(list.map(f => [BO.foodKey(f.name), f])) };
    }
    return BO.cache.foods;
  };
  BO.forgetFoods = () => { BO.cache.foods = null; };

  /** The values of an amount of a food (mirrors FoodMath.For on the server); null when they can't be worked out. */
  BO.macrosFor = function macrosFor(food, amount, unit) {
    if (!food || (food.kcal == null && food.proteinG == null) || amount == null) return null;
    let f = null;
    if (food.basis === 'perPiece') f = unit === 'pieces' ? amount : food.pieceGrams > 0 ? amount / food.pieceGrams : null;
    else f = unit === 'grams' ? amount / 100 : food.pieceGrams ? amount * food.pieceGrams / 100 : null;
    if (f == null) return null;
    const s = v => v == null ? null : v * f;
    return { kcal: s(food.kcal), proteinG: s(food.proteinG), carbsG: s(food.carbsG), fatG: s(food.fatG) };
  };

  /** Adds up values; unknown items are counted. */
  BO.sumMacros = function sumMacros(items) {
    const t = { kcal: null, proteinG: null, carbsG: null, fatG: null, unknown: 0 };
    for (const m of items) {
      if (!m) { t.unknown++; continue; }
      for (const k of ['kcal', 'proteinG', 'carbsG', 'fatG']) if (m[k] != null) t[k] = (t[k] || 0) + m[k];
      if (m.unknown) t.unknown += m.unknown;
    }
    return t;
  };
  BO.macrosKnown = m => !!m && (m.kcal != null || m.proteinG != null);

  /** A planned meal's values from its foods (plan editor shape: foods with name, amount, unit). */
  BO.mealMacros = (meal, foods) => BO.sumMacros(meal.foods.filter(f => f.name.trim()).map(f => BO.macrosFor(foods.byKey.get(BO.foodKey(f.name)), f.amount, f.unit)));

  const round = v => Math.round(v);
  const grams = v => v >= 10 ? Math.round(v) : Math.round(v * 10) / 10;
  BO.kcalText = v => v == null ? '–' : round(v).toLocaleString('en-GB');
  BO.gramsText = v => v == null ? '–' : `${grams(v)} g`;

  /** Chips for a meal or food: "520 kcal" "36 g protein" (carbs and fat with { full: true }). */
  BO.macroChips = function macroChips(m, { full = false, kcal = true } = {}) {
    if (!BO.macrosKnown(m)) return '';
    const parts = [];
    if (kcal && m.kcal != null) parts.push(`<i>${BO.kcalText(m.kcal)} kcal</i>`);
    if (m.proteinG != null) parts.push(`<i class="p">${BO.gramsText(m.proteinG)} protein</i>`);
    if (full && m.carbsG != null) parts.push(`<i>${BO.gramsText(m.carbsG)} carbs</i>`);
    if (full && m.fatG != null) parts.push(`<i>${BO.gramsText(m.fatG)} fat</i>`);
    return parts.join('');
  };

  const SOURCE = { milo: 'Milo list', openFoodFacts: 'scanned', own: '' };
  BO.foodSourceTag = f => SOURCE[f.source] ? `<i class="src">${f.source === 'openFoodFacts' && f.sourceRef ? 'Open Food Facts' : SOURCE[f.source]}</i>` : '';
  const perText = f => f.basis === 'perPiece' ? (f.pieceName || 'piece') : '100 g';

  /** Saves a food found in Milo's list or on Open Food Facts (or chosen from yours) under a name. */
  BO.saveFoodFrom = async function saveFoodFrom(hit, name, extra = {}) {
    const body = Object.assign({
      name: name || hit.name,
      basis: hit.basis || 'per100g',
      pieceGrams: hit.pieceGrams ?? null,
      pieceName: hit.pieceName ?? null,
      kcal: hit.kcal ?? null, proteinG: hit.proteinG ?? null, carbsG: hit.carbsG ?? null, fatG: hit.fatG ?? null,
      source: hit.source || 'own', sourceRef: hit.sourceRef ?? null,
    }, extra);
    const saved = await BO.saving(BO.api('POST', '/api/foods', body));
    BO.forgetFoods();
    return saved;
  };

  /* ---------- the food search ---------- */

  /**
   * Find a food: your foods, Milo's list (as you type), packaged foods (Open Food Facts, on Enter or the button) and the
   * barcode scanner. onPick(food) gets one of your foods (a found food is saved first: under `saveAs` when given, else
   * under its own name). onPlain(name) adds the typed name as it is.
   */
  BO.foodPicker = function foodPicker({ title, hint = '', query = '', saveAs = null, onPick, onPlain }) {
    BO.sheet((body, close) => {
      let packaged = null;    // null = not asked; [] = nothing found
      let packagedBusy = false;
      let result = { mine: [], milo: [] };
      let seq = 0;
      body.innerHTML = `<div><h3>${esc(title)}</h3><span class="sub">${esc(hint || 'Your foods first, then Milo’s list, then packaged products.')}</span></div>
        <form class="srch" data-srch><input class="field" type="search" enterkeyhint="search" placeholder="Search foods" value="${esc(query)}" data-q aria-label="Search foods" autocomplete="off"><button type="button" class="btn ink" data-scan>${icons.scan}Scan</button></form>
        <div class="fres" data-res aria-live="polite"></div>
        <p class="fine" data-foot></p>`;
      const q = () => body.querySelector('[data-q]').value.trim();
      const row = (kind, i, name, detail, m) => `<button type="button" class="fr" data-pick="${kind}:${i}"><div><b>${esc(name)}</b>${detail ? `<span class="sub">${esc(detail)}</span>` : ''}</div>
        <div class="fn">${m && m.kcal != null ? `<span><b class="num">${round(m.kcal)}</b> kcal</span>` : ''}${m && m.proteinG != null ? `<em class="num">${BO.gramsText(m.proteinG)}</em>` : ''}</div></button>`;
      function paint() {
        const text = q();
        let html = '';
        if (result.mine.length) html += `<div class="fgrp">Your foods</div>${result.mine.map((f, i) => row('mine', i, f.name, `per ${perText(f)}`, f)).join('')}`;
        if (result.milo.length) html += `<div class="fgrp">Milo list <span>per 100 g</span></div>${result.milo.map((h, i) => row('milo', i, h.name, h.detail, h)).join('')}`;
        if (packaged && packaged.length) html += `<div class="fgrp">Packaged <span>Open Food Facts · per 100 g</span></div>${packaged.map((h, i) => row('pack', i, h.name, h.detail, h)).join('')}`;
        if (packaged && !packaged.length) html += '<div class="fgrp">Packaged <span>nothing found</span></div>';
        if (!html) html = text ? '<p class="sub" style="margin:8px 0">Nothing in your foods or Milo’s list.</p>' : '';
        body.querySelector('[data-res]').innerHTML = html;
        const foot = [];
        if (text.length >= 2 && packaged == null) foot.push(`<button type="button" class="link-btn" style="font-size:12.5px" data-packaged>${packagedBusy ? 'Searching…' : `Search packaged products for “${esc(text)}”`}</button>`);
        foot.push(`<button type="button" class="link-btn" style="font-size:12.5px" data-own>Enter the label yourself</button>`);
        if (onPlain && text) foot.push(`<button type="button" class="link-btn" style="font-size:12.5px;color:var(--mute)" data-plain>Just add “${esc(text)}”</button>`);
        body.querySelector('[data-foot]').innerHTML = foot.join(' · ');
      }
      async function search() {
        const mine = ++seq;
        packaged = null;
        try {
          const r = await BO.api('GET', `/api/foods/search?q=${encodeURIComponent(q())}`);
          if (mine === seq) { result = r; paint(); }
        } catch (e) { /* keep what is shown */ }
      }
      async function searchPackaged() {
        const text = q();
        if (text.length < 2 || packagedBusy) return;
        packagedBusy = true; paint();
        try { packaged = await BO.api('GET', `/api/foods/packaged?q=${encodeURIComponent(text)}`); }
        catch (e) { BO.toast(e.message, { error: true }); packaged = null; }
        packagedBusy = false; paint();
      }
      async function pick(kind, i) {
        try {
          let food;
          if (kind === 'mine') {
            food = result.mine[i];
            if (saveAs && BO.foodKey(saveAs) !== BO.foodKey(food.name)) food = await BO.saveFoodFrom(food, saveAs, { source: food.source, sourceRef: food.sourceRef });
          } else {
            const hit = kind === 'milo' ? result.milo[i] : packaged[i];
            food = await BO.saveFoodFrom(hit, saveAs || hit.name);
          }
          close();
          onPick(food);
        } catch (e) { /* the toast explains */ }
      }
      body.addEventListener('input', e => { if (e.target.matches('[data-q]')) BO.debounce('food-search', 200, search); });
      body.querySelector('[data-srch]').addEventListener('submit', e => { e.preventDefault(); searchPackaged(); });
      body.addEventListener('click', e => {
        const p = e.target.closest('[data-pick]');
        if (p) { const [kind, i] = p.dataset.pick.split(':'); pick(kind, Number(i)); return; }
        if (e.target.closest('[data-packaged]')) { searchPackaged(); return; }
        if (e.target.closest('[data-own]')) { close(); BO.foodEditor({ name: saveAs || q(), onSaved: onPick }); return; }
        if (e.target.closest('[data-plain]')) { close(); onPlain(q()); return; }
        if (e.target.closest('[data-scan]')) {
          close();
          BO.scanBarcode({ saveAs, onPick });
        }
      });
      paint();
      search();
      setTimeout(() => { const input = body.querySelector('[data-q]'); if (input && !('ontouchstart' in window)) input.focus(); }, 300);
    }, { label: title });
  };

  /* ---------- the label editor ---------- */

  /** Type or change a food's label. food = one of yours (edit), or { name } for a new one. */
  BO.foodEditor = function foodEditor({ food = null, name = '', onSaved, onDeleted }) {
    const f = Object.assign({ name, basis: 'per100g', kcal: null, proteinG: null, carbsG: null, fatG: null, pieceGrams: null, pieceName: null }, food || {});
    BO.sheet((body, close) => {
      const num = v => v == null ? '' : BO.load(v);
      const paint = () => {
        const per = f.basis === 'perPiece';
        body.innerHTML = `<div><h3>${food ? esc(f.name) : 'New food'}</h3><span class="sub">From the pack label. ${food && f.source !== 'own' ? `Values from ${f.source === 'milo' ? 'Milo’s list' : 'Open Food Facts'}; change them if your pack differs.` : 'Only calories and protein are needed.'}</span></div>
          ${food ? '' : `<label class="lbl-f">Name<input class="field" data-f="name" value="${esc(f.name)}" maxlength="80" placeholder="e.g. Oats"></label>`}
          <div class="seg" style="align-self:flex-start" role="group" aria-label="Values are for"><button type="button" data-basis="per100g" aria-pressed="${!per}">Per 100 g</button><button type="button" data-basis="perPiece" aria-pressed="${per}">Per piece</button></div>
          <div class="mac-in">
            <label>Calories<span><input class="num" inputmode="decimal" data-f="kcal" value="${num(f.kcal)}" placeholder="0"><em>kcal</em></span></label>
            <label class="p">Protein<span><input class="num" inputmode="decimal" data-f="proteinG" value="${num(f.proteinG)}" placeholder="0"><em>g</em></span></label>
            <label>Carbs<span><input class="num" inputmode="decimal" data-f="carbsG" value="${num(f.carbsG)}" placeholder="–"><em>g</em></span></label>
            <label>Fat<span><input class="num" inputmode="decimal" data-f="fatG" value="${num(f.fatG)}" placeholder="–"><em>g</em></span></label>
          </div>
          <div class="row" style="flex-wrap:wrap">
            <label class="lbl-f" style="flex:1;min-width:120px">${per ? 'One piece weighs' : 'One piece weighs (optional)'}<span class="row" style="gap:6px"><input class="field small num" inputmode="decimal" data-f="pieceGrams" value="${num(f.pieceGrams)}" placeholder="g"><span class="sub">g</span></span></label>
            <label class="lbl-f" style="flex:1;min-width:120px">A piece is a<input class="field small" data-f="pieceName" value="${esc(f.pieceName || '')}" maxlength="20" placeholder="slice, scoop…"></label>
          </div>
          <p class="fine">${per ? 'With the weight, the plan can use grams too.' : 'With a piece weight, the plan can count it in pieces.'}</p>
          <div class="row">${food ? '<button type="button" class="btn danger" data-del>Delete</button>' : '<button type="button" class="btn ghost" data-cancel>Cancel</button>'}<button type="button" class="btn primary" style="flex:1" data-save>Save food</button></div>`;
      };
      const read = () => body.querySelectorAll('[data-f]').forEach(i => {
        const k = i.dataset.f;
        f[k] = ['name', 'pieceName'].includes(k) ? i.value.trim() : BO.readNumber(i.value);
      });
      body.addEventListener('click', async e => {
        const b = e.target.closest('[data-basis]');
        if (b) { read(); f.basis = b.dataset.basis; paint(); return; }
        if (e.target.closest('[data-cancel]')) { close(); return; }
        if (e.target.closest('[data-del]')) {
          try { await BO.saving(BO.api('DELETE', `/api/foods/${food.id}`)); BO.forgetFoods(); close(); if (onDeleted) onDeleted(food); } catch (err) { }
          return;
        }
        if (e.target.closest('[data-save]')) {
          read();
          if (!f.name) { BO.toast('Give the food a name.', { error: true }); return; }
          if (f.kcal == null && f.proteinG == null) { BO.toast('Type at least the calories or the protein.', { error: true }); return; }
          const payload = { name: f.name, basis: f.basis, pieceGrams: f.pieceGrams, pieceName: f.pieceName || null, kcal: f.kcal, proteinG: f.proteinG, carbsG: f.carbsG, fatG: f.fatG, source: food ? f.source : 'own', sourceRef: food ? f.sourceRef : null };
          try {
            const saved = await BO.saving(food ? BO.api('PUT', `/api/foods/${food.id}`, payload) : BO.api('POST', '/api/foods', payload));
            BO.forgetFoods();
            close();
            if (onSaved) onSaved(saved);
          } catch (err) { }
        }
      });
      paint();
    }, { label: food ? food.name : 'New food' });
  };

  /* ---------- the barcode scanner ---------- */

  let zxing = null;
  function loadZxing() {
    if (!zxing) zxing = new Promise((resolve, reject) => {
      const s = document.createElement('script');
      s.src = '/js/vendor/zxing.min.js';
      s.onload = () => resolve(window.ZXing);
      s.onerror = () => { zxing = null; reject(new Error('Couldn’t load the scanner.')); };
      document.head.appendChild(s);
    });
    return zxing;
  }

  /** The camera, full screen: reads EAN/UPC barcodes, looks them up on Open Food Facts and saves the product. */
  BO.scanBarcode = function scanBarcode({ saveAs = null, onPick }) {
    const el = document.createElement('div');
    el.className = 'cam';
    el.setAttribute('role', 'dialog');
    el.setAttribute('aria-label', 'Scan a barcode');
    el.innerHTML = `<div class="cam-top"><button type="button" class="s-btn" data-close>${icons.x} Close</button><span>Scan a barcode</span><span style="width:84px"></span></div>
      <div class="cam-view"><video playsinline muted></video><div class="frame"></div><p class="cam-msg" data-msg>Starting the camera…</p></div>
      <form class="cam-type" data-type><input class="field" inputmode="numeric" placeholder="or type the numbers" data-code aria-label="Barcode number"><button class="btn">Look up</button></form>`;
    document.body.appendChild(el);
    document.body.style.overflow = 'hidden';
    const video = el.querySelector('video');
    const msg = t => { const m = el.querySelector('[data-msg]'); if (m) m.textContent = t; };
    let stream = null, stopped = false, timer = null, reader = null, busy = false;

    function stop() {
      stopped = true;
      clearInterval(timer);
      try { if (reader) reader.reset(); } catch (e) { }
      if (stream) stream.getTracks().forEach(t => t.stop());
    }
    function close() { stop(); el.remove(); document.body.style.overflow = ''; }

    async function found(code) {
      if (busy) return;
      busy = true;
      BO.buzz(20);
      msg(`Looking up ${code}…`);
      try {
        const hit = await BO.api('GET', `/api/foods/barcode/${encodeURIComponent(code)}`);
        stop();
        result(hit);
      } catch (e) {
        msg(e.status === 404 ? `${code} isn’t on Open Food Facts yet. Type the label instead.` : e.message);
        busy = false;
        if (e.status === 404) {
          el.querySelector('.cam-type').insertAdjacentHTML('afterend', `<button type="button" class="btn" style="margin:0 18px 18px" data-own>Enter the label yourself</button>`);
        }
      }
    }

    function result(hit) {
      let serving = !!hit.pieceGrams;
      const card = document.createElement('div');
      card.className = 'sheet cam-sheet';
      const paint = () => {
        const m = serving ? BO.macrosFor({ basis: 'per100g', kcal: hit.kcal, proteinG: hit.proteinG }, hit.pieceGrams, 'grams') : hit;
        card.innerHTML = `<div class="row" style="align-items:flex-start"><span class="hk-icon ok">${icons.check}</span><div><h3 style="font-size:21px">${esc(hit.name)}</h3><span class="sub">${esc(hit.detail || '')}${hit.detail ? ' · ' : ''}found on Open Food Facts</span></div></div>
          ${hit.pieceGrams ? `<div class="seg" style="align-self:flex-start"><button type="button" data-per="100" aria-pressed="${!serving}">Per 100 g</button><button type="button" data-per="serving" aria-pressed="${serving}">Per serving · ${BO.load(hit.pieceGrams)} g</button></div>` : ''}
          <div class="mac-in"><label>Calories<span><b class="num big-v">${BO.kcalText(m.kcal)}</b><em>kcal</em></span></label><label class="p">Protein<span><b class="num big-v">${m.proteinG == null ? '–' : grams(m.proteinG)}</b><em>g</em></span></label></div>
          <div class="row"><button type="button" class="btn ghost" data-again>Scan again</button><button type="button" class="btn primary" style="flex:1" data-add>Add ${esc(saveAs ? `as “${saveAs}”` : 'it')}</button></div>`;
      };
      paint();
      el.appendChild(card);
      card.addEventListener('click', async e => {
        const per = e.target.closest('[data-per]');
        if (per) { serving = per.dataset.per === 'serving'; paint(); return; }
        if (e.target.closest('[data-again]')) { card.remove(); busy = false; start(); return; }
        if (e.target.closest('[data-add]')) {
          try {
            const food = await BO.saveFoodFrom(hit, saveAs || hit.name, serving ? { pieceName: 'serving' } : {});
            close();
            onPick(food);
          } catch (err) { }
        }
      });
    }

    async function start() {
      stopped = false;
      if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) { msg('This browser can’t use the camera here (it needs HTTPS). Type the numbers below.'); return; }
      try {
        if ('BarcodeDetector' in window) {
          const detector = new window.BarcodeDetector({ formats: ['ean_13', 'ean_8', 'upc_a', 'upc_e'] });
          stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false });
          video.srcObject = stream;
          await video.play();
          msg('Point at the barcode');
          timer = setInterval(async () => {
            if (stopped || busy || video.readyState < 2) return;
            try { const codes = await detector.detect(video); if (codes.length) found(codes[0].rawValue); } catch (e) { }
          }, 250);
        } else {
          const ZX = await loadZxing();
          const hints = new Map([[ZX.DecodeHintType.POSSIBLE_FORMATS, [ZX.BarcodeFormat.EAN_13, ZX.BarcodeFormat.EAN_8, ZX.BarcodeFormat.UPC_A, ZX.BarcodeFormat.UPC_E]]]);
          reader = new ZX.BrowserMultiFormatReader(hints, 300);
          msg('Point at the barcode');
          await reader.decodeFromConstraints({ video: { facingMode: 'environment' }, audio: false }, video, r => { if (r && !stopped) found(r.getText()); });
        }
      } catch (e) {
        msg(e && e.name === 'NotAllowedError' ? 'Camera access was refused. Allow it in the browser, or type the numbers below.' : 'Couldn’t start the camera. Type the numbers below.');
      }
    }

    el.addEventListener('click', e => {
      if (e.target.closest('[data-close]')) { close(); return; }
      if (e.target.closest('[data-own]')) { close(); BO.foodEditor({ name: saveAs || '', onSaved: onPick }); }
    });
    el.querySelector('[data-type]').addEventListener('submit', e => {
      e.preventDefault();
      const code = el.querySelector('[data-code]').value.replace(/\D/g, '');
      if (code.length < 8) { msg('A barcode has 8 to 14 digits.'); return; }
      found(code);
    });
    start();
  };
})();
