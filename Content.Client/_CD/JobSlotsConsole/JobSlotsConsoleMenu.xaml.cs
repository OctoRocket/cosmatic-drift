using Content.Client.Administration.UI.CustomControls;
using Content.Client.UserInterface.Controls;
using Content.Shared.Roles;
using Content.Shared._CD.JobSlotsConsole;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Client._CD.JobSlotsConsole;

[GenerateTypedNameReferences]
public sealed partial class JobSlotsConsoleMenu : FancyWindow
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    private readonly Dictionary<ProtoId<DepartmentPrototype>, List<JobRow>> _departmentRows = new();
    private readonly Dictionary<ProtoId<DepartmentPrototype>, Label> _departmentLabels = new();
    private JobSlotsConsoleState? _currentState;

    public event Action<ProtoId<JobPrototype>, JobSlotAdjustment>? OnAdjustPressed;

    public JobSlotsConsoleMenu()
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);

        SearchBar.OnTextChanged += _ => UpdateJobList();
    }

    public void UpdateState(JobSlotsConsoleState state)
    {
        _currentState = state;

        DepartmentsList.RemoveAllChildren();
        _departmentRows.Clear();
        _departmentLabels.Clear();

        var jobsByDepartment = new Dictionary<DepartmentPrototype, List<(JobPrototype proto, int? slots, bool blacklisted)>>();

        foreach (var (jobId, slots) in state.Jobs)
        {
            var proto = _protoManager.Index(jobId);
            var department = GetPrimaryDepartmentForJob(proto.ID);

            if (department is not { } dept)
                continue;

            if (!jobsByDepartment.TryGetValue(dept, out var jobs))
            {
                jobs = new List<(JobPrototype, int?, bool)>();
                jobsByDepartment[dept] = jobs;
            }

            var blacklisted = state.BlacklistedJobs.Contains(jobId);
            jobs.Add((proto, slots, blacklisted));
        }

        // Sort and add departments
        foreach (var (department, jobs) in jobsByDepartment.OrderBy(x => x.Key, DepartmentUIComparer.Instance))
        {
            var sortedJobs = jobs
                .OrderByDescending(x => x.proto.RealDisplayWeight)
                .ThenBy(x => x.proto.LocalizedName)
                .ToList();

            AddDepartmentSection(department, sortedJobs);
        }

        UpdateJobList();
    }

    private void AddDepartmentSection(DepartmentPrototype department, List<(JobPrototype proto, int? slots, bool blacklisted)> jobs)
    {
        var departmentBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(0, 5),
        };

        // Add department header
        var label = new Label
        {
            Text = Loc.GetString(department.Name),
            HorizontalAlignment = HAlignment.Center,
            StyleClasses = { "LabelHeading" },
            ModulateSelfOverride = department.Color,
            FontColorOverride = department.Color, // css trolling
        };

        _departmentLabels[department.ID] = label;
        departmentBox.AddChild(label);
        departmentBox.AddChild(new HSeparator { Margin = new Thickness(5, 3, 5, 5) });

        // Add job rows
        var jobRows = new List<JobRow>();
        foreach (var (proto, slots, blacklisted) in jobs)
        {
            var row = new JobRow(proto.ID, slots, blacklisted);
            row.OnAdjustPressed += adjustment =>
                OnAdjustPressed?.Invoke(proto.ID, adjustment);
            row.ShowDebugControls(_currentState?.Debug ?? false);
            jobRows.Add(row);
            departmentBox.AddChild(row);
        }

        _departmentRows[department.ID] = jobRows;
        DepartmentsList.AddChild(departmentBox);
    }

    private void UpdateJobList()
    {
        var search = SearchBar.Text.Trim().ToLowerInvariant();

        foreach (var (departmentId, rows) in _departmentRows)
        {
            var departmentLabel = _departmentLabels[departmentId];
            var hasVisibleJobs = false;

            foreach (var row in rows)
            {
                var visible = string.IsNullOrEmpty(search) ||
                             row.JobName.Contains(search, StringComparison.InvariantCultureIgnoreCase);
                row.Visible = visible;
                hasVisibleJobs |= visible;
            }

            departmentLabel.Parent!.Visible = hasVisibleJobs;
        }
    }

    private DepartmentPrototype? GetPrimaryDepartmentForJob(ProtoId<JobPrototype> jobId)
    {
        return _protoManager.EnumeratePrototypes<DepartmentPrototype>()
            .FirstOrDefault(d => d.Roles.Contains(jobId) && d.Primary)
            ?? _protoManager.EnumeratePrototypes<DepartmentPrototype>()
                .FirstOrDefault(d => d.Roles.Contains(jobId));
    }
}