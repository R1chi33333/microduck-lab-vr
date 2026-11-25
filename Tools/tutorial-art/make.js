// Renders the tutorial controller illustrations (comic style: thick ink, flat cel colours, halftone shading,
// the button to press painted yellow and ringed with a hand-drawn red circle) to PNGs for Unity.
// node Tools/tutorial-art/make.js  ->  Assets/MicroduckVR/Resources/Tutorial/*.png
const { chromium } = require('playwright');
const fs = require('fs'), path = require('path');
const root = path.resolve(__dirname, '../..');
const out = path.join(root, 'Assets/MicroduckVR/Resources/Tutorial');
fs.mkdirSync(out, { recursive: true });
const font = fs.readFileSync(path.join(root, 'Assets/MicroduckVR/Fonts/Bangers-Regular.ttf')).toString('base64');

const INK = '#1B1430', BODY = '#45465C', LIGHT = '#686A86', SHADE = '#2C2D3E', CREAM = '#FFF6E4', HI = '#FFE45C', RED = '#FF2A2A';

// One Touch Pro controller seen from above, drawn as the LEFT one in a 300 x 380 box (the right one is its mirror):
// a round head with the thumbstick near the top, a camera lens at the outer top, the two face buttons below the
// stick and the menu (left) / Meta (right) button at the edge; the handle sweeps down and outwards, the trigger
// pokes out under the head on the inner side, the grip runs along the inner edge of the handle.
// Returns the shapes (mirrorable) and the labels/spots in box coordinates.
function controller(hi) {
  const on = (p) => hi.includes(p);
  const fill = (p, base) => on(p) ? HI : base;
  let s = '';
  // handle (under the head)
  s += `<path d="M86 150 C64 214 34 292 36 342 C38 376 86 384 106 360 C134 324 174 262 214 196 Z" fill="${BODY}" stroke="${INK}" stroke-width="8" stroke-linejoin="round"/>`;
  s += `<path d="M76 210 C60 260 48 310 52 340" fill="none" stroke="${LIGHT}" stroke-width="10" stroke-linecap="round"/>`;
  // grip: a long pad on the inner edge of the handle
  s += `<path d="M184 222 C160 262 134 302 106 338 C122 352 148 344 162 328 C182 302 202 270 214 236 Z" fill="${fill('grip', SHADE)}" stroke="${INK}" stroke-width="6" stroke-linejoin="round"/>`;
  s += `<path d="M112 250 C100 290 84 330 86 352 C104 330 128 296 150 262Z" fill="url(#dots)" opacity="0.8"/>`;
  // trigger: tab under the head on the inner side
  s += `<path d="M184 196 C210 190 236 204 236 230 C236 250 214 258 200 248 C190 240 186 222 184 196Z" fill="${fill('trigger', SHADE)}" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>`;
  s += `<path d="M206 206 C220 210 228 220 228 232" fill="none" stroke="${on('trigger') ? '#FFFFFF' : LIGHT}" stroke-width="5" stroke-linecap="round" opacity="0.85"/>`;
  // head: round disc with a rim and a slightly smaller face
  s += `<ellipse cx="160" cy="118" rx="118" ry="104" fill="${BODY}" stroke="${INK}" stroke-width="8"/>`;
  s += `<ellipse cx="158" cy="112" rx="104" ry="90" fill="${LIGHT}" stroke="${INK}" stroke-width="5"/>`;
  s += `<path d="M78 70 C100 40 140 28 178 30" fill="none" stroke="#FFFFFF" stroke-width="7" stroke-linecap="round" opacity="0.75"/>`;
  s += `<path d="M236 150 C220 182 186 200 146 202 C200 214 252 190 266 140 Z" fill="url(#dots)" opacity="0.85"/>`;
  // thumbstick near the top
  const stickC = on('stick') || on('stickclick') ? HI : '#2A2B3C';
  s += `<ellipse cx="150" cy="84" rx="32" ry="27" fill="${SHADE}" stroke="${INK}" stroke-width="6"/>`;
  s += `<ellipse cx="150" cy="76" rx="24" ry="22" fill="${stickC}" stroke="${INK}" stroke-width="6"/>`;
  s += `<ellipse cx="143" cy="68" rx="8" ry="5" fill="#FFFFFF" opacity="0.7"/>`;
  // camera lens at the outer top edge
  s += `<circle cx="238" cy="64" r="12" fill="${SHADE}" stroke="${INK}" stroke-width="5"/><circle cx="238" cy="64" r="5" fill="#8FD3FF"/>`;
  // face buttons: lower (X / A) and upper (Y / B); the system button at the inner edge
  const btn = (cx, cy, r, p, base) => `<ellipse cx="${cx}" cy="${cy + 4}" rx="${r}" ry="${r * 0.88}" fill="${SHADE}" stroke="${INK}" stroke-width="6"/>` +
      `<ellipse cx="${cx}" cy="${cy}" rx="${r}" ry="${r * 0.88}" fill="${fill(p, base)}" stroke="${INK}" stroke-width="6"/>`;
  s += btn(150, 146, 20, 'low', CREAM);
  s += btn(208, 128, 20, 'high', CREAM);
  s += btn(86, 126, 15, 'sys', '#B9BED0');
  const spots = { stick: [150, 78, 44], stickclick: [150, 78, 44], trigger: [212, 226, 36], grip: [160, 282, 48], low: [150, 148, 32], high: [208, 130, 32] };
  const labels = { low: [150, 146], high: [208, 128], sys: [86, 126] };
  return { svg: s, spots, labels };
}

// a wobbly hand-drawn ring (two passes) + little "press!" ticks
function ring(cx, cy, r, seed) {
  const pts = (k, j) => { let d = ''; for (let i = 0; i <= 40; i++) { const a = i / 40 * Math.PI * 2.1 + seed; const rr = r * (1 + 0.06 * Math.sin(a * 3 + k + seed)) + j; d += (i ? 'L' : 'M') + (cx + Math.cos(a) * rr * 1.05).toFixed(1) + ' ' + (cy + Math.sin(a) * rr * 0.92).toFixed(1); } return d; };
  let s = `<path d="${pts(0, 0)}" fill="none" stroke="${RED}" stroke-width="8" stroke-linecap="round" stroke-linejoin="round"/>`;
  s += `<path d="${pts(1.7, 4)}" fill="none" stroke="${RED}" stroke-width="4" stroke-linecap="round" opacity="0.8"/>`;
  for (let i = 0; i < 3; i++) { const a = -2.2 + i * 0.5 + seed * 0.1; const x1 = cx + Math.cos(a) * (r + 14), y1 = cy + Math.sin(a) * (r + 14); s += `<path d="M${x1.toFixed(1)} ${y1.toFixed(1)} L${(x1 + Math.cos(a) * 16).toFixed(1)} ${(y1 + Math.sin(a) * 16).toFixed(1)}" stroke="${INK}" stroke-width="6" stroke-linecap="round"/>`; }
  return s;
}

function scene(hi) {
  // hi: "L:part" / "R:part"; parts: stick, stickclick, trigger, grip, A, B, X, Y
  const map = (h) => ({ A: 'low', X: 'low', B: 'high', Y: 'high' }[h] || h);
  const hiL = hi.filter(h => h.startsWith('L:')).map(h => map(h.slice(2)));
  const hiR = hi.filter(h => h.startsWith('R:')).map(h => map(h.slice(2)));
  const l = controller(hiL), r = controller(hiR);
  const OX = 14, OY = 16, RX = 626;          // left box at OX; right box mirrored so its x runs from RX leftwards
  const Lx = (x) => OX + x, Rx = (x) => RX - x, Y = (y) => OY + y;
  let s = `<svg xmlns="http://www.w3.org/2000/svg" width="640" height="420" viewBox="0 0 640 420">
  <defs><style>@font-face{font-family:Bangers;src:url(data:font/ttf;base64,${font})}</style>
  <pattern id="dots" width="9" height="9" patternUnits="userSpaceOnUse"><circle cx="3" cy="3" r="2.2" fill="${INK}" opacity="0.55"/></pattern></defs>`;
  s += `<g transform="translate(${OX} ${OY})">${l.svg}</g>`;
  s += `<g transform="translate(${RX} ${OY}) scale(-1 1)">${r.svg}</g>`;
  // button faces, never mirrored
  const t = (x, y, txt, size = 26) => `<text x="${x}" y="${y + size * 0.34}" font-family="Bangers" font-size="${size}" text-anchor="middle" fill="${INK}">${txt}</text>`;
  s += t(Lx(l.labels.low[0]), Y(l.labels.low[1]), 'X') + t(Lx(l.labels.high[0]), Y(l.labels.high[1]), 'Y');
  s += t(Rx(r.labels.low[0]), Y(r.labels.low[1]), 'A') + t(Rx(r.labels.high[0]), Y(r.labels.high[1]), 'B');
  const [mx, my] = l.labels.sys; for (let i = -1; i <= 1; i++) s += `<path d="M${Lx(mx) - 7} ${Y(my) + i * 5} h14" stroke="${INK}" stroke-width="3" stroke-linecap="round"/>`;
  const [ox, oy] = r.labels.sys; s += `<ellipse cx="${Rx(ox)}" cy="${Y(oy)}" rx="7" ry="5" fill="none" stroke="${INK}" stroke-width="3"/>`;
  hiL.forEach((p, i) => { const [x, y, rr] = l.spots[p]; s += ring(Lx(x), Y(y), rr, 0.6 + i); if (p === 'stickclick') s += burst(Lx(x) - 60, Y(y) - 52, 'CLICK!'); });
  hiR.forEach((p, i) => { const [x, y, rr] = r.spots[p]; s += ring(Rx(x), Y(y), rr, 1.3 + i); if (p === 'stickclick') s += burst(Rx(x) + 60, Y(y) - 52, 'CLICK!'); });
  s += label(150, 410, 'LEFT', hiL.length > 0) + label(490, 410, 'RIGHT', hiR.length > 0);
  return s + '</svg>';
}
function label(x, y, t, on) { return `<text x="${x}" y="${y}" font-family="Bangers" font-size="34" text-anchor="middle" fill="${on ? RED : '#97A0AE'}" stroke="${INK}" stroke-width="${on ? 1.5 : 0}">${t}</text>`; }
function burst(x, y, t) {
  let d = ''; for (let i = 0; i <= 16; i++) { const a = i / 16 * Math.PI * 2, rr = i % 2 ? 26 : 44; d += (i ? 'L' : 'M') + (x + Math.cos(a) * rr * 1.5).toFixed(1) + ' ' + (y + Math.sin(a) * rr).toFixed(1); }
  return `<path d="${d}Z" fill="${HI}" stroke="${INK}" stroke-width="5" stroke-linejoin="round"/><text x="${x}" y="${y + 10}" font-family="Bangers" font-size="28" text-anchor="middle" fill="${INK}">${t}</text>`;
}

const sheets = {
  pads: [], walk: ['L:stick'], turn: ['R:stick'], beak: ['R:trigger'], chirp: ['R:A'], bag: ['L:Y', 'R:trigger'],
  skates: ['R:B', 'L:grip', 'L:trigger'], laser: ['R:grip'], view: ['L:stickclick', 'R:stick'],
};

(async () => {
  const browser = await chromium.launch(); const page = await browser.newPage({ viewport: { width: 640, height: 420 }, deviceScaleFactor: 2 });
  for (const [name, hi] of Object.entries(sheets)) {
    await page.setContent(`<html><body style="margin:0;background:transparent">${scene(hi)}</body></html>`);
    await page.waitForTimeout(150);
    await page.screenshot({ path: path.join(out, `pads_${name}.png`), omitBackground: true });
  }
  await browser.close();
  console.log('ok', Object.keys(sheets).length);
})();
