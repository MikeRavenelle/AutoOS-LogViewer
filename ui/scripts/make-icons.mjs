import { app, BrowserWindow } from "electron";
import { execFileSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { iconSvg } from "./icon-svg.mjs";

const buildDir = path.join(path.dirname(fileURLToPath(import.meta.url)), "../build");

async function rasterize(win, svg, sizes) {
  const url = `data:image/svg+xml;base64,${Buffer.from(svg).toString("base64")}`;
  const dataUrls = await win.webContents.executeJavaScript(`(async () => {
    const img = new Image();
    img.src = ${JSON.stringify(url)};
    await img.decode();
    return ${JSON.stringify(sizes)}.map((n) => {
      const c = document.createElement("canvas");
      c.width = c.height = n;
      const g = c.getContext("2d");
      g.imageSmoothingQuality = "high";
      g.drawImage(img, 0, 0, n, n);
      return c.toDataURL("image/png");
    });
  })()`);
  return new Map(sizes.map((n, i) => [n, Buffer.from(dataUrls[i].split(",")[1], "base64")]));
}

function buildIco(pngs) {
  const entries = [...pngs.entries()].sort((a, b) => a[0] - b[0]);
  const header = Buffer.alloc(6 + 16 * entries.length);
  header.writeUInt16LE(0, 0);
  header.writeUInt16LE(1, 2);
  header.writeUInt16LE(entries.length, 4);
  let offset = header.length;
  entries.forEach(([size, png], i) => {
    const e = 6 + 16 * i;
    header.writeUInt8(size >= 256 ? 0 : size, e);
    header.writeUInt8(size >= 256 ? 0 : size, e + 1);
    header.writeUInt16LE(1, e + 4);
    header.writeUInt16LE(32, e + 6);
    header.writeUInt32LE(png.length, e + 8);
    header.writeUInt32LE(offset, e + 12);
    offset += png.length;
  });
  return Buffer.concat([header, ...entries.map(([, png]) => png)]);
}

app.whenReady().then(async () => {
  const win = new BrowserWindow({ show: false });
  await win.loadURL("about:blank");

  mkdirSync(path.join(buildDir, "icons"), { recursive: true });
  writeFileSync(path.join(buildDir, "icon.svg"), iconSvg("full"));

  const mac = await rasterize(win, iconSvg("mac"), [16, 32, 64, 128, 256, 512, 1024]);
  const iconset = path.join(mkdtempSync(path.join(tmpdir(), "olv-icon-")), "icon.iconset");
  mkdirSync(iconset);
  for (const n of [16, 32, 128, 256, 512]) {
    writeFileSync(path.join(iconset, `icon_${n}x${n}.png`), mac.get(n));
    writeFileSync(path.join(iconset, `icon_${n}x${n}@2x.png`), mac.get(n * 2));
  }
  execFileSync("iconutil", ["-c", "icns", iconset, "-o", path.join(buildDir, "icon.icns")]);
  rmSync(path.dirname(iconset), { recursive: true, force: true });

  const full = await rasterize(win, iconSvg("full"), [16, 24, 32, 48, 64, 128, 256, 512, 1024]);
  writeFileSync(
    path.join(buildDir, "icon.ico"),
    buildIco(new Map([16, 24, 32, 48, 64, 128, 256].map((n) => [n, full.get(n)]))),
  );
  for (const n of [16, 32, 48, 64, 128, 256, 512, 1024]) {
    writeFileSync(path.join(buildDir, "icons", `${n}x${n}.png`), full.get(n));
  }
  writeFileSync(path.join(buildDir, "icon.png"), full.get(1024));

  console.log(`icons written to ${buildDir}`);
  app.quit();
});
