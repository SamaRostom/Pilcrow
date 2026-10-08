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

App icons — rounded square, mark at 48% of the side:

| File | Use |
|---|---|
| `appicon-teal.svg` | Primary. README header, app icon, favicon source |
| `appicon-ink.svg` | Dark alternative |

Full-bleed logos — rounded corners, mark fills the square:

| File | Use |
|---|---|
| `logo-white-on-teal.svg` | Strongest contrast |
| `logo-ink-on-white.svg` | Documents, print, light UI |
| `logo-teal-on-ink.svg` | Dark surfaces |
| `logo-paper-on-ink.svg` | Dark surfaces, quieter |
| `logo-teal-on-paper.svg` | Light surfaces, lowest contrast — avoid below 64px |

Mark only, transparent, 456×628:

| File | Use |
|---|---|
| `logo-mark-ink.svg` | On light backgrounds |
| `logo-mark-teal.svg` | On light backgrounds |
| `logo-mark-white.svg` | On dark backgrounds |

Favicons: `favicon-512.png`, `favicon-180.png` (Apple touch icon), `favicon-32.png`.

## Notes

- Corner radius is 22.37% of the side, the macOS app-icon ratio.
- The hairlines drop out below about 32px. A small-size variant with thickened thin strokes and an opened counter is still needed for 16–24px UI.
- C2PA metadata was stripped from the SVGs, cutting each from ~10KB to ~2.7KB. Re-exporting from the design tool will reintroduce it.
