using Avalonia.Controls;
using Avalonia.Media;
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

    [ObservableProperty] private Color _color = Colors.White;

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
                .AsNoTracking()
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
            .AsNoTracking()
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
            Color = Color.ToUInt32(),
        };

        _dbContext.EquipmentTags.Add(equipmentTag);
        await _dbContext.SaveChangesAsync();

        await GetCategories();
        await GetEquipmentTags();

        Category = null;
        Name = null;
        Color = Colors.White;
        ClearErrors();

        if (View is Control control)
        {
            if (control.Find<AutoCompleteBox>("CategoryTextBox") is { } tb)
            {
                tb.Focus();
            }
        }
    }

    [RelayCommand]
    private async Task DeleteEquipmentTag(int id)
    {
        var equipmentTag = await _dbContext.EquipmentTags.FirstOrDefaultAsync(tag => tag.Id == id);
        if (equipmentTag is not null)
        {
            _dbContext.EquipmentTags.Remove(equipmentTag);
            await _dbContext.SaveChangesAsync();

            // Update the categories
            await GetCategories();

            // Remove the tag from the data grid
            if (EquipmentTags.FirstOrDefault(tag => tag.Id == id) is { } existingTag)
            {
                EquipmentTags.Remove(existingTag);
            }
        }
    }

    [RelayCommand]
    private async Task EditTagComplete(DataGridCellEditEndedEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit &&
            e.Column.GetCellContent(e.Row)?.DataContext is EquipmentTag cell)
        {
            var equipmentTag = await _dbContext.EquipmentTags.FirstOrDefaultAsync(tag => tag.Id == cell.Id);
            if (equipmentTag is not null)
            {
                equipmentTag.Category = cell.Category;
                equipmentTag.Name = cell.Name;
                equipmentTag.Color = cell.Color;
                await _dbContext.SaveChangesAsync();
            }
        }
    }

    [RelayCommand]
    private void ClearColor()
    {
        Color = Colors.White;
    }

    [RelayCommand]
    private async Task ChangeTagColor(EquipmentTag tag)
    {
        var equipmentTag = await _dbContext.EquipmentTags.FirstOrDefaultAsync(t => t.Id == tag.Id);
        if (equipmentTag is not null)
        {
            equipmentTag.Color = tag.Color;
            await _dbContext.SaveChangesAsync();
        }
    }
}
