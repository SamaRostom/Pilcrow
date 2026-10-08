# Pilcrow — brand assets

## Colours

| Role | Hex | Use |
|---|---|---|
| Ink | `#0F2E3D` | Headings, body text, dark surfaces |
| Teal | `#2BB3A3` | Primary buttons, links, active states |
| Paper | `#F1F5F4` | Page background |
| White | `#FFFFFF` | Cards and surfaces |

Teal passes contrast against white for large text and graphics only. Never set body copy in teal.

## Files

Square logos, 1047×1047, background included:

| File | Use |
|---|---|
| `logo-white-on-teal.svg` | Primary. Strongest contrast, best at small sizes |
| `logo-ink-on-white.svg` | Documents, print, light UI |
| `logo-teal-on-ink.svg` | Dark surfaces |
| `logo-paper-on-ink.svg` | Dark surfaces, quieter |
| `logo-teal-on-paper.svg` | Light surfaces, lowest contrast — avoid below 64px |

Mark only, transparent:

| File | Use |
|---|---|
| `logo-mark-ink.svg` | 456×628, on light backgrounds |
| `logo-mark-teal.svg` | 456×628, on light backgrounds |
| `logo-mark-white.svg` | 456×628, on dark backgrounds |
| `mark-square-*.svg` | 760×760, same marks centred on a square for app icons and avatars |

Favicons: `favicon-512.png`, `favicon-180.png` (Apple touch icon), `favicon-32.png`.

## Notes

- The hairlines drop out below about 32px. A separate small-size version with thickened thin strokes and an opened counter is still needed for 16–24px UI.
- C2PA metadata was stripped from the SVGs, cutting each from ~10KB to ~2.5KB. Re-exporting from the design tool will reintroduce it.
