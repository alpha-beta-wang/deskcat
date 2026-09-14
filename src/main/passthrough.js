const { ipcMain } = require('electron');

// A small transparent window is preferable to unreliable hit-testing here.
// On Windows, a non-focusable ignored-mouse overlay can fail to receive the
// forwarded hover event that would turn hit-testing back on. Keep this tiny
// window interactive so drag and the context menu always work.
function wirePassthrough(win) {
  win.setIgnoreMouseEvents(false);
}

module.exports = { wirePassthrough };
