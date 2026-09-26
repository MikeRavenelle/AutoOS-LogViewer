import type { Command, ErrorDto, HelloPayload, RequestPayloads, ResultTypes } from "./protocol";

interface Pending {
  resolve: (value: unknown) => void;
  reject: (reason: Error) => void;
}

export class EngineRequestError extends Error {
  constructor(public readonly code: string, message: string) {
    super(message);
  }
}

export class EngineClient {
  private ws: WebSocket | null = null;
  private nextId = 1;
  private readonly pending = new Map<number, Pending>();

  hello: HelloPayload | null = null;
  onclose: (() => void) | null = null;

  async connect(url: string): Promise<void> {
    const ws = new WebSocket(url);
    this.ws = ws;

    ws.onmessage = (e: MessageEvent<string>) => this.handleMessage(e.data);
    ws.onclose = () => {
      for (const p of this.pending.values())
        p.reject(new EngineRequestError("disconnected", "Engine connection closed"));
      this.pending.clear();
      this.onclose?.();
    };

    await new Promise<void>((resolve, reject) => {
      ws.onopen = () => resolve();
      ws.onerror = () => reject(new Error(`Cannot connect to engine at ${url}`));
    });
  }

  request<C extends Command>(cmd: C, payload: RequestPayloads[C]): Promise<ResultTypes[C]> {
    const ws = this.ws;
    if (!ws || ws.readyState !== WebSocket.OPEN)
      return Promise.reject(new EngineRequestError("disconnected", "Engine not connected"));

    const id = this.nextId++;
    const promise = new Promise<ResultTypes[C]>((resolve, reject) => {
      this.pending.set(id, { resolve: resolve as (v: unknown) => void, reject });
    });
    ws.send(JSON.stringify({ id, cmd, payload }));
    return promise;
  }

  private handleMessage(raw: string): void {
    const msg = JSON.parse(raw) as
      | { event: string; payload: unknown; id?: undefined }
      | { id: number; ok: true; result: unknown }
      | { id: number; ok: false; error: ErrorDto };

    if (msg.id === undefined) {
      if (msg.event === "hello") this.hello = msg.payload as HelloPayload;
      return;
    }

    const p = this.pending.get(msg.id);
    if (!p) return;
    this.pending.delete(msg.id);
    if (msg.ok) p.resolve(msg.result);
    else p.reject(new EngineRequestError(msg.error.code, msg.error.message));
  }
}
