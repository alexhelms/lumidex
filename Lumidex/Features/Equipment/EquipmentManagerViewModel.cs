using Avalonia.Threading;
using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Lumidex.Features.Equipment;

public partial class EquipmentManagerViewModel : ValidatableViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LumidexDbContext _dbContext;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Category is required.", AllowEmptyStrings = false)]
    [MaxLength(128, ErrorMessage = "128 character maximum.")]
    [NotifyCanExecuteChangedFor(nameof(AddEquipmentTagCommand))]
    private string? _category;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.", AllowEmptyStrings = false)]
    [MaxLength(128, ErrorMessage = "128 character maximum.")]
    [NotifyCanExecuteChangedFor(nameof(AddEquipmentTagCommand))]
    private string? _name;

    public AvaloniaList<string> CategoryAutoComplete { get; } = new(DefaultCategories);
    public AvaloniaList<EquipmentTag> EquipmentTags { get; } = new();

    private static List<string> DefaultCategories { get; } = [
        "Camera",
        "Filter Wheel",
        "Filter",
        "Focuser",
        "Mount",
        "Rotator",
        "Telescope",
    ];

    public EquipmentManagerViewModel(
        IServiceProvider serviceProvider,
        LumidexDbContext dbContext)
    {
        _serviceProvider = serviceProvider;
        _dbContext = dbContext;

        Dispatcher.UIThread.InvokeAsync(GetCategories);
        Dispatcher.UIThread.InvokeAsync(GetEquipmentTags);
    }

    private async Task GetCategories()
    {
        var newCategories = new List<string>(DefaultCategories)
            .Concat(await _dbContext
                .EquipmentTags
                .Select(tag => tag.Category)
                .Distinct()
                .ToListAsync())
            .Distinct()
            .Order()
            .ToList();

        CategoryAutoComplete.Clear();
        CategoryAutoComplete.AddRange(newCategories);
    }

    private async Task GetEquipmentTags()
    {
        var equpimentTags = await _dbContext
            .EquipmentTags
            .OrderByDescending(tag => tag.Id)
            .ToListAsync();

        EquipmentTags.Clear();
        EquipmentTags.AddRange(equpimentTags);
    }

    [RelayCommand]
    private async Task AddEquipmentTag()
    {
        if (Category is null or { Length: 0 }) return;
        if (Name is null or { Length: 0 }) return;

        var equipmentTag = new EquipmentTag
        {
            Category = Category,
            Name = Name,
        };

        _dbContext.EquipmentTags.Add(equipmentTag);
        await _dbContext.SaveChangesAsync();

        await GetCategories();
        await GetEquipmentTags();

        Category = null;
        Name = null;
        ClearErrors();
    }
}
