import { contextBridge, ipcRenderer, webUtils } from "electron";

contextBridge.exposeInMainWorld("olv", {
  engineUrl: (): Promise<string> => ipcRenderer.invoke("engine:url"),
  openLogDialog: (): Promise<string | null> => ipcRenderer.invoke("dialog:openLog"),
  initialLogPath: (): Promise<string | null> => ipcRenderer.invoke("initialLogPath"),
  initialComparePath: (): Promise<string | null> => ipcRenderer.invoke("initialComparePath"),
  saveTextFile: (defaultName: string, content: string): Promise<boolean> =>
    ipcRenderer.invoke("dialog:saveText", defaultName, content),
  saveChartPng: (
    defaultName: string,
    rect: { x: number; y: number; width: number; height: number },
  ): Promise<boolean> => ipcRenderer.invoke("dialog:savePng", defaultName, rect),
  pathForDroppedFile: (file: File): string => webUtils.getPathForFile(file),
});
