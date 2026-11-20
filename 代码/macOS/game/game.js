const room = document.querySelector('.room');
const canvas = room.querySelector('canvas');
const pixels = canvas.getContext('2d', { alpha: false });
pixels.imageSmoothingEnabled = false;

function loadImage(file) {
  const image = new Image();
  image.src = 'assets/' + file;
  return image;
}
const background = loadImage('room.png');
const outfits = window.wardrobeOutfits.map(file => ({
  file,
  name: file.replace(/^\d+-|\.png$/g, ''),
}));
const defaultOutfit = outfits.find(({ file }) => file.startsWith('05-')) ?? outfits[0];
function readWardrobe() {
  try {
    return JSON.parse(localStorage.getItem('wardrobe') || '{}') ?? {};
  } catch {
    return {};
  }
}
const saved = readWardrobe();
const hidden = new Set(Array.isArray(saved.hidden) ? saved.hidden : []);
let worn = outfits.find(({ file }) => file === saved.worn) ?? defaultOutfit;
let player = loadImage('outfits/' + worn.file);
let sidePlayer = loadImage('outfits-side/' + worn.file);
let backPlayer = loadImage('outfits-back/' + worn.file);

function saveWardrobe() {
  try {
    localStorage.setItem('wardrobe', JSON.stringify({ worn: worn.file, hidden: [...hidden] }));
  } catch {
    // Keep the current session usable when storage is unavailable.
  }
}
const companion = loadImage('companion.png');

const walkingBodies = new WeakMap();
function drawWalkingPlayer() {
  const sprite = backFacing ? backPlayer : sideFacing ? sidePlayer : player;
  if (!sprite.naturalWidth) return;
  const p = window.walkProfiles[worn.file.slice(0, 2)];
  const w = sprite.width, h = sprite.height;
  pixels.save();
  pixels.translate(canvas.width * position.x, canvas.height * position.y);
  if (sideFacing && facing < 0) pixels.scale(-1, 1);
  pixels.translate(-w / 2 + (sideFacing ? 0 : Math.sin(walkingTime) * stride * .6), -h - (sideFacing ? 0 : (1 - Math.cos(walkingTime * 2)) * stride * .2));
  if (stride < .001) {
    pixels.drawImage(sprite, 0, 0);
  } else if (p.kind === 'gown') {
    pixels.drawImage(sprite, 0, 0);
    pixels.fillStyle = '#3e271c';
    for (let leg = 0; leg < 2; leg++) {
      const cycle = Math.sin(walkingTime + leg * Math.PI);
      const lift = Math.max(0, Math.cos(walkingTime + leg * Math.PI));
      pixels.fillRect(w / 2 + (sideFacing ? cycle * 6 : leg === 0 ? -11 : 3), 175 + (sideFacing ? 0 : cycle - lift * .3) * stride, 8, 5 * stride);
    }
  } else {
    let body = walkingBodies.get(sprite);
    if (!body) {
      body = document.createElement('canvas');
      body.width = w; body.height = h;
      const ctx = body.getContext('2d');
      ctx.drawImage(sprite, 0, 0);
      ctx.clearRect(0, p.root, w, h - p.root);
      walkingBodies.set(sprite, body);
    }
    const middle = Math.floor(w / 2);
    for (let leg = 0; leg < 2; leg++) {
      const cycle = Math.sin(walkingTime + leg * Math.PI);
      const left = sideFacing ? 0 : leg === 0 ? 0 : middle - 1;
      const width = sideFacing ? w : leg === 0 ? middle + 1 : w - middle + 1;
      for (let y = p.root; y < h; y++) {
        const t = (y - p.root) / (h - p.root - 1);
        const ease = sideFacing ? t : t * t * (3 - 2 * t);
        const swing = Math.max(0, Math.cos(walkingTime + leg * Math.PI));
        const bend = Math.sin(t * Math.PI) * swing;
        const dx = sideFacing ? Math.round((cycle * ease + bend * .25) * p.travel * stride * 3.8) : 0;
        const vertical = sideFacing ? -swing * .45 : cycle * 1.45 - swing * p.lift * .46;
        const dy = vertical * ease * stride;
        const nextT = Math.min(1, t + 1 / (h - p.root - 1));
        const nextEase = sideFacing ? nextT : nextT * nextT * (3 - 2 * nextT);
        const nextDy = vertical * nextEase * stride;
        const rowHeight = Math.max(.4, 1 + nextDy - dy + .25);
        pixels.drawImage(sprite, left, y, width, 1, left + dx, y + dy, width, rowHeight);
      }
    }
    pixels.drawImage(body, 0, 0);
  }
  pixels.restore();
}

function drawSprite(image, x, y, flip = false, bounce = 0) {
  if (!image.naturalWidth) return;
  const left = Math.round(canvas.width * x - image.width / 2);
  const top = Math.round(canvas.height * y) - image.height - bounce;
  pixels.save();
  if (flip) {
    pixels.translate(left + image.width, top);
    pixels.scale(-1, 1);
    pixels.drawImage(image, 0, 0);
  } else {
    pixels.drawImage(image, left, top);
  }
  pixels.restore();
}
const prompt = document.querySelector('#prompt');
const dialog = document.querySelector('dialog');
const title = document.querySelector('#dialog-title');
const content = document.querySelector('#dialog-content');
const keys = new Set();
const position = { x: .43, y: .71 };
const directions = {
  ArrowUp: [0, -1], ArrowDown: [0, 1], ArrowLeft: [-1, 0], ArrowRight: [1, 0],
  w: [0, -1], s: [0, 1], a: [-1, 0], d: [1, 0],
};
const objects = {
  wardrobe: { label: '衣橱', x: .79, y: .52 },
  map: { label: '旅行回忆电视', x: .53, y: .52 },
  gift: { label: '生日礼物', x: .675, y: .74 },
  dog: { label: '摸摸西施狗', x: .43, y: .765 },
};
const obstacles = [
  [.045, .535, .23, .94], [.70, .68, .91, .91],
  [.16, .48, .32, .55], [.90, .50, .985, .54],
  [.81, .54, .945, .695], [.375, .80, .475, .90], [.527, .608, .59, .657],
];
const ratio = canvas.width / canvas.height;
const distance = (a, b) => Math.hypot(a.x - b.x, (a.y - b.y) / ratio);

function isWalkable(x, y) {
  const marginX = .014;
  const marginY = .022;
  return x >= .075 && x <= .91 && y >= .515 && y <= .96 &&
    !obstacles.some(([left, top, right, bottom]) =>
      x + marginX > left && x - marginX < right &&
      y + marginY > top && y - marginY < bottom);
}

function nearestObject() {
  return Object.keys(objects)
    .filter(id => distance(position, objects[id]) < .085)
    .sort((a, b) => distance(position, objects[a]) - distance(position, objects[b]))[0];
}

function interact(id = nearestObject()) {
  if (!id || dialog.open) return;
  keys.clear();
  title.textContent = objects[id].label;
  if (id === 'wardrobe') {
    showWardrobe(false);
  } else if (id === 'map') {
    content.innerHTML = '<div class="travel-map"><img src="assets/map.png" alt="旅行贴板上的世界地图"><button data-memory="0" style="left:52%;top:28%" aria-label="示例旅行一">1</button><button data-memory="1" style="left:78%;top:36%" aria-label="示例旅行二">2</button></div><p id="memory">选一个编号，看看旅行卡片的效果。</p>';
    const memories = ['示例 · 山间旅行。这里可以放一张照片和当天的故事。', '示例 · 海边旅行。这里可以记录一起看过的风景。'];
    content.querySelectorAll('[data-memory]').forEach(pin => {
      pin.addEventListener('click', () => {
        content.querySelector('#memory').textContent = memories[Number(pin.dataset.memory)];
      });
    });
  } else if (id === 'gift') {
    content.innerHTML = '<p>桌上的礼物是给你的。</p><button id="unwrap">拆开礼物</button>';
    document.querySelector('#unwrap').addEventListener('click', () => {
      content.innerHTML = '<span class="gift-icon" aria-hidden="true">🎂</span><p>生日快乐！愿我们再一起去很多很多地方。</p>';
    });
  } else {
    content.innerHTML = '<p>它摇了摇尾巴，往你手心里蹭了蹭。</p>';
  }
  dialog.showModal();
}

window.addEventListener('keydown', event => {
  if (dialog.open || event.altKey || event.ctrlKey || event.metaKey) return;
  const key = event.key.length === 1 ? event.key.toLowerCase() : event.key;
  if (Object.hasOwn(directions, key)) {
    event.preventDefault();
    keys.add(key);
  }
  if (key === 'e' && !event.repeat) interact();
});
window.addEventListener('keyup', event => {
  keys.delete(event.key.length === 1 ? event.key.toLowerCase() : event.key);
});
window.addEventListener('blur', () => keys.clear());
document.querySelectorAll('[data-object]').forEach(button => {
  button.addEventListener('click', () => interact(button.dataset.object));
});
document.querySelectorAll('[data-key]').forEach(button => {
  button.addEventListener('pointerdown', event => {
    button.setPointerCapture(event.pointerId);
    keys.add(button.dataset.key);
  });
  for (const event of ['pointerup', 'pointercancel', 'lostpointercapture']) {
    button.addEventListener(event, () => keys.delete(button.dataset.key));
  }
});
document.querySelector('#interact').addEventListener('click', () => interact());

let previous = performance.now();
let walkingTime = 0;
let stride = 0;
let facing = 1;
let sideFacing = false;
let backFacing = false;
function tick(now) {
  const step = Math.min((now - previous) / 1000, .04) * .19;
  previous = now;
  let dx = 0;
  let dy = 0;
  for (const key of keys) {
    dx += directions[key][0];
    dy += directions[key][1];
  }
  const oldX = position.x;
  const oldY = position.y;
  const length = Math.hypot(dx, dy) || 1;
  const x = position.x + dx * step / length;
  const y = position.y + dy * step * ratio / length;
  if (isWalkable(x, position.y)) position.x = x;
  if (isWalkable(position.x, y)) position.y = y;
  const moving = oldX !== position.x || oldY !== position.y;
  if (dx) { facing = Math.sign(dx); sideFacing = true; backFacing = false; }
  else if (dy) { sideFacing = false; backFacing = dy < 0; }
  const seconds = step / .19;
  stride += ((moving ? 1 : 0) - stride) * Math.min(1, seconds * 12);
  if (stride < .001) { stride = 0; walkingTime = 0; }
  else walkingTime += seconds * 8;
  if (background.naturalWidth) {
    pixels.drawImage(background, 0, 0);
    const drawPlayer = drawWalkingPlayer;
    if (position.y < .64) drawPlayer();
    drawSprite(companion, .56, .64);
    if (position.y >= .64) drawPlayer();
  }
  const target = nearestObject();
  const label = target ? 'E · ' + objects[target].label : '';
  if (prompt.textContent !== label) prompt.textContent = label;
  requestAnimationFrame(tick);
}
requestAnimationFrame(tick);





function showWardrobe(managing) {
  content.replaceChildren();
  const toolbar = document.createElement('div');
  toolbar.className = 'wardrobe-toolbar';
  const grid = document.createElement('div');
  grid.className = 'wardrobe-grid';
  content.append(toolbar, grid);
  const selected = new Set();
  const addButton = (label, action) => {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = label;
    button.addEventListener('click', action);
    toolbar.append(button);
    return button;
  };
  addButton(managing ? '返回换装' : '批量管理', () => showWardrobe(!managing));
  if (managing) {
    const changeVisibility = shouldHide => {
      for (const file of selected) {
        if (shouldHide) hidden.add(file);
        else hidden.delete(file);
      }
      saveWardrobe();
      showWardrobe(true);
    };
    addButton('全选', () => {
      grid.querySelectorAll('input').forEach(input => { input.checked = true; });
      outfits.forEach(({ file }) => selected.add(file));
    });
    addButton('隐藏所选', () => changeVisibility(true));
    addButton('恢复所选', () => changeVisibility(false));
  }
  const available = outfits.filter(({ file }) => managing || !hidden.has(file));
  if (!available.length) grid.textContent = '衣橱里没有可选装扮。可在批量管理里恢复。';
  for (const outfit of available) {
    const card = document.createElement(managing ? 'label' : 'button');
    card.className = 'outfit-card';
    const preview = loadImage('outfits/' + outfit.file);
    preview.alt = '';
    const label = document.createElement('span');
    label.textContent = outfit.name + (hidden.has(outfit.file) ? ' · 已隐藏' : worn === outfit ? ' · 穿着中' : '');
    card.append(preview, label);
    if (managing) {
      const checkbox = document.createElement('input');
      checkbox.type = 'checkbox';
      checkbox.setAttribute('aria-label', outfit.name);
      checkbox.addEventListener('change', () => {
        if (checkbox.checked) selected.add(outfit.file);
        else selected.delete(outfit.file);
      });
      card.prepend(checkbox);
    } else {
      card.setAttribute('aria-pressed', String(worn === outfit));
      card.addEventListener('click', () => {
        worn = outfit;
        player = preview;
        sidePlayer = loadImage('outfits-side/' + worn.file);
        backPlayer = loadImage('outfits-back/' + worn.file);
        saveWardrobe();
        showWardrobe(false);
      });
    }
    grid.append(card);
  }
}
