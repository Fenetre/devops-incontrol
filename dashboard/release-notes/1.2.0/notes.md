# What's new in version 1.2.0

## New UI update
The dashboard has been refreshed with Nuxt UI components for a more modern and consistent look and feel.

## Easier allowlist management for DB Monitor
You can now add or remove databases from the allowlist directly from the DB Monitor view. Each database row shows a shield button on hover that toggles the allowlist status instantly — no need to edit the project configuration manually.

## Reviewers overview in PR Monitor
The PR Monitor now includes a Reviewers tab that groups active pull requests by reviewer. This makes it easy to spot bottlenecks — see at a glance who has the most pending reviews, and drill into their assigned PRs with vote status.

## Sprint Creator
A new "Sprint Creator" tab in the Sprint Manager lets you create sprints directly from the dashboard. It automatically suggests the next sprint name, parent path, start date, and end date based on the patterns found in existing sprints. Two actions are available:
- **Create Sprint** — creates the iteration at the project level only.
- **Create & Add to Team** — creates the iteration and subscribes the selected team to it.

## Sprint Populator template manipulation
The Sprint Populator now supports selecting and excluding individual templates and tasks before applying. You can uncheck stories or specific tasks in the preview step so only the work items you need are cloned into the new sprint.

## Template Manager
A new Template Manager view lets you manage and copy work item templates across Azure DevOps projects and teams. Key features:

- **Create templates** — build new work item templates directly from the dashboard with a guided step-by-step form (project → team → work item type → details).
- **Delete templates** — remove templates you no longer need, with a confirmation dialog to prevent accidents.
- **Use as base** — clone an existing template as a starting point for a new one, pre-filling name, description, and all field values.
- **Default field values** — set pre-filled field values using a visual editor. Each field can only be added once, and available fields are loaded dynamically based on the selected work item type.
- **Multi-select & copy** — pick one or more templates from a source project/team and copy them to multiple target projects at once.
- **Custom field detection** — the preview detects custom fields that are missing in target projects and lists them clearly before you apply. Templates requiring missing fields are automatically skipped during copy.
- **Overwrite existing templates** — when a template with the same name already exists in the target, you can choose to overwrite it (delete + recreate) or create a duplicate alongside it.
- **Grouped results** — apply results are shown by outcome (created, overwritten, skipped, failed) with clear explanations and PAT permission guidance when relevant.

## Windows Installer
DevOps InControl now ships as a proper Windows installer (`DevOpsInControl-1.2.0-Setup.exe`). Double-click to install — no developer tools required.

- Installs to your user profile (no admin rights needed)
- Creates a Start Menu shortcut with a branded terminal launcher
- Optional desktop shortcut and auto-start at logon
- Registers in Apps & Features with a proper uninstaller
- Includes your existing configuration so it works immediately after install

## Roadmap
A brand-new portfolio roadmap board that gives you a single visual overview of all your features and epics across multiple Azure DevOps projects and teams.

- **Multi-project, multi-team in one view** — see features from different projects side by side, each color-coded by project. No more switching between backlogs to understand cross-team timelines.
- **Sprint overlay across teams** — view sprints from multiple teams layered on a single timeline, making it easy to spot alignment issues and plan handoffs across team boundaries.
- **Quarter and Sprint views** — toggle between a high-level quarterly view or a detailed sprint-based Gantt layout depending on your planning horizon.
- **Drag to reschedule** — grab any feature bar and drag or resize it to change start and target dates. Changes are staged locally and pushed to Azure DevOps in one batch.
- **Epic grouping with smart ordering** — features are automatically grouped under their parent epics, with epics sorted by the earliest planned child so the most relevant work appears first.
- **Dependency linking** — click the chain icon on any feature to link it as predecessor or successor to another. Dependencies are shown as visual arrows between bars.
- **Dirty/push workflow** — all changes (dates, links) are staged locally with a clear amber indicator. Review your changes in a preview panel before pushing them to Azure DevOps.
- **Filter and search** — narrow the board by project, state, or free-text search to focus on what matters right now.
- **Inline detail editing** — click any card to open a detail panel where you can edit title, state, assignee, iteration, tags, description, and custom fields — all without leaving the roadmap.
- **Create epics and features** — add new epics or features directly from the roadmap. Pick a project, set a title, and optionally assign dates — the work item is created in Azure DevOps and appears on the board immediately.