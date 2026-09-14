const { AnimationPlayer } = require('./animation-player.js');
const { Mover } = require('./mover.js');
const { createInputHandler } = require('./input-handler.js');
const { SPRITES, CLIPS } = require('./sprite-config.js');
const { ipcRenderer } = require('electron');

const PET_W = 180;
const WINDOW_W = 280;
const WINDOW_H = 240;
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
  x: Math.max(48, window.innerWidth - 250),
  y: Math.max(48, window.innerHeight - 190),
  speed: 45,
  bounds: { w: WINDOW_W, h: WINDOW_H },
  footprint: { w: PET_W, h: 150 },
});
resize();
window.addEventListener('resize', resize);

const player = new AnimationPlayer(CLIPS);
let state = 'rest';
let mode = 'normal';
let actionTimer = 40 + Math.random() * 40;
let blinkTimer = 4 + Math.random() * 4;
let blinkRemaining = 0;
let dragging = false;
let dragOffset = null;
let last = performance.now();
const inputQueue = [];
let lastDrawRect = null;

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

function handleInput(event) {
  // Drag messages bypass the low-idle-FPS queue: cursor movement must remain
  // responsive even while the pet is otherwise rendering economically.
  if (event.type === 'drag-start') {
    dragging = true;
    dragOffset = { x: event.x - mover.x, y: event.y - mover.y };
    setState('rest');
    return;
  }
  if (event.type === 'drag-move') {
    if (dragOffset) {
      mover.x = Math.max(0, Math.min(canvas.width - currentClip().drawW, event.x - dragOffset.x));
      mover.y = Math.max(0, Math.min(canvas.height - currentClip().drawH, event.y - dragOffset.y));
    }
    return;
  }
  if (event.type === 'drag-end') {
    dragging = false;
    dragOffset = null;
    return;
  }
  inputQueue.push(event);
}

player.play('rest');
createInputHandler(canvas, getPetRect, handleInput, (over) => ipcRenderer.send('cat:hover', over));

ipcRenderer.on('cat:mode', (_event, nextMode) => {
  mode = nextMode;
  if (mode === 'quiet') setState('rest');
});

ipcRenderer.invoke('cat:settings').then((settings) => {
  mode = settings.mode || 'normal';
  if (mode === 'quiet') setState('rest');
});

function startNearbyWalk() {
  const target = mover.randomNearbyPoint(70);
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
    return;
  }
  if (state === 'walk') return;
  actionTimer -= dt;
  if (actionTimer <= 0) {
    if (Math.random() < 0.55) startNearbyWalk();
    actionTimer = 40 + Math.random() * 40;
  }
}

function updateBlink(dt) {
  if (state !== 'rest' || dragging) {
    blinkRemaining = 0;
    return;
  }
  if (blinkRemaining > 0) {
    blinkRemaining = Math.max(0, blinkRemaining - dt);
    return;
  }
  blinkTimer -= dt;
  if (blinkTimer <= 0) {
    // 240ms total, cross-faded: enough to read as a blink without a visible frame swap.
    blinkRemaining = 0.36;
    blinkTimer = 7 + Math.random() * 5;
  }
}

function drawClip(image, clip, sourceX, destinationX, destinationY, alpha = 1) {
  const cropX = sourceX + (clip.cropX || 0);
  const cropW = clip.cropW || (image.naturalWidth / (clip.sourceFrames || clip.frames));
  ctx.globalAlpha = alpha;
  ctx.drawImage(image, cropX, clip.cropY, cropW, clip.cropH, destinationX, destinationY, clip.drawW, clip.drawH);
}

function render() {
  const clip = currentClip();
  const dirty = { x: mover.x - 4, y: mover.y - 4, w: clip.drawW + 8, h: clip.drawH + 8 };
  if (lastDrawRect) ctx.clearRect(lastDrawRect.x, lastDrawRect.y, lastDrawRect.w, lastDrawRect.h);
  ctx.clearRect(dirty.x, dirty.y, dirty.w, dirty.h);
  const image = images[clip.sheet];
  if (!image.complete || image.naturalWidth === 0) return;
  const frameW = image.naturalWidth / (clip.sourceFrames || clip.frames);
  const frame = player.currentFrame();
  const sourceX = frame * frameW;
  const blinkMix = state === 'rest' && blinkRemaining > 0
    ? (blinkRemaining > 0.18 ? (0.36 - blinkRemaining) / 0.18 : blinkRemaining / 0.18)
    : 0;
  ctx.save();
  if (state === 'walk' && mover.facing === 1) {
    ctx.translate(mover.x + clip.drawW, mover.y);
    ctx.scale(-1, 1);
    drawClip(image, clip, sourceX, 0, 0);
  } else {
    drawClip(image, clip, sourceX, mover.x, mover.y, 1 - blinkMix);
    if (blinkMix > 0) drawClip(image, clip, frameW, mover.x, mover.y, blinkMix);
  }
  ctx.restore();
  lastDrawRect = dirty;
}

function frame(now) {
  const dt = Math.min(0.05, (now - last) / 1000);
  last = now;
  const input = inputQueue.shift();
  if (input) {
    if (input.type === 'menu') ipcRenderer.send('cat:menu');
    // A simple click intentionally does not change pose or animation.
    if (input.type === 'pet') actionTimer = Math.max(actionTimer, 40);
  }
  decideAutonomy(dt);
  updateBlink(dt);
  if (state === 'walk') mover.update(dt);
  player.update(dt);
  render();
  // Temporarily raise the cadence while blinking so the cross-fade cannot be
  // skipped by the intentionally low idle frame rate.
  const delay = blinkRemaining > 0 ? 33 : (state === 'walk' ? 55 : 125);
  setTimeout(() => requestAnimationFrame(frame), delay);
}

requestAnimationFrame(frame);
