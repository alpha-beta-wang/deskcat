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
  const getState = () => ({ muted: store.get('muted'), autostart: store.get('autostart'), mode: store.get('mode'), position: store.get('position') });
  const setState = (patch) => {
    for (const [k, v] of Object.entries(patch)) store.set(k, v);
    if ('autostart' in patch) applyAutostart(patch.autostart);
    if ('mode' in patch && win) win.webContents.send('cat:mode', patch.mode);
    if (tray) tray.rebuild();
  };

  let win = null;
  let tray = null; // keep a reference so the tray isn't garbage-collected
  app.whenReady().then(() => {
    applyAutostart(store.get('autostart'));
    win = createOverlay(store.get('position'));
    wirePassthrough(win);
    tray = createTray(win, getState, setState);
    ipcMain.handle('cat:settings', () => getState());
    ipcMain.on('cat:window-position', (_event, position) => {
      if (!Number.isFinite(position?.x) || !Number.isFinite(position?.y)) return;
      win.setPosition(Math.round(position.x), Math.round(position.y));
      setState({ position: { x: Math.round(position.x), y: Math.round(position.y) } });
    });
    ipcMain.on('cat:menu', () => showPetMenu(win, getState, setState));
  });
  app.on('second-instance', () => { if (win) win.show(); });
  app.on('window-all-closed', () => app.quit());
}
