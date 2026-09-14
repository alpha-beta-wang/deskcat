const { ipcMain } = require('electron');

// The fixed full-desktop overlay receives forwarded mouse moves. It captures
// clicks only while the cursor is over the cat; transparent desktop regions
// remain click-through.
function wirePassthrough(win) {
  win.setIgnoreMouseEvents(true, { forward: true });
  ipcMain.on('cat:hover', (_event, over) => {
    win.setIgnoreMouseEvents(!over, { forward: true });
  });
}

module.exports = { wirePassthrough };
