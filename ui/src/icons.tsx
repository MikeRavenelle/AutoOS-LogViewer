import type { SVGProps } from "react";

export type IconName =
  | "folder"
  | "clock"
  | "line"
  | "bars"
  | "scatter"
  | "play"
  | "compare"
  | "grid"
  | "save"
  | "export"
  | "eye"
  | "warn"
  | "lock"
  | "boost"
  | "fuel"
  | "engine"
  | "can"
  | "ignition"
  | "electrical"
  | "misc"
  | "system"
  | "sun"
  | "moon"
  | "chevron"
  | "close"
  | "reset";

function Svg(props: SVGProps<SVGSVGElement>) {
  return (
    <svg
      viewBox="0 0 20 20"
      width="1em"
      height="1em"
      stroke="currentColor"
      strokeWidth={1.6}
      strokeLinecap="round"
      strokeLinejoin="round"
      fill="none"
      aria-hidden="true"
      {...props}
    />
  );
}

export function Icon({ name, className }: { name: IconName; className?: string }) {
  switch (name) {
    case "folder":
      return (
        <Svg className={className}>
          <path d="M2 6.5V5a1 1 0 0 1 1-1h4l1.5 2H17a1 1 0 0 1 1 1v1M2 6.5 3.2 15a1 1 0 0 0 1 .8h9.6a1 1 0 0 0 1-.8L18 6.5H2Z" />
        </Svg>
      );
    case "clock":
      return (
        <Svg className={className}>
          <circle cx="10" cy="10" r="7.3" />
          <path d="M10 6.2v4l2.6 1.5" />
        </Svg>
      );
    case "line":
      return (
        <Svg className={className}>
          <path d="M2.5 15.2 7 9l3 3 5.5-7" />
          <path d="M2.5 17.3h15" strokeWidth={1.3} />
        </Svg>
      );
    case "bars":
      return (
        <Svg className={className}>
          <path d="M4 16v-6M10 16V4M16 16v-8" />
          <path d="M2.5 17.3h15" strokeWidth={1.3} />
        </Svg>
      );
    case "scatter":
      return (
        <Svg className={className}>
          <circle cx="5" cy="14" r="1.05" fill="currentColor" stroke="none" />
          <circle cx="9.3" cy="7.2" r="1.05" fill="currentColor" stroke="none" />
          <circle cx="13" cy="11.2" r="1.05" fill="currentColor" stroke="none" />
          <circle cx="16" cy="5" r="1.05" fill="currentColor" stroke="none" />
          <path d="M2.5 17.3h15" strokeWidth={1.3} />
        </Svg>
      );
    case "play":
      return (
        <Svg className={className}>
          <circle cx="10" cy="10" r="7.3" />
          <path d="M8.3 7 13 10l-4.7 3V7Z" fill="currentColor" stroke="none" />
        </Svg>
      );
    case "compare":
      return (
        <Svg className={className}>
          <rect x="2.3" y="3.6" width="9.6" height="9.6" rx="1.4" />
          <rect x="8" y="7" width="9.6" height="9.6" rx="1.4" />
        </Svg>
      );
    case "grid":
      return (
        <Svg className={className}>
          <rect x="2.4" y="2.4" width="6.4" height="6.4" rx="1" />
          <rect x="11.2" y="2.4" width="6.4" height="6.4" rx="1" />
          <rect x="2.4" y="11.2" width="6.4" height="6.4" rx="1" />
          <rect x="11.2" y="11.2" width="6.4" height="6.4" rx="1" />
        </Svg>
      );
    case "save":
      return (
        <Svg className={className}>
          <path d="M4 2.6h9.2l4.2 4.2V17.4H4V2.6Z" />
          <path d="M6.6 2.6v4.6h6.4V2.6M6.6 12.4h6.8v5H6.6Z" />
        </Svg>
      );
    case "export":
      return (
        <Svg className={className}>
          <path d="M10 2.6v9M6.6 8.6 10 12l3.4-3.4" />
          <path d="M3 13v3.2a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V13" />
        </Svg>
      );
    case "eye":
      return (
        <Svg className={className}>
          <path d="M2 10s3-5.4 8-5.4S18 10 18 10s-3 5.4-8 5.4S2 10 2 10Z" />
          <circle cx="10" cy="10" r="2.1" />
        </Svg>
      );
    case "warn":
      return (
        <Svg className={className}>
          <path d="M10 3 18 17H2L10 3Z" />
          <path d="M10 8v4M10 14.4h.01" strokeWidth={1.9} />
        </Svg>
      );
    case "lock":
      return (
        <Svg className={className}>
          <rect x="4.5" y="9" width="11" height="7.6" rx="1.4" />
          <path d="M6.6 9V6.8a3.4 3.4 0 0 1 6.8 0V9" />
        </Svg>
      );
    case "boost":
      return (
        <Svg className={className}>
          <path d="M4 16 9 4l1.5 5H16l-6 9-1-6H4Z" />
        </Svg>
      );
    case "fuel":
      return (
        <Svg className={className}>
          <path d="M5 17V6a1 1 0 0 1 1-1h5a1 1 0 0 1 1 1v11M4 17h9M13 8.5h1.3L16 10v4.2a1 1 0 0 1-2 0" />
        </Svg>
      );
    case "engine":
      return (
        <Svg className={className}>
          <circle cx="10" cy="10" r="3.2" />
          <path d="M10 3v2M10 15v2M17 10h-2M5 10H3M14.8 5.2l-1.4 1.4M6.6 13.4l-1.4 1.4M14.8 14.8l-1.4-1.4M6.6 6.6 5.2 5.2" />
        </Svg>
      );
    case "can":
      return (
        <Svg className={className}>
          <rect x="2.5" y="5" width="15" height="10" rx="1.6" />
          <path d="M6 9.5h1.6M9.5 9.5h1.6M13 9.5h1.6" />
        </Svg>
      );
    case "ignition":
      return (
        <Svg className={className}>
          <path d="M11 2.5 4.5 11.5H9l-1 6L15.5 8.5H11l.5-6Z" />
        </Svg>
      );
    case "electrical":
      return (
        <Svg className={className}>
          <path d="M4 4h9l-3 5h6l-9 9 2-7H4l3-7Z" />
        </Svg>
      );
    case "misc":
      return (
        <Svg className={className}>
          <circle cx="5.5" cy="10" r="1.15" fill="currentColor" stroke="none" />
          <circle cx="10" cy="10" r="1.15" fill="currentColor" stroke="none" />
          <circle cx="14.5" cy="10" r="1.15" fill="currentColor" stroke="none" />
        </Svg>
      );
    case "system":
      return (
        <Svg className={className}>
          <rect x="2.5" y="3" width="15" height="10" rx="1.5" />
          <path d="M7 17h6M10 13v4" />
        </Svg>
      );
    case "sun":
      return (
        <Svg className={className}>
          <circle cx="10" cy="10" r="3.4" />
          <path d="M10 2.5v2M10 15.5v2M17.5 10h-2M4.5 10h-2M15.3 4.7l-1.4 1.4M6.1 13.9l-1.4 1.4M15.3 15.3l-1.4-1.4M6.1 6.1 4.7 4.7" />
        </Svg>
      );
    case "moon":
      return (
        <Svg className={className}>
          <path d="M16.5 11.8A6.8 6.8 0 0 1 8.2 3.5a6.8 6.8 0 1 0 8.3 8.3Z" />
        </Svg>
      );
    case "chevron":
      return (
        <Svg className={className}>
          <path d="M6 8l4 4 4-4" />
        </Svg>
      );
    case "close":
      return (
        <Svg className={className}>
          <path d="M5 5l10 10M15 5 5 15" />
        </Svg>
      );
    case "reset":
      return (
        <Svg className={className}>
          <path d="M4 10a6 6 0 1 1 1.8 4.3M4 10V5.5M4 10h4.5" />
        </Svg>
      );
  }
}
