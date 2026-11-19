'use strict';

const { app, BrowserWindow } = require('electron');
const path = require('node:path');

function openGame() {
  const window = new BrowserWindow({
    title: '我们的小屋',
    width: 1120,
    height: 760,
    minWidth: 540,
    minHeight: 420,
    backgroundColor: '#241715',
    autoHideMenuBar: true,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });

  window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  window.loadFile(path.join(__dirname, 'index.html'));
}

app.whenReady().then(() => {
  openGame();
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) openGame();
  });
});
app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});
