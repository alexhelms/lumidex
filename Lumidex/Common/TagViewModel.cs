﻿using Avalonia.Media;

namespace Lumidex.Common;

public partial class TagViewModel : ObservableObject, IEquatable<TagViewModel?>
{
    private static Dictionary<string, IImmutableBrush> _brushCache = new();

    [ObservableProperty]
    public partial int Id { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Color { get; set; } = "#ff808080";

    [ObservableProperty]
    public partial ObservableCollectionEx<ImageFileViewModel> ImageFiles { get; set; } = new();

    [ObservableProperty]
    public partial int TaggedImageCount { get; set; } = new();

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public IImmutableBrush Brush
    {
        get
        {
            if (!_brushCache.TryGetValue(Color, out var brush))
            {
                brush = new SolidColorBrush(Avalonia.Media.Color.Parse(Color)).ToImmutable();
                _brushCache[Color] = brush;
            }

            return brush;
        }
    }

    partial void OnColorChanged(string value)
    {
        OnPropertyChanged(nameof(Brush));
    }   

    #region Equality

    public override bool Equals(object? obj)
    {
        return Equals(obj as TagViewModel);
    }

    public bool Equals(TagViewModel? other)
    {
        return other is not null &&
               Id == other.Id;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }

    public static bool operator ==(TagViewModel? left, TagViewModel? right)
    {
        return EqualityComparer<TagViewModel>.Default.Equals(left, right);
    }

    public static bool operator !=(TagViewModel? left, TagViewModel? right)
    {
        return !(left == right);
    }

    #endregion
}
