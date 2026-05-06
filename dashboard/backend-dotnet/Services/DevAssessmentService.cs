using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DashboardApi.Models;

namespace DashboardApi.Services;

/// <summary>
/// Handles all Azure DevOps data fetching and enrichment for the Dev Assessment feature.
/// </summary>
public static class DevAssessmentService
{
    // ===================================================================
    // Completed PRs with enriched data
    // ===================================================================

    public static async Task<List<DevAssessmentPrItem>> FetchCompletedPrs(
        HttpClient http, string organization, string project, int months)
        => await FetchCompletedPrs(http, organization, project,
            DateTime.UtcNow.AddMonths(-months), null);

    public static async Task<List<DevAssessmentPrItem>> FetchCompletedPrs(
        HttpClient http, string organization, string project, DateTime since, DateTime? until)
    {
        var projUrl = $"https://dev.azure.com/{organization}/{Uri.EscapeDataString(project)}/_apis";
        var minDate = since.ToString("o");

        // 1. Fetch all completed PRs (paged)
        var allPrs = new List<JsonElement>();
        var skip = 0;
        while (true)
        {
            var url = $"{projUrl}/git/pullrequests" +
                      $"?searchCriteria.status=completed" +
                      $"&searchCriteria.minTime={Uri.EscapeDataString(minDate)}" +
                      $"&$skip={skip}&$top=100&api-version=7.1";

            var resp = await http.GetAsync(url);
            AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
            if (!resp.IsSuccessStatusCode) break;

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            var batch = new List<JsonElement>();
            if (doc.RootElement.TryGetProperty("value", out var items))
                foreach (var item in items.EnumerateArray())
                    batch.Add(item.Clone());

            if (batch.Count == 0) break;
            allPrs.AddRange(batch);
            if (batch.Count < 100) break;
            skip += batch.Count;
        }

        // Filter by until date if provided (PR API only supports minTime, not maxTime)
        if (until.HasValue)
        {
            var maxDate = until.Value;
            allPrs = allPrs.Where(pr =>
            {
                if (pr.TryGetProperty("closedDate", out var cd) && cd.ValueKind == JsonValueKind.String)
                {
                    if (DateTime.TryParse(cd.GetString(), out var closed))
                        return closed <= maxDate;
                }
                return true;
            }).ToList();
        }

        // 2. Enrich each PR in parallel
        var results = new ConcurrentBag<DevAssessmentPrItem>();
        await Parallel.ForEachAsync(allPrs, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (pr, _) =>
        {
            var item = await EnrichPr(http, projUrl, pr, organization, project);
            if (item != null) results.Add(item);
        });

        return results.ToList();
    }

    // ===================================================================
    // PR enrichment — orchestrator
    // ===================================================================

    private static async Task<DevAssessmentPrItem?> EnrichPr(
        HttpClient http, string projUrl, JsonElement pr, string organization, string project)
    {
        var prId = pr.GetProperty("pullRequestId").GetInt32();
        var repoId = GetNestedString(pr, "repository", "id");
        var repoName = GetNestedString(pr, "repository", "name");
        var title = GetString(pr, "title");
        var createdBy = GetNestedString(pr, "createdBy", "displayName");
        var createdByUniqueName = GetNestedString(pr, "createdBy", "uniqueName").ToLowerInvariant();
        var creationDate = GetString(pr, "creationDate");
        var closedDate = GetString(pr, "closedDate");

        if (string.IsNullOrEmpty(repoId)) return null;

        // Compute business hours to complete (8h workday, Mon-Fri)
        double? hoursToComplete = null;
        DateTimeOffset closedAt = default;
        DateTimeOffset prStartTime = default;
        if (DateTimeOffset.TryParse(creationDate, out var createdAt))
            prStartTime = createdAt;
        if (DateTimeOffset.TryParse(closedDate, out closedAt) && prStartTime != default)
            hoursToComplete = CountBusinessHours(prStartTime, closedAt);

        // --- Parallel enrichment fetches ---
        var threadTask = FetchThreads(http, projUrl, repoId, prId);
        var changeTask = FetchChangeStats(http, projUrl, repoId, prId);
        var wiCountTask = FetchLinkedWorkItemCount(http, projUrl, repoId, prId);

        await Task.WhenAll(threadTask, changeTask, wiCountTask);

        var (threads, lastIterationDate) = threadTask.Result;
        var (filesChanged, linesAdded, linesDeleted) = changeTask.Result;
        var linkedWiCount = wiCountTask.Result;

        // --- Focused analysis helpers ---
        var (reviewers, selfApproved) = AnalyzeReviewers(pr, createdByUniqueName);
        var threadResult = AnalyzeThreads(threads, createdByUniqueName, prStartTime, closedAt);

        // Recalculate hours-to-complete from effective start (accounts for draft publish)
        if (threadResult.EffectiveStart != default && closedAt != default)
            hoursToComplete = CountBusinessHours(threadResult.EffectiveStart, closedAt);

        // Approval to merge timing
        double? hoursApprovalToMerge = null;
        var lastDeltaAfterApproval = false;
        if (lastIterationDate != null && closedAt != default)
        {
            hoursApprovalToMerge = Math.Round(Math.Max(0, (closedAt - lastIterationDate.Value).TotalHours), 1);
            lastDeltaAfterApproval = CheckLastDeltaAfterApproval(threads, createdByUniqueName, lastIterationDate.Value);
        }

        return new DevAssessmentPrItem
        {
            PrId = prId,
            Title = title,
            Repository = repoName,
            CreatedBy = createdBy,
            Project = project,
            Organization = organization,
            CreationDate = creationDate,
            ClosedDate = closedDate,
            HoursToComplete = hoursToComplete,
            Iterations = threadResult.WaitForAuthorCount + (threadResult.HasApproval ? 1 : 0),
            FilesChanged = filesChanged,
            LinesAdded = linesAdded,
            LinesDeleted = linesDeleted,
            LinkedWorkItemCount = linkedWiCount,
            HoursToFirstReview = threadResult.HoursToFirstReview,
            HoursAuthorResponse = threadResult.HoursAuthorResponse,
            HoursApprovalToMerge = hoursApprovalToMerge,
            ReviewCycles = threadResult.ReviewCycles,
            UnresolvedThreadCount = threadResult.UnresolvedCount,
            LastDeltaAfterApproval = lastDeltaAfterApproval,
            SelfApproved = selfApproved,
            Reviewers = reviewers,
        };
    }

    // ===================================================================
    // Reviewer analysis
    // ===================================================================

    private static (List<DevAssessmentReviewer> reviewers, bool selfApproved) AnalyzeReviewers(
        JsonElement pr, string createdByUniqueName)
    {
        var reviewers = new List<DevAssessmentReviewer>();
        var selfApproved = false;

        if (!pr.TryGetProperty("reviewers", out var reviewerArr))
            return (reviewers, true); // no reviewers = self-approved

        var approverFound = false;
        foreach (var r in reviewerArr.EnumerateArray())
        {
            var displayName = GetString(r, "displayName");
            var vote = r.TryGetProperty("vote", out var v) ? v.GetInt32() : 0;

            if (!string.IsNullOrEmpty(displayName))
                reviewers.Add(new DevAssessmentReviewer { Name = displayName, Vote = vote });

            if (vote > 0)
            {
                approverFound = true;
                var uniqueName = GetString(r, "uniqueName").ToLowerInvariant();
                if (uniqueName == createdByUniqueName)
                    selfApproved = true;
            }
        }

        if (!approverFound)
            selfApproved = true; // merged with no approvals

        return (reviewers, selfApproved);
    }

    // ===================================================================
    // Thread analysis
    // ===================================================================

    internal record ThreadAnalysisResult(
        double? HoursToFirstReview,
        double? HoursAuthorResponse,
        int UnresolvedCount,
        int ReviewCycles,
        int WaitForAuthorCount,
        bool HasApproval,
        DateTimeOffset EffectiveStart);

    private static ThreadAnalysisResult AnalyzeThreads(
        List<JsonElement> threads, string createdByUniqueName,
        DateTimeOffset prStartTime, DateTimeOffset closedAt)
    {
        if (threads.Count == 0)
            return new ThreadAnalysisResult(null, null, 0, 0, 0, false, prStartTime);

        double? hoursToFirstReview = null;
        double? hoursAuthorResponse = null;
        var unresolvedCount = 0;
        var reviewCycles = 0;
        var waitForAuthorCount = 0;
        var hasApproval = false;

        DateTimeOffset? firstReviewComment = null;
        DateTimeOffset? lastReviewerComment = null;
        DateTimeOffset? authorResponseAfterReview = null;
        DateTimeOffset? draftPublishedDate = null;

        foreach (var thread in threads)
        {
            // Count unresolved threads
            var status = GetString(thread, "status");
            if (status is "active" or "pending")
                unresolvedCount++;

            // Check for draft-published event and vote threads
            if (thread.TryGetProperty("properties", out var props))
            {
                // Detect when PR was published from draft
                if (props.TryGetProperty("CodeReviewIsDraftUpdated", out _))
                {
                    if (thread.TryGetProperty("publishedDate", out var pubDateEl) &&
                        DateTimeOffset.TryParse(pubDateEl.GetString(), out var pubTs) &&
                        (draftPublishedDate == null || pubTs > draftPublishedDate))
                    {
                        draftPublishedDate = pubTs;
                    }
                }

                if (props.TryGetProperty("CodeReviewVoteResult", out var voteResult))
                {
                    var voteVal = voteResult.TryGetProperty("$value", out var vv) ? vv.GetString() ?? "" : "";
                    if (voteVal == "0") reviewCycles++;
                    if (voteVal == "-5") waitForAuthorCount++;  // "Wait for author"
                    if (voteVal is "10" or "5") hasApproval = true; // Approved / Approved with suggestions
                }
            }

            if (!thread.TryGetProperty("comments", out var comments)) continue;
            foreach (var comment in comments.EnumerateArray())
            {
                var commentType = GetString(comment, "commentType");
                if (commentType == "system") continue;

                var authorUniqueName = GetNestedString(comment, "author", "uniqueName").ToLowerInvariant();
                if (!DateTimeOffset.TryParse(GetString(comment, "publishedDate"), out var pubDate)) continue;

                if (authorUniqueName != createdByUniqueName)
                {
                    // Reviewer comment
                    if (firstReviewComment == null || pubDate < firstReviewComment)
                        firstReviewComment = pubDate;
                    if (lastReviewerComment == null || pubDate > lastReviewerComment)
                        lastReviewerComment = pubDate;
                }
                else if (lastReviewerComment != null && pubDate > lastReviewerComment)
                {
                    // Author response after a reviewer comment
                    if (authorResponseAfterReview == null || pubDate < authorResponseAfterReview)
                        authorResponseAfterReview = pubDate;
                }
            }
        }

        // Use draft-published date as effective start if the PR was ever a draft
        var effectiveStart = draftPublishedDate ?? prStartTime;

        if (firstReviewComment != null && effectiveStart != default)
            hoursToFirstReview = CountBusinessHours(effectiveStart, firstReviewComment.Value);

        if (lastReviewerComment != null && authorResponseAfterReview != null)
            hoursAuthorResponse = Math.Round((authorResponseAfterReview.Value - lastReviewerComment.Value).TotalHours, 1);

        return new ThreadAnalysisResult(
            hoursToFirstReview, hoursAuthorResponse, unresolvedCount,
            reviewCycles, waitForAuthorCount, hasApproval, effectiveStart);
    }

    // ===================================================================
    // Last delta after approval check
    // ===================================================================

    private static bool CheckLastDeltaAfterApproval(
        List<JsonElement> threads, string createdByUniqueName, DateTimeOffset lastIterationDate)
    {
        DateTimeOffset? lastReviewTs = null;
        foreach (var thread in threads)
        {
            if (!thread.TryGetProperty("comments", out var comments)) continue;
            foreach (var c in comments.EnumerateArray())
            {
                var aun = GetNestedString(c, "author", "uniqueName").ToLowerInvariant();
                if (aun != createdByUniqueName && DateTimeOffset.TryParse(GetString(c, "publishedDate"), out var pd))
                    if (lastReviewTs == null || pd > lastReviewTs) lastReviewTs = pd;
            }
        }
        return lastReviewTs != null && lastIterationDate > lastReviewTs;
    }

    // ===================================================================
    // Work Items
    // ===================================================================

    public static async Task<List<DevAssessmentWorkItem>> FetchWorkItems(
        HttpClient http, string organization, string project, int months)
        => await FetchWorkItems(http, organization, project,
            DateTime.UtcNow.AddMonths(-months), null);

    public static async Task<List<DevAssessmentWorkItem>> FetchWorkItems(
        HttpClient http, string organization, string project, DateTime since, DateTime? until)
    {
        var projUrl = $"https://dev.azure.com/{organization}/{Uri.EscapeDataString(project)}/_apis";
        var minDate = since.ToString("yyyy-MM-dd");
        var maxClause = until.HasValue
            ? $"AND [System.ChangedDate] <= '{until.Value:yyyy-MM-dd}'"
            : "";

        // WIQL: all work items changed in date range
        var wiql = $@"SELECT [System.Id] FROM WorkItems
            WHERE [System.TeamProject] = @project
              AND [System.WorkItemType] IN ('Task','Bug','User Story')
              AND [System.ChangedDate] >= '{minDate}'
              {maxClause}
            ORDER BY [System.ChangedDate] DESC";

        var wiqlUrl = $"{projUrl}/wit/wiql?api-version=7.1";
        var wiqlResp = await http.PostAsync(wiqlUrl, MakeJsonContent(new { query = wiql }));
        AzureDevOpsClient.ThrowIfPatScopeError(wiqlResp, wiqlUrl);
        if (!wiqlResp.IsSuccessStatusCode) return [];

        using var wiqlDoc = await JsonDocument.ParseAsync(await wiqlResp.Content.ReadAsStreamAsync());
        var ids = new List<int>();
        if (wiqlDoc.RootElement.TryGetProperty("workItems", out var wiItems))
            foreach (var wi in wiItems.EnumerateArray())
                if (wi.TryGetProperty("id", out var id))
                    ids.Add(id.GetInt32());

        if (ids.Count == 0) return [];

        // Batch fetch work items
        var fields = new[]
        {
            "System.Id", "System.Title", "System.WorkItemType", "System.State",
            "System.AssignedTo", "System.CreatedDate", "Microsoft.VSTS.Common.ClosedDate",
            "Microsoft.VSTS.Scheduling.OriginalEstimate", "Microsoft.VSTS.Scheduling.CompletedWork",
            "Microsoft.VSTS.Scheduling.RemainingWork", "System.ChangedDate"
        };

        var batchUrl = $"{projUrl}/wit/workitemsbatch?api-version=7.1";
        var workItems = new List<JsonElement>();

        foreach (var chunk in ids.Chunk(200))
        {
            var resp = await http.PostAsync(batchUrl, MakeJsonContent(new { ids = chunk, fields, expand = "Relations" }));
            AzureDevOpsClient.ThrowIfPatScopeError(resp, batchUrl);
            if (!resp.IsSuccessStatusCode) continue;
            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            if (doc.RootElement.TryGetProperty("value", out var items))
                foreach (var item in items.EnumerateArray())
                    workItems.Add(item.Clone());
        }

        var childMap = BuildChildMap(workItems);
        var stateChangeCounts = await FetchStateChangeCounts(http, projUrl, workItems);
        return BuildWorkItemResults(workItems, stateChangeCounts, childMap, organization, project);
    }

    private static Dictionary<int, List<int>> BuildChildMap(List<JsonElement> workItems)
    {
        var childMap = new Dictionary<int, List<int>>();
        foreach (var wi in workItems)
        {
            var wiId = wi.TryGetProperty("id", out var idVal) ? idVal.GetInt32() : 0;
            if (wiId == 0 || !wi.TryGetProperty("relations", out var rels)) continue;
            foreach (var rel in rels.EnumerateArray())
            {
                var relType = rel.TryGetProperty("rel", out var rt) ? rt.GetString() ?? "" : "";
                if (relType != "System.LinkTypes.Hierarchy-Forward") continue; // child link
                var url = rel.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                var lastSlash = url.LastIndexOf('/');
                if (lastSlash >= 0 && int.TryParse(url[(lastSlash + 1)..], out var childId))
                {
                    if (!childMap.ContainsKey(wiId)) childMap[wiId] = [];
                    childMap[wiId].Add(childId);
                }
            }
        }
        return childMap;
    }

    private static async Task<ConcurrentDictionary<int, (int stateChanges, int reopenCount, DateTimeOffset? firstInProgress)>>
        FetchStateChangeCounts(HttpClient http, string projUrl, List<JsonElement> workItems)
    {
        var stateChangeCounts = new ConcurrentDictionary<int, (int stateChanges, int reopenCount, DateTimeOffset? firstInProgress)>();
        var itemsToCheck = workItems.Take(500).ToList();
        await Parallel.ForEachAsync(itemsToCheck, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (wi, _) =>
        {
            var itemId = wi.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
            if (itemId == 0) return;

            var updUrl = $"{projUrl}/wit/workitems/{itemId}/updates?api-version=7.1&$top=100";
            var resp = await http.GetAsync(updUrl);
            AzureDevOpsClient.ThrowIfPatScopeError(resp, updUrl);
            if (!resp.IsSuccessStatusCode) return;

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            if (!doc.RootElement.TryGetProperty("value", out var updates)) return;

            var stateChanges = 0;
            var reopens = 0;
            DateTimeOffset? firstInProgress = null;

            foreach (var upd in updates.EnumerateArray())
            {
                if (!upd.TryGetProperty("fields", out var flds)) continue;
                if (!flds.TryGetProperty("System.State", out var stateChange)) continue;

                var newVal = stateChange.TryGetProperty("newValue", out var nv) ? nv.GetString() ?? "" : "";
                var oldVal = stateChange.TryGetProperty("oldValue", out var ov) ? ov.GetString() ?? "" : "";

                if (!string.IsNullOrEmpty(oldVal) && !string.IsNullOrEmpty(newVal) && oldVal != newVal)
                {
                    stateChanges++;
                    if (oldVal is "Closed" or "Resolved" or "Done" &&
                        newVal is "Active" or "New" or "Committed")
                        reopens++;
                }

                // Track first time entering an active state
                if (firstInProgress == null && newVal is "In Progress" or "Active" or "Committed")
                {
                    if (flds.TryGetProperty("System.ChangedDate", out var changedDate))
                    {
                        var dateStr = changedDate.TryGetProperty("newValue", out var dv) ? dv.GetString() ?? "" : "";
                        if (DateTimeOffset.TryParse(dateStr, out var dt))
                            firstInProgress = dt;
                    }
                    // Fallback: use the revision date
                    if (firstInProgress == null && upd.TryGetProperty("revisedDate", out var revised) &&
                        DateTimeOffset.TryParse(revised.GetString(), out var revDt) && revDt.Year < 9999)
                        firstInProgress = revDt;
                }
            }

            stateChangeCounts[itemId] = (stateChanges, reopens, firstInProgress);
        });

        return stateChangeCounts;
    }

    private static List<DevAssessmentWorkItem> BuildWorkItemResults(
        List<JsonElement> workItems,
        ConcurrentDictionary<int, (int stateChanges, int reopenCount, DateTimeOffset? firstInProgress)> stateChangeCounts,
        Dictionary<int, List<int>> childMap,
        string organization, string project)
    {
        var result = new List<DevAssessmentWorkItem>();
        foreach (var wi in workItems)
        {
            var itemId = wi.TryGetProperty("id", out var idP) ? idP.GetInt32() : 0;
            if (itemId == 0) continue;
            if (!wi.TryGetProperty("fields", out var f)) continue;

            var createdStr = GetFieldString(f, "System.CreatedDate");
            var closedStr = GetFieldString(f, "Microsoft.VSTS.Common.ClosedDate");
            var state = GetFieldString(f, "System.State");
            var workItemType = GetFieldString(f, "System.WorkItemType");

            double? cycleTime = null;
            double? activeAge = null;

            stateChangeCounts.TryGetValue(itemId, out var counts);

            // Determine start time based on work item type
            DateTimeOffset? startTime = null;
            if (workItemType == "Bug")
            {
                // Bug: starts when it enters In Progress
                startTime = counts.firstInProgress;
            }
            else if (workItemType == "User Story")
            {
                // User Story: starts when first child task enters In Progress
                if (childMap.TryGetValue(itemId, out var childIds))
                {
                    foreach (var cid in childIds)
                    {
                        if (stateChangeCounts.TryGetValue(cid, out var childCounts) && childCounts.firstInProgress != null)
                        {
                            if (startTime == null || childCounts.firstInProgress < startTime)
                                startTime = childCounts.firstInProgress;
                        }
                    }
                }
            }
            else
            {
                // Tasks and other types: use own firstInProgress, fallback to created date
                startTime = counts.firstInProgress;
            }

            // Fallback to created date if no In Progress transition found
            if (startTime == null && DateTimeOffset.TryParse(createdStr, out var createdDtFallback))
                startTime = createdDtFallback;

            if (startTime != null)
            {
                if (!string.IsNullOrEmpty(closedStr) && DateTimeOffset.TryParse(closedStr, out var closedDt))
                    cycleTime = CountBusinessHours(startTime.Value, closedDt);

                if (state is "Active" or "Committed" or "New" or "In Progress" or "Resolved")
                    activeAge = CountBusinessHours(startTime.Value, DateTimeOffset.UtcNow);
            }

            var assignedTo = "";
            if (f.TryGetProperty("System.AssignedTo", out var at))
                assignedTo = at.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";

            result.Add(new DevAssessmentWorkItem
            {
                Id = itemId,
                Title = GetFieldString(f, "System.Title"),
                WorkItemType = workItemType,
                Project = project,
                Organization = organization,
                AssignedTo = assignedTo,
                State = state,
                CreatedDate = createdStr,
                ClosedDate = string.IsNullOrEmpty(closedStr) ? null : closedStr,
                CycleTimeHours = cycleTime,
                ActiveAgeHours = activeAge,
                OriginalEstimate = GetFieldDouble(f, "Microsoft.VSTS.Scheduling.OriginalEstimate"),
                CompletedWork = GetFieldDouble(f, "Microsoft.VSTS.Scheduling.CompletedWork"),
                RemainingWork = GetFieldDouble(f, "Microsoft.VSTS.Scheduling.RemainingWork"),
                ReopenCount = counts.reopenCount,
                StateChanges = counts.stateChanges,
            });
        }
        return result;
    }

    // ===================================================================
    // Stale Review PRs (active PRs waiting >2 business days)
    // ===================================================================

    public static async Task<(List<DevAssessmentStaleReviewPr> stale, List<DevAssessmentOpenPr> openPrs)> FetchActivePrData(
        HttpClient http, string organization, string project)
    {
        var projUrl = $"https://dev.azure.com/{organization}/{Uri.EscapeDataString(project)}/_apis";

        // Fetch active PRs
        var allPrs = new List<JsonElement>();
        var skip = 0;
        while (true)
        {
            var url = $"{projUrl}/git/pullrequests?searchCriteria.status=active&$skip={skip}&$top=100&api-version=7.1";
            var resp = await http.GetAsync(url);
            AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
            if (!resp.IsSuccessStatusCode) break;
            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            var batch = new List<JsonElement>();
            if (doc.RootElement.TryGetProperty("value", out var items))
                foreach (var item in items.EnumerateArray())
                    batch.Add(item.Clone());
            if (batch.Count == 0) break;
            allPrs.AddRange(batch);
            if (batch.Count < 100) break;
            skip += batch.Count;
        }

        var stale = new ConcurrentBag<DevAssessmentStaleReviewPr>();
        var openPrs = new ConcurrentBag<DevAssessmentOpenPr>();
        await Parallel.ForEachAsync(allPrs, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (pr, _) =>
        {
            var prId = pr.GetProperty("pullRequestId").GetInt32();
            var repoId = GetNestedString(pr, "repository", "id");
            var createdByUnique = GetNestedString(pr, "createdBy", "uniqueName").ToLowerInvariant();
            var creationDate = GetString(pr, "creationDate");
            var isDraft = pr.TryGetProperty("isDraft", out var d) && d.GetBoolean();

            if (isDraft || string.IsNullOrEmpty(repoId)) return;

            // Fetch threads for unresolved count and stale review check
            var threadUrl = $"{projUrl}/git/repositories/{repoId}/pullrequests/{prId}/threads?api-version=7.1";
            var threadResp = await http.GetAsync(threadUrl);
            AzureDevOpsClient.ThrowIfPatScopeError(threadResp, threadUrl);
            if (!threadResp.IsSuccessStatusCode) return;

            using var threadDoc = await JsonDocument.ParseAsync(await threadResp.Content.ReadAsStreamAsync());
            var unresolvedCount = 0;
            var hasReview = false;
            if (threadDoc.RootElement.TryGetProperty("value", out var threads))
            {
                foreach (var thread in threads.EnumerateArray())
                {
                    var status = GetString(thread, "status");
                    if (status is "active" or "pending")
                        unresolvedCount++;

                    if (!hasReview && thread.TryGetProperty("comments", out var comments))
                    {
                        foreach (var c in comments.EnumerateArray())
                        {
                            if (GetString(c, "commentType") == "system") continue;
                            var authorUnique = GetNestedString(c, "author", "uniqueName").ToLowerInvariant();
                            if (authorUnique != createdByUnique) { hasReview = true; break; }
                        }
                    }
                }
            }

            // Add to open PRs list
            var repoName = GetNestedString(pr, "repository", "name");
            openPrs.Add(new DevAssessmentOpenPr
            {
                PrId = prId,
                Title = GetString(pr, "title"),
                Repository = repoName,
                CreatedBy = GetNestedString(pr, "createdBy", "displayName"),
                Project = project,
                Organization = organization,
                UnresolvedThreadCount = unresolvedCount,
            });

            // Stale review check
            if (!DateTimeOffset.TryParse(creationDate, out var createdAt)) return;
            var bizDays = CountBusinessDays(createdAt, DateTimeOffset.UtcNow);
            if (bizDays < 2 || hasReview) return;

            var reviewers = new List<string>();
            if (pr.TryGetProperty("reviewers", out var revArr))
                foreach (var r in revArr.EnumerateArray())
                {
                    var name = GetString(r, "displayName");
                    if (!string.IsNullOrEmpty(name)) reviewers.Add(name);
                }

            stale.Add(new DevAssessmentStaleReviewPr
            {
                PrId = prId,
                Title = GetString(pr, "title"),
                Url = $"https://dev.azure.com/{organization}/{Uri.EscapeDataString(project)}/_git/{Uri.EscapeDataString(repoName)}/pullrequest/{prId}",
                Repository = repoName,
                CreatedBy = GetNestedString(pr, "createdBy", "displayName"),
                Project = project,
                Organization = organization,
                CreationDate = creationDate,
                BusinessDaysWaiting = bizDays,
                Reviewers = reviewers,
            });
        });

        return (stale.ToList(), openPrs.ToList());
    }

    // ===================================================================
    // Per-PR enrichment helpers
    // ===================================================================

    private static async Task<(List<JsonElement> threads, DateTimeOffset? lastIterationDate)> FetchThreads(
        HttpClient http, string projUrl, string repoId, int prId)
    {
        // Fetch threads
        var threadUrl = $"{projUrl}/git/repositories/{repoId}/pullrequests/{prId}/threads?api-version=7.1";
        var threadResp = await http.GetAsync(threadUrl);
        AzureDevOpsClient.ThrowIfPatScopeError(threadResp, threadUrl);
        var threads = new List<JsonElement>();
        if (threadResp.IsSuccessStatusCode)
        {
            using var doc = await JsonDocument.ParseAsync(await threadResp.Content.ReadAsStreamAsync());
            if (doc.RootElement.TryGetProperty("value", out var arr))
                foreach (var t in arr.EnumerateArray())
                    threads.Add(t.Clone());
        }

        // Fetch iterations for last push date
        DateTimeOffset? lastIterDate = null;
        var iterUrl = $"{projUrl}/git/repositories/{repoId}/pullrequests/{prId}/iterations?api-version=7.1";
        var iterResp = await http.GetAsync(iterUrl);
        if (iterResp.IsSuccessStatusCode)
        {
            using var doc = await JsonDocument.ParseAsync(await iterResp.Content.ReadAsStreamAsync());
            if (doc.RootElement.TryGetProperty("value", out var iters))
            {
                foreach (var iter in iters.EnumerateArray())
                {
                    var dateStr = GetString(iter, "createdDate");
                    if (DateTimeOffset.TryParse(dateStr, out var dt))
                        if (lastIterDate == null || dt > lastIterDate) lastIterDate = dt;
                }
            }
        }

        return (threads, lastIterDate);
    }

    private static async Task<(int files, int added, int deleted)> FetchChangeStats(
        HttpClient http, string projUrl, string repoId, int prId)
    {
        var url = $"{projUrl}/git/repositories/{repoId}/pullrequests/{prId}/iterations/1/changes?api-version=7.1&$top=1000";
        var resp = await http.GetAsync(url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode) return (0, 0, 0);

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var files = 0;

        if (doc.RootElement.TryGetProperty("changeEntries", out var entries))
            files = entries.GetArrayLength();

        // Fallback: try the simpler changes endpoint for file count
        if (files == 0)
        {
            var url2 = $"{projUrl}/git/repositories/{repoId}/pullrequests/{prId}/changes?api-version=7.1";
            var resp2 = await http.GetAsync(url2);
            if (resp2.IsSuccessStatusCode)
            {
                using var doc2 = await JsonDocument.ParseAsync(await resp2.Content.ReadAsStreamAsync());
                if (doc2.RootElement.TryGetProperty("changes", out var changes))
                    files = changes.GetArrayLength();
            }
        }

        // Azure DevOps doesn't reliably return line counts in the PR changes API,
        // so we report file count as the primary size metric
        return (files, 0, 0);
    }

    private static async Task<int> FetchLinkedWorkItemCount(
        HttpClient http, string projUrl, string repoId, int prId)
    {
        var url = $"{projUrl}/git/repositories/{repoId}/pullrequests/{prId}/workitems?api-version=7.1";
        var resp = await http.GetAsync(url);
        AzureDevOpsClient.ThrowIfPatScopeError(resp, url);
        if (!resp.IsSuccessStatusCode) return 0;
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        return doc.RootElement.TryGetProperty("value", out var arr) ? arr.GetArrayLength() : 0;
    }

    // ===================================================================
    // Utility helpers
    // ===================================================================

    internal static double CountBusinessHours(DateTimeOffset from, DateTimeOffset to)
    {
        const double dayStart = 9.0;
        const double dayEnd = 17.0;

        if (to <= from) return 0;

        double total = 0;
        var current = from;

        while (current < to)
        {
            if (current.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                var todayStart = current.Date.AddHours(dayStart);
                var todayEnd = current.Date.AddHours(dayEnd);

                var windowStart = current < todayStart ? todayStart : current.DateTime;
                var windowEnd = to.DateTime < todayEnd ? to.DateTime : todayEnd;

                if (windowEnd > windowStart)
                    total += (windowEnd - windowStart).TotalHours;
            }
            current = new DateTimeOffset(current.Date.AddDays(1).AddHours(dayStart), current.Offset);
        }

        return Math.Round(total, 1);
    }

    internal static int CountBusinessDays(DateTimeOffset from, DateTimeOffset to)
    {
        var days = 0;
        var current = from.Date.AddDays(1);
        var end = to.Date;
        while (current <= end)
        {
            if (current.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                days++;
            current = current.AddDays(1);
        }
        return days;
    }

    private static string GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? "" : "";

    private static string GetNestedString(JsonElement el, string prop1, string prop2) =>
        el.TryGetProperty(prop1, out var a) && a.TryGetProperty(prop2, out var b) ? b.GetString() ?? "" : "";

    private static string GetFieldString(JsonElement fields, string fieldName) =>
        fields.TryGetProperty(fieldName, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString() : "";

    private static double? GetFieldDouble(JsonElement fields, string fieldName)
    {
        if (!fields.TryGetProperty(fieldName, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), out var d)) return d;
        return null;
    }

    private static HttpContent MakeJsonContent(object data) =>
        new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");

    public static HttpClient BuildClient(string pat)
        => HttpClientPool.Get(pat, 120);
}
