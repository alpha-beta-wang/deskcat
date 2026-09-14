// Emits input events: {type:'drag-start'|'drag-move'|'drag-end'|'pet'|'play', x, y}
// getCatRect() returns the cat's current screen rect {x,y,w,h}.
function createInputHandler(canvas, getCatRect, emit, reportHover) {
  let dragging = false;
  let movedDuringDrag = false;
  const point = (e) => ({ x: e.clientX, y: e.clientY, screenX: e.screenX, screenY: e.screenY });

  const inCat = (e) => {
    const r = getCatRect();
    return e.clientX >= r.x && e.clientX <= r.x + r.w &&
           e.clientY >= r.y && e.clientY <= r.y + r.h;
  };

  canvas.addEventListener('mousemove', (e) => {
    reportHover(inCat(e));
    if (dragging) { movedDuringDrag = true; emit({ type: 'drag-move', ...point(e) }); }
  });

  canvas.addEventListener('mousedown', (e) => {
    if (!inCat(e)) return;
    dragging = true; movedDuringDrag = false;
    emit({ type: 'drag-start', ...point(e) });
  });

  window.addEventListener('mouseup', (e) => {
    if (!dragging) return;
    dragging = false;
    emit({ type: 'drag-end', ...point(e) });
    if (!movedDuringDrag) emit({ type: 'pet', ...point(e) }); // click w/o move = pet
  });

  canvas.addEventListener('dblclick', (e) => {
    if (inCat(e)) emit({ type: 'play', x: e.clientX, y: e.clientY });
  });

  canvas.addEventListener('contextmenu', (e) => {
    if (!inCat(e)) return;
    e.preventDefault();
    emit({ type: 'menu', x: e.clientX, y: e.clientY });
  });
}

module.exports = { createInputHandler };
