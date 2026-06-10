# Screenshot Update Guide

All screenshots are located in `Documentation/screenshots/`.

Since v1.2.0 introduced a full UI refresh (Nuxt UI 3 components), **all existing screenshots should be retaken** to reflect the new visual style. Below is the complete list with notes on what each should capture.

---

## NEW Screenshots (must be added)

| Filename | Page | What to Capture |
|---|---|---|
| `roadmap.png` | roadmap.html | Full Roadmap board in Quarter view with sprint overlay — showing at least 2 epics with feature bars positioned across quarters, some with amber "dirty" rings |
| `roadmap-drag.png` | roadmap.html | *(optional)* A feature bar mid-drag with the ghost element visible and target column highlighted |
| `roadmap-links.png` | roadmap.html | *(optional)* Dependency arrows between features, with the link confirmation modal open |
| `roadmap-create.png` | roadmap.html | The create modal showing the coloured header bar ("Create Feature"), form fields with iteration pre-selected, and footer with "Press Esc to close" |
| `roadmap-detail-view.png` | roadmap.html | The detail modal in **view mode** — coloured header bar with #ID, title, type/state badges, tags, last changed (orange), description, and comments section |
| `roadmap-detail-edit.png` | roadmap.html | The detail modal in **edit mode** — title input, state/assignee/iteration/tags dropdowns, description editor, custom fields (both dropdown and rich text types), and footer with Cancel/Save |
| `roadmap-config.png` | roadmap.html | *(optional)* Configuration tab with projects and sprint views configured |
| `orphan-preview.png` | issue-detail.html | The orphan check work item preview modal — coloured header with #ID title, type/state badges, description rendered, comments, and footer |
| `pr-reviewers.png` | pr-details.html | The Reviewers tab showing the bottleneck list grouped by reviewer |
| `template-manager.png` | template-manager.html | *(existing file — REPLACE)* The Template Manager showing Create tab with field editor and existing template list |

---

## REPLACE Screenshots (UI refresh — retake with current look)

All of these files already exist but show the old UI (pre-Nuxt UI 3). They need to be retaken with the current visual style.

| Filename | Page | What to Capture |
|---|---|---|
| `dashboard.png` | dashboard.html | Main dashboard with project cards, coloured badges, run checks button |
| `getting-started.png` | getting-started.html | Setup wizard (any step showing the modern UI) |
| `settings.png` | settings.html | Settings modal with General/Database/Email tabs |
| `configuration.png` | managing-projects.html | Configuration screen with project list and enabled checks |
| `issue-detail.png` | issue-detail.html | Flagged issues table with action buttons |
| `tags.png` | tags.html | Tag overview showing tag list with counts |
| `pr-monitor.png` | pr-monitor.html | PR Monitor overview with project cards and flag badges |
| `pr-details.png` | pr-details.html | PR list table with flag columns and search |
| `db-monitor.png` | db-monitor.html | DB Monitor grid showing discovered databases |
| `db-project.png` | db-project.html | DB Project cleanup view with coloured tiles (include shield icon on hover) |
| `sprint-populator.png` | sprint-populator.html | Sprint Populator preview step with checkboxes for selective inclusion |
| `velocity.png` | velocity.html | Capacity tab with team member table and day-off calendar |
| `dev-assessment.png` | dev-assessment.html | Developer Assessment metrics table/chart |
| `permissions.png` | permissions.html | Permission matrix view |
| `check-permissions.png` | permissions.html | Check Permissions / person group audit view |
| `permission-check.png` | permissions.html | *(duplicate of above — can be removed or kept as alias)* |
| `pipelines.png` | pipelines.html | Build pipeline list with status indicators |
| `releases.png` | pipelines.html | Release deployment view (if still shown as separate tab) |

---

## Tips for Taking Screenshots

1. Use a **clean browser window** (no extensions visible) at ~1280×800 resolution.
2. Use **light mode** for primary screenshots (dark mode can be shown as optional secondary).
3. Ensure there is **realistic sample data** visible (not empty states).
4. Crop to the **content area only** (exclude browser chrome and OS taskbar).
5. Save as **PNG** for crisp text rendering.
6. Keep file sizes reasonable — compress with a tool like TinyPNG if over 500 KB.
