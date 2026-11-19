'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const Zip = require('adm-zip');

const runtime = process.argv[2];
if (!runtime) throw new Error('Pass the official Electron darwin-arm64 ZIP path.');

const project = path.resolve(__dirname, '../..');
const game = path.join(__dirname, 'game');
const assets = path.join(project, '图像素材');
const output = path.join(project, '下载包', '我们的小屋-Mac-M芯片.zip');
const zip = new Zip(runtime);
const appBundle = zip.getEntries().find(entry => /^[^/]+\.app\/Contents\/MacOS\/Electron$/.test(entry.entryName));
assert(appBundle, 'Electron application bundle missing.');
const executable = appBundle.getData();
assert.equal(executable.readUInt32LE(0), 0xfeedfacf, 'Expected a Mach-O executable.');
assert.equal(executable.readUInt32LE(4), 0x0100000c, 'Expected the Apple Silicon runtime.');

const originalBundle = appBundle.entryName.split('/')[0];
const bundle = '我们的小屋.app';
for (const entry of zip.getEntries()) {
  if (entry.entryName.startsWith(originalBundle + '/')) {
    entry.entryName = bundle + entry.entryName.slice(originalBundle.length);
  }
}
const originalEntries = zip.getEntries().map(entry => [entry.entryName, entry.attr]);
const resources = bundle + '/Contents/Resources/app/';
for (const file of ['main.cjs', 'package.json']) {
  zip.addFile(resources + file, fs.readFileSync(path.join(__dirname, file)));
}
for (const file of ['index.html', 'style.css', 'game.js']) {
  zip.addFile(resources + file, fs.readFileSync(path.join(game, file)));
}
const images = {
  'room.png': ['房间', 'room-modern-game.png'],
  'map.png': ['房间', 'map-game.png'],
  'player.png': ['人物', 'player-game.png'],
  'companion.png': ['人物', 'companion-game.png'],
};
for (const [file, segments] of Object.entries(images)) {
  zip.addFile(resources + 'assets/' + file, fs.readFileSync(path.join(assets, ...segments)));
}
const outfitDir = path.join(assets, '人物', '游戏装扮');
const outfitFiles = fs.readdirSync(outfitDir).filter(file => /^\d\d-.+\.png$/u.test(file)).sort();
assert(outfitFiles.length, 'Wardrobe library is empty.');
for (const file of outfitFiles) {
  zip.addFile(resources + 'assets/outfits/' + file, fs.readFileSync(path.join(outfitDir, file)));
  zip.addFile(resources + 'assets/outfits-side/' + file, fs.readFileSync(path.join(assets, '人物', '侧面装扮', file)));
  zip.addFile(resources + 'assets/outfits-back/' + file, fs.readFileSync(path.join(assets, '人物', '背面装扮', file)));
}
const profiles = Object.fromEntries(fs.readFileSync(path.join(project, '代码', 'walk-profiles.csv'), 'utf8').trim().split(/\r?\n/).slice(1).map(line => {
  const [id, kind, root, left, right, travel, lift] = line.split(',');
  return [id, { kind, root: +root, left: +left, right: +right, travel: +travel, lift: +lift }];
}));
for (const file of outfitFiles) assert(profiles[file.slice(0, 2)], 'Missing walk profile: ' + file);
zip.addFile(resources + 'outfits.js', Buffer.from('window.wardrobeOutfits = ' + JSON.stringify(outfitFiles) + ';window.walkProfiles = ' + JSON.stringify(profiles) + ';'));
zip.writeZip(output);
const packaged = new Zip(output);
for (const [name, attributes] of originalEntries) {
  assert.equal(packaged.getEntry(name).attr, attributes, 'Archive mode changed: ' + name);
}
assert.equal(JSON.parse(packaged.readAsText(resources + 'package.json')).main, 'main.cjs');
assert(packaged.getEntry(resources + 'assets/room.png'));
console.log(output);





