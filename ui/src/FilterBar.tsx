import type { ChannelDto, FilterDto } from "./protocol";

export interface FilterBarProps {
  channels: ChannelDto[];
  filters: FilterDto[];
  onChange: (filters: FilterDto[]) => void;
}

const OPS: { value: FilterDto["op"]; label: string }[] = [
  { value: "gt", label: ">" },
  { value: "gte", label: "≥" },
  { value: "lt", label: "<" },
  { value: "lte", label: "≤" },
  { value: "eq", label: "=" },
  { value: "neq", label: "≠" },
];

export function FilterBar({ channels, filters, onChange }: FilterBarProps) {
  const addFilter = () => {
    const channelId = channels[0]?.id ?? 0;
    onChange([...filters, { channelId, op: "gt", value: 90 }]);
  };

  const updateFilter = (index: number, patch: Partial<FilterDto>) => {
    onChange(filters.map((f, i) => (i === index ? { ...f, ...patch } : f)));
  };

  const removeFilter = (index: number) => {
    onChange(filters.filter((_, i) => i !== index));
  };

  return (
    <div className="filter-bar">
      {filters.map((f, i) => (
        <span className="filter-row" key={i}>
          <select value={f.channelId} onChange={(e) => updateFilter(i, { channelId: Number(e.target.value) })}>
            {channels.map((c) => (
              <option key={c.id} value={c.id}>
                {(c.computed ? "ƒ " : "") + c.name + (c.unit ? ` (${c.unit})` : "")}
              </option>
            ))}
          </select>
          <select value={f.op} onChange={(e) => updateFilter(i, { op: e.target.value as FilterDto["op"] })}>
            {OPS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
          <input
            type="number"
            value={f.value}
            onChange={(e) => updateFilter(i, { value: Number(e.target.value) || 0 })}
          />
          <button className="chip-x" title="Remove filter" onClick={() => removeFilter(i)}>
            ×
          </button>
        </span>
      ))}
      <button className="secondary filter-add" onClick={addFilter}>
        + Filter
      </button>
    </div>
  );
}
