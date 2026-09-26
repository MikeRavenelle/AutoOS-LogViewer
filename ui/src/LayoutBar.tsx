import { useState } from "react";
import type { LayoutDto } from "./protocol";

export interface LayoutBarProps {
  layouts: LayoutDto[];
  onApply: (layout: LayoutDto) => void;
  onSave: (name: string) => void;
  onDelete: (name: string) => void;
}

export function LayoutBar({ layouts, onApply, onSave, onDelete }: LayoutBarProps) {
  const [selected, setSelected] = useState("");
  const [saving, setSaving] = useState(false);
  const [name, setName] = useState("");

  return (
    <span className="layout-bar">
      <select
        value={selected}
        onChange={(e) => {
          const layoutName = e.target.value;
          setSelected(layoutName);
          const layout = layouts.find((l) => l.name === layoutName);
          if (layout) onApply(layout);
        }}
      >
        <option value="">Layout…</option>
        {layouts.map((l) => (
          <option key={l.name} value={l.name}>
            {l.name}
          </option>
        ))}
      </select>
      {selected && (
        <button
          className="chip-x"
          title={`Delete layout "${selected}"`}
          onClick={() => {
            onDelete(selected);
            setSelected("");
          }}
        >
          ×
        </button>
      )}
      {saving ? (
        <form
          className="layout-save"
          onSubmit={(e) => {
            e.preventDefault();
            if (name.trim()) {
              onSave(name.trim());
              setSelected(name.trim());
              setName("");
              setSaving(false);
            }
          }}
        >
          <input
            autoFocus
            placeholder="Layout name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => e.key === "Escape" && setSaving(false)}
          />
          <button type="submit" disabled={!name.trim()}>
            Save
          </button>
        </form>
      ) : (
        <button className="secondary" onClick={() => setSaving(true)}>
          Save layout
        </button>
      )}
    </span>
  );
}
