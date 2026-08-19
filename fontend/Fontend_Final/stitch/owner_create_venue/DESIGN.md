---
name: Warm Luxury Lounge
colors:
  surface: '#fff9ed'
  surface-dim: '#dfd9cf'
  surface-bright: '#fff9ed'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f9f3e8'
  surface-container: '#f3ede2'
  surface-container-high: '#ede7dd'
  surface-container-highest: '#e8e2d7'
  on-surface: '#1d1b15'
  on-surface-variant: '#53433b'
  inverse-surface: '#333029'
  inverse-on-surface: '#f6f0e5'
  outline: '#867369'
  outline-variant: '#d9c2b6'
  surface-tint: '#8f4d20'
  primary: '#8c4a1e'
  on-primary: '#ffffff'
  primary-container: '#aa6233'
  on-primary-container: '#fffbff'
  inverse-primary: '#ffb68c'
  secondary: '#795836'
  on-secondary: '#ffffff'
  secondary-container: '#ffd2a8'
  on-secondary-container: '#7a5837'
  tertiary: '#4f6054'
  on-tertiary: '#ffffff'
  tertiary-container: '#68796c'
  on-tertiary-container: '#f6fff5'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#ffdbc9'
  primary-fixed-dim: '#ffb68c'
  on-primary-fixed: '#321200'
  on-primary-fixed-variant: '#723609'
  secondary-fixed: '#ffdcbd'
  secondary-fixed-dim: '#eabe95'
  on-secondary-fixed: '#2c1600'
  on-secondary-fixed-variant: '#5e4021'
  tertiary-fixed: '#d5e7d8'
  tertiary-fixed-dim: '#b9cbbd'
  on-tertiary-fixed: '#101f16'
  on-tertiary-fixed-variant: '#3a4a3f'
  background: '#fff9ed'
  on-background: '#1d1b15'
  surface-variant: '#e8e2d7'
typography:
  display-lg:
    fontFamily: Playfair Display
    fontSize: 48px
    fontWeight: '700'
    lineHeight: 56px
    letterSpacing: -0.02em
  display-lg-mobile:
    fontFamily: Playfair Display
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
    letterSpacing: -0.01em
  headline-md:
    fontFamily: Playfair Display
    fontSize: 32px
    fontWeight: '600'
    lineHeight: 40px
  headline-sm:
    fontFamily: Playfair Display
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
  title-lg:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  body-lg:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '400'
    lineHeight: 28px
  body-md:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  label-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '500'
    lineHeight: 20px
    letterSpacing: 0.05em
  caption-handwriting:
    fontFamily: Playfair Display
    fontSize: 16px
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
  base: 8px
  xs: 4px
  sm: 12px
  md: 24px
  lg: 48px
  xl: 80px
  container-max: 1280px
  gutter: 24px
  margin-mobile: 16px
---

## Brand & Style

The design system is anchored in the "Warm Luxury Lounge" aesthetic, evoking the sensory experience of a high-end listening room. It balances the timeless elegance of traditional craftsmanship with the precision of modern high-fidelity audio equipment. The target audience values curation, quality, and an unhurried digital experience.

The design style is **Tactile Minimalism with a hint of Glassmorphism**. It utilizes organic textures, soft shadows with warm color tints, and physical metaphors such as walnut wood finishes and velvet-inspired surfaces. The emotional response is one of intimacy, comfort, and sophisticated relaxation—like sitting in a leather chair under natural afternoon light.

## Colors

The palette is built on a foundation of warm, organic neutrals to mimic natural materials.

- **Primary (#C97B4A):** A rich Terracotta/Wood accent used for primary actions and active states. It represents the warmth of glowing vacuum tubes and stained timber.
- **Secondary (#A9835E):** A Tan/Camel shade used for supporting elements, borders, and secondary buttons.
- **Tertiary (#3A4A3F):** Deep Moss Green, reserved for subtle success states or specialized biological/organic categories.
- **Neutral/Background (#F2ECE1):** A Warm Beige/Cream that serves as the canvas, reducing the harshness of pure white and providing a paper-like or plaster-like quality.
- **Text (#2B2A27):** A Charcoal/Dark Wood color for high-contrast legibility without the clinical feel of pure black.

## Typography

This design system uses a high-contrast typographic pairing to signal luxury.

- **Headlines:** Playfair Display provides an authoritative, editorial feel. Use larger sizes for artist names and album titles.
- **UI & Body:** Inter ensures maximum legibility for functional elements, metadata, and long-form descriptions. 
- **Captions:** An italicized variant of Playfair Display is used for "handwritten" annotations on Polaroid-style image frames to add a personal, curated touch.
- **Labels:** Use Inter in uppercase with slight letter spacing for navigation items and small UI badges.

## Layout & Spacing

The layout follows a **Fixed Grid** philosophy on desktop to maintain the feeling of a curated editorial spread, while transitioning to a fluid model on mobile devices.

- **Desktop:** 12-column grid with a 1280px max-width. Use generous `lg` and `xl` spacing to create "breathing room" around featured content.
- **Mobile:** 4-column fluid grid with 16px side margins. 
- **Rhythm:** All spacing must be a multiple of the 8px base unit. Vertical rhythm should prioritize white space over information density to maintain the "lounge" mood.

## Elevation & Depth

Hierarchy is established through **Tonal Layering** and **Warm Ambient Shadows**.

- **Surfaces:** The base layer is the Warm Beige background. Raised elements (cards, menus) use a pure white (`#FFFFFF`) surface to pop against the beige.
- **Shadows:** Avoid grey shadows. Use a soft, diffused shadow with a subtle brown tint (e.g., `rgba(43, 42, 39, 0.08)`).
- **Depth Levels:**
  - *Level 1 (Cards):* Minimal blur (8px), low offset.
  - *Level 2 (Popovers/Modals):* Large blur (24px), vertical offset (12px) to simulate light coming from above.
- **Glass:** For playback bars or overlays, use a backdrop-blur (12px) with a semi-transparent white tint (80% opacity) to create a frosted glass effect that suggests depth without clutter.

## Shapes

The shape language is consistently soft and approachable. 

- **Primary Radius:** 12px (defined as `rounded-md`) is the standard for cards, input fields, and buttons.
- **Large Radius:** 24px (`rounded-xl`) is used for featured containers and promotional banners.
- **Polaroid Style:** Images should be wrapped in a white frame with a 12px padding on the top/sides and a 40px padding on the bottom to accommodate "handwritten" captions.

## Components

- **Buttons:** Primary buttons use the Terracotta background with white text. Secondary buttons use a Tan border with Charcoal text. All buttons have a 12px corner radius.
- **Segmented Controls:** Used for toggles (e.g., "Playlist" vs "Album"). These should look like physical switches on high-end audio gear—recessed into the surface with a subtle inner shadow.
- **Cards:** White background, 12px radius, and warm tinted shadows. Polaroid-style cards are used specifically for user-generated content or "Listening Memories."
- **Input Fields:** Soft beige background (slightly darker than the main background) with a 1px Tan border. The border thickens and changes to Terracotta on focus.
- **Lists:** Clean, high-contrast rows with subtle dividers (1px Tan at 20% opacity). Use Inter for list item titles and metadata.
- **Play Controls:** Large, circular primary action buttons. The "Play" button is always the most prominent element on a screen, utilizing the primary Terracotta color.