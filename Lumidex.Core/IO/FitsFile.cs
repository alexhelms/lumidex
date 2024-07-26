using Serilog;

namespace Lumidex.Core.IO;

public partial class FitsFile : IDisposable
{
    private readonly nint _handle;
    private readonly IoMode _ioMode;
    private readonly string _filename;

    public enum IoMode
    {
        Read = 0,
        ReadWrite = 1,
    };

    // cfitsio handles these for us
    private static readonly string[] ForbiddenKeywords =
    {
        "SIMPLE",
        "BITPIX",
        "NAXIS",
        "NAXIS1",
        "NAXIS2",
        "EXTEND",
        "BZERO",
        "BSCALE",
    };

    // Default comments put in by cfitsio, not sure if these can be disabled?
    private static readonly string[] CfitsioDefaultComments =
    {
        @"FITS (Flexible Image Transport System) format is defined in 'Astronomy",
        @"and Astrophysics', volume 376, page 359; bibcode: 2001A&A...376..359H"
    };

    public FitsFile(string filename, IoMode mode)
    {
        _ioMode = mode;
        _filename = filename;

        Native.OpenDiskFile(ref _handle, filename, (Native.IOMode)mode, out var status);
        if (status != Native.ErrorCode.OK)
        {
            var errorMessage = Native.GetErrorMessage();
            if (status == Native.ErrorCode.FILE_NOT_OPENED)
            {
                throw new FileNotFoundException(errorMessage, filename);
            }
            else
            {
                throw new FitsException(status, errorMessage);
            }
        }
    }

    #region IDisposable

    private bool _disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // dispose managed resources
            }

            if (_handle != 0)
            {
                Native.CloseFile(_handle, out _);
            }

            _disposed = true;
        }
    }

    ~FitsFile()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion

    private Type ResolveHeaderDatatype(char dtype) => dtype switch
    {
        'C' => typeof(string),  // character
        'L' => typeof(bool),    // logical
        'I' => typeof(int),     // integer
        'F' => typeof(double),  // floating point
        _ => throw new ArgumentOutOfRangeException($"Unsupported FITS header data type {dtype}"),
    };

    public ImageHeader ReadHeader()
    {
        if (_handle == 0)
            throw new ObjectDisposedException("FITS image already disposed");

        Native.ErrorCode status;

        var naxes = new long[2];
        Native.ReadImageHeader(_handle, naxes.Length, out var simple, out var imageType, out _,
            naxes, out _, out _, out var extended, out status);
        Native.ThrowIfNotOk(status);

        var header = new ImageHeader
        {
            FileExtension = ".fits",
            Width = (int) naxes[0],
            Height = (int) naxes[1],
        };

        Native.GetHeaderPosition(_handle, out var nkeys, out _, out status);
        Native.ThrowIfNotOk(status);

        const int EntryMaxBytes = 81;
        Span<byte> keywordSpan = stackalloc byte[EntryMaxBytes];
        Span<byte> valueSpan = stackalloc byte[EntryMaxBytes];
        Span<byte> commentSpan = stackalloc byte[EntryMaxBytes];

        for (int i = 1; i <= nkeys; i++)
        {
            status = Native.ErrorCode.OK;
            string keyword = string.Empty;
            string value = string.Empty;

            try
            {
                Native.ReadKeyByNumber(_handle, i, keywordSpan, valueSpan, commentSpan, out status);
                Native.ThrowIfNotOk(status);

                keyword = keywordSpan.NullTerminatedToString();
                value = valueSpan.NullTerminatedToString();
            }
            catch (FitsException fe)
            {
                Log.Warning(fe, "Error reading FITS header keyword {Count} in {Filename}", i, _filename);
                continue;
            }

            if (value.Length > 0)
            {
                try
                {
                    Native.GetKeyType(value, out var dtype, out status);
                    Native.ThrowIfNotOk(status);

                    var entryType = ResolveHeaderDatatype((char)dtype);
                    switch (entryType)
                    {
                        case var _ when entryType == typeof(string):
                            Native.ReadKeyString(_handle, keyword, valueSpan, commentSpan, out status);
                            Native.ThrowIfNotOk(status);
                            header.Items.Add(new StringHeaderEntry(
                                keyword: keyword,
                                comment: commentSpan.NullTerminatedToString(),
                                value: valueSpan.NullTerminatedToString()));
                            break;
                        case var _ when entryType == typeof(bool):
                            unsafe
                            {
                                int tempValue = 0;
                                Native.ReadKey(_handle, Native.DataType.TLOGICAL, keyword, &tempValue, commentSpan, out status);
                                Native.ThrowIfNotOk(status);
                                header.Items.Add(new BooleanHeaderEntry(
                                    keyword: keyword,
                                    comment: commentSpan.NullTerminatedToString(),
                                    value: tempValue == 1));
                            }
                            break;
                        case var _ when entryType == typeof(int):
                            unsafe
                            {
                                int tempValue = 0;
                                Native.ReadKey(_handle, Native.DataType.TINT, keyword, &tempValue, commentSpan, out status);
                                Native.ThrowIfNotOk(status);
                                header.Items.Add(new IntegerHeaderEntry(
                                    keyword: keyword,
                                    comment: commentSpan.NullTerminatedToString(),
                                    value: tempValue));
                            }
                            break;
                        case var _ when entryType == typeof(double):
                            unsafe
                            {
                                double tempValue = 0;
                                Native.ReadKey(_handle, Native.DataType.TDOUBLE, keyword, &tempValue, commentSpan, out status);
                                Native.ThrowIfNotOk(status);
                                header.Items.Add(new FloatHeaderEntry(
                                    keyword: keyword,
                                    comment: commentSpan.NullTerminatedToString(),
                                    value: tempValue));
                            }
                            break;
                    }
                }
                catch (FitsException fe)
                {
                    Log.Warning(fe, "Error reading FITS header keyword {Keyword} in {Filename}", keyword, _filename);
                    continue;
                }
            }
        }

        return header;
    }
}
