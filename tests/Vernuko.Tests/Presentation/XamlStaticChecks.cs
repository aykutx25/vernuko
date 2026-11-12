using System.Reflection;
using System.Text.RegularExpressions;

namespace Vernuko.Tests.Presentation;

/// <summary>
/// The WPF app cannot run on the Linux CI, so these checks catch the two most common XAML mistakes statically:
/// unknown StaticResource keys (crash at runtime) and binding paths that exist on no view-model type (silent failure).
/// The Windows smoke test (--smoke-test) is the authoritative runtime check.
/// </summary>
public partial class XamlStaticChecks
{
    private static readonly string DesktopDir = FindDesktopDir();

    private static readonly HashSet<string> WpfMembers = new(StringComparer.Ordinal)
    {
        "DataContext", "IsDropDownOpen", "Background", "BorderBrush", "ActualWidth", "PlacementTarget", "Tag", "Count",
        "(TextElement.Foreground)", "Content",
    };

    [Fact]
    public void All_static_resource_keys_are_defined()
    {
        var xaml = AllXaml();
        var defined = new HashSet<string>(xaml.SelectMany(x => KeyRegex().Matches(x.Text).Select(m => m.Groups[1].Value)), StringComparer.Ordinal);
        var missing = xaml
            .SelectMany(x => StaticResourceRegex().Matches(x.Text).Select(m => (x.File, Key: m.Groups[1].Value)))
            .Where(r => !r.Key.StartsWith("{x:Type", StringComparison.Ordinal) && !defined.Contains(r.Key))
            .Select(r => $"{Path.GetFileName(r.File)}: {r.Key}")
            .Distinct()
            .ToList();
        Assert.True(missing.Count == 0, "Undefined StaticResource keys:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void All_binding_paths_exist_on_some_view_model_or_dto()
    {
        var members = KnownMembers();
        var problems = new List<string>();
        foreach (var (file, text) in AllXaml().Where(x => !x.File.Contains("Themes", StringComparison.Ordinal)))
        {
            foreach (Match m in BindingRegex().Matches(text))
            {
                var path = m.Groups[1].Value.Trim();
                if (path.Length == 0 || path.StartsWith('(') || path == ".")
                {
                    continue;
                }

                foreach (var segment in path.Split('.'))
                {
                    if (!WpfMembers.Contains(segment) && !members.Contains(segment))
                    {
                        problems.Add($"{Path.GetFileName(file)}: {path} (unknown '{segment}')");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, "Binding paths not found on any type:\n" + string.Join("\n", problems.Distinct()));
    }

    [Fact]
    public void Every_view_model_has_a_view_template()
    {
        var app = File.ReadAllText(Path.Combine(DesktopDir, "App.xaml"));
        var vmTypes = typeof(Vernuko.Presentation.RootViewModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && (t.Name.EndsWith("ViewModel", StringComparison.Ordinal)) && t != typeof(Vernuko.Presentation.RootViewModel))
            .Where(t => t.IsSubclassOf(typeof(Vernuko.Presentation.Common.ViewModelBase)))
            .Select(t => t.Name)
            .ToList();
        var missing = vmTypes.Where(n => !app.Contains($":{n}}}", StringComparison.Ordinal)).ToList();
        Assert.True(missing.Count == 0, "View models without a DataTemplate: " + string.Join(", ", missing));
    }

    [Fact]
    public void ComboBox_templates_show_the_display_member_in_the_closed_box()
    {
        // Regression from the first Windows run: a custom ComboBox template without ContentTemplateSelector shows
        // the record's ToString() ("ServiceDto { Id = … }") instead of DisplayMemberPath.
        foreach (var (file, text) in AllXaml())
        {
            foreach (Match m in SelectionBoxPresenterRegex().Matches(text))
            {
                Assert.True(m.Value.Contains("ContentTemplateSelector=\"{TemplateBinding ItemTemplateSelector}\"", StringComparison.Ordinal),
                    $"{Path.GetFileName(file)}: ComboBox selection presenter must bind ContentTemplateSelector to ItemTemplateSelector");
            }
        }
    }

    private static HashSet<string> KnownMembers()
    {
        var assemblies = new[]
        {
            typeof(Vernuko.Presentation.RootViewModel).Assembly,
            typeof(Vernuko.Application.AppointmentListItem).Assembly,
            typeof(Vernuko.Reporting.DailySummary).Assembly,
            typeof(Vernuko.Core.Money).Assembly,
        };
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in assemblies.SelectMany(a => a.GetTypes()))
        {
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                names.Add(p.Name);
            }
        }

        return names;
    }

    private static List<(string File, string Text)> AllXaml() =>
        Directory.EnumerateFiles(DesktopDir, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => (f, File.ReadAllText(f)))
            .ToList();

    private static string FindDesktopDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src", "Vernuko.Desktop")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return Path.Combine(dir ?? throw new DirectoryNotFoundException("repo root"), "src", "Vernuko.Desktop");
    }

    [GeneratedRegex("x:Key=\"([^\"]+)\"")]
    private static partial Regex KeyRegex();

    [GeneratedRegex(@"\{StaticResource\s+([^}\s]+)\}")]
    private static partial Regex StaticResourceRegex();

    [GeneratedRegex(@"\{Binding\s+(?:Path=)?([A-Za-z_.()]+)?[,}]")]
    private static partial Regex BindingRegex();

    [GeneratedRegex(@"<ContentPresenter[^>]*SelectionBoxItem[^>]*/>", RegexOptions.Singleline)]
    private static partial Regex SelectionBoxPresenterRegex();
}
