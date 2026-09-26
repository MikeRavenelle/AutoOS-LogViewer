import type { AnnotationDto } from "./protocol";

export interface AnnotationsPanelProps {
  annotations: AnnotationDto[];
  onDelete: (id: string) => void;
}

export function AnnotationsPanel({ annotations, onDelete }: AnnotationsPanelProps) {
  if (annotations.length === 0) return null;

  return (
    <div className="annotations-panel">
      <div className="selection-stats-header">
        <span>Notes</span>
      </div>
      <ul className="annotations-list">
        {annotations.map((a) => (
          <li key={a.id}>
            <span className="annotation-time">{a.time.toFixed(2)}s</span>
            <span className="annotation-text">{a.text}</span>
            <button className="chip-x" title="Delete note" onClick={() => onDelete(a.id)}>
              ×
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
