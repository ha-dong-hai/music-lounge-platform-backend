// Signature MusicLounge motif -- a small hand-tuned soundwave glyph (5 bars, deliberately
// asymmetric heights, not a generic "equalizer" icon pulled from a library) that sits next to the
// wordmark in every shell (OwnerHeader, AdminSidebar). Added 2026-08-17 per docs/stitch/Stitch-Master-Brief.md
// Design Execution Principles #8 -- a recurring detail specific to this brand reads as "designed
// for MusicLounge," not "generated for any app," which Phosphor icons alone can't carry.
// Used in exactly one place per shell (restraint, same rule as color/glass/motion) -- not a
// decorative pattern repeated across the page.
export default function BrandMark({ className = "" }: { className?: string }) {
  const bars = [0.45, 0.75, 1, 0.6, 0.35];
  return (
    <svg
      viewBox="0 0 24 16"
      className={className}
      width="20"
      height="14"
      fill="none"
      aria-hidden="true"
    >
      {bars.map((h, i) => (
        <rect
          key={i}
          x={i * 5.5}
          y={(16 - 16 * h) / 2}
          width="3"
          height={16 * h}
          rx="1.5"
          fill="currentColor"
        />
      ))}
    </svg>
  );
}
