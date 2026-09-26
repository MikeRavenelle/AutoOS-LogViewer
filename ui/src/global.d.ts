interface OlvBridge {
  engineUrl(): Promise<string>;
  openLogDialog(): Promise<string | null>;
  initialLogPath(): Promise<string | null>;
  initialComparePath(): Promise<string | null>;
  saveTextFile(defaultName: string, content: string): Promise<boolean>;
  saveChartPng(
    defaultName: string,
    rect: { x: number; y: number; width: number; height: number },
  ): Promise<boolean>;
  pathForDroppedFile(file: File): string;
}

interface Window {
  olv: OlvBridge;
}
