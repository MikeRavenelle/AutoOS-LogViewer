export type ThemePref = "system" | "light" | "dark";

const STORAGE_KEY = "olv-theme";

export function loadThemePref(): ThemePref {
  const v = localStorage.getItem(STORAGE_KEY);
  return v === "light" || v === "dark" || v === "system" ? v : "system";
}

export function storeThemePref(pref: ThemePref): void {
  localStorage.setItem(STORAGE_KEY, pref);
}

export function applyThemePref(pref: ThemePref): void {
  if (pref === "system") document.documentElement.removeAttribute("data-theme");
  else document.documentElement.setAttribute("data-theme", pref);
}

export function resolveIsDark(pref: ThemePref): boolean {
  if (pref === "dark") return true;
  if (pref === "light") return false;
  return window.matchMedia?.("(prefers-color-scheme: dark)").matches ?? true;
}

export function watchResolvedTheme(pref: ThemePref, cb: (isDark: boolean) => void): () => void {
  if (pref !== "system" || !window.matchMedia) return () => {};
  const mql = window.matchMedia("(prefers-color-scheme: dark)");
  const handler = () => cb(mql.matches);
  mql.addEventListener("change", handler);
  return () => mql.removeEventListener("change", handler);
}

export const CHART_THEME = {
  dark: {
    axisStroke: "#9aa4ad",
    gridStroke: "#242a30",
    splitLine: "#1d232a",
    tooltipBg: "#1c2126",
    tooltipBorder: "#2c333a",
    tooltipText: "#d7dde3",
    seriesLabel: "#e8edf2",
    itemBorder: "#111417",
    itemBorderEmphasis: "#d7dde3",
    scatterFallback: "#4cc2ff",
    gradient: ["#20406b", "#2563eb", "#4cc2ff", "#7ee787", "#ffd33d", "#ffb454", "#ff7b72"],
  },
  light: {
    axisStroke: "#6b7280",
    gridStroke: "#d5d9e0",
    splitLine: "#e4e7ec",
    tooltipBg: "#ffffff",
    tooltipBorder: "#d5d9e0",
    tooltipText: "#1b1e24",
    seriesLabel: "#1b1e24",
    itemBorder: "#ffffff",
    itemBorderEmphasis: "#1b1e24",
    scatterFallback: "#3b62c9",
    gradient: ["#8fb3f0", "#3b62c9", "#0e8d7d", "#2f7a52", "#b5901a", "#b5691a", "#b8402f"],
  },
} as const;
