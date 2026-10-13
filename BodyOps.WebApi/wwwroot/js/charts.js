/* BodyOps — charts drawn as inline SVG: the line chart (weight, progression), the body map and the consistency grid. */
'use strict';

(() => {
  const esc = BO.esc;

  /** Rounds an axis range out to "nice" steps and returns the tick values. */
  function ticks(min, max, count = 4) {
    if (min === max) { min -= 1; max += 1; }
    const raw = (max - min) / count;
    const power = Math.pow(10, Math.floor(Math.log10(raw)));
    const step = [1, 2, 2.5, 5, 10].map(m => m * power).find(s => s >= raw) || raw;
    const lo = Math.floor(min / step) * step;
    const hi = Math.ceil(max / step) * step;
    const values = [];
    for (let v = lo; v <= hi + step / 2; v += step) values.push(Math.round(v * 100) / 100);
    return values;
  }

  /**
   * A line over time. points: [{ date: 'yyyy-MM-dd', value }]. Options: goal (dashed line), goalLabel,
   * highlight (index drawn as a record), width, height, unit.
   */
  BO.lineChart = function lineChart(points, { goal = null, goalLabel = '', highlight = -1, width = 340, height = 180, label = v => String(v) } = {}) {
    if (!points.length) return '';
    const L = 36, R = width - 14, T = 16, B = height - 26;
    const values = points.map(p => p.value).concat(goal != null ? [goal] : []);
    const yTicks = ticks(Math.min(...values), Math.max(...values));
    const lo = yTicks[0], hi = yTicks[yTicks.length - 1];
    const times = points.map(p => BO.parseDate(p.date).getTime());
    const t0 = times[0], t1 = times[times.length - 1];
    const x = i => points.length === 1 ? (L + R) / 2 : L + (times[i] - t0) / Math.max(1, t1 - t0) * (R - L);
    const y = v => T + (hi - v) / (hi - lo || 1) * (B - T);

    let s = '';
    yTicks.forEach(v => {
      s += `<line class="grid-l" x1="${L}" x2="${R}" y1="${y(v)}" y2="${y(v)}"/>`;
      s += `<text class="ax" x="${L - 6}" y="${y(v) + 4}" text-anchor="end">${esc(Number.isInteger(v) ? v : v.toFixed(1))}</text>`;
    });
    if (goal != null) {
      s += `<line class="target-l" x1="${L}" x2="${R}" y1="${y(goal)}" y2="${y(goal)}"/>`;
      s += `<text class="lbl-t" x="${R}" y="${y(goal) - 6}" text-anchor="end">${esc(goalLabel)}</text>`;
    }
    const d = points.map((p, i) => `${i ? 'L' : 'M'}${x(i).toFixed(1)},${y(p.value).toFixed(1)}`).join(' ');
    if (points.length > 1) {
      s += `<path class="area" d="${d} L${x(points.length - 1).toFixed(1)},${B} L${x(0).toFixed(1)},${B} Z"/>`;
      s += `<path class="series" d="${d}"/>`;
    }
    const showDots = points.length <= 40;
    points.forEach((p, i) => {
      const last = i === points.length - 1;
      if (i === highlight) s += `<circle class="dot-pr" cx="${x(i)}" cy="${y(p.value)}" r="6"><title>${esc(BO.shortDate(p.date))} · ${esc(label(p.value))}</title></circle>`;
      else if (showDots || last) s += `<circle class="${last ? 'dot dot-end' : 'dot'}" cx="${x(i)}" cy="${y(p.value)}" r="${last ? 5 : 3.5}"><title>${esc(BO.shortDate(p.date))} · ${esc(label(p.value))}</title></circle>`;
    });
    const lastIndex = points.length - 1;
    s += `<text class="lbl" x="${Math.min(x(lastIndex), R) - 8}" y="${y(points[lastIndex].value) - 12}" text-anchor="end">${esc(label(points[lastIndex].value))}</text>`;
    const xLabels = points.length === 1 ? [0] : [0, lastIndex];
    if (points.length > 4) xLabels.splice(1, 0, Math.round(lastIndex / 2));
    xLabels.forEach((i, n) => {
      const anchor = points.length === 1 ? 'middle' : n === 0 ? 'start' : i === lastIndex ? 'end' : 'middle';
      s += `<text class="ax" x="${x(i)}" y="${height - 8}" text-anchor="${anchor}">${esc(BO.shortDate(points[i].date))}</text>`;
    });
    return `<svg class="chart" viewBox="0 0 ${width} ${height}" role="img">${s}</svg>`;
  };

  /** Front and back figures with each muscle shaded by how many sets it got. totals: [{ muscle, sets }]. */
  BO.bodyMap = function bodyMap(totals) {
    const sets = {};
    totals.forEach(t => { sets[t.muscle] = t.sets; });
    const max = Math.max(0, ...totals.map(t => t.sets));
    const fill = m => sets[m] ? `style="fill:var(--accent);fill-opacity:${(0.25 + 0.75 * sets[m] / max).toFixed(2)}"` : 'style="fill:var(--s3)"';
    const tip = m => `<title>${BO.MUSCLES[m]} · ${sets[m] ? Math.round(sets[m] * 10) / 10 : 0} sets</title>`;
    const E = (cx, cy, rx, ry, m) => m
      ? `<ellipse class="bm-m" cx="${cx}" cy="${cy}" rx="${rx}" ry="${ry}" ${fill(m)}>${tip(m)}</ellipse>`
      : `<ellipse class="bm-n" cx="${cx}" cy="${cy}" rx="${rx}" ry="${ry}"/>`;
    const R = (x, y, w, h, r, m) => m
      ? `<rect class="bm-m" x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" ${fill(m)}>${tip(m)}</rect>`
      : `<rect class="bm-n" x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}"/>`;
    const P = (pts, m) => `<polygon class="bm-m" points="${pts}" ${fill(m)}>${tip(m)}</polygon>`;
    const front = c => E(c, 24, 11, 12) + R(c - 5, 34, 10, 8, 3)
      + P(`${c - 21},47 ${c - 6},39 ${c},43 ${c - 8},48`, 'traps') + P(`${c + 21},47 ${c + 6},39 ${c},43 ${c + 8},48`, 'traps')
      + E(c - 24, 54, 10, 9, 'shoulders') + E(c + 24, 54, 10, 9, 'shoulders') + R(c - 16, 45, 15, 18, 5, 'chest') + R(c + 1, 45, 15, 18, 5, 'chest')
      + R(c - 11, 65, 22, 31, 6, 'abs') + E(c - 30, 74, 7, 13, 'biceps') + E(c + 30, 74, 7, 13, 'biceps')
      + E(c - 33, 100, 6, 14, 'forearms') + E(c + 33, 100, 6, 14, 'forearms') + E(c - 35, 119, 4, 5) + E(c + 35, 119, 4, 5)
      + R(c - 12, 97, 24, 11, 4) + E(c - 8, 133, 9, 24, 'quads') + E(c + 8, 133, 9, 24, 'quads')
      + E(c - 8, 161, 5, 5) + E(c + 8, 161, 5, 5) + E(c - 8, 186, 6, 20) + E(c + 8, 186, 6, 20) + E(c - 8, 209, 6, 4) + E(c + 8, 209, 6, 4)
      + `<text class="bm-cap" x="${c}" y="236" text-anchor="middle">Front</text>`;
    const back = c => E(c, 24, 11, 12) + R(c - 5, 34, 10, 8, 3) + P(`${c},40 ${c - 19},51 ${c},73 ${c + 19},51`, 'traps')
      + E(c - 24, 54, 10, 9, 'shoulders') + E(c + 24, 54, 10, 9, 'shoulders')
      + P(`${c - 18},59 ${c - 2},61 ${c - 2},92 ${c - 14},80`, 'lats') + P(`${c + 18},59 ${c + 2},61 ${c + 2},92 ${c + 14},80`, 'lats')
      + E(c - 30, 74, 7, 13, 'triceps') + E(c + 30, 74, 7, 13, 'triceps') + E(c - 33, 100, 6, 14, 'forearms') + E(c + 33, 100, 6, 14, 'forearms')
      + E(c - 35, 119, 4, 5) + E(c + 35, 119, 4, 5) + R(c - 11, 90, 22, 13, 4, 'lowerBack') + E(c - 9, 112, 10, 9, 'glutes') + E(c + 9, 112, 10, 9, 'glutes')
      + E(c - 8, 141, 9, 22, 'hamstrings') + E(c + 8, 141, 9, 22, 'hamstrings') + E(c - 8, 166, 5, 5) + E(c + 8, 166, 5, 5)
      + E(c - 8, 188, 7, 17, 'calves') + E(c + 8, 188, 7, 17, 'calves') + E(c - 8, 209, 6, 4) + E(c + 8, 209, 6, 4)
      + `<text class="bm-cap" x="${c}" y="236" text-anchor="middle">Back</text>`;
    return `<svg class="bodymap" viewBox="0 0 300 245" role="img" aria-label="Muscle map, front and back">${front(78)}${back(222)}</svg>`;
  };

  /** The muscles with the most sets, as bars. */
  BO.muscleRank = function muscleRank(totals, count = 7) {
    const top = totals.filter(t => t.sets > 0).slice(0, count);
    if (!top.length) return '';
    const max = top[0].sets;
    return `<div class="mrank">${top.map(t => `<div class="mr"><span>${BO.MUSCLES[t.muscle]}</span><span class="bar"><i style="width:${Math.round(t.sets / max * 100)}%"></i></span><span class="v num">${Math.round(t.sets * 10) / 10} sets</span></div>`).join('')}</div>`;
  };

  /** One column per week, one cell per day (Monday on top); darker = more complete. days: [{ date, score }]. */
  BO.heatmap = function heatmap(days, today) {
    if (!days.length) return '';
    const weeks = [];
    days.forEach((day, i) => { if (i % 7 === 0) weeks.push([]); weeks[weeks.length - 1].push(day); });
    const cols = weeks.map(week => {
      const cells = [];
      for (let i = 0; i < 7; i++) {
        const day = week[i];
        if (!day) { cells.push('<i class="f"></i>'); continue; }
        const label = `${BO.fullDate(day.date)} · ${day.score == null ? 'nothing logged' : Math.round(day.score * 100) + '%'}`;
        cells.push(day.score == null
          ? `<i class="n" title="${esc(label)}"></i>`
          : `<i style="background:var(--accent);opacity:${(0.2 + 0.8 * day.score).toFixed(2)}" title="${esc(label)}"></i>`);
      }
      return `<div class="col">${cells.join('')}</div>`;
    }).join('');
    return `<div class="heat" style="--weeks:${weeks.length}">${cols}</div>
      <div class="heat-legend"><span>Less</span><i style="background:var(--s3)"></i><i style="background:var(--accent);opacity:.35"></i><i style="background:var(--accent);opacity:.6"></i><i style="background:var(--accent);opacity:.8"></i><i style="background:var(--accent)"></i><span>More</span><span style="margin-left:8px">dashed = nothing logged</span></div>`;
  };
})();
