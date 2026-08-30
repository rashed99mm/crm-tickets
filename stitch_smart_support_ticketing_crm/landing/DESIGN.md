---
name: Command Center
colors:
  surface: '#F8FAFC'
  surface-dim: '#cbdbf5'
  surface-bright: '#f8f9ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#eff4ff'
  surface-container: '#e5eeff'
  surface-container-high: '#dce9ff'
  surface-container-highest: '#d3e4fe'
  on-surface: '#0b1c30'
  on-surface-variant: '#444653'
  inverse-surface: '#213145'
  inverse-on-surface: '#eaf1ff'
  outline: '#757684'
  outline-variant: '#c4c5d5'
  surface-tint: '#3755c3'
  primary: '#00288e'
  on-primary: '#ffffff'
  primary-container: '#1e40af'
  on-primary-container: '#a8b8ff'
  inverse-primary: '#b8c4ff'
  secondary: '#4b41e1'
  on-secondary: '#ffffff'
  secondary-container: '#645efb'
  on-secondary-container: '#fffbff'
  tertiary: '#003d28'
  on-tertiary: '#ffffff'
  tertiary-container: '#00563a'
  on-tertiary-container: '#5bcf9e'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dde1ff'
  primary-fixed-dim: '#b8c4ff'
  on-primary-fixed: '#001453'
  on-primary-fixed-variant: '#173bab'
  secondary-fixed: '#e2dfff'
  secondary-fixed-dim: '#c3c0ff'
  on-secondary-fixed: '#0f0069'
  on-secondary-fixed-variant: '#3323cc'
  tertiary-fixed: '#85f8c4'
  tertiary-fixed-dim: '#68dba9'
  on-tertiary-fixed: '#002114'
  on-tertiary-fixed-variant: '#005137'
  background: '#f8f9ff'
  on-background: '#0b1c30'
  surface-variant: '#d3e4fe'
  border-subtle: '#E2E8F0'
  status-open: '#4F46E5'
  status-pending: '#F59E0B'
  status-resolved: '#059669'
  status-escalated: '#DC2626'
  priority-critical: '#B91C1C'
  priority-high: '#EF4444'
  priority-medium: '#F59E0B'
  priority-low: '#10B981'
typography:
  display-lg:
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
  label-lg:
    fontFamily: Inter
    fontSize: 13px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.02em
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
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  unit: 4px
  gutter: 16px
  margin-desktop: 24px
  margin-mobile: 16px
  container-sm: 8px
  container-md: 16px
  container-lg: 24px
---

## Brand & Style
The design system is engineered for high-stakes, enterprise-grade Customer Support environments. The brand personality is **authoritative, systematic, and ultra-reliable**, designed to instill a sense of calm control and "Command Center" efficiency. 

The visual style follows a **Corporate / Modern** movement with a heavy emphasis on information density and functional utility. It utilizes a rigorous layout to manage high-volume data while maintaining a premium, trustworthy feel. The interface prioritizes scannability and quick-action execution, ensuring that agents can process complex tickets without cognitive fatigue. The aesthetic is defined by a clean, grid-based structure, high-contrast typography, and a "data-first" hierarchy.

## Colors
The palette is rooted in "Trust Blue" and "Action Indigo" to establish a professional and energetic workspace. 

- **Primary (Trust Blue):** Used for structural brand elements, main navigation, and critical headers.
- **Secondary (Action Indigo):** Reserved for primary calls to action and interactive elements.
- **Tertiary (Success Emerald):** Dedicated to positive outcomes, resolution states, and completed workflows.
- **Neutrals:** A sophisticated range of Slate Grays is used to create a tiered surface architecture, moving from light backgrounds to darker borders for high density without visual clutter.
- **Semantic Logic:** Status and Priority colors are strictly reserved for their respective indicators to ensure consistent color-coded scanning across the CRM.

## Typography
The typography strategy leverages **Hanken Grotesk** for structural impact and **Inter** for sustained readability in dense data environments.

- **Legibility:** Inter is chosen for its neutral tone and exceptional performance at 12px-14px sizes, essential for ticket body content and chat logs.
- **Hierarchy:** Hanken Grotesk provides a sharp, geometric contrast for headings and dashboard metrics.
- **Bilingual Support:** All weights are selected to ensure visual parity between English (LTR) and Arabic (RTL). For Arabic, increase line height by 15% to accommodate script descenders.
- **Data Clarity:** `data-mono` (JetBrains Mono) is utilized for Ticket IDs, timestamps, and technical logs to ensure zero ambiguity between similar characters (e.g., 0 and O).

## Layout & Spacing
The layout uses a **Fixed-Fluid Hybrid Grid** to maximize workspace utility.

- **Structure:** Global navigation (64px) and Sidebars (280px-320px) are fixed to maintain tool persistence. The central content area is fluid, utilizing a 12-column grid to organize ticket lists and detail views.
- **Density Rhythm:** A 4px base unit governs all spacing. For high-density views (tables/lists), use 8px vertical padding. For content-heavy views (ticket notes), use 16px padding.
- **Breakpoints:**
  - **Desktop (1440px+):** 3-column "Wide Mode" (Nav / Inbox List / Ticket Detail / Inspector Sidebar).
  - **Laptop/Tablet (1024px - 1439px):** 2-column view with collapsible inspector.
  - **Mobile (<768px):** Single-column focus mode. Use full-width drawers for ticket actions.

## Elevation & Depth
Depth is conveyed through **Tonal Layering** and **Subtle Ambient Shadows** to maintain a grounded, professional feel.

- **Layer 0 (App Background):** Slate 50.
- **Layer 1 (Main Content/Cards):** White surfaces with a 1px border (Slate 200). Use a very soft shadow (4px blur, 2% opacity) to provide lift.
- **Layer 2 (Floating/Interactive):** Dropdowns and popovers use a 1px border in Slate 300 and a 12px blur shadow at 8% opacity.
- **Focus States:** High-priority modals utilize a backdrop blur (4px) to recede the background and force focus on the immediate task.

## Shapes
This design system uses a **Rounded (8px)** profile to modernize the interface while maintaining a "solid" enterprise feel.

- **Core Elements:** Buttons, Input fields, and Status badges use the base 8px (0.5rem) radius.
- **Large Containers:** Dashboard cards and Modals may scale up to 12px (0.75rem) for a more approachable container feel.
- **Contextual Shapes:** Priority indicators use a circular "dot" prefix within a rounded-sm container to ensure accessibility and quick color identification.

## Components
Consistent styling across these core components ensures the CRM feels like a unified tool.

- **Status Badges:** 
  - **Open:** Indigo background, white text.
  - **Pending:** Amber background, dark amber text.
  - **Resolved:** Emerald background, white text.
  - **Escalated:** Red background, white text.
- **Priority Indicators:** Use a combination of a colored dot and text. `Critical` uses a bold Red-900 text on Red-50 background for maximum contrast.
- **Channel Icons:** Minimalist 20px icons (Email, WhatsApp, SMS) should be paired with `label-md` text and placed in the top-right of ticket headers.
- **Input Fields:** 1px Slate-300 border. On focus, use a 1px Trust Blue border with a 2px subtle glow.
- **Chat Bubbles:**
  - **Internal/Agent:** Trust Blue background, white text, 8px radius.
  - **Customer:** Slate-100 background, Slate-900 text, 8px radius.
- **Lists:** Use alternating row stripes (Slate-50) for data-heavy tables to assist horizontal scanning.