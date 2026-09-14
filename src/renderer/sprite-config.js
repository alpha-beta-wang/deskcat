// AI-assisted sprite sheets made from the user's Mochi photo references.
// Each sheet is horizontal; crops remove the deliberately generous transparent padding.
const SPRITES = {
  sheets: {
    walk: { src: '../../assets/cats/mochi-walk.png' },
    rest: { src: '../../assets/cats/mochi-rest.png' },
  },
  clips: {
    // Keep the quiet pose intentionally static: separately generated blink frames looked like a hard cut.
    rest: { sheet: 'rest', frames: 1, sourceFrames: 2, fps: 1, loop: true, cropY: 155, cropH: 560, drawW: 180, drawH: 114 },
    // A smaller walking footprint keeps the upright pose from visually "popping" bigger than the lying pose.
    walk: { sheet: 'walk', frames: 6, fps: 7, loop: true, cropY: 115, cropH: 460, drawW: 140, drawH: 178 },
  },
};

// AnimationPlayer only needs {frames, fps, loop} per clip:
const CLIPS = Object.fromEntries(
  Object.entries(SPRITES.clips).map(([k, v]) => [k, { frames: v.frames, fps: v.fps, loop: v.loop }])
);

module.exports = { SPRITES, CLIPS };
