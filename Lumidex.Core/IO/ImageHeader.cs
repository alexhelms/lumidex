namespace Lumidex.Core.IO;

public class ImageHeader
{
    public int Width { get; set; }
    public int Height { get; set; }
    public string FileExtension { get; set; } = string.Empty;
    public List<IHeaderEntry> Items { get; set; } = new();

    public IHeaderEntry? GetEntry(string keyword)
        => Items.FirstOrDefault(item => item.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase));

    public HeaderEntry<T>? GetEntry<T>(string keyword)
        where T : IComparable
    {
        return Items
            .OfType<HeaderEntry<T>>()
            .FirstOrDefault(item => item.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase));
    }
}

public interface IHeaderEntry
{
    string Keyword { get; }
    string Comment { get; set; }
}

public abstract record HeaderEntry<T> : IHeaderEntry
    where T : IComparable
{
    private T? _value;

    protected HeaderEntry(string keyword, T value)
        : this(keyword, value, string.Empty)
    {
    }

    protected HeaderEntry(string keyword, T value, string comment)
    {
        Keyword = keyword;
        Comment = comment;
        _value = value;
    }

    public string Keyword { get; }
    public string Comment { get; set; }
    public bool Changed { get; private set; }

    public virtual T? Value
    {
        get => _value;
        set
        {
            if (_value?.CompareTo(value) != 0)
            {
                Changed = true;
            }

            _value = value;
        }
    }
}

public record StringHeaderEntry : HeaderEntry<string>
{
    public StringHeaderEntry(string keyword, string value)
        : this(keyword, value, string.Empty)
    {
    }

    public StringHeaderEntry(string keyword, string value, string comment)
        : base(keyword, value, comment)
    {
    }

    public override string? Value
    {
        get => base.Value;
        set => base.Value = value;
    }
}

public record BooleanHeaderEntry : HeaderEntry<bool>
{
    public BooleanHeaderEntry(string keyword, bool value)
        : this(keyword, value, string.Empty)
    {
    }

    public BooleanHeaderEntry(string keyword, bool value, string comment)
        : base(keyword, value, comment)
    {
    }

    public override bool Value
    {
        get => base.Value;
        set => base.Value = value;
    }
}

public record IntegerHeaderEntry : HeaderEntry<int>
{
    public IntegerHeaderEntry(string keyword, int value)
        : this(keyword, value, string.Empty)
    {
    }

    public IntegerHeaderEntry(string keyword, int value, string comment)
        : base(keyword, value, comment)
    {
    }

    public override int Value
    {
        get => base.Value;
        set => base.Value = value;
    }
}

public record FloatHeaderEntry : HeaderEntry<double>
{
    public FloatHeaderEntry(string keyword, double value)
        : this(keyword, value, string.Empty)
    {
    }

    public FloatHeaderEntry(string keyword, double value, string comment)
        : base(keyword, value, comment)
    {
    }

    public override double Value
    {
        get => base.Value;
        set => base.Value = value;
    }
}
