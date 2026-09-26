import { app, BrowserWindow, dialog, ipcMain } from "electron";
import { spawn, type ChildProcess } from "node:child_process";
import { existsSync, writeFileSync } from "node:fs";
import path from "node:path";

let engine: ChildProcess | null = null;
let engineUrl: Promise<string> | null = null;
let mainWindow: BrowserWindow | null = null;

function findDotnet(): string {
  const candidates = [
    process.env.OLV_DOTNET,
    "/opt/homebrew/bin/dotnet",
    "/usr/local/share/dotnet/dotnet",
    "/usr/bin/dotnet",
  ].filter((p): p is string => !!p);
  for (const p of candidates) if (existsSync(p)) return p;
  return "dotnet";
}

function engineDllPath(): string {
  if (process.env.OLV_ENGINE_DLL) return process.env.OLV_ENGINE_DLL;
  return path.join(
    __dirname,
    "../../engine/src/Engine.Host/bin/Debug/net10.0/OpenLogViewer.Engine.Host.dll",
  );
}

function engineCommand(): { cmd: string; args: string[]; env: NodeJS.ProcessEnv } {
  if (app.isPackaged) {
    const exe = process.platform === "win32" ? "OpenLogViewer.Engine.Host.exe" : "OpenLogViewer.Engine.Host";
    return { cmd: path.join(process.resourcesPath, "engine", exe), args: [], env: process.env };
  }
  return {
    cmd: findDotnet(),
    args: [engineDllPath()],
    env: { ...process.env, DOTNET_ROOT: process.env.DOTNET_ROOT ?? "/opt/homebrew/opt/dotnet/libexec" },
  };
}

function startEngine(): Promise<string> {
  const { cmd, args, env } = engineCommand();
  const entry = args[0] ?? cmd;
  if (!existsSync(entry)) {
    return Promise.reject(
      new Error(
        app.isPackaged
          ? `Engine missing from install: ${entry}`
          : `Engine not built: ${entry}\nRun: dotnet build engine`,
      ),
    );
  }

  engine = spawn(cmd, args, { env, stdio: ["ignore", "pipe", "pipe"], windowsHide: true });
  engine.stderr?.on("data", (d: Buffer) => console.error(`[engine] ${d}`));

  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error("Engine start timed out")), 20_000);
    let buffered = "";
    engine!.stdout?.on("data", (d: Buffer) => {
      buffered += d.toString();
      const m = buffered.match(/ENGINE_READY (\S+)/);
      if (m) {
        clearTimeout(timer);
        resolve(m[1]);
      }
    });
    engine!.on("exit", (code) => {
      clearTimeout(timer);
      reject(new Error(`Engine exited early (code ${code})`));
    });
  });
}

function createWindow(): void {
  const win = new BrowserWindow({
    width: 1400,
    height: 900,
    backgroundColor: "#111417",
    webPreferences: {
      preload: path.join(__dirname, "preload.cjs"),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });
  mainWindow = win;
  win.on("closed", () => {
    if (mainWindow === win) mainWindow = null;
  });

  const devUrl = process.env.VITE_DEV_SERVER_URL;
  if (devUrl) {
    void win.loadURL(devUrl);
  } else {
    void win.loadFile(path.join(__dirname, "../dist/index.html"));
  }
}

app.whenReady().then(() => {
  engineUrl = startEngine();
  engineUrl.catch((err) => {
    dialog.showErrorBox("OpenLogViewer engine failed to start", String(err?.message ?? err));
  });

  ipcMain.handle("engine:url", () => engineUrl);
  ipcMain.handle("initialLogPath", () => process.env.OLV_OPEN_LOG ?? null);
  ipcMain.handle("initialComparePath", () => process.env.OLV_COMPARE_LOG ?? null);
  ipcMain.handle("dialog:openLog", async () => {
    const { canceled, filePaths } = await dialog.showOpenDialog({
      title: "Open datalog",
      filters: [
        { name: "Datalogs (CSV, MLG)", extensions: ["csv", "mlg"] },
        { name: "All files", extensions: ["*"] },
      ],
      properties: ["openFile"],
    });
    return canceled ? null : filePaths[0];
  });
  ipcMain.handle("dialog:saveText", async (_e, defaultName: string, content: string) => {
    const { canceled, filePath } = await dialog.showSaveDialog({
      title: "Export CSV",
      defaultPath: defaultName,
      filters: [{ name: "CSV", extensions: ["csv"] }],
    });
    if (canceled || !filePath) return false;
    writeFileSync(filePath, content, "utf-8");
    return true;
  });
  ipcMain.handle(
    "dialog:savePng",
    async (_e, defaultName: string, rect: { x: number; y: number; width: number; height: number }) => {
      if (!mainWindow) return false;
      const image = await mainWindow.webContents.capturePage(rect);
      const { canceled, filePath } = await dialog.showSaveDialog({
        title: "Export chart image",
        defaultPath: defaultName,
        filters: [{ name: "PNG", extensions: ["png"] }],
      });
      if (canceled || !filePath) return false;
      writeFileSync(filePath, image.toPNG());
      return true;
    },
  );

  createWindow();
  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") app.quit();
});

app.on("will-quit", () => {
  engine?.kill();
  engine = null;
});

for (const sig of ["SIGINT", "SIGTERM"] as const) {
  process.on(sig, () => {
    engine?.kill();
    engine = null;
    app.exit(0);
  });
}
