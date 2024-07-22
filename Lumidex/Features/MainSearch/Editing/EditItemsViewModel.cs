using Humanizer;
using Lumidex.Core.Data;
using Lumidex.Features.MainSearch.Editing.Messages;
using Lumidex.Services;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Lumidex.Features.MainSearch.Editing;

public partial class EditItemsViewModel : ViewModelBase
{
    private readonly static List<PropertyInfo> UserEditableProperties;

    private readonly DialogService _dialogService;
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    [ObservableProperty] ObservableCollectionEx<ImageFileViewModel> _selectedItems = new();
    [ObservableProperty] ObservableCollectionEx<EditFieldViewModel> _properties = new();

    public Action CloseDialog { get; set; } = () => { };

    public string HeaderText => $"Editing {SelectedItems.Count} {"Item".ToQuantity(SelectedItems.Count, ShowQuantityAs.None)}";

    static EditItemsViewModel()
    {
        UserEditableProperties = typeof(ImageFileViewModel)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<UserEditableAttribute>() != null)
            .ToList();
    }

    public EditItemsViewModel(
        DialogService dialogService,
        IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _dialogService = dialogService;
        _dbContextFactory = dbContextFactory;
    }

    partial void OnSelectedItemsChanged(ObservableCollectionEx<ImageFileViewModel> value)
    {
        BuildUi(value);
    }

    [RelayCommand]
    private async Task Save()
    {
        var changedItems = Properties
            .Where(p => p.Editable)
            .ToList();

        if (changedItems.Count > 0)
        {
            try
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                var imageFileIds = SelectedItems
                    .Select(x => x.Id)
                    .ToHashSet();
                var imageFiles = dbContext.ImageFiles
                    .Where(f => imageFileIds.Contains(f.Id))
                    .OrderBy(f => f.Id)
                    .ToList();

                // Get the PropertyInfo for the property on the database entity
                var dbPropInfos = new Dictionary<string, PropertyInfo>();
                foreach (var item in changedItems)
                {
                    dbPropInfos[item.PropInfo.Name] = typeof(ImageFile).GetProperty(item.PropInfo.Name)!;
                }

                foreach (var (imageFile, vm) in imageFiles.Zip(SelectedItems.OrderBy(x => x.Id)))
                {
                    foreach (var item in changedItems)
                    {
                        // Get the new value from the edit field VM
                        object? value = item.GetType().GetProperty("Value")!.GetValue(item);

                        // Set the new VM value
                        item.PropInfo.SetValue(vm, value);

                        // Set the new entity value
                        dbPropInfos[item.PropInfo.Name].SetValue(imageFile, value);
                    }
                }

                int count = dbContext.SaveChanges();
                Log.Information("{Count} items edited", count);

                if (count > 0)
                {
                    Messenger.Send(new ImageFilesEditedMessage
                    {
                        ImageFiles = SelectedItems,
                    });
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error saving user edited image file fields");
                await _dialogService.ShowMessageDialog(
                    "An error occurred while saving your changes." +
                    Environment.NewLine +
                    "Please try again.");
            }
        }

        CloseDialog();
    }

    private void BuildUi(IList<ImageFileViewModel> items)
    {
        var properties = new List<EditFieldViewModel>();

        try
        {
            // Create a lookup keyed by property info and the value is a hash set of all values across all image files.
            var lookup = new Dictionary<PropertyInfo, HashSet<object?>>();
            foreach (var item in items)
            {
                foreach (var property in UserEditableProperties)
                {
                    var value = property.GetValue(item);
                    lookup.TryGetValue(property, out var existingValues);
                    existingValues ??= [];
                    existingValues.Add(value);
                    lookup[property] = existingValues;
                }
            }

            // Create the view models for editing fields of the selected image files.
            // If all image files have the same value, the lookup set will only contain one value.
            // If all image files have different values, the lookup set will contain multiple values.
            // A field containing multiple values requires the user to define the value for the selected image fields if they have chosen to edit that field.
            foreach (var property in UserEditableProperties)
            {
                var propType = property.PropertyType;
                var values = lookup[property];
                object? value = values.FirstOrDefault();

                if (propType.IsEnum)
                {
                    var vm = new EnumEditFieldViewModel
                    {
                        PropInfo = property,
                        HasMultipleValues = values.Count > 1,
                        Values = Enum.GetValues(propType).OfType<object>().ToList(),
                    };

                    if (!vm.HasMultipleValues)
                    {
                        vm.Value = value;
                    }

                    properties.Add(vm);
                }
                else if (propType == typeof(string))
                {
                    var vm = new StringEditFieldViewModel
                    {
                        PropInfo = property,
                        HasMultipleValues = values.Count > 1,
                    };

                    if (!vm.HasMultipleValues)
                    {
                        vm.Value = (string?)value;
                    }

                    properties.Add(vm);
                }
                else if (Nullable.GetUnderlyingType(propType) is { } underlyingType)
                {
                    if (underlyingType == typeof(double))
                    {
                        var vm = new DoubleEditFieldViewModel
                        {
                            PropInfo = property,
                            HasMultipleValues = values.Count > 1,
                        };

                        if (!vm.HasMultipleValues)
                        {
                            vm.Value = (double?)value;
                        }

                        properties.Add(vm);
                    }
                    else if (underlyingType == typeof(int))
                    {
                        var vm = new IntegerEditFieldViewModel
                        {
                            PropInfo = property,
                            HasMultipleValues = values.Count > 1,
                        };

                        if (!vm.HasMultipleValues)
                        {
                            vm.Value = (int?)value;
                        }

                        properties.Add(vm);
                    }
                    else if (underlyingType == typeof(DateTime))
                    {
                        var vm = new DateTimeEditFieldViewModel
                        {
                            PropInfo = property,
                            HasMultipleValues = values.Count > 1,
                        };

                        if (!vm.HasMultipleValues)
                        {
                            vm.Value = (DateTime?)value;
                        }

                        properties.Add(vm);
                    }
                    else
                    {
                        Log.Debug("Unhandled editable field {Name}", property.Name);
                    }
                }
                else
                {
                    Log.Debug("Unhandled editable field {Name}", property.Name);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Error creating image file edit fields");
        }

        Properties = new(properties.OrderBy(x => x.PropInfo.Name));
    }
}

public abstract partial class EditFieldViewModel : ObservableObject 
{
    [ObservableProperty] bool _editable;
    [ObservableProperty] bool _hasMultipleValues;
    public PropertyInfo PropInfo { get; set; } = null!;
    public string Watermark
    {
        get
        {
            if (HasMultipleValues)
                return "<Multiple Values>";
            return "";
        }
    }
}

public partial class EnumEditFieldViewModel : EditFieldViewModel
{
    [ObservableProperty] object? _value;
    public List<object> Values { get; set; } = [];
}

public partial class StringEditFieldViewModel : EditFieldViewModel
{
    [ObservableProperty] string? _value;

    [RelayCommand]
    private void ClearValue() => Value = null;
}

public partial class DoubleEditFieldViewModel : EditFieldViewModel
{
    [ObservableProperty] double? _value;

    [RelayCommand]
    private void ClearValue() => Value = null;
}

public partial class IntegerEditFieldViewModel : EditFieldViewModel
{
    [ObservableProperty] int? _value;

    [RelayCommand]
    private void ClearValue() => Value = null;
}

public partial class DateTimeEditFieldViewModel : EditFieldViewModel
{
    [ObservableProperty] DateTime? _value;
    [ObservableProperty] DateTimeOffset? _date;
    [ObservableProperty] TimeSpan? _time;

    partial void OnValueChanged(DateTime? value)
    {
        if (value is not null)
        {
            Date = new DateTimeOffset(new DateTime(value.Value.Year, value.Value.Month, value.Value.Day));
            Time = value.Value.TimeOfDay;
        }
        else
        {
            Date = null;
            Time = null;
        }

        OnPropertyChanged(nameof(Date));
        OnPropertyChanged(nameof(Time));
    }

    partial void OnDateChanged(DateTimeOffset? value)
    {
        if (value is not null && Time is not null)
        {
            Value = new DateTime(
                new DateOnly(value.Value.Year, value.Value.Month, value.Value.Day),
                new TimeOnly(Time.Value.Ticks));
        }
    }

    partial void OnTimeChanged(TimeSpan? value)
    {
        if (value is not null && Date is not null)
        {
            Value = new DateTime(
                new DateOnly(Date.Value.Year, Date.Value.Month, Date.Value.Day),
                new TimeOnly(value.Value.Ticks));
        }
    }

    [RelayCommand]
    private void ClearValue()
    {
        Value = null;
        Date = null;
        Time = null;
    }
}