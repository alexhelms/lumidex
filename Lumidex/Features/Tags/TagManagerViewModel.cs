using AutoMapper;
using AutoMapper.QueryableExtensions;
using Avalonia.Controls;
using Avalonia.Media;
using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Lumidex.Features.Tags;

public partial class TagManagerViewModel : ValidatableViewModelBase
{
    private static readonly Color DefaultColor = Colors.Gray;

    private readonly IServiceProvider _serviceProvider;
    private readonly IMapper _mapper;
    private readonly LumidexDbContext _dbContext;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.", AllowEmptyStrings = false)]
    [MaxLength(128, ErrorMessage = "128 character maximum.")]
    [NotifyCanExecuteChangedFor(nameof(AddTagCommand))]
    private string? _name;

    [ObservableProperty] Color _color = DefaultColor;
    [ObservableProperty] AvaloniaList<TagViewModel> _tags = new();

    public TagManagerViewModel(
        IServiceProvider serviceProvider,
        IMapper mapper,
        LumidexDbContext dbContext)
    {
        _serviceProvider = serviceProvider;
        _mapper = mapper;
        _dbContext = dbContext;
    }

    protected override async void OnActivated()
    {
        base.OnActivated();

        var tags = await _dbContext
            .Tags
            .AsNoTracking()
            .Include(tag => tag.TaggedImages)
            .OrderByDescending(tag => tag.Id)
            .ProjectTo<TagViewModel>(_mapper.ConfigurationProvider)
            .ToListAsync();

        Tags = new(tags);
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

        Tags.Insert(0, _mapper.Map<TagViewModel>(tag));

        Name = null;
        Color = DefaultColor;
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
            e.Column.GetCellContent(e.Row)?.DataContext is TagViewModel cell)
        {
            var dbTag = await _dbContext.Tags.FirstOrDefaultAsync(tag => tag.Id == cell.Id);
            if (dbTag is not null)
            {
                dbTag.Name = cell.Name;
                dbTag.Color = cell.Color.ToString();
                await _dbContext.SaveChangesAsync();
            }
        }
    }

    [RelayCommand]
    private void ClearColor()
    {
        Color = DefaultColor;
    }

    [RelayCommand]
    private async Task ChangeTagColor(TagViewModel? tag)
    {
        if (tag is null) return;

        var dbTag = await _dbContext.Tags.FirstOrDefaultAsync(t => t.Id == tag.Id);
        if (dbTag is not null)
        {
            dbTag.Color = tag.Color.ToString();
            await _dbContext.SaveChangesAsync();
        }
    }
}
