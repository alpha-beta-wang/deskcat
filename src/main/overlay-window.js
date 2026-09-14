const { BrowserWindow, screen } = require('electron');
const path = require('path');

function createOverlay(position) {
  const { workArea } = screen.getPrimaryDisplay();
  const win = new BrowserWindow({
    x: position?.x ?? (workArea.x + workArea.width - 300),
    y: position?.y ?? (workArea.y + workArea.height - 270),
    width: 280, height: 240,
    transparent: true,
    frame: false,
    resizable: false,
    movable: false,
    skipTaskbar: true,
    alwaysOnTop: true,
    focusable: true,
    hasShadow: false,
    webPreferences: {
      nodeIntegration: true,
      contextIsolation: false,
      autoplayPolicy: 'no-user-gesture-required', // let mood/ambient sounds play without a click
    },
  });
  win.setAlwaysOnTop(true, 'screen-saver');
  win.setSkipTaskbar(true);
  win.setVisibleOnAllWorkspaces(true);
  win.loadFile(path.join(__dirname, '../renderer/index.html'));
  return win;
}

module.exports = { createOverlay };
