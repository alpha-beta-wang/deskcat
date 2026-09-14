const { app, ipcMain } = require('electron');
const Store = require('electron-store');
const { createOverlay } = require('./overlay-window');
const { wirePassthrough } = require('./passthrough');
const { createTray, showPetMenu } = require('./tray');
const { applyAutostart } = require('./autostart');

const gotLock = app.requestSingleInstanceLock();
if (!gotLock) {
  app.quit();
} else {
  const store = new Store({ defaults: { muted: true, autostart: false, mode: 'normal', position: null } });
  let currentPosition = store.get('position');
  const getState = () => ({ muted: store.get('muted'), autostart: store.get('autostart'), mode: store.get('mode'), position: currentPosition });
  const setState = (patch) => {
    for (const [k, v] of Object.entries(patch)) store.set(k, v);
    if ('autostart' in patch) applyAutostart(patch.autostart);
    if ('mode' in patch && win) win.webContents.send('cat:mode', patch.mode);
    if (tray) tray.rebuild();
  };

  let win = null;
  let tray = null; // keep a reference so the tray isn't garbage-collected
  let dragAnchor = null;
  app.whenReady().then(() => {
    applyAutostart(store.get('autostart'));
    win = createOverlay(currentPosition);
    wirePassthrough(win);
    tray = createTray(win, getState, setState);
    ipcMain.handle('cat:settings', () => getState());
    ipcMain.on('cat:drag-start', (_event, point) => {
      if (!Number.isFinite(point?.screenX) || !Number.isFinite(point?.screenY)) return;
      const [x, y] = win.getPosition();
      dragAnchor = { screenX: point.screenX, screenY: point.screenY, x, y };
    });
    ipcMain.on('cat:drag-move', (_event, point) => {
      if (!dragAnchor || !Number.isFinite(point?.screenX) || !Number.isFinite(point?.screenY)) return;
      const x = Math.round(dragAnchor.x + point.screenX - dragAnchor.screenX);
      const y = Math.round(dragAnchor.y + point.screenY - dragAnchor.screenY);
      win.setPosition(x, y);
      currentPosition = { x, y };
    });
    ipcMain.on('cat:drag-end', () => {
      dragAnchor = null;
      store.set('position', currentPosition);
    });
    ipcMain.on('cat:menu', () => showPetMenu(win, getState, setState));
  });
  app.on('second-instance', () => { if (win) win.show(); });
  app.on('window-all-closed', () => app.quit());
}
