---
name: Aura & Echo
colors:
  surface: '#fbf9f4'
  surface-dim: '#dbdad5'
  surface-bright: '#fbf9f4'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f5f3ee'
  surface-container: '#f0eee9'
  surface-container-high: '#eae8e3'
  surface-container-highest: '#e4e2dd'
  on-surface: '#1b1c19'
  on-surface-variant: '#4d4540'
  inverse-surface: '#30312e'
  inverse-on-surface: '#f2f1ec'
  outline: '#7e756f'
  outline-variant: '#cfc4bd'
  surface-tint: '#635d5a'
  primary: '#181512'
  on-primary: '#ffffff'
  primary-container: '#2d2926'
  on-primary-container: '#96908b'
  inverse-primary: '#cdc5c0'
  secondary: '#775a19'
  on-secondary: '#ffffff'
  secondary-container: '#fed488'
  on-secondary-container: '#785a1a'
  tertiary: '#310501'
  on-tertiary: '#ffffff'
  tertiary-container: '#4d190e'
  on-tertiary-container: '#cb7c6b'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#e9e1dc'
  primary-fixed-dim: '#cdc5c0'
  on-primary-fixed: '#1e1b18'
  on-primary-fixed-variant: '#4b4642'
  secondary-fixed: '#ffdea5'
  secondary-fixed-dim: '#e9c176'
  on-secondary-fixed: '#261900'
  on-secondary-fixed-variant: '#5d4201'
  tertiary-fixed: '#ffdad3'
  tertiary-fixed-dim: '#ffb4a4'
  on-tertiary-fixed: '#3a0a03'
  on-tertiary-fixed-variant: '#723527'
  background: '#fbf9f4'
  on-background: '#1b1c19'
  surface-variant: '#e4e2dd'
typography:
  display-lg:
    fontFamily: Libre Caslon Text
    fontSize: 64px
    fontWeight: '400'
    lineHeight: 72px
    letterSpacing: -0.02em
  display-md:
    fontFamily: Libre Caslon Text
    fontSize: 48px
    fontWeight: '400'
    lineHeight: 56px
    letterSpacing: -0.01em
  headline-lg:
    fontFamily: Libre Caslon Text
    fontSize: 32px
    fontWeight: '400'
    lineHeight: 40px
  headline-lg-mobile:
    fontFamily: Libre Caslon Text
    fontSize: 28px
    fontWeight: '400'
    lineHeight: 36px
  headline-md:
    fontFamily: Libre Caslon Text
    fontSize: 24px
    fontWeight: '400'
    lineHeight: 32px
  body-lg:
    fontFamily: Manrope
    fontSize: 18px
    fontWeight: '400'
    lineHeight: 28px
  body-md:
    fontFamily: Manrope
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  label-caps:
    fontFamily: Manrope
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.1em
  button:
    fontFamily: Manrope
    fontSize: 14px
    fontWeight: '500'
    lineHeight: 20px
    letterSpacing: 0.02em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  unit: 8px
  container-max: 1440px
  gutter: 32px
  margin-desktop: 64px
  margin-mobile: 20px
  stack-sm: 16px
  stack-md: 32px
  stack-lg: 80px
---

## Brand & Style

The design system is rooted in the concept of "The Quiet Luxury of Sound." It targets a discerning audience—connoisseurs, artists, and lounge curators—who value atmosphere as much as the music itself. The visual language is high-end minimalism, characterized by generous whitespace, a sophisticated interplay between classical typography and modern structure, and a tactile sense of quality.

The emotional goal is to evoke calm, exclusivity, and cultural depth. By utilizing a "gallery-style" approach to layout, the UI recedes to allow the imagery of the artists and the venues to take center stage, creating a premium experience that feels curated rather than manufactured.

## Colors

The palette is inspired by natural interior materials: parchment, stone, blackened steel, and aged gold. 

- **Primary (Charcoal):** Used for primary text and structural definition. It provides a grounded, ink-on-paper feel.
- **Neutral (Cream/Parchment):** The foundation of the system. This off-white base reduces eye strain and feels more artisanal than pure white.
- **Secondary (Muted Gold):** Reserved for high-importance actions, premium status indicators, and subtle accents in navigation.
- **Tertiary (Terracotta):** A warm, earthy tone used for interactive states or specific artist-related highlights to add a human touch.

Color application should be sparse. High contrast is achieved through typography size and weight rather than a saturation of color.

## Typography

The typography strategy relies on the tension between the editorial elegance of **Libre Caslon Text** and the functional precision of **Manrope**.

- **Serif (Headlines):** Use Libre Caslon Text for all major headings and display moments. It signals authority and classic beauty. Use tight tracking for larger sizes to maintain a cohesive "block" look.
- **Sans-Serif (Body & UI):** Manrope is used for all functional text, data, and body copy. It ensures legibility in dense admin views (Owners/Admins) while maintaining a modern, balanced aesthetic.
- **Labeling:** Utilize the `label-caps` style for small descriptors, section headers in sidebars, and overlines. The increased letter spacing provides a clean, architectural feel.

## Layout & Spacing

The layout philosophy follows a **fixed grid** approach for desktop to preserve the intended white space, transitioning to a fluid model for mobile.

- **Desktop (1440px):** 12-column grid with wide 64px margins. This "gallery margin" frames the content, making the platform feel like a high-end publication.
- **Rhythm:** Use a strict 8px base unit. Vertical rhythm is critical; use `stack-lg` (80px) between major sections to ensure the UI feels "airy."
- **Role-Based Density:** 
    - *Audience/Artist views* utilize maximum whitespace and large-scale imagery. 
    - *Admin/Owner/Staff views* may tighten vertical spacing to `stack-sm` for data-heavy tables, but must maintain the wide horizontal margins to preserve the brand's premium feel.

## Elevation & Depth

To maintain a minimalist aesthetic, this design system avoids heavy shadows. Depth is communicated through **Tonal Layers** and **Low-Contrast Outlines**.

- **Surface Tiers:** The base background is the Neutral (Cream). Modals or "floating" panels use a slightly lighter off-white or pure white to subtly lift from the base.
- **Ghost Borders:** Use 1px borders in a very light charcoal (at 10-15% opacity). This defines boundaries without adding visual "weight."
- **Soft Ambient Occlusion:** If depth is required for a floating element (like a music player bar), use a very large (40px+) blur radius with a low-opacity (5%) primary color tint, creating a soft glow rather than a hard shadow.

## Shapes

The shape language is "Soft-Modern." While the brand is elegant, it avoids the hyper-roundness of consumer apps in favor of a more structured, architectural look.

- **Standard Elements:** Buttons, input fields, and small cards use a 4px (0.25rem) radius.
- **Containment:** Larger containers or hero image frames use 8px (0.5rem) to feel intentional but not "bubbly."
- **Interactive States:** On hover, avoid scaling or dramatic movements. A simple color shift or the appearance of a subtle underline is preferred.

## Components

- **Buttons:** Primary buttons are solid Charcoal with Cream text. Secondary buttons use a fine 1px border. All buttons use the `button` typography style with generous horizontal padding (min 24px).
- **Input Fields:** Underlined style rather than fully boxed where possible for the Audience view; fully boxed with 1px light borders for Admin/Staff views. Use Manrope for input text.
- **Cards:** No shadows. Cards are defined by their content alignment and a subtle background tint or 1px border. Image-to-text ratio should favor the image.
- **Chips:** Small, rectangular with a 2px radius. Use for genres or status tags. Keep backgrounds very desaturated.
- **Music Player:** A persistent, minimal bar at the bottom of the screen. Use a "Glassmorphism" effect (backdrop-filter: blur) to allow the content to scroll behind it, maintaining a sense of depth.
- **Data Tables (Admin):** Clean, no vertical lines. Use the `label-caps` for headers and `body-md` for row data. High horizontal padding to maintain the "airy" signature of the system.