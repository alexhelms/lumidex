using System.Reflection;
using Lumidex.Services;

namespace Lumidex.Features.Main;

public partial class AboutViewModel : ViewModelBase
{
    private readonly SystemService _systemService;

    public string? Copyright { get; }
    public string? Version { get; }
    public string? VersionToolTip { get; }
    public DateTime? CommitDate { get; }

    public AboutViewModel(SystemService systemService)
    {
        _systemService = systemService;

        Copyright = "© 2024 Alex Helms and Contributors";

        if (Assembly.GetExecutingAssembly().GetType("GitVersionInformation") is { } versionType)
        {
            var fields = versionType.GetFields();
            Version = GetFieldValue(fields, "SemVer");
            VersionToolTip = GetFieldValue(fields, "InformationalVersion");
            CommitDate = DateTime.Parse(GetFieldValue(fields, "CommitDate"));
        }

        static string GetFieldValue(IEnumerable<FieldInfo> fields, string fieldName)
            => (string)fields.First(f => f.Name == fieldName).GetValue(null)!;
    }

    [RelayCommand]
    private Task SetClipboard(string? value) => _systemService.SetClipboard(value);
}
