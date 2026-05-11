[![DevOps InControl](https://www.fenetre.nl/media/skan3fog/devopsincontrol-banner-github.png)](https://www.devopsincontrol.com)

# DevOps InControl
A control and management application for Microsoft Azure DevOps made by scrummaster Berend and the AI Technocore.

<p>
<a href="https://github.com/Fenetre/devops-incontrol/stargazers">
<img alt="GitHub stars" src="https://img.shields.io/github/stars/Fenetre/devops-incontrol?color=%2317c495">
</a>

  <a href="https://github.com/Fenetre/devops-incontrol/network/members">
<img alt="GitHub forks" src="https://img.shields.io/github/forks/Fenetre/devops-incontrol?color=%2317c495">
</a>

  <a href="https://github.com/Fenetre/devops-incontrol/blob/main/LICENSE">
<img alt="license" src="https://img.shields.io/github/license/Fenetre/devops-incontrol?color=%2317c495">
</a>
</p>

## DevOps InControl Key Features

-  Sprint Populator/Templates – Save time by cloning sprint templates and common tasks into active sprints with a single click.
-  Automated Project Health Checks – Instantly flag missing estimates, orphaned tasks, stale sprint items, and unassigned work items that clutter your backlog.
-  Orphan Parent Assignment – Assign or change parent work items directly from the orphan check with smart candidate suggestions ranked by relevance.
-  Unified PR Monitor – Track pull requests across all organizations and repositories in one view; identify unreviewed, rejected, or stagnant PRs at a glance.
-  PR Approval & Stale Detection – Surface PRs that are approved but not yet completed, PRs with no reviewers, and PRs inactive for a configurable number of days.
-  Resolved PR Ready – Identify tasks and bugs with all required PRs completed, ready to move to Done.
-  One-Click Notifications – Send automated email reminders to assignees directly from the dashboard for missing estimates or overdue tasks.
-  Team Velocity Tracking – Monitor planned vs. actual capacity across sprints with per-team and per-sprint breakdowns to improve future forecasting.
-  Sprint Capacity Management – Set and update team member capacity, activities, and days off per sprint directly from the dashboard — no need to navigate Azure DevOps.
-  Developer Assessment – Gain insights into code review participation, PR turnaround speeds, stale reviews, open PR counts, and individual delivery trends over configurable time ranges.
-  Pipeline & Release Monitor – A bird's-eye view of your build pipeline runs and release deployment statuses per project.
-  Database Governance – Automatically verify that your SQL Server and PostgreSQL databases correspond to active DevOps tickets and flag "orphaned" databases with configurable rules.
-  Permission Overview – Full matrix views of repository, area, pipeline, release, and wiki permissions per project with group membership resolution.
-  Check Permissions – Audit whether your DevOps projects conform to configurable security rules, and exclude irrelevant projects from the scope via a deny-list.
-  Personal Access Audit – Look up specific people to see which organizations and projects they can access, helping minimize unnecessary permissions after offboarding or role changes.
-  Tag Management & Health – Scan your entire project to see which tags are in use and how many items each tag covers.
-  Tag Bulk Operations – Delete ghost tags, rename tags across all work items, and remove tags in bulk directly from the dashboard.
-  Smart Version Filtering – Logic that automatically hides noise like version numbers (v1.2.0) so you can focus on functional tags.
-  Dark Mode – Full dark/light theme toggle with system preference detection.
-  Demo Mode – Showcase the dashboard with anonymized data without exposing real project information.
-  Multi-Project & Multi-Org Support – Monitor multiple Azure DevOps projects across different organizations from a single dashboard.

# DevOps InControl

A dashboard that monitors your Azure DevOps backlogs, pull requests, and sprint health.  
Runs locally on your Windows PC — no cloud hosting needed.

---

## What you need before installing

| Prerequisite | How to get it |
|---|---|
| **Windows 10/11** | You're probably already on it. |
| **.NET 8.0+ SDK** | Download from [dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0). Pick the **SDK** installer (not Runtime). |
| **Node.js 18+** | Download from [nodejs.org](https://nodejs.org). Pick the **LTS** version. |
| **Azure DevOps PAT** | See [Create a PAT](#create-a-personal-access-token-pat) below. |

To check if you already have them, open **PowerShell** and run:

```powershell
dotnet --version   # should print 8.x.x or higher
node --version     # should print v18.x.x or higher, tested on v18 and v22
```

---

## Installation (one-time)

1. **Open PowerShell** in the DevOps InControl folder.  
   *(Right-click the folder → "Open in Terminal", or `cd` to it.)*

2. **Run the installer:**

   ```powershell
   .\install.ps1
   ```

   This will:
   - Verify .NET and Node.js are installed.
   - Download frontend and backend dependencies.
   - Build the app.
   - **Ask if you want DevOps InControl to start automatically when you log in.** Press Enter (or type `Y`) for yes.

That's it. Installation is done.

---

## Starting the app

```powershell
.\start.ps1
```

Your browser will open to **http://localhost:5172** with a setup wizard that walks you through:

1. **Set a password** — protects your dashboard.
2. **Enter your Azure DevOps organization name and PAT** — the wizard verifies they work.
3. **Pick your first project** — select which Azure DevOps project to monitor.

After setup, the dashboard is ready. Bookmark **http://localhost:5172** for next time.

> **Tip:** If you enabled auto-start during installation, the app starts silently every time you log into Windows. Just open the bookmark.

---

## Create a Personal Access Token (PAT)

1. Go to [dev.azure.com](https://dev.azure.com) and sign in.
2. Click your profile picture (top right) → **Personal access tokens**.
3. Click **+ New Token**.
4. Give it a name (e.g. "DevOps InControl").
5. Set the expiration to the maximum your organization allows.
6. Click 'Show all scopes'
7. Under **Scopes**, select:
- **Build (Read)** — pipeline runs overview
- **Code (Read)** — pull request monitoring
- **Graph (Read)** — group membership resolution
- **Identity (Read)** — permission checks
- **Project and Team (Read)** — project and team listing
- **Release (Read)** — release deployments overview
- **Security (Manage)** — repo & area permission audits
- **Wiki (Read)** — wiki permission checks
- **Work Items (Read & Write)** — backlog checks, sprint monitoring, tag management

8. Click **Create** and **Copy the token** — you won't see it again.

Paste this token into the setup wizard when prompted.

---

## Stopping the app

Press **Ctrl+C** in the PowerShell window where the app is running. If it was started automatically (background mode), run:

```powershell
Get-Process -Name DashboardApi | Stop-Process
```

---

## Updating

Pull the latest code, then re-run the installer:

```powershell
git pull
.\install.ps1
```

---

## Troubleshooting

| Problem | Solution |
|---|---|
| `dotnet: command not found` | Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and restart PowerShell. |
| `npm: command not found` | Install [Node.js](https://nodejs.org) and restart PowerShell. |
| Build fails with "file is locked" | Another instance is running. Run `Get-Process -Name DashboardApi \| Stop-Process` first, then try again. |
| Browser shows raw HTML text | Run `.\install.ps1` again to rebuild, then `.\start.ps1`. |
| "Execution of scripts is disabled" | Run `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` and try again. |
| Port 5172  already in use | Run f.e. `.\start.ps1 -BackendPort 9090` to use a different port. |

## Contributions

We are not accepting external pull requests for this repository at this time.

This repository is maintained by Fenêtre BV. Issues, suggestions, or questions may still be shared through the appropriate communication channels, but unsolicited pull requests may be closed without review.

Thank you for your understanding.

## Follow Us

<p>
<a href="https://www.facebook.com/fenetrebv" target="_blank"><img src="https://cdn.simpleicons.org/facebook/00aeef" alt="Fenêtre on Facebook" title="Fenêtre on Facebook" height="24"></a>&nbsp;&nbsp;
<a href="https://twitter.com/FenetreBV" target="_blank"><img src="https://cdn.simpleicons.org/x/00aeef" alt="Fenêtre on X" title="Fenêtre on X" height="24"></a>&nbsp;&nbsp;
<a href="https://www.linkedin.com/company/fenetre-online-solutions/" target="_blank"><img src="https://api.iconify.design/simple-icons:linkedin.svg?color=%2300aeef" alt="Fenêtre on LinkedIn" title="Fenêtre on LinkedIn" height="24" width="24px"></a>&nbsp;&nbsp;
<a href="https://www.instagram.com/fenetrebv/" target="_blank"><img src="https://cdn.simpleicons.org/instagram/00aeef" alt="Fenêtre on Instagram" title="Fenêtre on Instagram" height="24"></a>&nbsp;&nbsp;
<a href="https://www.youtube.com/FenetreBV" target="_blank"><img src="https://cdn.simpleicons.org/youtube/00aeef" alt="Fenêtre on YouTube" title="Fenêtre on YouTube" height="24"></a>&nbsp;&nbsp;
<a href="https://github.com/Fenetre" target="_blank"><img src="https://cdn.simpleicons.org/github/00aeef" alt="Fenêtre on GitHub" title="Fenêtre on GitHub" height="24"></a>
</p>
