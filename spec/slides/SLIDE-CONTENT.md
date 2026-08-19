# MusicLounge — SEP490 Defence Deck: Content Specification

**Purpose of this file.** It is a build spec for whoever assembles the PowerPoint — a
human or another AI. Every slide below gives the exact on-slide text, the image file to
place, and the layout it should follow. Nothing here is a placeholder unless it is
explicitly marked `⚠️ NEEDS INPUT`.

**Structure source.** The slide order, section breakdown and layout patterns are copied
from the reference deck `914268931-SEP490-G30-HouseCleaners-Network-Platform-Slide.pdf`
(46 slides). Where MusicLounge has more material than the reference (10 use case
diagrams instead of 5, 9 ER diagrams instead of 2), the extra slides are marked
`[BACKUP]` and sit after Q&A so the main deck stays defence-length.

**Language.** On-slide text is **English**, matching the reference deck and the SEP490
document requirement. Each slide also carries a **Vietnamese speaker note** for the
person presenting. Do not put the Vietnamese on the slide.

**Images.** All 34 diagrams are in `images/` beside this file. Every filename below is
exact. Do not regenerate or redraw them — they are produced by a validated toolchain
(`diagrams/`) that mechanically proves no overlapping shapes, no connector crossing an
unrelated element, and no label sitting on the wrong line.

---

## ⚠️ Read this before building — three real constraints

**1. Most diagrams are portrait; a 16:9 slide is not.**
A 0.5-aspect image scaled to fit slide height fills only ~28% of the slide width, so its
text is small. This is unavoidable — the diagrams are one-column by design because a
two-column ERD layout was mathematically proven unable to keep connector labels off
unrelated lines. The reference deck has the same issue on its own slides 20–24 and
accepts it. Placement rule for every diagram: **fit to slide height, centre
horizontally, keep the aspect ratio, never stretch.**

Diagrams that genuinely will not read on one slide (aspect < 0.56, height > 4000px) and
should be **split across two slides** (top half / bottom half, with a "1 of 2" marker):

| File | Size | Split? |
|---|---|---|
| `flow-owner.png` | 3600×6932 | yes — 2 slides |
| `flow-audience.png` | 3360×6188 | yes — 2 slides |
| `erd-show-catalogue.png` | 2336×5142 | yes — 2 slides |
| `erd-personalisation.png` | 2064×4378 | yes — 2 slides |
| `flow-admin.png` | 3000×4608 | borderline — 1 slide acceptable |
| `uc-staff.png` | 1432×3296 | borderline — 1 slide acceptable |

**2. Two items still need the team's own input** — marked `⚠️ NEEDS INPUT` on slides
3, 9 and 10. I did not invent student IDs, contact details, or market statistics.

**3. Sections 02 and 05 describe process, not code.** Slides 14–18 (Scrum, tools,
Jira/Meet/GitHub screenshots) and 40 (testing tools) need **your own screenshots**. I
have written the text; the screenshots are yours to capture.

---

## Deck map

| Section | Slides | Count |
|---|---|---|
| Cover & Team | 1–4 | 4 |
| 01 Project Introduction | 5–12 | 8 |
| 02 Project Management Plan | 13–18 | 6 |
| 03 Software Requirement Specification | 19–28 | 10 |
| 04 Software Design Description | 29–40 | 12 |
| 05 Software Testing | 41–45 | 5 |
| 06 Demo · 07 Q&A · Close | 46–48 | 3 |
| **Main deck** | **1–48** | **48** |
| Backup / appendix | B1–B15 | 15 (5 of them split over 2 slides → 20 physical) |

Every slide except section dividers carries the reference deck's furniture: FPT
University + project logo top-left, product URL top-right, slide number bottom-left,
product name bottom-right.

---

# Cover & Team

### Slide 1 — Cover `[CORE]`
**Layout:** Reference slide 1. Large two-line product name left, logo right, two QR
codes side by side beneath, presenter block bottom-left.

**On-slide text**

- Title: **MusicLounge**
- Subtitle: **Live-Music Venue Ticketing, Livestream & Donation Platform**
- Badge: `SOFTWARE ENGINEERING CAPSTONE PROJECT`
- Two QR codes, labelled **FOR AUDIENCE** and **FOR VENUE OWNER** ⚠️ generate from the
  deployed URLs
- `PRESENTED BY` / **GROUP GSU26SE68**
- Top-right URL: ⚠️ your deployed site URL

**Speaker note (VI):** Mở bằng một câu duy nhất — MusicLounge là nền tảng bán vé,
livestream và donate dành riêng cho phòng trà nhạc sống nhỏ tại Việt Nam. Đừng giải
thích kiến trúc ở slide này.

---

### Slide 2 — About Our Team: Supervisor `[CORE]`
**Layout:** Reference slide 2. Photo left, name card right.

**On-slide text**

- Heading: **About Our Team**
- **Nguyễn Trọng Tài** — Supervisor
- `taint@fpt.edu.vn`

**Speaker note (VI):** Giới thiệu ngắn, 1 câu cảm ơn giảng viên hướng dẫn.

---

### Slide 3 — About Our Team: Members `[CORE]`
**Layout:** Reference slide 3. Row of portrait photos on a connecting line, name badge
under each, student ID below that.

**On-slide text**

- Heading: **About Our Team**
- Subtitle: *Four engineers, one platform — from database schema to deployed cloud.*

| Name | Role | Student ID |
|---|---|---|
| Hà Đông Hải | Leader | ⚠️ NEEDS INPUT |
| Huỳnh Trung Nhiên | Member | ⚠️ NEEDS INPUT |
| Nguyễn Quang Anh Khoa | Member | ⚠️ NEEDS INPUT |
| Nguyễn Huy Phúc | Member | ⚠️ NEEDS INPUT |

⚠️ **NEEDS INPUT:** student IDs (HE######) and photos. The reference deck shows 5
members; we have 4 — lay the row out as 4 evenly spaced, not 5 with a gap.

**Speaker note (VI):** Mỗi người 1 câu: tên + mảng phụ trách. Tổng cộng dưới 30 giây.

---

### Slide 4 — Table of Content `[CORE]`
**Layout:** Reference slide 4. Solid colour panel, numbered list, generous line spacing.

**On-slide text**
```
01  PROJECT INTRODUCTION
02  PROJECT MANAGEMENT PLAN
03  SOFTWARE REQUIREMENT SPECIFICATION
04  SOFTWARE DESIGN DESCRIPTION
05  SOFTWARE TESTING
06  DEMO
07  Q & A
```

**Speaker note (VI):** Đọc lướt, không dừng. Nói rõ phần Demo nằm gần cuối để hội đồng
biết sẽ được xem sản phẩm chạy thật.

---

# 01 — Project Introduction

### Slide 5 — Section divider `[CORE]`
**Layout:** Reference slide 5. Photo panel left, oversized two-line heading right.
**On-slide text:** **Project Introduction**
**Image:** ⚠️ a photo of a small live-music venue / acoustic café interior.
**Speaker note (VI):** Chuyển cảnh, không nói gì thêm.

---

### Slide 6 — 1.1 Project Scope `[CORE]`
**Layout:** Reference slide 6. Five vertical rounded pillars, icon in a circle at the
top of each, `01`–`05` badge at the foot.

**On-slide text**

- Eyebrow: `1. PROJECT INTRODUCTION` / `1.1 Project Scope`

| # | Pillar | Body |
|---|---|---|
| 01 | **Ticketing built for a venue, not an event** | Online and walk-in sales, seating zones and tiers, whole-evening validity, QR check-in at the door. |
| 02 | **Gated livestream** | Only a genuine ticket holder can watch, with a per-ticket device limit. |
| 03 | **Transparent performer donations** | Every tip is traceable from the audience member to the performer's payout, on a double-entry ledger. |
| 04 | **In-venue operations** | Food and drink ordering from the table, a staff order board, and counter ticket sales. |
| 05 | **Venue presence** | A 360° virtual tour, AI-generated posters, and personalised show recommendations. |

**Speaker note (VI):** Nhấn trụ cột 1 và 3 — đó là hai thứ khác biệt so với nền tảng bán
vé thông thường. Trụ 4 và 5 là phần mở rộng, nói nhanh.

---

### Slide 7 — 1.2 Business Opportunity `[CORE]`
**Layout:** Reference slide 7. Photo + oversized section number left, three stacked
colour boxes right, closing paragraph beneath.

**On-slide text**

- Big label: **1.2 BUSINESS OPPORTUNITY**
- Box 1 — **A SEGMENT NOBODY BUILT FOR** — General ticketing platforms assume one fixed
  showtime. A lounge runs a different show most nights and keeps selling after the music
  starts.
- Box 2 — **STREAMING WITH NO TICKET GATE** — General streaming platforms cannot limit a
  broadcast to people who actually paid.
- Box 3 — **TIPS WITH NO PAPER TRAIL** — Performer tips happen as cash or ad-hoc
  transfers, with no shared record of what was collected or paid out.
- Closing line: *No existing product brings ticketing, gated livestream, auditable
  donations and in-venue operations together for this segment. That gap is the
  opportunity.*

**Speaker note (VI):** Đây là slide "vì sao làm". Nói theo trình tự: chủ phòng trà đang
dùng 3 công cụ rời rạc, và tiền tip cho nghệ sĩ hiện không ai kiểm soát được.

---

### Slide 8 — 1.3 Motivation Factors `[CORE]`
**Layout:** Reference slide 8. Four boxes in a 2×2 grid, oversized ghost number in each.

**On-slide text**

- Eyebrow: `1. PROJECT INTRODUCTION` / `1.3 Project Background — Motivation Factors`

| # | Heading | Body |
|---|---|---|
| 01 | **RISING DEMAND FOR INTIMATE LIVE MUSIC** | Music lounges, acoustic cafés and *phòng trà* are drawing younger audiences back to small rooms. |
| 02 | **THE ROOM HAS A CEILING** | A venue can only sell as many tickets as it has seats; livestream is the only way past that limit. |
| 03 | **MANUAL, PAPER-BASED OPERATIONS** | Door sales are cash and paper; there is no shared record between the counter, the door and the owner. |
| 04 | **PERFORMERS CANNOT SEE THEIR OWN MONEY** | A performer has no way to verify what the venue collected on their behalf. |

**Speaker note (VI):** Bốn yếu tố này chính là bốn thứ dẫn tới bốn nhóm chức năng chính.
Nếu hội đồng hỏi "vì sao cần livestream", câu trả lời nằm ở ô 02.

---

### Slide 9 — 1.4 Market Situation (World) `[CORE]` ⚠️ NEEDS INPUT
**Layout:** Reference slide 9. Infographic-heavy: a chart, a map, and two labelled
callout blocks.

⚠️ **I have not written figures for this slide, on purpose.** No market-size data exists
anywhere in the project repository, and inventing a number that a committee can check is
a worse outcome than an honest gap. Source these before the defence and cite them on the
slide the way the reference deck does (`Source: Statista (2024)`):

- Global live-music / live-events market size and CAGR — try **Statista**, **IFPI Global
  Music Report**, **PwC Global Entertainment & Media Outlook**.
- Livestream-concert or virtual-events market size — try **Grand View Research**,
  **MarketsandMarkets**.
- Creator-tipping / virtual-gifting volume — try **Streamlabs / Stream Hatchet**
  quarterly reports.

**Speaker note (VI):** Chỉ nêu 2–3 con số, mỗi con số kèm nguồn. Không đọc hết bảng.

---

### Slide 10 — 1.5 Market Situation (Vietnam) `[CORE]` ⚠️ NEEDS INPUT
**Layout:** Reference slide 10. Three or four icon + big-number pairs, source line at
the bottom right.

⚠️ **Same as slide 9 — figures needed.** What to source:

- Number of live-music venues / cafés with live performance in Hà Nội and TP.HCM.
- Vietnamese online event-ticketing market value (**Ticketbox**, **VNPay**, or
  **Statista Vietnam** publish partial figures).
- Vietnamese digital-payment adoption rate — supports the VNPay-only design decision.
- Smartphone / mobile-internet penetration — supports the mobile Staff and F&B apps.

**Speaker note (VI):** Slide này quan trọng vì chứng minh thị trường VN đủ lớn. Cố gắng
lấy được ít nhất 1 con số về số lượng phòng trà / quán cà phê nhạc sống.

---

### Slide 11 — 1.6 Existing Systems `[CORE]`
**Layout:** Reference slide 11. Three product logos across the top, comparison table
beneath with the criteria as rows.

**On-slide text**

- Eyebrow: `1. PROJECT INTRODUCTION` / `1.6 Existing Systems`

| | **Ticketbox** | **Twitch + Streamlabs** | **Veeps** |
|---|---|---|---|
| What it is | Vietnam's largest general event ticketing platform | Global live-stream + real-time tipping | Ticketed livestream for touring artists (Live Nation) |
| Ticketing | ✅ QR entry, seat & zone | ❌ | ✅ pay-per-view / subscription |
| Gated livestream | ❌ | ❌ open to anyone | ✅ |
| Performer tipping | ❌ | ✅ but paid direct to the individual | ⚠️ fixed charity option only |
| In-venue F&B / door tools | ❌ | ❌ | ❌ |
| Per-venue recurring tooling | ❌ each event is standalone | ❌ | ❌ built for large venues |
| Venue-side settlement | ❌ | ❌ no venue in the money path | ❌ money goes artist-direct |

- Footer line: *Each covers part of the problem. None covers a venue that runs a different
  show every night and is legally the intermediary handling a performer's money.*

**Speaker note (VI):** Đây là slide dễ bị hỏi nhất. Chuẩn bị sẵn: "Veeps gần nhất với mô
hình của chúng em — hybrid vừa diễn trực tiếp vừa livestream — nhưng Veeps làm cho nghệ
sĩ lưu diễn ở venue lớn, và tiền đi thẳng từ vé sang nghệ sĩ, không có bước đối soát ở
phía venue."

---

### Slide 12 — 1.7 Regulatory Basis `[CORE]`
**Layout:** Two columns of three cards. Decree number as the card heading, one line of
consequence beneath. *(No reference-deck equivalent — this is a MusicLounge-specific
strength worth its own slide.)*

**On-slide text**

- Heading: **Built Against Vietnamese Regulation, Not Retrofitted**

| Regulation | What it forced into the design |
|---|---|
| **Decree 52/2024** — payment intermediation | All money moves through licensed intermediary VNPay; the platform never touches card data. |
| **Decree 117/2025** — withholding tax | Tax withheld per transaction, posted to a dedicated tax ledger account. |
| **Decree 147/2024** — takedown & phone verification | A 24-hour moderation SLA with an internal warning at ~20 hours; verified-phone field on the user record. |
| **Decree 85/2021** — mandatory complaint channel | The complaint entity plus a guest-accessible filing and lookup path. |
| **Law 91/2025/QH15** — personal data protection | AI consent defaults off; export and erasure on request; erasure anonymises in place so legally-retained financial records survive. |
| **Decree 144/2020 + VCPMC** — performance licensing | A permit reference, and a royalty reference where applicable, required before a show may be submitted for review. |

**Speaker note (VI):** Slide "ăn điểm". Nhấn ý cuối: xoá dữ liệu cá nhân là **ẩn danh
tại chỗ**, không xoá cứng — vì Luật Kế toán yêu cầu giữ chứng từ 10 năm. Đây là chỗ hai
luật xung đột và nhóm đã xử lý có chủ đích.

---

# 02 — Project Management Plan

### Slide 13 — Section divider `[CORE]`
**Layout:** Reference slide 12. Agile lifecycle wheel left, oversized two-line heading
right.
**On-slide text:** **Software** / **LIFE CYCLE**
**Speaker note (VI):** Chuyển cảnh.

---

### Slide 14 — How We Applied Scrum `[CORE]`
**Layout:** Reference slide 13. Sprint diagram left, five numbered rows right.

**On-slide text**

- Heading: **HOW WE APPLIED**
- Project duration: **20/05/2026 – 31/08/2026 · 15 weeks · 101 days**

| # | Ceremony | What we did |
|---|---|---|
| 01 | **SPRINT PLANNING** | Define sprint goals; pull items from the product backlog into the sprint backlog. |
| 02 | **DAILY SCRUM** | 15-minute stand-up: what was done, what is next, what is blocked. |
| 03 | **DEVELOPMENT & TESTING** | Implement user stories; run unit, integration and acceptance tests continuously. |
| 04 | **SPRINT REVIEW** | Demo completed features to the supervisor; collect feedback. |
| 05 | **SPRINT RETROSPECTIVE** | Reflect on the process; agree actions for the next sprint. |

- Callout: **Total estimated effort: 488 man-days across 75 work-breakdown items**

**Speaker note (VI):** Nếu bị hỏi sprint dài bao nhiêu, trả lời theo đúng thực tế của
nhóm — đừng nói con số đẹp mà Jira không chứng minh được.

---

### Slide 15 — Tools Used `[CORE]` ⚠️ NEEDS SCREENSHOT
**Layout:** Reference slide 14. Tool logos connected by thin lines around a centre.

**On-slide text** — Heading: **TOOLS USED**

| Tool | Used for |
|---|---|
| **Jira** | Backlog and sprint tracking (Kanban) |
| **GitHub** | Source control and pull-request review |
| **GitHub Actions** | CI/CD — build, test, deploy to Azure |
| **Google Meet** | Sprint planning and review with the supervisor |
| **Discord / Messenger** | Internal daily communication |
| **Figma + Google Stitch** | UI design and prototyping |
| **Visual Studio / VS Code** | Development |

**Speaker note (VI):** Nói nhanh, đây là slide hình.

---

### Slide 16 — Project Tracking `[CORE]` ⚠️ NEEDS SCREENSHOT
**Layout:** Reference slide 15 — full-bleed Jira board screenshot, thin caption.
**On-slide text:** Heading **PROJECT TRACKING**; caption: *Jira board — sprint backlog,
in-progress and done columns.*
**Speaker note (VI):** Chỉ vào cột Done, nói số story hoàn thành ở sprint gần nhất.

---

### Slide 17 — Sprint Review `[CORE]` ⚠️ NEEDS SCREENSHOT
**Layout:** Reference slide 16 — Google Meet recording screenshot with participant tiles.
**On-slide text:** Heading **SPRINT REVIEW**
**Speaker note (VI):** 1 câu: mỗi sprint đều demo cho giảng viên hướng dẫn và ghi nhận
feedback.

---

### Slide 18 — Manage Code `[CORE]` ⚠️ NEEDS SCREENSHOT
**Layout:** Reference slide 17 — GitHub commit history screenshot, dark theme.
**On-slide text:** Heading **MANAGE CODE**; caption: *Feature-branch workflow — every
change reviewed in a pull request before merge.*
**Speaker note (VI):** Nếu hội đồng hỏi ai làm gì, mở GitHub Insights → Contributors.

---

# 03 — Software Requirement Specification

### Slide 19 — Section divider `[CORE]`
**Layout:** Reference slide 18. Photo panel left, three-line oversized heading right.
**On-slide text:** **Software** / **Requirement** / **Specification**

---

### Slide 20 — Context Diagram `[CORE]`
**Image:** `images/context.png` — 3800×2092, aspect 1.82. **Fits 16:9 well — place
nearly full-bleed.**
**Layout:** Reference slide 19. Small title top-left, diagram filling the rest.

**On-slide text**

- Title: **Context Diagram**
- Optional footnote: *DFD notation (Yourdon / DeMarco): the system is one process, every
  external party is a rectangle, every arrow is named for the data it carries.*

**Speaker note (VI):** Chỉ vào hình tròn giữa — toàn bộ hệ thống. 10 thực thể ngoài chia
hai nhóm: người dùng bên trái, dịch vụ bên phải. Nhấn: mỗi mũi tên đặt tên theo **dữ
liệu**, không phải hành động — đó là đúng chuẩn DFD.

---

### Slide 21 — Actors & Roles `[CORE]`
**Layout:** Six cards, 3×2 grid. Role name as heading, one-line description, a small
badge showing whether the role has a login.

**On-slide text** — Heading: **Actors**

| Actor | Login? | Description |
|---|---|---|
| **Guest** | no | Browses published shows and venues, explores a 360° tour, files a complaint with a reference number — but cannot buy, watch or donate. |
| **Audience** | ✅ | Discovers shows, buys and manages tickets, watches gated livestreams, tips performers, orders food and drink in-venue, rates shows. |
| **Owner** | ✅ | Registers a venue, manages seating zones and the 360° tour, creates and operates shows, handles donations, views earnings. Pays a platform subscription. |
| **Staff** | ✅ | Assigned by an Owner to exactly one venue. Sells walk-in tickets, checks tickets at the door, works the F&B board, starts and ends a broadcast. |
| **Admin** | ✅ | Approves venues, moderates shows and livestreams, processes refunds, verifies bank accounts, issues penalties, resolves complaints, monitors the ledger. |
| **Performer** | **no** | Appears in a show's line-up and receives donations. Has a public profile and a payout bank account, but no login — the Owner maintains the record. |

- Footer: **6 actors · 4 login roles · 109 use cases**

**Speaker note (VI):** Hai chỗ dễ bị hỏi. (1) Performer **không có tài khoản đăng nhập** —
đây là quyết định có chủ đích, chủ venue quản lý hộ. (2) Admin được tạo thẳng trong CSDL,
cố ý không có đường tự đăng ký.

---

### Slide 22 — Use Case Diagram: Guest `[CORE]`
**Image:** `images/uc-guest.png` — 2036×3260, aspect 0.62. Fit to height, centred.
**On-slide text:** Title **UCD for Guest** · badge `14 use cases`
**Speaker note (VI):** Guest xem được nhưng không mua được. Đường `«include»` tới "Look
up a complaint" là kênh bắt buộc theo Nghị định 85/2021.

---

### Slide 23 — Use Case Diagram: Audience — Account & Discovery `[CORE]`
**Image:** `images/uc-audience-account.png` — 1668×3116, aspect 0.54. Fit to height.
**On-slide text:** Title **UCD for Audience — Account & Discovery**
**Speaker note (VI):** Nhóm tài khoản, đăng nhập, khám phá show và gợi ý cá nhân hoá.

---

### Slide 24 — Use Case Diagram: Audience — Transactions `[CORE]`
**Image:** `images/uc-audience-transaction.png` — 2436×3580, aspect 0.68. Fit to height.
**On-slide text:** Title **UCD for Audience — Ticketing, Livestream, Donation & F&B**
**Speaker note (VI):** Slide trọng tâm của actor Audience. Nếu hội đồng hỏi về `«extend»`,
giải thích: mũi tên đi **từ** use case mở rộng **tới** use case cơ sở, và chỉ chạy khi
điều kiện đúng — khác với `«include»` luôn luôn chạy.

---

### Slide 25 — Use Case Diagram: Owner `[CORE]`
**Image:** `images/uc-owner-show.png` — 2228×3224, aspect 0.69. Fit to height.
**On-slide text:** Title **UCD for Owner — Show Management**
**Speaker note (VI):** Owner tạo show ở trạng thái Draft, gửi duyệt, Admin duyệt mới
Published. Không có đường tắt.
> Owner has two more use case diagrams — `uc-owner-venue.png` and `uc-owner-finance.png`
> — placed in the backup section as **B1** and **B2**.

---

### Slide 26 — Use Case Diagram: Staff `[CORE]`
**Image:** `images/uc-staff.png` — 1432×3296, aspect 0.43. Fit to height; borderline
legibility, consider splitting.
**On-slide text:** Title **UCD for Staff** · badge `14 use cases`
**Speaker note (VI):** Staff là actor duy nhất dùng app mobile trong lúc show đang diễn:
bán vé tại quầy, soát vé, bảng order đồ ăn, bật/tắt phát sóng.

---

### Slide 27 — Use Case Diagram: Admin `[CORE]`
**Image:** `images/uc-admin-moderation.png` — 1432×2264, aspect 0.63. Fit to height.
**On-slide text:** Title **UCD for Admin — Moderation & Compliance**
**Speaker note (VI):** Kiểm duyệt venue, show, livestream; SLA 24 giờ theo Nghị định
147/2024.
> `uc-admin-platform.png` is backup slide **B3**.

---

### Slide 28 — Functional & Non-functional Requirements `[CORE]`
**Layout:** Reference slide 28. Two tables side by side, coloured header rows.

**Left table — Functional Requirements**

| Code | Module |
|---|---|
| IAM | Identity & Access Management |
| VEN | Venue Management |
| SHW | Show Management |
| TKT | Ticketing & Check-in |
| LIV | Livestream |
| DON | Donation & Performer Payout |
| FNB | Food & Beverage Ordering |
| PAY | Payment & Settlement |
| SUB | Subscription |
| MOD | Moderation & Compliance |
| NOT | Notification |
| ADM | Admin Console |

**Right table — Non-functional Requirements**

| Category | Requirement |
|---|---|
| **User interfaces** | Chrome, Firefox, Edge, Safari; responsive for desktop, tablet and mobile |
| **External interfaces** | Payment: VNPay · Video: Mux / Cloudflare Stream · Push: Firebase Cloud Messaging · SMS: Twilio · Identity: Google OAuth · Maps: Google Maps · AI: Gemini / OpenAI |
| **Hardware interfaces** | Desktop and mobile devices; camera required for QR check-in |
| **Security** | JWT bearer authentication; secrets in Azure Key Vault; the platform never handles card data |
| **Reliability** | Payment confirmation is driven by a server-to-server IPN callback, so a ticket still confirms if the customer closes the browser |
| **Auditability** | Every money movement is a balanced double-entry ledger journal, verified by a daily integrity job |

- Footer: **25 controllers · 209 endpoints · 30 business rules · 68 entities**

**Speaker note (VI):** Nhấn hai dòng cuối bảng phải — IPN và sổ cái kép. Đó là hai quyết
định kỹ thuật bảo vệ được trước hội đồng.

---

# 04 — Software Design Description

### Slide 29 — Section divider `[CORE]`
**Layout:** Reference slide 29. Photo panel left, three-line oversized heading right.
**On-slide text:** **Software** / **Design** / **Description**

---

### Slide 30 — System Architecture `[CORE]` ⭐
**Image:** `images/architecture-system.png` — 4200×2820, aspect 1.49. Fit to height,
centred. **This is the slide the reference deck's slide 30 corresponds to.**
**Layout:** Reference slide 30. Small title top-left, diagram filling the rest.

**On-slide text**

- Title: **System Architecture**
- Optional caption strip: *ASP.NET Core 8 on Azure App Service · React 18 on Static Web
  Apps · Azure SQL · 5 third-party services*

**Speaker note (VI):** Đi theo đường dữ liệu, đừng đọc từng ô. "Người dùng → trình duyệt
→ Static Web Apps tải bundle React; mọi lệnh gọi API đi thẳng vào App Service. Bên trong
App Service là stack .NET: controller → MediatR → EF Core, cộng SignalR cho realtime và
Hangfire cho job nền. Bên trái là dịch vụ Azure, bên phải là dịch vụ bên thứ ba." Nhấn:
mỗi mũi tên hai đầu vì đều là request–response, và mỗi đường đều thẳng nên nhãn không thể
bị đọc nhầm sang đường khác.

---

### Slide 31 — Layered Architecture `[CORE]`
**Image:** `images/architecture-layers.png` — 3440×3560, aspect 0.97. Fit to height.
**On-slide text**

- Title: **Layered Architecture — Clean Architecture + CQRS**
- Callout: *Every dependency points inward. Domain references nothing at all.*

**Speaker note (VI):** Slide này trả lời câu "cái gì phụ thuộc cái gì", khác slide trước
là "cái gì chạy bằng công nghệ gì". Điểm nhấn: Infrastructure nằm **ngoài** nhưng mũi tên
vẫn trỏ **vào trong** — vì Application khai báo interface, Infrastructure hiện thực. Nhờ
vậy test được business rule mà không cần database.

---

### Slide 32 — Package Diagram `[CORE]`
**Image:** `images/package-application.png` — 2600×2480, aspect 1.05. Fit to height.
**On-slide text:** Title **Package Diagram** · footnote *UML 2.5.1 — dependencies use
the `«use»` keyword.*
**Speaker note (VI):** 28 feature folder trong Application, mỗi folder là một bounded
context nhỏ.

---

### Slide 33 — Class Diagram (CQRS) `[CORE]`
**Image:** `images/class-cqrs.png` — 3000×2800, aspect 1.07. Fit to height.
**On-slide text**

- Title: **Class Diagram — CQRS Request Pipeline**
- Callout: *Four pipeline behaviours run in a fixed order: Logging → Validation →
  ActiveUser → Transaction.*

**Speaker note (VI):** Chỉ vào tam giác rỗng — đó là ký hiệu realization (hiện thực
interface) theo UML, không phải mũi tên thường. Nhấn: chỉ `TransactionBehavior` bị ràng
buộc vào `ICommand`, nên query không mở transaction vô ích.

---

### Slide 34 — Deployment Diagram `[CORE]`
**Image:** `images/deployment.png` — 3760×2840, aspect 1.32. Fit to height.
**On-slide text**

- Title: **Deployment Diagram — MusicLounge on Microsoft Azure**
- Footnote: *UML 2.5.1 — a communication path is an association: a plain line with no
  arrowhead.*

**Speaker note (VI):** Nếu hội đồng hỏi vì sao đường không có mũi tên: theo UML, đường
liên lạc trong deployment là **association**, mũi tên có hướng sẽ mang nghĩa dependency —
sai chuẩn. App Service lấy secret từ Key Vault bằng managed identity nên không có bí mật
nào nằm trong gói triển khai.

---

### Slide 35 — Database Design: Core `[CORE]`
**Image:** `images/erd-core.png` — 3960×2560, aspect 1.55. Fit to height.
**On-slide text**

- Title: **Database Design — Core Entities**
- Footnote: *Crow's Foot / Information Engineering notation. Inner mark = minimum
  (optionality), outer mark = maximum.*
- Badge: **68 entities · 95 relationships**

**Speaker note (VI):** 20 thực thể cốt lõi trên tổng 68. Nếu bị hỏi vì sao một số quan hệ
có vòng tròn thay vì gạch: vòng tròn = tối thiểu 0, tức khoá ngoại nullable trong code.
Đây là đối chiếu trực tiếp từ entity C#, không vẽ theo cảm tính.

---

### Slide 36 — Database Design: Money `[CORE]`
**Image:** `images/erd-money.png` — 1520×3124, aspect 0.49. Fit to height.
**On-slide text:** Title **Database Design — Payment, Settlement & Ledger**
**Speaker note (VI):** Miền quan trọng nhất. Mỗi giao dịch tiền sinh ra một bút toán cân
bằng nợ–có; có job chạy hằng ngày kiểm tra tính toàn vẹn sổ cái.
> The other 7 domain ERDs are backup slides **B4–B10**.

---

### Slide 37 — Sequence: Ticket Purchase `[CORE]` ⭐
**Image:** `images/seq-ticket-purchase.png` — 3261×2899, aspect 1.12. Fit to height.
**On-slide text**

- Title: **Sequence Diagram — Ticket Purchase (hold → pay → confirm via VNPay IPN)**

**Speaker note (VI):** Kể như một câu chuyện. "Khán giả chọn hạng vé → hệ thống giữ chỗ
15 phút dưới khoá đặt vé → tạo Payment và Ticket ở trạng thái Pending → chuyển sang
VNPay." Điểm nhấn quan trọng nhất: **IPN server-to-server mới là xác nhận có thẩm quyền,
không phải redirect trình duyệt** — nên khách tắt tab vé vẫn được xác nhận. Handler
idempotent vì VNPay có retry. Nhánh dưới: hết 15 phút, job xoá bản ghi giữ chỗ; không cần
hoàn lại bộ đếm nào vì số chỗ trống được tính động.

---

### Slide 38 — Sequence: Donation `[CORE]`
**Image:** `images/seq-donation.png` — 3291×2462, aspect 1.34. Fit to height.
**On-slide text:** Title **Sequence Diagram — Performer Donation & Split**
**Speaker note (VI):** Tỷ lệ chia được **chốt lại tại thời điểm donate** và lưu vào bản
ghi, nên đổi cấu hình sau này không làm sai lệch giao dịch cũ.

---

### Slide 39 — Sequence: Moderation `[CORE]`
**Image:** `images/seq-moderation.png` — 3039×2391, aspect 1.27. Fit to height.
**On-slide text:** Title **Sequence Diagram — Show Moderation & 24-hour SLA**
**Speaker note (VI):** Gemini chấm điểm rủi ro để xếp hàng ưu tiên, **không tự quyết** —
người vẫn là người duyệt cuối. Nếu Gemini lỗi, hệ thống trả điểm trung tính chứ không
chặn request.

---

### Slide 40 — State Machines `[CORE]`
**Images:** `images/state-show.png` (3308×2162) **and** `images/state-ticket.png`
(2848×1764) — place **side by side**, each fit to height. Combined aspect works on 16:9.
**On-slide text**

- Title: **State Machines — Show and Ticket lifecycle**
- Left caption: *LoungeShowStatus* · Right caption: *TicketStatus*

**Speaker note (VI):** Nếu hội đồng tinh ý sẽ hỏi về `Refunded`. Trả lời thẳng: enum có
khai báo nhưng **không nơi nào trong code gán giá trị đó** — vé hoàn tiền kết thúc ở
`Cancelled` kèm bút toán đảo. Nhóm vẽ nó vì trạng thái tồn tại trong schema, và ghi chú
rõ là không tới được. Đây là điểm cộng về tính trung thực, đừng giấu.

---

# 05 — Software Testing

### Slide 41 — Section divider `[CORE]`
**Layout:** Reference slide 37. Illustration left, two-line oversized heading right.
**On-slide text:** **Software** / **Testing**

---

### Slide 42 — Testing Strategy `[CORE]`
**Layout:** Reference slide 39. Four overlapping circles, `STEP 01`–`STEP 04`.

**On-slide text** — Heading: **Testing Strategy**

| Step | Level | What it covers |
|---|---|---|
| 01 | **Unit** | Domain rules and command/query handlers, isolated from database and network. |
| 02 | **Integration** | Handler + EF Core + real database, including transaction rollback behaviour. |
| 03 | **Frontend component** | React components and hooks against a mocked API. |
| 04 | **End-to-end** | A real browser driving the deployed stack through complete user journeys. |

**Speaker note (VI):** Nhấn tầng 2 — test tích hợp chạy với database thật, nên bắt được
lỗi rollback mà unit test không thấy.

---

### Slide 43 — Testing Tools `[CORE]`
**Layout:** Reference slide 40. Logo row, generous whitespace.

**On-slide text** — Heading: **Testing Tools**

| Tool | Level |
|---|---|
| **xUnit** + **FluentAssertions** | Backend unit & integration |
| **Testcontainers / SQL Server** | Integration database |
| **Vitest** + **React Testing Library** | Frontend component |
| **Playwright** | End-to-end |
| **GitHub Actions** | Runs every level on each pull request |

**Speaker note (VI):** Nói 1 câu: toàn bộ 5 công cụ này chạy tự động trên mỗi pull request.

---

### Slide 44 — Test Results `[CORE]` ⭐
**Layout:** Reference slides 41–43. Results table left, two charts right (a bar per
level, a donut for pass rate).

**On-slide text** — Heading: **Test Report**

| Level | Suites | Tests | Passed | Failed |
|---|---|---|---|---|
| Backend (unit + integration) | 61 | 460 | 456 | 4 |
| Frontend component | 100 | 307 | 307 | 0 |
| End-to-end | 1 | 28 | 28 | 0 |
| **Total** | **162** | **795** | **791** | **4** |

- Big number: **99.5% pass rate**

**Speaker note (VI):** ⚠️ Có 4 test fail và **con số này nằm trên slide, không giấu**.
Chuẩn bị sẵn câu trả lời: đó là 4 test đã fail từ trước, không liên quan tới tính năng
đang bảo vệ, và nhóm biết rõ nguyên nhân. Nếu bạn sửa được trước hôm bảo vệ thì cập nhật
lại bảng — nhưng **đừng sửa con số trên slide mà không sửa test**.

---

### Slide 45 — Quality Gates `[CORE]`
**Layout:** Three cards.
**On-slide text** — Heading: **Beyond Tests — Automated Quality Gates**

| Gate | What it proves |
|---|---|
| **Ledger integrity job** | A daily job re-checks that every money journal still balances; a mismatch raises an alert. |
| **Diagram conformance validator** | Every diagram in this deck is checked against the source code — use case names, entity names and enum states must exist in `src/`, or the build fails. |
| **CI on every pull request** | Build, all 162 test suites, and deployment to Azure run automatically. |

**Speaker note (VI):** Điểm nhấn ở gate thứ 2 — sơ đồ trong slide này **được kiểm tra tự
động khớp với code**, 109/109 use case và 68 thực thể. Không có sơ đồ nào vẽ tay rồi để
lệch với hệ thống thật.

---

# 06 Demo · 07 Q&A · Close

### Slide 46 — DEMO `[CORE]`
**Layout:** Reference slide 45. Oversized centred word, photo strip bottom.
**On-slide text:** **DEMO**
**Speaker note (VI):** Kịch bản demo đề xuất, theo đúng thứ tự tiền chạy:

1. Khán giả tìm show → mua vé → thanh toán VNPay sandbox → nhận vé QR
2. Staff quét QR soát vé tại cửa
3. Bật livestream → chứng minh người không có vé **không xem được**
4. Khán giả donate cho nghệ sĩ → chỉ ra bút toán sổ cái vừa sinh ra
5. Owner xem trang Finance → đối chiếu đúng số tiền vừa phát sinh
6. Admin duyệt venue / xử lý hoàn tiền

---

### Slide 47 — Q & A `[CORE]`
**Layout:** Reference slide 46.
**On-slide text:** **Q & A**

---

### Slide 48 — Thanks `[CORE]`
**Layout:** Reference slide 44. Oversized two-line heading, photo bottom-right.
**On-slide text:** **Thanks for** / **watching**

---

# Backup / Appendix — after Q&A

Keep these loaded but hidden. Pull them up only if the committee asks.

| # | Title | Image | Size |
|---|---|---|---|
| **B1** | UCD for Owner — Venue Management | `images/uc-owner-venue.png` | 1844×2976 |
| **B2** | UCD for Owner — Finance & Subscription | `images/uc-owner-finance.png` | 1844×3292 |
| **B3** | UCD for Admin — Platform Administration | `images/uc-admin-platform.png` | 1432×2512 |
| **B4** | UCD for System — background jobs | `images/uc-system.png` | 1652×2440 |
| **B5** | ERD — Identity & Access | `images/erd-identity-access.png` | 1112×2722 |
| **B6** | ERD — Venue | `images/erd-venue.png` | 1656×3294 |
| **B7** | ERD — Show Catalogue *(split over 2 slides)* | `images/erd-show-catalogue.png` | 2336×5142 |
| **B8** | ERD — Ticketing | `images/erd-ticketing.png` | 2200×3656 |
| **B9** | ERD — Food & Beverage | `images/erd-food-beverage.png` | 1384×2514 |
| **B10** | ERD — Personalisation *(split over 2 slides)* | `images/erd-personalisation.png` | 2064×4378 |
| **B11** | ERD — Operations | `images/erd-operations.png` | 908×2144 |
| **B12** | Screen Flow — Audience *(split over 2 slides)* | `images/flow-audience.png` | 3360×6188 |
| **B13** | Screen Flow — Owner *(split over 2 slides)* | `images/flow-owner.png` | 3600×6932 |
| **B14** | Screen Flow — Admin | `images/flow-admin.png` | 3000×4608 |
| **B15** | Activity Diagram — Payment | `images/activity-payment.png` | 5984×3880 |

**B4 speaker note (VI):** "System" là actor không phải người — 8 use case chạy nền bởi
Hangfire: huỷ thanh toán bỏ dở, giải phóng chỗ giữ quá hạn, đối soát, kiểm tra sổ cái,
cảnh báo SLA kiểm duyệt.

**B12–B14 speaker note (VI):** 75 màn hình phân bổ: Audience Web 30 · Owner Web 27 ·
Admin Web 16 · Staff Mobile 7 · Audience Mobile F&B 2.

---

## Numbers used in this deck — all from `spec/build/facts.py`

Every figure below is generated from the repository and self-checked, not typed by hand.
If a slide needs a number that is not here, it does not exist yet — do not invent it.

| Figure | Value |
|---|---|
| Client surfaces | 5 |
| Distinct screens | 75 |
| Entities | 68 |
| Controllers | 25 |
| API endpoints | 209 |
| Use cases | 109 |
| Actors / login roles | 6 / 4 |
| Hangfire job classes (recurring) | 30 (22) |
| Business rules | 30 |
| Estimated effort | 488 man-days |
| Project duration | 20/05/2026 – 31/08/2026 · 15 weeks |
| Tests: backend / frontend / e2e | 460 / 307 / 28 |
| Tests: total, passed, rate | 795 · 791 · 99.5% |
