---
name: translating-css-to-uss
description: Use when building or styling Unity UI Toolkit interfaces (UXML/USS files), or when authoring or porting an HTML/CSS design mock destined for Unity (including frontend-design skill output) — read before writing the first line of USS or mock CSS.
---

# Translating CSS to USS (Unity UI Toolkit)

## Overview

USS is a narrow CSS subset running on the Yoga flexbox engine. Design **inside
the subset from the start**: a mock authored in full CSS needs redesign to
port; one authored inside the budget ports mechanically. Verified against
Unity 6000.x docs (this project: 6000.5).

## Constraint budget

If it's not in the "Have" column, it does not exist in USS. HTML mocks
intended for Unity must also stay inside this table.

| Area | Have | Don't have → instead |
|---|---|---|
| Layout | Flexbox only (Yoga); `position: relative\|absolute` | No grid/float/`sticky` → nest flex containers |
| **Flex default** | **`flex-direction: column`** — not `row` like the web | Set `row` explicitly |
| Units | `px`, `%`; `deg`; `s`/`ms` | No `em`/`rem`/`vw`/`vh`, no `calc()` → px scale tokens as `--vars` |
| Selectors | Type (`Button`), `.class`, `#name`, descendant, `>`, chained pseudo-classes | No sibling/attribute/`:nth-*` → toggle classes from C# |
| Pseudo-classes | `:hover :active :inactive :focus :disabled :enabled :checked :root` | No `:selected` (use `:checked`), no `::before`/`::after` |
| Variables | `--name` + `var()`; theme (`.tss`) files | — |
| Runtime theming | Swap the PanelSettings theme asset (`.tss`); palettes as var-only token stylesheets | No writing USS custom properties from C# → preset themes, not arbitrary runtime colors |
| Fills | Solid colors; per-side `border-*`; `border-radius`; `opacity`; `background-image` (texture/sprite/VectorImage); `background-size/position/repeat`; 9-slice (`-unity-slice-*`) | No gradients, `box-shadow`, `filter`, blend modes → bake into a texture/sprite asset |
| Text | `font-size`, `color`, `letter-spacing`, `word-spacing`, `text-shadow`, `text-overflow`, `white-space`, `-unity-font-style` (normal/bold/italic), `-unity-text-align`, `-unity-text-outline` | No `font-weight: <n>` → Font Asset per weight; no `text-transform` → case the string itself; no `text-align` → `-unity-text-align` |
| Motion | `transition-*` fired by class toggles; `translate:`, `rotate:`, `scale:`, `transform-origin` as **separate properties** | No `@keyframes`, no `transform:` shorthand → orchestrate in C# (`schedule.Execute`) |
| Stacking | Paint order = child order; `BringToFront()` | No `z-index` |
| Responsive | `%` + flex; `GeometryChangedEvent` callback; theme stylesheets | No media queries |
| Scroll/overflow | `overflow: hidden\|visible`; `ScrollView` element | No `overflow: scroll\|auto` |
| Display | `display: flex\|none`; `visibility: hidden` | No `inline`/`block`/`grid` values |
| Assets | Font Assets, imported textures/sprites/SVG-as-VectorImage via `url()`/`resource()` | No web fonts, no external URLs |

## HTML → UXML element map

Root boilerplate: `<ui:UXML xmlns:ui="UnityEngine.UIElements">…</ui:UXML>`.
Text is never bare content — it lives in a `text` attribute.

| HTML | UXML |
|---|---|
| `<div>` | `<ui:VisualElement>` |
| `<p>` / `<span>` / `<h*>` | `<ui:Label text="…">` |
| `<button>` | `<ui:Button text="…">` |
| `<input type="text">` | `<ui:TextField>` |
| checkbox | `<ui:Toggle>` |
| `<select>` | `<ui:DropdownField>` |
| `<input type="range">` | `<ui:Slider>` |
| `<img>` | `<ui:Image>` or `background-image` on a VisualElement |
| scrolling region | `<ui:ScrollView>` |
| long/dynamic list | `<ui:ListView>` (virtualized — never hand-roll) |
| component/partial | `<ui:Template src="…">` + `<ui:Instance>` |

## Working with frontend-design output

The frontend-design skill's process (token plan, signature element, critique
passes) is medium-agnostic — run it as written. Its **outputs** map as:

1. **Palette hexes + type roles** → USS `--vars` in one theme/tokens
   stylesheet; one Font Asset per type role *and weight*. Component USS
   consumes `var()` only — no raw hex outside the tokens file.
2. **HTML eyeball mocks** must be authored inside the constraint budget
   (flex-only, px/%, flat fills). Where the design wants a gradient, glow, or
   shadow, list it as a **texture/sprite asset to produce** — don't fake it in
   CSS the port can't keep.
3. **Motion plan** → state micro-interactions as USS transitions on class
   toggles (`EnableInClassList` from C#); orchestrated sequences in C#.
4. **Signature element** — consider whether it belongs in world-space
   rendering rather than UI chrome (see scope below).

## Project conventions

- **Scope**: UI Toolkit covers HUD, panels, and inspector chrome only. The
  atlas scene look (2.5D perspective, glows, billboards) is world-space
  rendering — never rebuild it out of styled VisualElements.
- **Naming**: BEM (Unity's own recommendation), prefixed `ssg-`:
  `.ssg-panel__header--collapsed`. Never restyle `.unity-*` built-in classes
  globally; add your own class beside them.
- **State**: C# toggles classes; USS owns all appearance and transitions.

## Common mistakes

- Layout stacks vertically "for no reason" → you forgot the **column default**.
- `transform: translate(4px)` silently does nothing → separate `translate:` property.
- `text-align` / `font-weight` silently do nothing → `-unity-text-align` / font asset or `-unity-font-style`.
- List perf collapses at scale → hand-rolled children instead of `ListView`.
- Styles look right in UI Builder, wrong at runtime → check the PanelSettings theme (`.tss`) in play mode, not just the Builder's default theme.
