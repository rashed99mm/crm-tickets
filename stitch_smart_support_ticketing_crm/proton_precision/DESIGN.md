---
name: Proton Precision
colors:
  surface: '#f8f9ff'
  surface-dim: '#cbdbf5'
  surface-bright: '#f8f9ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#eff4ff'
  surface-container: '#e5eeff'
  surface-container-high: '#dce9ff'
  surface-container-highest: '#d3e4fe'
  on-surface: '#0b1c30'
  on-surface-variant: '#45464d'
  inverse-surface: '#213145'
  inverse-on-surface: '#eaf1ff'
  outline: '#76777d'
  outline-variant: '#c6c6cd'
  surface-tint: '#565e74'
  primary: '#000000'
  on-primary: '#ffffff'
  primary-container: '#131b2e'
  on-primary-container: '#7c839b'
  inverse-primary: '#bec6e0'
  secondary: '#515f74'
  on-secondary: '#ffffff'
  secondary-container: '#d5e3fd'
  on-secondary-container: '#57657b'
  tertiary: '#000000'
  on-tertiary: '#ffffff'
  tertiary-container: '#001e2f'
  on-tertiary-container: '#008cc7'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dae2fd'
  primary-fixed-dim: '#bec6e0'
  on-primary-fixed: '#131b2e'
  on-primary-fixed-variant: '#3f465c'
  secondary-fixed: '#d5e3fd'
  secondary-fixed-dim: '#b9c7e0'
  on-secondary-fixed: '#0d1c2f'
  on-secondary-fixed-variant: '#3a485c'
  tertiary-fixed: '#c9e6ff'
  tertiary-fixed-dim: '#89ceff'
  on-tertiary-fixed: '#001e2f'
  on-tertiary-fixed-variant: '#004c6e'
  background: '#f8f9ff'
  on-background: '#0b1c30'
  surface-variant: '#d3e4fe'
typography:
  display:
    fontFamily: Hanken Grotesk
    fontSize: 36px
    fontWeight: '700'
    lineHeight: 44px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Hanken Grotesk
    fontSize: 28px
    fontWeight: '600'
    lineHeight: 34px
    letterSpacing: -0.01em
  headline-md:
    fontFamily: Hanken Grotesk
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  body-lg:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  body-sm:
    fontFamily: Inter
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 18px
  label-md:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.05em
  data-mono:
    fontFamily: JetBrains Mono
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 20px
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  base: 4px
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 32px
  2xl: 48px
  gutter: 20px
  margin: 24px
---

## Brand & Style

The design system is engineered for high-efficiency CRM and ticketing environments where data density and clarity are paramount. The brand personality is authoritative, systematic, and dependable, designed to instill a sense of calm control during high-pressure support scenarios.

The visual style follows **Modern Corporate Minimalism** with a focus on functional utility. It avoids unnecessary decorative elements, favoring a rigorous grid, subtle tonal shifts for hierarchy, and a refined color application that directs attention to actionable data. The interface prioritizes the "Information First" principle, ensuring that agents can scan, identify, and resolve issues with minimal cognitive load.

## Colors

The palette is anchored by **Deep Blue** (Primary) to convey stability and professional rigor. **Slate Grays** (Neutral) provide the structural framework for the UI, used for borders, secondary text, and background layering.

- **Primary (#0F172A):** Used for global navigation, primary buttons, and heavy headings.
- **Accents:** Use **Cyan-Blue (#0EA5E9)** for interactive elements like links and active states to provide a clear visual affordance without the weight of the primary color.
- **Semantic Colors:** Reserved strictly for status and priority.
    - **Critical/High:** A sharp Red for urgent attention.
    - **Warning/Medium:** A warm Amber for pending tasks.
    - **Success/Low:** A grounded Green for resolved or low-priority items.
- **Surface Tones:** Use a range of very light grays (Slate 50 to 200) to differentiate sidebar, content area, and inspector panels.

## Typography

This design system utilizes a dual-font strategy to balance character and readability. 

- **Hanken Grotesk** is used for headlines and dashboard metrics. Its sharp, contemporary geometry provides a professional "tech-forward" feel.
- **Inter** is the workhorse for all body copy, inputs, and ticket descriptions. It is selected for its exceptional legibility at small sizes and high X-height.
- **JetBrains Mono** is introduced for technical data strings, ticket IDs, and timestamps, allowing for quick character recognition in dense tables.

For mobile, scale `display` down to 24px and `headline-lg` to 20px. Ensure `body-md` remains at 14px to maintain legibility for long-form ticket notes.

## Layout & Spacing

The design system employs a **Fixed-Fluid Hybrid Grid**. Global navigation and sidebars (Inspector) are fixed-width to ensure tool accessibility, while the central workspace is fluid to maximize data visibility on ultrawide monitors.

- **Rhythm:** A 4px baseline grid governs all spacing. Use `16px (md)` for standard padding within cards and containers.
- **Data Density:** In ticket lists and tables, use "Compact" (8px vertical padding) and "Comfortable" (16px vertical padding) modes.
- **Breakpoints:** 
    - **Desktop (1280px+):** 3-column layout (Nav / Main List / Details).
    - **Tablet (768px - 1279px):** 2-column layout (Main List / Details) with a collapsible sidebar.
    - **Mobile (<767px):** Single-column stacked view with full-screen ticket modals.

## Elevation & Depth

To maintain a clean, professional aesthetic, this design system avoids heavy shadows. Instead, it utilizes **Tonal Layering** and **Low-Contrast Outlines**.

- **Level 0 (Background):** Slate 50 (#F8FAFC).
- **Level 1 (Cards/Containers):** Pure White (#FFFFFF) with a 1px border in Slate 200 (#E2E8F0). No shadow.
- **Level 2 (Popovers/Dropdowns):** Pure White with a 1px border and a subtle, high-diffused shadow (0px 10px 15px -3px rgba(0, 0, 0, 0.05)).
- **Active State:** Elements being dragged or high-priority modals use a slightly deeper shadow to indicate focus, but never exceeding 10% opacity.

## Shapes

The design system uses a **Soft (0.25rem)** roundedness profile. This maintains a structured, professional appearance while avoiding the aggressive feel of sharp corners.

- **Buttons & Inputs:** 4px (0.25rem) radius.
- **Cards & Modals:** 8px (0.5rem) radius for a slightly softer container feel.
- **Priority Badges:** 4px radius or fully pill-shaped (100px) to distinguish them from interactive buttons.
- **Chat Bubbles:** 12px radius, except for the tail corner which should match the component's roundedness (4px).

## Components

### Data Tables
- **Header:** Sticky positioning, Slate 100 background, `label-md` typography.
- **Rows:** 1px bottom border in Slate 100. Hover state uses Slate 50.
- **Cells:** Use `data-mono` for IDs and `body-sm` for content.

### Priority Badges
- High-contrast background for Critical (Red text on light red background).
- Subtle "dot" indicator next to the text for accessibility.
- Small caps typography for readability.

### Input Fields
- Border-based (1px Slate 300).
- Focused state: 1px Blue 500 border with a 3px light blue outer glow (halo).
- Labels are `label-md` placed above the field.

### Chat Interface
- **Agent Bubbles:** Primary Deep Blue background with White text.
- **Customer Bubbles:** Slate 100 background with Slate 900 text.
- **Timestamps:** `body-sm` in Slate 400, positioned outside the bubble.

### Buttons
- **Primary:** Deep Blue background, White text.
- **Secondary:** White background, 1px Slate 300 border, Slate 700 text.
- **Ghost:** No background, Blue 600 text, for secondary actions like "Cancel" or "View Log."