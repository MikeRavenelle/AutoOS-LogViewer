export type PressureUnit = "psi" | "kPa";
export type TempUnit = "°F" | "°C";
export type SpeedUnit = "mph" | "km/h";
export type LambdaUnit = "lambda" | "AFR";

export interface UnitPrefs {
  pressure: PressureUnit;
  temp: TempUnit;
  speed: SpeedUnit;
  lambda: LambdaUnit;
}

export const DEFAULT_UNIT_PREFS: UnitPrefs = {
  pressure: "psi",
  temp: "°F",
  speed: "mph",
  lambda: "lambda",
};

const PSI_PER_KPA = 6.894757;
const KMH_PER_MPH = 1.609344;
const STOICH_AFR_GASOLINE = 14.7;

const SCALE_CONVERSIONS: {
  baseUnit: string;
  targetUnit: string;
  factor: number;
  check: (p: UnitPrefs) => boolean;
}[] = [
  { baseUnit: "psi", targetUnit: "kPa", factor: PSI_PER_KPA, check: (p) => p.pressure === "kPa" },
  { baseUnit: "mph", targetUnit: "km/h", factor: KMH_PER_MPH, check: (p) => p.speed === "km/h" },
  { baseUnit: "lambda", targetUnit: "AFR", factor: STOICH_AFR_GASOLINE, check: (p) => p.lambda === "AFR" },
];

export function displayValue(value: number, baseUnit: string, prefs: UnitPrefs): { value: number; unit: string } {
  if (baseUnit === "°F" && prefs.temp === "°C") return { value: ((value - 32) * 5) / 9, unit: "°C" };
  for (const c of SCALE_CONVERSIONS) {
    if (baseUnit === c.baseUnit && c.check(prefs)) return { value: value * c.factor, unit: c.targetUnit };
  }
  return { value, unit: baseUnit };
}

export function displayDelta(delta: number, baseUnit: string, prefs: UnitPrefs): { value: number; unit: string } {
  if (baseUnit === "°F" && prefs.temp === "°C") return { value: (delta * 5) / 9, unit: "°C" };
  for (const c of SCALE_CONVERSIONS) {
    if (baseUnit === c.baseUnit && c.check(prefs)) return { value: delta * c.factor, unit: c.targetUnit };
  }
  return { value: delta, unit: baseUnit };
}

export function displayUnit(baseUnit: string, prefs: UnitPrefs): string {
  if (baseUnit === "°F" && prefs.temp === "°C") return "°C";
  for (const c of SCALE_CONVERSIONS) {
    if (baseUnit === c.baseUnit && c.check(prefs)) return c.targetUnit;
  }
  return baseUnit;
}

export function displayLabel(name: string, baseUnit: string, prefs: UnitPrefs): string {
  const unit = displayUnit(baseUnit, prefs);
  return unit ? `${name} (${unit})` : name;
}

export function formatValue(value: number, baseUnit: string, prefs: UnitPrefs, digits = 2): string {
  return displayValue(value, baseUnit, prefs).value.toFixed(digits);
}
