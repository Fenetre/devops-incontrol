# DevOps InControl Dashboard — Application Documentation

## Overview

**DevOps InControl Dashboard** is a DevOps monitoring and analytics platform built with a **.NET 8 backend** and **Vue 3 + Tailwind CSS frontend**. It connects to Azure DevOps to monitor projects, track pull requests, manage databases, and assess developer performance.

| | |
|---|---|
| **Backend** | .NET 8 (ASP.NET Core) — `http://127.0.0.1:8080` |
| **Frontend** | Vue 3 + Tailwind CSS + Vite (served by backend in production) |
| **Database Drivers** | SQL Server + PostgreSQL |
| **Credentials** | Windows DPAPI encryption (user-bound) |
| **Auth** | API Key — required to unlock all endpoints |

---

## Getting Started

### Prerequisites

- .NET 8 SDK
- Node.js (v18+)
- Windows (required for DPAPI credential storage)
- Azure DevOps Personal Access Token (PAT)

### Startup

```powershell
.\start.ps1                  # Production mode (single process, serves built frontend)
.\start.ps1 -Dev             # Dev mode (hot-reload via Vite + dotnet watch)
.\start.ps1 -Background      # Headless mode (for scheduled task)
```

In production mode, the script builds the frontend into `dist/` and runs a single .NET process on port 8080 that serves both the API and the frontend. In dev mode, it starts the .NET backend (`dotnet watch run`) and the Vite dev server side by side.

### First-Time Setup

On first launch, the browser opens to `http://localhost:8080` and a **Setup Wizard** guides you through:

1. **Set an API Key** — protects your dashboard with a password
2. **Add a PAT** — enter your organization name and Personal Access Token (validated against Azure DevOps)
3. **Add your first project** — pick a project, optionally select an area path, and choose which checks to enable

After the wizard completes, checks run immediately and results appear on the dashboard.

You can add more projects and configure database servers, email, and other settings from the **Configuration** page and **Settings** modal at any time.

> ![Screenshot: Login screen with API key input](screenshots/01-login.png)

> ![Screenshot: Settings modal with PAT and DB server configuration](screenshots/02-settings.png)

---

## Navigation

The sidebar provides access to all sections of the application. It features collapsible sections, a search box to filter projects/checks, and an expand/collapse toggle.

> ![Screenshot: Sidebar navigation expanded](screenshots/03-sidebar.png)

| Section | Description |
|---------|-------------|
| **DevOps Monitor** | Dashboard + drill-down per project & check type |
| **PR Monitor** | Pull request overview + per-project detail |
| **DB Monitor** | Database discovery + allowlist & rule checking |
| **Sprint Populator** | Clone sprint templates into active sprints (same-project or cross-project source) |
| **Velocity** | Team capacity and velocity metrics per sprint |
| **DEV Assessment** | Developer velocity, review times, PR iterations |
| **Permission Check** | Repo, area, pipeline, release, and wiki permissions |
| **Check Permissions** | User/group permission matrix and audit |
| **Pipelines** | Build pipeline overview |
| **Releases** | Release pipeline overview |
| **Settings** (gear) | Credentials, DB servers, email configuration |

---

## Views

### 1. Dashboard

Central hub showing all monitored projects with their check results and overall health status.

> ![Screenshot: Dashboard with project cards showing check results](screenshots/04-dashboard.png)

**Features:**
- **Project Cards** — grid layout showing project name, enabled checks, and flagged item count
- **Run Checks** — executes all enabled checks for all projects
- **Auto-refresh** — configurable interval (Off / 1 / 5 / 10 / 30 min)
- **Hide All-Clear** — toggle to hide projects with no issues
- **Last Run** — timestamp + total issues found; stale data warning (>5 min)

**DB Monitor Summary** (lower section):
- Per-project breakdown: **Total** / **OK** / **NOK** database count
- Allowlisted databases are counted as OK
- NOK = closed ticket + no ticket

> ![Screenshot: Dashboard DB Monitor summary section](screenshots/05-dashboard-db-summary.png)

---

### 2. Configuration

Manage which projects are monitored and what checks are enabled.

> ![Screenshot: Configuration view with project form](screenshots/06-config.png)

#### DevOps Projects

| Field | Description |
|-------|-------------|
| **Organization** | Autocomplete from Azure DevOps (requires PAT) |
| **Project** | Loads from selected organization |
| **Area Path** | Optional nested area path filter |
| **Enabled Checks** | Checkbox per check type; API version per check |
| **Ignore Patterns** | Title / parent title substring filters (comma-separated) |

Check-specific settings:
- `release_pr_check`, `pr_approval_check`, `stale_pr_check`, `unreviewed_pr_check` — optional repository name filter
- `stale_pr_check` — configurable days threshold (default: 14)
- `unreviewed_pr_check` — ignore reviewers list (e.g., "Build Service, Project Collection")

#### DB Projects

| Field | Description |
|-------|-------------|
| **Project Name** | Display name |
| **Database Name Filter** | Substring filter (case-insensitive) |
| **Database Server** | Select from configured servers (1–3) |
| **Allowlist** | Database names to skip rule checks (one per line) |

---

### 3. Issue Detail

Drill-down into flagged items for a specific check in a project.

> ![Screenshot: Issue detail view with table of flagged items](screenshots/07-issue-detail.png)

**Table columns:** ID → Title (with work item type icon) → Assigned To → State → Iteration → Created

**Work item type icons:**
- 🔥 Red flame — Bug / Defect
- 📘 Blue book — User Story
- 📋 Amber clipboard — Task

**State badges** are color-coded:
- Green = Active/New
- Blue = Resolved
- Gray = Closed/Done

**Check-specific actions:**
- **Missing Estimates** — "Send email to [Assignee]" per developer
- **Orphan Check** — "Copy parent-done items" / "Copy all items with links"

---

### 4. PR Monitor

Central overview of all pull requests across monitored projects.

> ![Screenshot: PR Monitor with project summary cards](screenshots/09-pr-monitor.png)

**Features:**
- **Project Summary Cards** — project name, org, PR count, flag badges
- **Flag types:** `unreviewed`, `rejected`, `stale`, `approval_ready`
- **All clear** badge when no flags
- Click card to drill into project-specific PRs

#### PR Project Detail

> ![Screenshot: PR project detail with PR list](screenshots/10-pr-project.png)

- Full PR list with columns: ID, Title, Author, Repo, Status, Reviewers, Flags
- Filter by status/flag
- Direct links to PRs in Azure DevOps

---

### 5. DB Monitor

Discover and list all user databases across configured servers.

> ![Screenshot: DB Monitor showing all discovered databases](screenshots/11-db-monitor.png)

**Features:**
- Queries all configured DB servers
- Database grid with copy-on-hover
- Warning if DB credentials not configured

#### DB Project Detail

> ![Screenshot: DB project detail with rule checking results](screenshots/12-db-project.png)

**Rule check statuses:**
| Status | Indicator | Meaning |
|--------|-----------|---------|
| OK | 🟢 Green dot | No issues found |
| Allowlisted | ⚪ White dot (gray border) | On allowlist — skipped |
| No ticket | ⚫ Gray dot | No ticket reference in name |
| Closed | 🔴 Red dot | Referenced ticket is closed/done |

**Actions:**
- **Refresh** — re-fetches databases and auto-runs rules
- **Copy All Closed** — copies names of databases with closed tickets

---

### 6. Sprint Populator

Clone "sprint template" user stories (with child tasks) into an active sprint.

> ![Screenshot: Sprint Populator wizard](screenshots/13-sprint-populator.png)

**Workflow:**
1. **Select Target Project** — where new work items will be created
2. **Select Template Source Project (optional)** — defaults to the target project
3. **Select Team** — loads when target project is selected
4. **Select Sprint** — target iteration dropdown
5. **Preview** — shows templates found (tagged "sprint template") with final titles
6. **Confirm & Create** — checkbox confirmation, then creates work items

Sprint number is auto-detected from the sprint name and substituted (e.g., "X" → sprint number in template titles).

---

### 7. DEV Assessment

Analyze developer performance over a configurable time window.

> ![Screenshot: DEV Assessment with team metrics](screenshots/14-dev-assessment.png)

**Configuration:**
- Time window: 1 / 3 / 6 / 12 months
- Project filter (checkboxes)
- Member filter (toggle individual team members)

**Alerts Section:**
- **Stale Review PRs** — PRs waiting >2 business days for first review

**Team Metrics (bar charts):**
| Metric | Description |
|--------|-------------|
| PRs per Developer | Sortable (A–Z, ascending, descending) |
| Avg Hours to Complete | Color-coded (green=fast, red=slow) |
| Avg Iterations | Count of review rounds |
| Avg First Review Time | Time to first reviewer response |

Click on a developer card to drill into individual metrics.

> ![Screenshot: Individual developer drill-down](screenshots/15-dev-individual.png)

---

## Check Types Reference

| # | Check | Label | What it flags |
|---|-------|-------|---------------|
| 1 | `orphan_check` | Backlog Orphans | Work items without parent, or parent is Done/Closed |
| 2 | `stale_sprint_check` | Stale Sprint Items | Items active in a sprint whose end date has passed |
| 3 | `missing_estimate_check` | Missing Estimates | Tasks/bugs in current sprint without Original Estimate or Remaining Work (configurable) |
| 4 | `unassigned_check` | Unassigned Items | Items in current sprint with no assignee |
| 5 | `release_pr_check` | Release PR Issues | Done bugs in current sprint missing required release PRs |
| 6 | `resolved_pr_check` | Resolved PR Ready | Resolved items with all PRs completed — ready for Done |
| 7 | `tag_overview_check` | Tag Overview | All tags in project with usage count (info-only) |
| 8 | `pr_approval_check` | PR Approval Ready | Active PRs with all approvals but not yet completed |
| 9 | `stale_pr_check` | Stale PRs | Active PRs inactive for N days (configurable) |
| 10 | `unreviewed_pr_check` | Unreviewed PRs | Active PRs with no reviewers assigned |

---

## Architecture

### File Structure

```
dashboard/
├── backend-dotnet/                    # .NET 8 API server
│   ├── Controllers/
│   │   ├── ChecksController.cs        # Run checks, return results
│   │   ├── ProjectsController.cs      # CRUD projects + check types
│   │   ├── DbProjectsController.cs    # DB project management
│   │   ├── DevOpsController.cs        # Azure DevOps proxy calls
│   │   ├── PrMonitorController.cs     # PR monitoring endpoints
│   │   ├── SettingsController.cs      # Credential management
│   │   ├── SprintPopulatorController.cs
│   │   ├── DevAssessmentController.cs
│   │   ├── VelocityController.cs      # Team capacity & velocity
│   │   ├── CheckPermissionsController.cs # User/group permission audit
│   │   └── PermissionCheckController.cs  # Repo/area/pipeline permissions
│   ├── Middleware/
│   │   └── AllMiddleware.cs           # API key, CSRF, rate limit, security headers
│   ├── Services/
│   │   ├── AzureDevOpsClient.cs       # REST client for Azure DevOps
│   │   ├── ConfigStore.cs             # Project config persistence
│   │   ├── CredentialManagerService.cs # Windows Credential Manager
│   │   ├── CryptoService.cs           # DPAPI encryption/decryption
│   │   ├── DbConnector.cs             # SQL Server + PostgreSQL
│   │   ├── DevAssessmentService.cs    # Developer analytics
│   │   ├── HttpClientPool.cs          # Shared HTTP client pooling
│   │   └── Checks/                    # Pluggable check system
│   │       ├── CheckRegistry.cs
│   │       ├── Helpers.cs
│   │       ├── MissingEstimateCheck.cs
│   │       ├── OrphanCheck.cs
│   │       └── ...
│   ├── Models/
│   │   ├── ApiModels.cs               # Request/response DTOs
│   │   └── ConfigModels.cs            # Configuration models
│   └── Program.cs                     # Startup & dependency injection
│
├── frontend/
│   ├── src/
│   │   ├── App.vue                    # Root layout + API key gate
│   │   ├── main.js                    # Entry point
│   │   ├── router/index.js            # Route definitions
│   │   ├── stores/monitor.js          # Pinia state management
│   │   ├── composables/
│   │   │   ├── useApi.js              # HTTP client with API key
│   │   │   ├── useTheme.js            # Dark/light mode toggle
│   │   │   ├── useKeyboardShortcuts.js # Global keyboard shortcuts
│   │   │   ├── useDemoMode.js         # Demo/anonymized data mode
│   │   │   ├── useDebounce.js         # Debounced input helper
│   │   │   └── useCsvExport.js        # CSV export utility
│   │   ├── components/
│   │   │   ├── SidebarMenu.vue        # Navigation sidebar
│   │   │   ├── ProjectCard.vue        # Dashboard project card
│   │   │   ├── IssueTable.vue         # Reusable flagged items table
│   │   │   ├── SettingsModal.vue      # Credentials configuration
│   │   │   ├── DataFreshness.vue      # Data age indicator
│   │   │   ├── EmptyState.vue         # Empty state placeholder
│   │   │   └── KeyboardShortcutsHelp.vue # Shortcut reference overlay
│   │   └── views/
│   │       ├── DashboardView.vue      # Main dashboard
│   │       ├── SetupWizardView.vue    # First-time setup wizard
│   │       ├── ConfigView.vue         # Project/DB project config
│   │       ├── IssueDetailView.vue    # Check results drill-down
│   │       ├── TagDetailView.vue      # Tag-based issue view
│   │       ├── PrMonitorView.vue      # PR overview
│   │       ├── PrProjectView.vue      # Per-project PRs
│   │       ├── DbMonitorView.vue      # Database discovery
│   │       ├── DbProjectView.vue      # Per-project DB rules
│   │       ├── SprintPopulatorView.vue
│   │       ├── VelocityView.vue       # Team capacity & velocity
│   │       ├── DevAssessmentView.vue
│   │       ├── PermissionCheckView.vue # Repo/area permissions
│   │       ├── CheckPermissionsView.vue # User/group audit
│   │       ├── PipelinesView.vue      # Build pipelines
│   │       └── ReleasesView.vue       # Release pipelines
│   └── vite.config.js
│
└── data/
    └── config.json                    # Runtime configuration
```

### Data Flow

```
Browser ──► .NET API (:8080) ──► Azure DevOps REST API
                │                     │
                │  (serves Vue SPA    ├──► SQL Server / PostgreSQL
                │   from dist/)       │
                │                     └──► Windows DPAPI (credentials)
                │                          config.json (projects)
                └── Pinia Store
                    (reactive state)
```

In dev mode (`-Dev`), Vite runs a separate dev server on `:5173` with hot-reload, proxying API calls to the backend on `:8080`.

### API Key Flow

All API requests include `X-API-Key` header. The backend middleware validates this against the stored key before processing any request.

---

## Screenshots

> **Note:** Screenshots should be placed in a `docs/screenshots/` folder. To capture them:
>
> 1. Start the app with `.\start.ps1`
> 2. Open `http://localhost:5173` in a browser
> 3. Take screenshots of each view and save with the filenames referenced above

| Screenshot | Filename | Description |
|------------|----------|-------------|
| Login | `01-login.png` | API key input screen |
| Settings | `02-settings.png` | Settings modal (PAT, DB servers, email) |
| Sidebar | `03-sidebar.png` | Sidebar navigation expanded |
| Dashboard | `04-dashboard.png` | Main dashboard with project cards |
| DB Summary | `05-dashboard-db-summary.png` | Dashboard DB Monitor summary |
| Config | `06-config.png` | Configuration view with project form |
| Issues | `07-issue-detail.png` | Issue detail with flagged items table |
| PR Monitor | `09-pr-monitor.png` | PR Monitor project cards |
| PR Detail | `10-pr-project.png` | PR project detail with PR list |
| DB Monitor | `11-db-monitor.png` | All discovered databases |
| DB Project | `12-db-project.png` | DB project with rule checking |
| Sprint | `13-sprint-populator.png` | Sprint Populator wizard |
| DEV Team | `14-dev-assessment.png` | DEV Assessment team metrics |
| DEV Individual | `15-dev-individual.png` | Developer individual drill-down |

---

## Deployment

### Windows Auto-Start

The installer registers a Registry Run key (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run\DevOpsInControl`) that launches the app at logon. No admin rights or scheduled tasks required.

For dev installs, `scripts\install.ps1` offers the same option during setup.

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `AZDO_PAT` | Yes | Azure DevOps Personal Access Token |
| `API_KEY` | Recommended | Frontend/API authentication key |
| `DB_PASSWORD` | Optional | Database password (if not using DPAPI) |
| `SMTP_PASSWORD` | Optional | SMTP password for email notifications |
| `CORS_ORIGINS` | Optional | Comma-separated allowed origins (defaults to localhost variants) |
| `ALLOW_UNPROTECTED_API` | Optional | Set `true` to bypass API key requirement (local dev only) |
| `DB_TRUST_SERVER_CERTIFICATE` | Optional | Trust SQL Server TLS certificate (default: `true`) |
| `DB_POSTGRES_SSL_MODE` | Optional | PostgreSQL SSL mode (default: `prefer`) |
| `SMTP_ALLOW_INSECURE` | Optional | Allow insecure SMTP connections |
| `TRUST_X_FORWARDED_FOR` | Optional | Trust X-Forwarded-For header from proxies |
| `TRUSTED_PROXY_IPS` | Optional | Comma-separated trusted proxy IPs (default: `127.0.0.1,::1`) |
| `API_AUTH_SESSION_DAYS` | Optional | Auth session cookie lifetime in days (default: `7`, range: 1–365) |
| `AUTH_COOKIE_SECURE` | Deprecated | Ignored; auth session cookie is always issued with `Secure` |
| `RATE_LIMIT` | Optional | Max requests per window (default: `200`) |
| `RATE_WINDOW` | Optional | Rate limit window in seconds (default: `60`) |
| `MAX_BODY_BYTES` | Optional | Max request body size (default: `1048576`) |
| `AZDO_TIMEOUT` | Optional | Azure DevOps HTTP request timeout |

### Credential Storage

Credentials (PAT, DB passwords) are encrypted using **Windows DPAPI** and stored locally. They are bound to the Windows user account and cannot be decrypted by other users or on other machines.

Use `Save-Secrets.ps1` to store credentials from the command line.
