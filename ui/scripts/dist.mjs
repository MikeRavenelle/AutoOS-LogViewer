import { execFileSync, spawnSync } from "node:child_process";
import { existsSync, rmSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const uiDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const repoDir = path.resolve(uiDir, "..");
const engineHost = path.join(repoDir, "engine/src/Engine.Host");

const PLATFORMS = {
  mac: { rid: "osx-arm64", flag: "--mac" },
  win: { rid: "win-x64", flag: "--win" },
  linux: { rid: "linux-x64", flag: "--linux" },
};

const argv = process.argv.slice(2);
const picked = Object.keys(PLATFORMS).filter((p) => argv.includes(`--${p}`));
const targets = picked.length ? picked : Object.keys(PLATFORMS);
const skipTests = argv.includes("--skip-tests");

const brewDotnet = "/opt/homebrew/bin/dotnet";
const dotnet = existsSync(brewDotnet) ? brewDotnet : "dotnet";
const env = { ...process.env };
if (dotnet === brewDotnet) env.DOTNET_ROOT ??= "/opt/homebrew/opt/dotnet/libexec";

function step(title) {
  console.log(`\n\x1b[1;36m==> ${title}\x1b[0m`);
}

function run(cmd, args, opts = {}) {
  const r = spawnSync(cmd, args, { stdio: "inherit", cwd: uiDir, env, ...opts });
  if (r.status !== 0) {
    console.error(`\n\x1b[31mFailed: ${cmd} ${args.join(" ")}\x1b[0m`);
    process.exit(r.status ?? 1);
  }
}

function quiet(cmd, args) {
  try {
    return execFileSync(cmd, args, { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"], env });
  } catch {
    return null;
  }
}

if (targets.includes("linux") && quiet("which", ["rpmbuild"]) === null) {
  console.error("\x1b[31mrpmbuild not found (needed for the .rpm). Install it: brew install rpm\x1b[0m");
  process.exit(1);
}

if (!skipTests) {
  step("Engine tests");
  run(dotnet, ["test", path.join(repoDir, "engine")]);
  step("UI typecheck");
  run("npm", ["run", "typecheck"]);
}

step(`Publishing engine (${targets.map((t) => PLATFORMS[t].rid).join(", ")})`);
rmSync(path.join(uiDir, "engine-bin"), { recursive: true, force: true });
for (const t of targets) {
  run(dotnet, [
    "publish", engineHost,
    "-c", "Release",
    "-r", PLATFORMS[t].rid,
    "--self-contained",
    "-p:PublishSingleFile=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:DebugType=none",
    "-o", path.join(uiDir, "engine-bin", t),
  ]);
}

step("Building UI");
run("npm", ["run", "build"]);

const builderArgs = ["electron-builder", ...targets.map((t) => PLATFORMS[t].flag), "--publish", "never"];

if (targets.includes("mac")) {
  const identities = quiet("security", ["find-identity", "-v", "-p", "codesigning"]) ?? "";
  const devId = identities.match(/"(Developer ID Application: [^"]+)"/)?.[1];
  const profile = process.env.OLV_NOTARY_PROFILE ?? "OpenLogViewer";
  if (devId) {
    console.log(`macOS signing identity: ${devId}`);
    const notaryReady = quiet("xcrun", ["notarytool", "history", "--keychain-profile", profile]) !== null;
    if (notaryReady) {
      console.log(`macOS notarization: keychain profile "${profile}"`);
      env.APPLE_KEYCHAIN_PROFILE = profile;
      builderArgs.push("-c.mac.notarize=true");
    } else {
      console.warn(`\x1b[33mNo notarytool profile "${profile}" — signing without notarization.\x1b[0m`);
      builderArgs.push("-c.mac.notarize=false");
    }
  } else {
    console.warn(
      "\x1b[33mNo \"Developer ID Application\" identity in the keychain — building an ad-hoc signed .dmg\n" +
        "(runs on this Mac; other Macs need right-click > Open). See the header of scripts/dist.mjs.\x1b[0m",
    );
    env.CSC_IDENTITY_AUTO_DISCOVERY = "false";
    builderArgs.push("-c.mac.identity=-", "-c.mac.hardenedRuntime=false", "-c.mac.notarize=false");
  }
}

step("Packaging installers");
run("npx", builderArgs);

step("Done");
console.log(`Installers are in ${path.join(uiDir, "release")}`);
