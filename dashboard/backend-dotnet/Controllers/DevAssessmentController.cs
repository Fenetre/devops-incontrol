using DashboardApi.Models;
using DashboardApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DashboardApi.Controllers;

[ApiController]
[Route("api/dev-assessment")]
public class DevAssessmentController(ConfigStore configStore, ILogger<DevAssessmentController> logger) : ControllerBase
{
    private const int MaxConcurrentProjectAssessments = 6;

    // ---------------------------------------------------------------
    // POST /api/dev-assessment/run
    // ---------------------------------------------------------------
    [HttpPost("run")]
    public async Task<DevAssessmentResponse> Run([FromBody] DevAssessmentRunRequest? request, CancellationToken cancellationToken = default)
    {
        var months = request?.Months is > 0 and <= 24 ? request.Months : 3;

        // Determine date range: explicit since/until (sprint) or months-based
        DateTime? since = null, until = null;
        if (!string.IsNullOrEmpty(request?.Since) && DateTime.TryParse(request.Since, out var s))
            since = s.ToUniversalTime();
        if (!string.IsNullOrEmpty(request?.Until) && DateTime.TryParse(request.Until, out var u))
            until = u.ToUniversalTime();

        var pat = SettingsController.GetPat(configStore);
        if (string.IsNullOrEmpty(pat))
            throw new BadHttpRequestException("PAT not configured.");

        var projects = configStore.ListProjects();
        if (request?.ProjectIds is { Count: > 0 } ids)
            projects = projects.Where(p => ids.Contains(p.Id)).ToList();
        if (projects.Count == 0)
            return new DevAssessmentResponse { Months = months };

        var allPrs = new List<DevAssessmentPrItem>();
        var allWorkItems = new List<DevAssessmentWorkItem>();
        var allStalePrs = new List<DevAssessmentStaleReviewPr>();
        var allOpenPrs = new List<DevAssessmentOpenPr>();

        using var projectThrottler = new SemaphoreSlim(MaxConcurrentProjectAssessments);
        var projectTasks = projects.Select(async project =>
        {
            await projectThrottler.WaitAsync();
            try
            {
                var http = DevAssessmentService.BuildClient(pat);

                Task<List<DevAssessmentPrItem>> prTask;
                Task<List<DevAssessmentWorkItem>> wiTask;

                if (since.HasValue)
                {
                    prTask = DevAssessmentService.FetchCompletedPrs(http, project.Organization, project.Project, since.Value, until);
                    wiTask = DevAssessmentService.FetchWorkItems(http, project.Organization, project.Project, since.Value, until);
                }
                else
                {
                    prTask = DevAssessmentService.FetchCompletedPrs(http, project.Organization, project.Project, months);
                    wiTask = DevAssessmentService.FetchWorkItems(http, project.Organization, project.Project, months);
                }

                var activeTask = DevAssessmentService.FetchActivePrData(http, project.Organization, project.Project);
                await Task.WhenAll(prTask, wiTask, activeTask);

                var (stalePrs, openPrs) = await activeTask;
                return (
                    Prs: await prTask,
                    WorkItems: await wiTask,
                    StalePrs: stalePrs,
                    OpenPrs: openPrs
                );
            }
            catch (AzureDevOpsPatScopeException)
            {
                throw; // Let the middleware handle it with a 403 response
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Dev assessment failed for {Project}", project.Project);
                return (
                    Prs: new List<DevAssessmentPrItem>(),
                    WorkItems: new List<DevAssessmentWorkItem>(),
                    StalePrs: new List<DevAssessmentStaleReviewPr>(),
                    OpenPrs: new List<DevAssessmentOpenPr>()
                );
            }
            finally
            {
                projectThrottler.Release();
            }
        }).ToList();

        var projectResults = await Task.WhenAll(projectTasks);
        foreach (var chunk in projectResults)
        {
            allPrs.AddRange(chunk.Prs);
            allWorkItems.AddRange(chunk.WorkItems);
            allStalePrs.AddRange(chunk.StalePrs);
            allOpenPrs.AddRange(chunk.OpenPrs);
        }

        return new DevAssessmentResponse
        {
            Prs = allPrs,
            WorkItems = allWorkItems,
            StaleReviewPrs = allStalePrs,
            OpenPrs = allOpenPrs,
            Months = months,
        };
    }
}
