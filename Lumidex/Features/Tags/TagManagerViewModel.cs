using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Lumidex.Features.Tags;

public partial class TagManagerViewModel : ValidatableViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LumidexDbContext _dbContext;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.", AllowEmptyStrings = false)]
    [MaxLength(128, ErrorMessage = "128 character maximum.")]
    [NotifyCanExecuteChangedFor(nameof(AddTagCommand))]
    private string? _name;

    [ObservableProperty] private Color _color = Colors.White;

    public AvaloniaList<TagViewModel> Tags { get; } = new();

    public TagManagerViewModel(
        IServiceProvider serviceProvider,
        LumidexDbContext dbContext)
    {
        _serviceProvider = serviceProvider;
        _dbContext = dbContext;

        Dispatcher.UIThread.InvokeAsync(GetTags);
    }

    private async Task GetTags()
    {
        var tags = await _dbContext
            .Tags
            .AsNoTracking()
            .OrderByDescending(tag => tag.Id)
            .Select(tag => new TagViewModel
            {
                Id = tag.Id,
                Name = tag.Name,
                Color = tag.Color,
                Count = tag.TaggedImages.Count(),
            })
            .ToListAsync();

        Tags.Clear();
        Tags.AddRange(tags);
    }

    [RelayCommand]
    private async Task AddTag()
    {
        if (Name is null or { Length: 0 }) return;

        var tag = new Tag
        {
            Name = Name,
            Color = Color.ToString(),
        };

        _dbContext.Tags.Add(tag);
        await _dbContext.SaveChangesAsync();

        Tags.Insert(0, new TagViewModel
        {
            Id = tag.Id,
            Name = tag.Name,
            Color = tag.Color,
        });

        Name = null;
        Color = Colors.White;
        ClearErrors();

        if (View is Control control)
        {
            if (control.Find<TextBox>("TagNameTextBox") is { } tb)
            {
                tb.Focus();
            }
        }
    }

    [RelayCommand]
    private async Task DeleteTag(int id)
    {
        var dbTag = await _dbContext.Tags.FirstOrDefaultAsync(tag => tag.Id == id);
        if (dbTag is not null)
        {
            _dbContext.Tags.Remove(dbTag);
            await _dbContext.SaveChangesAsync();

            // Remove the tag from the data grid
            if (Tags.FirstOrDefault(vm => vm.Id == id) is { } existingTag)
            {
                Tags.Remove(existingTag);
            }
        }
    }

    [RelayCommand]
    private async Task EditTagComplete(DataGridCellEditEndedEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit &&
            e.Column.GetCellContent(e.Row)?.DataContext is Tag cell)
        {
            var dbTag = await _dbContext.Tags.FirstOrDefaultAsync(tag => tag.Id == cell.Id);
            if (dbTag is not null)
            {
                dbTag.Name = cell.Name;
                dbTag.Color = cell.Color;
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
    private async Task ChangeTagColor(Tag tag)
    {
        var dbTag = await _dbContext.Tags.FirstOrDefaultAsync(t => t.Id == tag.Id);
        if (dbTag is not null)
        {
            dbTag.Color = tag.Color;
            await _dbContext.SaveChangesAsync();
        }
    }
}

public partial class TagViewModel : ObservableObject
{
    [ObservableProperty] int _id;
    [ObservableProperty] string _name = string.Empty;
    [ObservableProperty] string _color = Colors.White.ToString();
    [ObservableProperty] int _count;
}