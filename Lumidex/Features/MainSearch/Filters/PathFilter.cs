﻿using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.MainSearch.Filters;

public partial class PathFilter : FilterViewModelBase
{
    [ObservableProperty] string? _name;

    public override string DisplayName => "Path";

    protected override void OnClear() => Name = null;

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (Name is { Length: > 0 })
        {
            query = query.Where(f => EF.Functions.Like(f.Path, $"%{Name}%"));
        }

        return query;
    }

    public override string ToString() => $"{DisplayName} = {Name}";
}