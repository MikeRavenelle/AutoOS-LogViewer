# OpenLogViewer
[![License: MIT](https://shields.io)](https://opensource.org/licenses/MIT)

FOSS cross-platform automotive datalog viewer, an open-source competitor to
MegaLogViewer HD and other paid CSV viewers. Desktop-first (Electron),
browser-capable later.

**Architecture:** a C# .NET engine owns all state, parsing, decimation, and
computation, running as a child process behind a localhost WebSocket; the
Electron + React UI is a dumb renderer that sends intent (paths, viewports,
expressions) and draws what the engine returns. See `docs/protocol.md`.

## Status

Base features are implemented and verified against real ECU Connect
logs:

* Parse, plot, zoom/pan end to end
* Multi-pane plotting, multiple channels per pane, synchronized cursor
*  Math channels (expression parser + shipped presets)
* Two-log overlay comparison with anchor offset
* Histogram/table view (any channel vs any two axes)
* Layout profiles (save/load named pane layouts)

## Prerequisites

- .NET SDK 8+ (arm64-native on Apple Silicon; `brew install dotnet`)
- Node.js 20+ (`brew install node`)

## Run

```sh
cd ui
npm install
npm start        # builds engine + renderer + electron main, launches the app
```

Dev conveniences: `OLV_OPEN_LOG=/path/to/log.csv` auto-opens a log at launch;
`OLV_COMPARE_LOG=...` auto-opens a comparison log; `OLV_ENGINE_DLL=...`
points at a non-default engine build.

## Test / typecheck

```sh
dotnet test engine          # engine unit + dispatcher tests
cd ui && npm run typecheck  # strict TS
```

When protocol contracts change in
`engine/src/Engine.Host/Protocol/Messages.cs`, regenerate the TS mirror:

```sh
cd ui && npm run gen:protocol
```

## Layout

```
engine/   .NET solution: Engine.Core (parsing, decimation, expressions,
          histograms), Engine.Host (WebSocket host + protocol), tests,
          tools/ProtocolGen (C# -> TS type generation)
ui/       Electron + React + TypeScript renderer (uPlot traces, ECharts
          histograms)
docs/     protocol.md (wire semantics), other-viewer-features.md
fixtures/ real ECU Connect logs (untracked; supply your own locally —
          engine tests read from here)
```

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.


