// AI-assisted sprite sheets made from the user's Mochi photo references.
// Each sheet is horizontal; crops remove the deliberately generous transparent padding.
const SPRITES = {
  sheets: {
    walk: { src: '../../assets/cats/mochi-walk.png' },
    rest: { src: '../../assets/cats/mochi-rest.png' },
  },
  clips: {
    // The second source cell is only used for a brief cross-faded blink in loop.js.
    rest: { sheet: 'rest', frames: 1, sourceFrames: 2, fps: 1, loop: true, cropY: 155, cropH: 560, drawW: 180, drawH: 114 },
    // One clean, wide full-body stride avoids the narrow, shape-shifting generated sheet.
    walk: { sheet: 'walk', frames: 1, fps: 1, loop: true, cropX: 70, cropY: 140, cropW: 1460, cropH: 760, drawW: 180, drawH: 94 },
  },
};

// AnimationPlayer only needs {frames, fps, loop} per clip:
const CLIPS = Object.fromEntries(
  Object.entries(SPRITES.clips).map(([k, v]) => [k, { frames: v.frames, fps: v.fps, loop: v.loop }])
);

module.exports = { SPRITES, CLIPS };
