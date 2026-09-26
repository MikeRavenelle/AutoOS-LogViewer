import { useState } from "react";
import type { CustomMathChannelDto, MathPresetDto } from "./protocol";

export interface MathPanelProps {
  presets: MathPresetDto[];
  customChannels: CustomMathChannelDto[];
  inPane: Set<string>;
  error: string | null;
  showCustom: boolean;
  onToggleCustom: () => void;
  onTogglePreset: (preset: MathPresetDto) => void;
  onAddCustom: (name: string, unit: string, expression: string) => void;
  onDeleteCustom: (name: string) => void;
}

export function MathPanel({
  presets,
  customChannels,
  inPane,
  error,
  showCustom,
  onToggleCustom,
  onTogglePreset,
  onAddCustom,
  onDeleteCustom,
}: MathPanelProps) {
  const [name, setName] = useState("");
  const [unit, setUnit] = useState("");
  const [expression, setExpression] = useState("");

  return (
    <div className="math-panel">
      <div className="math-title">Math channels</div>
      <div className="math-presets">
        {presets.map((p) => (
          <button
            key={p.id}
            className={`preset${inPane.has(p.name) ? " selected" : ""}`}
            disabled={!p.available}
            title={
              p.available
                ? `${p.expression}\nClick to add/remove from the active pane`
                : `Unavailable in this log: ${p.expression}`
            }
            onClick={() => onTogglePreset(p)}
          >
            ƒ {p.name}
          </button>
        ))}
        <button className="preset custom-toggle" onClick={onToggleCustom}>
          {showCustom ? "− custom" : "+ custom"}
        </button>
      </div>
      {customChannels.length > 0 && (
        <ul className="math-custom-list">
          {customChannels.map((c) => (
            <li key={c.name} title={c.expression}>
              <span className="fx">ƒ</span> {c.name}
              <button
                className="chip-x"
                title={`Delete "${c.name}" — this removes it from every log, not just this one`}
                onClick={() => onDeleteCustom(c.name)}
              >
                ×
              </button>
            </li>
          ))}
        </ul>
      )}
      {showCustom && (
        <form
          className="math-form"
          onSubmit={(e) => {
            e.preventDefault();
            if (name.trim() && expression.trim()) onAddCustom(name.trim(), unit.trim(), expression.trim());
          }}
        >
          <input placeholder="Name" value={name} onChange={(e) => setName(e.target.value)} />
          <input placeholder="Unit (optional)" value={unit} onChange={(e) => setUnit(e.target.value)} />
          <textarea
            placeholder="[Boost Pressure Actual Sensor 1 (psi)] - [Turbocharger Inlet Pressure (psi)]"
            rows={3}
            value={expression}
            onChange={(e) => setExpression(e.target.value)}
          />
          <button type="submit" disabled={!name.trim() || !expression.trim()}>
            Add channel
          </button>
          <p className="math-form-hint">
            Saved globally — available on every log that has the channels it references, not just this one.
          </p>
        </form>
      )}
      {error && <div className="math-error">{error}</div>}
    </div>
  );
}
