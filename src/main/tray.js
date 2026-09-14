const { Tray, Menu, app } = require('electron');
const path = require('path');

// getState() -> { muted, autostart }; setState(patch) persists changes.
function createTray(win, getState, setState) {
  const tray = new Tray(path.join(__dirname, '../../assets/tray-icon.png'));
  function rebuild() {
    const state = getState();
    tray.setContextMenu(Menu.buildFromTemplate([
      { label: 'Show Mochi', click: () => win.show() },
      { label: 'Normal mode', type: 'radio', checked: state.mode === 'normal', click: () => setState({ mode: 'normal' }) },
      { label: 'Quiet mode', type: 'radio', checked: state.mode === 'quiet', click: () => setState({ mode: 'quiet' }) },
      { type: 'separator' },
      { label: state.muted ? 'Enable sound' : 'Mute sound', click: () => setState({ muted: !state.muted }) },
      { label: 'Start at login', type: 'checkbox', checked: state.autostart, click: (item) => setState({ autostart: item.checked }) },
      { type: 'separator' },
      { label: 'Quit', click: () => app.quit() },
    ]));
  }
  tray.setToolTip('Mochi');
  rebuild();
  return { tray, rebuild };
}

function showPetMenu(win, getState, setState) {
  const state = getState();
  Menu.buildFromTemplate([
    { label: 'Mochi', enabled: false },
    { type: 'separator' },
    { label: 'Normal mode', type: 'radio', checked: state.mode === 'normal', click: () => setState({ mode: 'normal' }) },
    { label: 'Quiet mode', type: 'radio', checked: state.mode === 'quiet', click: () => setState({ mode: 'quiet' }) },
    { type: 'separator' },
    { label: 'Hide', click: () => win.hide() },
    { label: 'Quit', click: () => app.quit() },
  ]).popup({ window: win });
}

module.exports = { createTray, showPetMenu };
