using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class DependencyResolverService
{
    private readonly LoggingService? _loggingService;

    public DependencyResolverService(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    public List<string> ResolveBuildOrder(List<string> requestedAppIds, Dictionary<string, AppItem> allAppsById)
    {
        var appLookup = allAppsById ?? new Dictionary<string, AppItem>(StringComparer.OrdinalIgnoreCase);
        var requestedIds = requestedAppIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (requestedIds.Count == 0 || appLookup.Count == 0)
        {
            return new List<string>();
        }

        var graph = BuildDependencyGraph(requestedIds, appLookup);
        if (graph.Nodes.Count == 0)
        {
            return new List<string>();
        }

        var workInDegree = new Dictionary<string, int>(graph.InDegree, StringComparer.OrdinalIgnoreCase);
        var ready = new PriorityQueue<string, int>();
        foreach (var node in graph.Nodes)
        {
            if (workInDegree.GetValueOrDefault(node) == 0)
            {
                ready.Enqueue(node, graph.DiscoveryOrder[node]);
            }
        }

        var ordered = new List<string>(graph.Nodes.Count);
        while (ready.Count > 0)
        {
            var node = ready.Dequeue();
            ordered.Add(node);

            if (!graph.Dependents.TryGetValue(node, out var dependents))
            {
                continue;
            }

            foreach (var dependent in dependents)
            {
                workInDegree[dependent]--;
                if (workInDegree[dependent] == 0)
                {
                    ready.Enqueue(dependent, graph.DiscoveryOrder[dependent]);
                }
            }
        }

        if (ordered.Count == graph.Nodes.Count)
        {
            return ordered;
        }

        var unresolved = graph.Nodes
            .Where(node => !ordered.Contains(node, StringComparer.OrdinalIgnoreCase))
            .OrderBy(node => graph.DiscoveryOrder[node])
            .ToList();

        foreach (var node in unresolved)
        {
            _loggingService?.LogWarning($"[DependencyResolver] Circular dependency detected involving {node} — skipping cycle");
            ordered.Add(node);
        }

        return ordered;
    }

    public List<AppItem> GetMissingDependencies(
        List<string> selectedAppIds,
        Dictionary<string, AppItem> allAppsById,
        DetectionService detectionService)
    {
        if (detectionService is null)
        {
            return new List<AppItem>();
        }

        var appLookup = allAppsById ?? new Dictionary<string, AppItem>(StringComparer.OrdinalIgnoreCase);
        if (selectedAppIds is null || selectedAppIds.Count == 0 || appLookup.Count == 0)
        {
            return new List<AppItem>();
        }

        var orderedIds = ResolveBuildOrder(selectedAppIds, appLookup);
        if (orderedIds.Count == 0)
        {
            return new List<AppItem>();
        }

        var selectedSet = new HashSet<string>(
            selectedAppIds.Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.OrdinalIgnoreCase);
        var currentPlatform = GetCurrentPlatformId();
        var dependencyCandidateApps = orderedIds
            .Where(id => !selectedSet.Contains(id))
            .Where(appLookup.ContainsKey)
            .Select(id => appLookup[id])
            .ToList();

        if (dependencyCandidateApps.Count == 0)
        {
            return new List<AppItem>();
        }

        var installedStates = detectionService.DetectInstalledAppStates(dependencyCandidateApps, currentPlatform);

        return orderedIds
            .Where(id => !selectedSet.Contains(id) && !installedStates.ContainsKey(id))
            .Where(appLookup.ContainsKey)
            .Select(id => appLookup[id])
            .ToList();
    }

    private DependencyGraph BuildDependencyGraph(IReadOnlyList<string> requestedIds, IReadOnlyDictionary<string, AppItem> allAppsById)
    {
        var nodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dependents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var discoveryOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var loggedMissing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var loggedCycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orderIndex = 0;

        void EnsureNode(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return;
            }

            if (nodes.Add(appId))
            {
                inDegree[appId] = 0;
            }

            if (!dependents.ContainsKey(appId))
            {
                dependents[appId] = new List<string>();
            }

            if (!discoveryOrder.ContainsKey(appId))
            {
                discoveryOrder[appId] = orderIndex++;
            }
        }

        void Visit(string appId, HashSet<string> visiting)
        {
            if (!allAppsById.TryGetValue(appId, out var app))
            {
                return;
            }

            EnsureNode(appId);
            if (!visiting.Add(appId))
            {
                return;
            }

            foreach (var dependencyId in app.Dependencies
                         .Where(id => !string.IsNullOrWhiteSpace(id))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!allAppsById.ContainsKey(dependencyId))
                {
                    var missingKey = $"{appId}->{dependencyId}";
                    if (loggedMissing.Add(missingKey))
                    {
                        _loggingService?.LogWarning(
                            $"[DependencyResolver] Dependency {dependencyId} required by {appId} not found in catalog — skipping");
                    }

                    continue;
                }

                if (visiting.Contains(dependencyId))
                {
                    var cycleKey = $"{appId}->{dependencyId}";
                    if (loggedCycles.Add(cycleKey))
                    {
                        _loggingService?.LogWarning(
                            $"[DependencyResolver] Circular dependency detected involving {appId} — skipping cycle");
                    }

                    continue;
                }

                EnsureNode(dependencyId);
                if (!dependents[dependencyId].Contains(appId, StringComparer.OrdinalIgnoreCase))
                {
                    dependents[dependencyId].Add(appId);
                    inDegree[appId] = inDegree.GetValueOrDefault(appId) + 1;
                }

                Visit(dependencyId, visiting);
            }

            visiting.Remove(appId);
        }

        foreach (var requestedId in requestedIds)
        {
            if (string.IsNullOrWhiteSpace(requestedId))
            {
                continue;
            }

            Visit(requestedId, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        return new DependencyGraph(nodes.ToList(), dependents, inDegree, discoveryOrder);
    }

    private static string GetCurrentPlatformId()
    {
        if (OperatingSystem.IsWindows())
        {
            return PlatformService.Windows;
        }

        if (OperatingSystem.IsLinux())
        {
            return PlatformService.Linux;
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macos";
        }

        return PlatformService.Unknown;
    }

    private sealed record DependencyGraph(
        List<string> Nodes,
        Dictionary<string, List<string>> Dependents,
        Dictionary<string, int> InDegree,
        Dictionary<string, int> DiscoveryOrder);
}
