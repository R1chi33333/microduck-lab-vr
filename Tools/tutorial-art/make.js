// Renders the tutorial controller illustrations (comic style: thick ink, flat cel colours, halftone shading,
// the button to press painted yellow and ringed with a hand-drawn red circle) to PNGs for Unity.
// node Tools/tutorial-art/make.js  ->  Assets/MicroduckVR/Resources/Tutorial/*.png
const { chromium } = require('playwright');
const fs = require('fs'), path = require('path');
const root = path.resolve(__dirname, '../..');
const out = path.join(root, 'Assets/MicroduckVR/Resources/Tutorial');
fs.mkdirSync(out, { recursive: true });
const font = fs.readFileSync(path.join(root, 'Assets/MicroduckVR/Fonts/Bangers-Regular.ttf')).toString('base64');

const INK = '#1B1430', BODY = '#56527A', LIGHT = '#7B77A6', SHADE = '#3B3858', CREAM = '#FFF6E4', HI = '#FFE45C', RED = '#FF2A2A';

// one controller, drawn for the right hand; the left one is the mirror image. Returns svg + the hot spots.
function controller(right, hi) {
  const L = !right;
  const lab = (a, b) => right ? a : b;
  const on = (p) => hi.includes(p);
  const fill = (p, base) => on(p) ? HI : base;
  // shapes in a 300 x 380 box, inner side (where the stick is) on the left for the right hand
  let s = '';
  // grip button on the inner side of the handle (drawn under the handle edge)
  s += `<path d="M78 200 C60 215 58 262 74 290 C82 298 94 292 96 280 C100 250 100 222 96 206 C92 196 84 195 78 200Z" fill="${fill('grip', SHADE)}" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>`;
  // handle
  s += `<path d="M92 150 C80 210 84 290 110 345 C120 366 152 370 168 352 C190 322 196 250 192 168 Z" fill="${BODY}" stroke="${INK}" stroke-width="8" stroke-linejoin="round"/>`;
  s += `<path d="M110 175 C104 230 108 300 126 340" fill="none" stroke="${LIGHT}" stroke-width="10" stroke-linecap="round"/>`;
  s += `<path d="M178 190 C182 250 176 310 160 345 C175 330 186 300 188 250 C190 220 186 200 178 190Z" fill="url(#dots)" opacity="0.9"/>`;
  // trigger: a curved finger tab poking out under the head on the outer side
  s += `<path d="M168 150 C196 156 226 176 230 204 C233 222 220 232 206 226 C192 220 184 200 172 186 Z" fill="${fill('trigger', SHADE)}" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>`;
  s += `<path d="M190 170 C206 180 218 192 220 206" fill="none" stroke="${on('trigger') ? '#FFFFFF' : LIGHT}" stroke-width="5" stroke-linecap="round" opacity="0.8"/>`;
  // head: a fat rounded top with the face plate
  s += `<path d="M40 110 C36 50 110 18 170 22 C236 26 270 70 262 120 C256 160 214 182 160 184 C96 186 44 164 40 110Z" fill="${BODY}" stroke="${INK}" stroke-width="8" stroke-linejoin="round"/>`;
  s += `<path d="M62 104 C62 66 118 42 168 44 C220 46 246 76 240 110 C236 140 204 158 158 160 C104 162 62 144 62 104Z" fill="${LIGHT}" stroke="${INK}" stroke-width="5"/>`;
  s += `<path d="M70 70 C90 46 130 36 160 38" fill="none" stroke="#FFFFFF" stroke-width="7" stroke-linecap="round" opacity="0.8"/>`;
  s += `<path d="M200 156 C232 146 252 128 256 104 C258 140 236 168 196 176Z" fill="url(#dots)" opacity="0.9"/>`;
  // thumbstick (inner side) and the two face buttons (outer side)
  const stickC = on('stick') || on('stickclick') ? HI : '#2A2840';
  s += `<ellipse cx="112" cy="100" rx="34" ry="30" fill="${SHADE}" stroke="${INK}" stroke-width="6"/>`;
  s += `<ellipse cx="112" cy="94" rx="25" ry="22" fill="${stickC}" stroke="${INK}" stroke-width="6"/>`;
  s += `<ellipse cx="104" cy="86" rx="8" ry="5" fill="#FFFFFF" opacity="0.7"/>`;
  const btn = (cx, cy, p, letter) => {
    let b = `<ellipse cx="${cx}" cy="${cy + 4}" rx="22" ry="19" fill="${SHADE}" stroke="${INK}" stroke-width="6"/>`;
    b += `<ellipse cx="${cx}" cy="${cy}" rx="22" ry="19" fill="${fill(p, CREAM)}" stroke="${INK}" stroke-width="6"/>`;
    b += `<text x="${cx}" y="${cy + 10}" font-family="Bangers" font-size="30" text-anchor="middle" fill="${INK}" ${L ? `transform="translate(${2 * cx} 0) scale(-1 1)"` : ''}>${letter}</text>`;
    return b;
  };
  s += btn(190, 118, lab('A', 'X') === 'A' ? 'A' : 'X', lab('A', 'X'));
  s += btn(204, 72, lab('B', 'Y') === 'B' ? 'B' : 'Y', lab('B', 'Y'));
  // hot spots (for the red rings), in this box's coordinates
  const spots = { stick: [112, 96, 44], stickclick: [112, 96, 44], trigger: [206, 200, 42], grip: [80, 246, 52],
                  A: [190, 120, 34], B: [204, 74, 34], X: [190, 120, 34], Y: [204, 74, 34] };
  return { svg: s, spots };
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
  // hi: list of "L:part" / "R:part"
  const hiL = hi.filter(h => h.startsWith('L:')).map(h => h.slice(2));
  const hiR = hi.filter(h => h.startsWith('R:')).map(h => h.slice(2));
  const l = controller(false, hiL), r = controller(true, hiR);
  let s = `<svg xmlns="http://www.w3.org/2000/svg" width="640" height="420" viewBox="0 0 640 420">
  <defs><style>@font-face{font-family:Bangers;src:url(data:font/ttf;base64,${font})}</style>
  <pattern id="dots" width="9" height="9" patternUnits="userSpaceOnUse"><circle cx="3" cy="3" r="2.2" fill="${INK}" opacity="0.55"/></pattern></defs>`;
  // left controller: mirrored
  s += `<g transform="translate(320 18) scale(-1 1)">${l.svg}</g>`;
  s += `<g transform="translate(320 18)">${r.svg}</g>`;
  hiL.forEach((p, i) => { const [x, y, rr] = l.spots[p]; s += ring(320 - x, 18 + y, rr, 0.6 + i); if (p === 'stickclick') s += burst(320 - x - 70, 18 + y - 70, 'CLICK!'); });
  hiR.forEach((p, i) => { const [x, y, rr] = r.spots[p]; s += ring(320 + x, 18 + y, rr, 1.3 + i); if (p === 'stickclick') s += burst(320 + x + 70, 18 + y - 70, 'CLICK!'); });
  s += label(320 - 150, 408, 'LEFT', hiL.length > 0) + label(320 + 150, 408, 'RIGHT', hiR.length > 0);
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
