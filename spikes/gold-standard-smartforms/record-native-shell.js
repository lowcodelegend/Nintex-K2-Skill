'use strict';

const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');
const WebSocket = require('ws');
const ffmpegPath = require('@ffmpeg-installer/ffmpeg').path;

const args = process.argv.slice(2);
function option(name, fallback) {
  const index = args.indexOf(`--${name}`);
  return index >= 0 && args[index + 1] ? args[index + 1] : fallback;
}

const outputDirectory = path.resolve(option(
  'output',
  '.artifacts/gold-standard-smartforms/runtime-video'
));
const width = Number(option('width', '1440'));
const height = Number(option('height', '900'));
const port = Number(option('port', '9990'));
const edgePath = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const trustedHost = 'spk2.trials.demome.tech';
const commandUrl = 'https://spk2.trials.demome.tech/Runtime/Runtime/Form/GUX.Gold%20Command%20Centre/';
const allowedRoot = path.resolve('.artifacts/gold-standard-smartforms');
const framesDirectory = path.join(outputDirectory, 'frames');
const profileDirectory = path.join(outputDirectory, '.edge-video-profile');
const videoPath = path.join(outputDirectory, 'gux-page-load-and-transition.mp4');
const metadataPath = path.join(outputDirectory, 'gux-page-load-and-transition.json');
const concatPath = path.join(framesDirectory, 'frames.txt');

if (
  outputDirectory !== allowedRoot &&
  !outputDirectory.startsWith(`${allowedRoot}${path.sep}`)
) {
  throw new Error(`Output must stay under ${allowedRoot}.`);
}

fs.mkdirSync(outputDirectory, { recursive: true });
for (const disposable of [framesDirectory, profileDirectory]) {
  const resolved = path.resolve(disposable);
  if (!resolved.startsWith(`${outputDirectory}${path.sep}`)) {
    throw new Error(`Unsafe disposable path: ${resolved}`);
  }
  fs.rmSync(resolved, { recursive: true, force: true });
}
fs.mkdirSync(framesDirectory, { recursive: true });

const delay = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));
const recordingStarted = Date.now();
const frames = [];
const timeline = [];
const diagnostics = [];
let edge;
let cdp;

class Cdp {
  constructor(url) {
    this.socket = new WebSocket(url);
    this.nextId = 0;
    this.pending = new Map();
    this.ready = new Promise((resolve, reject) => {
      this.socket.once('open', resolve);
      this.socket.once('error', reject);
    });
    this.socket.on('message', data => this.receive(JSON.parse(data.toString())));
  }

  receive(message) {
    if (message.id) {
      const pending = this.pending.get(message.id);
      if (!pending) return;
      this.pending.delete(message.id);
      if (message.error) pending.reject(new Error(message.error.message));
      else pending.resolve(message.result);
      return;
    }

    if (message.method === 'Page.screencastFrame') {
      const index = frames.length;
      const fileName = `frame-${String(index).padStart(5, '0')}.jpg`;
      fs.writeFileSync(path.join(framesDirectory, fileName), Buffer.from(message.params.data, 'base64'));
      frames.push({
        fileName,
        timestamp: Number(message.params.metadata.timestamp || 0)
      });
      this.notify('Page.screencastFrameAck', {
        sessionId: message.params.sessionId
      });
      return;
    }

    if (message.method === 'Runtime.exceptionThrown') {
      diagnostics.push(message.params.exceptionDetails.text || 'Runtime exception');
    } else if (message.method === 'Log.entryAdded') {
      diagnostics.push(message.params.entry.text || 'Browser log entry');
    }
  }

  async send(method, params = {}) {
    await this.ready;
    const id = ++this.nextId;
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.socket.send(JSON.stringify({ id, method, params }));
    });
  }

  notify(method, params = {}) {
    if (this.socket.readyState !== WebSocket.OPEN) return;
    const id = ++this.nextId;
    this.socket.send(JSON.stringify({ id, method, params }));
  }

  close() {
    if (this.socket.readyState === WebSocket.OPEN) this.socket.close();
  }
}

async function waitForEndpoint() {
  for (let attempt = 0; attempt < 100; attempt += 1) {
    try {
      const response = await fetch(`http://127.0.0.1:${port}/json/version`);
      if (response.ok) return;
    } catch (_) {}
    await delay(100);
  }
  throw new Error('Edge DevTools endpoint did not start.');
}

async function evaluate(expression) {
  const result = await cdp.send('Runtime.evaluate', {
    expression,
    returnByValue: true,
    awaitPromise: true
  });
  if (result.exceptionDetails) {
    throw new Error(result.exceptionDetails.text || 'Runtime evaluation failed.');
  }
  return result.result.value;
}

async function waitForPage(title, timeout = 20000) {
  const started = Date.now();
  while (Date.now() - started < timeout) {
    try {
      const state = await evaluate(`({
        title: document.title,
        url: location.href,
        ready: !!document.body && document.body.classList.contains('gux-ready'),
        shellVersion: document.body && document.body.getAttribute('data-gux-version')
      })`);
      if (state.title === title && state.ready) return state;
    } catch (_) {}
    await delay(80);
  }
  throw new Error(`Timed out waiting for ${title}.`);
}

function phase(name, details = {}) {
  timeline.push({
    name,
    elapsedMilliseconds: Date.now() - recordingStarted,
    ...details
  });
}

function escapeConcatName(fileName) {
  return fileName.replace(/'/g, "'\\''");
}

function encodeVideo() {
  if (frames.length < 2) throw new Error(`Only ${frames.length} screencast frame(s) were captured.`);

  const lines = [];
  for (let index = 0; index < frames.length; index += 1) {
    const frame = frames[index];
    lines.push(`file '${escapeConcatName(frame.fileName)}'`);
    let duration = 1 / 30;
    if (index < frames.length - 1) {
      const measured = frames[index + 1].timestamp - frame.timestamp;
      if (Number.isFinite(measured) && measured > 0) {
        duration = Math.min(Math.max(measured, 1 / 60), 1.5);
      }
    } else {
      duration = 1;
    }
    lines.push(`duration ${duration.toFixed(6)}`);
  }
  lines.push(`file '${escapeConcatName(frames[frames.length - 1].fileName)}'`);
  fs.writeFileSync(concatPath, `${lines.join('\n')}\n`, 'utf8');

  return new Promise((resolve, reject) => {
    const encoder = spawn(ffmpegPath, [
      '-y',
      '-hide_banner',
      '-loglevel', 'error',
      '-f', 'concat',
      '-safe', '0',
      '-i', concatPath,
      '-vf', 'fps=30,format=yuv420p',
      '-c:v', 'libx264',
      '-preset', 'medium',
      '-crf', '20',
      '-movflags', '+faststart',
      '-an',
      videoPath
    ], {
      cwd: framesDirectory,
      windowsHide: true,
      stdio: ['ignore', 'ignore', 'pipe']
    });
    let errorText = '';
    encoder.stderr.on('data', chunk => { errorText += chunk.toString(); });
    encoder.on('error', reject);
    encoder.on('exit', code => {
      if (code === 0) resolve();
      else reject(new Error(`FFmpeg exited ${code}: ${errorText.trim()}`));
    });
  });
}

async function run() {
  edge = spawn(edgePath, [
    '--headless=new',
    '--disable-gpu',
    '--no-first-run',
    `--remote-debugging-port=${port}`,
    `--user-data-dir=${profileDirectory}`,
    `--auth-server-allowlist=${trustedHost}`,
    `--auth-negotiate-delegate-allowlist=${trustedHost}`,
    'about:blank'
  ], {
    windowsHide: true,
    stdio: 'ignore'
  });

  await waitForEndpoint();
  const targetResponse = await fetch(
    `http://127.0.0.1:${port}/json/new?${encodeURIComponent('about:blank')}`,
    { method: 'PUT' }
  );
  if (!targetResponse.ok) throw new Error(`Could not create Edge target: ${targetResponse.status}.`);
  const target = await targetResponse.json();
  cdp = new Cdp(target.webSocketDebuggerUrl);
  await cdp.ready;
  await cdp.send('Page.enable');
  await cdp.send('Runtime.enable');
  await cdp.send('Log.enable');
  await cdp.send('Emulation.setDeviceMetricsOverride', {
    width,
    height,
    deviceScaleFactor: 1,
    mobile: false,
    screenWidth: width,
    screenHeight: height
  });
  await cdp.send('Page.startScreencast', {
    format: 'jpeg',
    quality: 88,
    maxWidth: width,
    maxHeight: height,
    everyNthFrame: 1
  });

  phase('recording-started');
  await delay(30);
  phase('command-navigation-start');
  await cdp.send('Page.navigate', { url: commandUrl });
  const commandState = await waitForPage('GUX.Gold Command Centre');
  phase('command-ready', commandState);
  await delay(1400);

  const clickResult = await evaluate(`(function () {
    var link = document.querySelector('[data-gux-code="MY_WORK"]');
    if (!link) return { clicked: false };
    link.click();
    return {
      clicked: true,
      curtainVisible: document.body.classList.contains('gux-leaving')
    };
  })()`);
  if (!clickResult.clicked) throw new Error('My Work navigation link was not found.');
  phase('my-work-clicked', clickResult);

  const myWorkState = await waitForPage('GUX.My Work');
  phase('my-work-ready', myWorkState);
  await delay(1600);
  phase('recording-ended');
  await cdp.send('Page.stopScreencast');
  await delay(200);
  await encodeVideo();

  const metadata = {
    recordedUtc: new Date().toISOString(),
    width,
    height,
    frameCount: frames.length,
    video: path.basename(videoPath),
    videoBytes: fs.statSync(videoPath).size,
    timeline,
    diagnostics
  };
  fs.writeFileSync(metadataPath, JSON.stringify(metadata, null, 2), 'utf8');
  process.stdout.write(`${JSON.stringify(metadata, null, 2)}\n`);
}

run()
  .catch(error => {
    process.stderr.write(`${error.stack || error.message}\n`);
    process.exitCode = 1;
  })
  .finally(async () => {
    try {
      if (cdp) cdp.close();
    } catch (_) {}
    try {
      if (edge && !edge.killed) edge.kill();
    } catch (_) {}
    await delay(200);
    try {
      const resolved = path.resolve(profileDirectory);
      if (resolved.startsWith(`${outputDirectory}${path.sep}`)) {
        fs.rmSync(resolved, { recursive: true, force: true });
      }
    } catch (_) {}
    try {
      const resolved = path.resolve(framesDirectory);
      if (fs.existsSync(videoPath) && resolved.startsWith(`${outputDirectory}${path.sep}`)) {
        fs.rmSync(resolved, { recursive: true, force: true });
      }
    } catch (_) {}
  });
