# Stitch Design Brief — MusicLounge, Audience Mobile (F&B Ordering)

> Distilled for Google Stitch from [View-Design-Spec.md](View-Design-Spec.md) §2 group G. Deliberately narrow-scope native app — see [platform-architecture.md](platform-architecture.md) for why this is split off from the main Audience experience, which lives on the website ([stitch-brief-audience-web.md](stitch-brief-audience-web.md)).

---

## 1. App overview

A single-purpose native mobile app used **only** while physically at a MusicLounge venue, watching a show in person: order food and drinks from the venue's menu without flagging down staff, and track your order's status. This is not a general MusicLounge app — no browsing shows, no tickets, no livestream, no donations here. If a user opens this app anywhere but a venue during a show, there's simply nothing useful for them to do; design around that reality rather than padding the app with unrelated features.

**Platform**: Native mobile app (iOS/Android). Assume the phone is in the user's hand at their table — design for one-handed use, large tap targets, minimal typing.

## 2. Suggested visual direction *(a starting point — adjust freely)*

Should feel like a fast, low-friction utility, not a full entertainment app — closer to a restaurant QR-ordering experience than to the main MusicLounge brand. A lighter touch of the same "warm, intimate venue" palette from the main site keeps it recognizably MusicLounge, but the design should prioritize speed and clarity over atmosphere: this gets opened mid-conversation, in low light, order needs to go in fast.

## 3. Navigation

No tab bar needed for a 2-screen app — a simple flow: open app → Order screen → after ordering, a persistent way back to "My Order" status (e.g. a small floating status pill or badge that's always visible while an order is active).

---

## 4. Screens

**Order F&B**
- Purpose: browse the venue's menu and place an order.
- Content: menu grouped by category, item cards (photo, name, price), cart/order summary (likely a bottom sheet or persistent mini-cart bar).
- Actions: add to cart; adjust quantity; "Place Order".
- States: sold-out items shown greyed-out with a "temporarily unavailable" label, not removed from the menu; empty state if the venue hasn't set up a menu.
- Note: the app needs to know *which venue* (and ideally which table/zone) the order belongs to — likely established once at the start of the session (e.g. a lightweight venue/table selection or a QR scan step) rather than asked again per item. Exact mechanism is an implementation detail beyond this brief — just design the ordering screen assuming venue/table context is already known by this point.

**My Order**
- Purpose: track order status without having to ask staff.
- Content: current order with a status stepper (Pending → Preparing → Served → Paid, or Cancelled as a side-exit), items and total.
- States: **status does not push-update in real time** — staff update it from their own tool, and this screen has no live channel to them. Design a clear manual "pull to refresh" affordance rather than implying the screen watches status live on its own.

---

*Source: [View-Design-Spec.md](View-Design-Spec.md) §2 group G (Đặt món F&B, Đơn F&B của tôi). See [platform-architecture.md](platform-architecture.md) for the platform-split rationale.*
