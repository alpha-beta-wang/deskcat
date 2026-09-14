const { AnimationPlayer } = require('./animation-player.js');
const { Mover } = require('./mover.js');
const { createInputHandler } = require('./input-handler.js');
const { SPRITES, CLIPS } = require('./sprite-config.js');
const { ipcRenderer } = require('electron');

const PET_W = 180;
const canvas = document.getElementById('stage');
const ctx = canvas.getContext('2d');
ctx.imageSmoothingEnabled = true;

function resize() {
  canvas.width = window.innerWidth;
  canvas.height = window.innerHeight;
  mover.bounds.w = canvas.width;
  mover.bounds.h = canvas.height;
}

const images = {};
for (const [key, sheet] of Object.entries(SPRITES.sheets)) {
  const image = new Image();
  image.src = sheet.src;
  images[key] = image;
}

const mover = new Mover({
  x: Math.max(24, window.innerWidth - PET_W - 36),
  y: Math.max(24, window.innerHeight - 150),
  speed: 45,
  bounds: { w: window.innerWidth, h: window.innerHeight },
  footprint: { w: PET_W, h: 150 },
});
resize();
window.addEventListener('resize', resize);

const player = new AnimationPlayer(CLIPS);
let state = 'rest';
let mode = 'normal';
let actionTimer = 40 + Math.random() * 40;
let last = performance.now();
const inputQueue = [];

function currentClip() { return SPRITES.clips[state]; }
function getPetRect() {
  const clip = currentClip();
  return { x: mover.x, y: mover.y, w: clip.drawW, h: Math.min(clip.drawH, 170) };
}
function setState(next) {
  if (state === next) return;
  state = next;
  player.play(currentClip() ? next : 'rest');
}

player.play('rest');
createInputHandler(
  canvas,
  getPetRect,
  (event) => inputQueue.push(event),
  (over) => ipcRenderer.send('cat:hover', over),
);

ipcRenderer.on('cat:mode', (_event, nextMode) => {
  mode = nextMode;
  if (mode === 'quiet') setState('rest');
});

ipcRenderer.invoke('cat:settings').then((settings) => {
  mode = settings.mode || 'normal';
  if (settings.position && Number.isFinite(settings.position.x) && Number.isFinite(settings.position.y)) {
    mover.x = settings.position.x;
    mover.y = settings.position.y;
    mover._clamp();
  }
  if (mode === 'quiet') setState('rest');
});

function persistPosition() {
  ipcRenderer.send('cat:position', { x: Math.round(mover.x), y: Math.round(mover.y) });
}

function startNearbyWalk() {
  const target = mover.randomNearbyPoint(110);
  mover.setTarget(target.x, target.y);
  setState('walk');
}

function decideAutonomy(dt) {
  if (mode === 'quiet') {
    setState('rest');
    return;
  }
  if (state === 'walk' && mover.arrived()) {
    setState('rest');
    actionTimer = 40 + Math.random() * 40;
    persistPosition();
    return;
  }
  if (state === 'walk') return;
  actionTimer -= dt;
  if (actionTimer <= 0) {
    if (Math.random() < 0.55) startNearbyWalk();
    actionTimer = 40 + Math.random() * 40;
  }
}

function render() {
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  const clip = currentClip();
  const image = images[clip.sheet];
  if (!image.complete || image.naturalWidth === 0) return;
  const frameW = image.naturalWidth / (clip.sourceFrames || clip.frames);
  const frame = player.currentFrame();
  const sourceX = frame * frameW;
  ctx.save();
  if (state === 'walk' && mover.facing === 1) {
    ctx.translate(mover.x + clip.drawW, mover.y);
    ctx.scale(-1, 1);
    ctx.drawImage(image, sourceX, clip.cropY, frameW, clip.cropH, 0, 0, clip.drawW, clip.drawH);
  } else {
    ctx.drawImage(image, sourceX, clip.cropY, frameW, clip.cropH, mover.x, mover.y, clip.drawW, clip.drawH);
  }
  ctx.restore();
}

function frame(now) {
  const dt = Math.min(0.05, (now - last) / 1000);
  last = now;
  const input = inputQueue.shift();
  if (input) {
    if (input.type === 'drag-start') setState('rest');
    if (input.type === 'drag-move') {
      mover.x = input.x - PET_W / 2;
      mover.y = input.y - 60;
      mover._clamp();
    }
    if (input.type === 'drag-end') persistPosition();
    if (input.type === 'menu') ipcRenderer.send('cat:menu');
    if (input.type === 'pet') actionTimer = 40 + Math.random() * 40;
  }
  decideAutonomy(dt);
  if (state === 'walk') mover.update(dt);
  player.update(dt);
  render();
  requestAnimationFrame(frame);
}

requestAnimationFrame(frame);
