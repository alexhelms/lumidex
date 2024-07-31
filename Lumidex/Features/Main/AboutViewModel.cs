using Lumidex.Core;
using Lumidex.Services;

namespace Lumidex.Features.Main;

public partial class AboutViewModel : ViewModelBase
{
    private readonly SystemService _systemService;

    public string? Copyright { get; } = LumidexUtil.Copyright;
    public string? Version { get; } = LumidexUtil.Version;
    public string? VersionToolTip { get; } = LumidexUtil.InformationalVersion;
    public DateTime? CommitDate { get; } = LumidexUtil.CommitDate;

    public AboutViewModel(SystemService systemService)
    {
        _systemService = systemService;
    }

    [RelayCommand]
    private Task SetClipboard(string? value) => _systemService.SetClipboard(value);
}
