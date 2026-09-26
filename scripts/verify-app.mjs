#!/usr/bin/env node

import { spawn } from "node:child_process";
import { writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const args = process.argv.slice(2);
const opt = (name, fallback = null) => {
  const i = args.indexOf(`--${name}`);
  return i >= 0 ? args[i + 1] : fallback;
};
const has = (name) => args.includes(`--${name}`);

const uiDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../ui");
const port = Number(opt("port", "9333"));
const logPath = opt("log");
const shotPath = opt("shot", "/tmp/olv-verify.png");
const waitFor = opt("wait-for", ".u-over");
const evalExprs = [];
for (let i = 0; i < args.length; i++) if (args[i] === "--eval") evalExprs.push(args[i + 1]);

if (!logPath) {
  console.error("--log <fixture path> is required");
  process.exit(2);
}

const env = { ...process.env, OLV_OPEN_LOG: path.resolve(logPath) };
if (opt("compare")) env.OLV_COMPARE_LOG = path.resolve(opt("compare"));

const appExe = opt("app");
if (appExe) {
  delete env.DOTNET_ROOT;
  env.PATH = "/usr/bin:/bin";
}
const electron = spawn(
  appExe ?? path.join(uiDir, "node_modules/.bin/electron"),
  appExe ? [`--remote-debugging-port=${port}`] : [".", `--remote-debugging-port=${port}`],
  { cwd: uiDir, env, stdio: ["ignore", "pipe", "pipe"] },
);
electron.stderr.on("data", () => {});
const wait = (ms) => new Promise((r) => setTimeout(r, ms));

let exitCode = 1;
try {
  let page = null;
  for (let i = 0; i < 60 && !page; i++) {
    try {
      const targets = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
      page = targets.find((t) => t.type === "page") ?? null;
    } catch {}
    if (!page) await wait(500);
  }
  if (!page) throw new Error("app never exposed a CDP page target");

  const ws = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    ws.onopen = resolve;
    ws.onerror = () => reject(new Error("CDP websocket failed"));
  });
  const pending = new Map();
  let id = 0;
  ws.onmessage = (e) => {
    const m = JSON.parse(e.data);
    if (m.id && pending.has(m.id)) {
      const p = pending.get(m.id);
      pending.delete(m.id);
      m.error ? p.reject(new Error(JSON.stringify(m.error))) : p.resolve(m.result);
    }
  };
  const cdp = (method, params = {}) =>
    new Promise((resolve, reject) => {
      pending.set(++id, { resolve, reject });
      ws.send(JSON.stringify({ id, method, params }));
    });
  const evalJs = async (expr) => {
    const r = await cdp("Runtime.evaluate", { expression: expr, returnByValue: true, awaitPromise: true });
    if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? "eval failed");
    return r.result.value;
  };

  let ready = false;
  for (let i = 0; i < 60 && !ready; i++) {
    ready = await evalJs(`!!document.querySelector(${JSON.stringify(waitFor)})`);
    if (!ready) await wait(500);
  }
  if (!ready) throw new Error(`wait-for selector never appeared: ${waitFor}`);
  await wait(1000);

  for (const expr of evalExprs) {
    console.log(`eval> ${expr}`);
    console.log(JSON.stringify(await evalJs(expr), null, 1));
  }

  const shot = await cdp("Page.captureScreenshot", { format: "png" });
  writeFileSync(shotPath, Buffer.from(shot.data, "base64"));
  console.log(`screenshot: ${shotPath}`);
  console.log("VERIFY OK");
  exitCode = 0;
} catch (err) {
  console.error(`VERIFY FAILED: ${err.message}`);
} finally {
  if (!has("keep")) electron.kill();
  process.exit(exitCode);
}
