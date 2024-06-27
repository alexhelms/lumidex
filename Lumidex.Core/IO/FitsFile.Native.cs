using System.Runtime.InteropServices;

namespace Lumidex.Core.IO;

public partial class FitsFile
{
    private Native.ImageType ResolveImageType(Type type) => type.Name switch
    {
        "Byte" => Native.ImageType.BYTE_IMG,
        "UInt16" => Native.ImageType.USHORT_IMG,
        "Int32" => Native.ImageType.LONG_IMG,
        "UInt32" => Native.ImageType.ULONG_IMG,
        "Single" => Native.ImageType.FLOAT_IMG,
        "Double" => Native.ImageType.DOUBLE_IMG,
        _ => throw new ArgumentOutOfRangeException($"Unhandled type {type} cannot convert to fits image type"),
    };

    private Native.DataType ResolveDataType(Type type) => type.Name switch
    {
        "Byte" => Native.DataType.TBYTE,
        "UInt16" => Native.DataType.TUSHORT,
        "Int32" => Native.DataType.TINT,
        "UInt32" => Native.DataType.TUINT,
        "Single" => Native.DataType.TFLOAT,
        "Double" => Native.DataType.TDOUBLE,
        "String" => Native.DataType.TSTRING,
        "Boolean" => Native.DataType.TLOGICAL,
        _ => throw new ArgumentOutOfRangeException($"Unhandled type {type} cannot convert to fits data type"),
    };

    private int SizeOfImageType(Native.ImageType imageType) => imageType switch
    {
        Native.ImageType.BYTE_IMG => sizeof(byte),
        Native.ImageType.SHORT_IMG => sizeof(short),
        Native.ImageType.LONG_IMG => sizeof(int),
        Native.ImageType.LONGLONG_IMG => sizeof(long),
        Native.ImageType.FLOAT_IMG => sizeof(float),
        Native.ImageType.DOUBLE_IMG => sizeof(double),
        Native.ImageType.SBYTE_IMG => sizeof(sbyte),
        Native.ImageType.USHORT_IMG => sizeof(ushort),
        Native.ImageType.ULONG_IMG => sizeof(uint),
        Native.ImageType.ULONGLONG_IMG => sizeof(ulong),
        _ => throw new ArgumentOutOfRangeException($"Unknown FITS image type {imageType}"),
    };

    private Native.DataType ResolveDataTypeFromImageType(Native.ImageType imageType) => imageType switch
    {
        Native.ImageType.BYTE_IMG => Native.DataType.TBYTE,
        Native.ImageType.SHORT_IMG => Native.DataType.TUSHORT,        // unsigned on purpose
        Native.ImageType.LONG_IMG => Native.DataType.TULONG,          // unsigned on purpose
        Native.ImageType.LONGLONG_IMG => Native.DataType.TULONGLONG,  // unsigned on purpose
        Native.ImageType.FLOAT_IMG => Native.DataType.TFLOAT,
        Native.ImageType.DOUBLE_IMG => Native.DataType.TDOUBLE,
        Native.ImageType.SBYTE_IMG => Native.DataType.TBYTE,          // unsigned on purpose
        Native.ImageType.USHORT_IMG => Native.DataType.TUSHORT,
        Native.ImageType.ULONG_IMG => Native.DataType.TULONG,
        Native.ImageType.ULONGLONG_IMG => Native.DataType.TULONGLONG,
        _ => throw new ArgumentOutOfRangeException($"Unknown FITS image type {imageType}"),
    };

    private Type ConvertDataTypeToType(Native.DataType dataType) => dataType switch
    {
        Native.DataType.TBYTE => typeof(byte),
        Native.DataType.TSBYTE => typeof(sbyte),
        Native.DataType.TUSHORT => typeof(ushort),
        Native.DataType.TSHORT => typeof(short),
        Native.DataType.TULONG => typeof(uint),
        Native.DataType.TLONG => typeof(int),
        Native.DataType.TULONGLONG => typeof(ulong),
        Native.DataType.TLONGLONG => typeof(long),
        Native.DataType.TFLOAT => typeof(float),
        Native.DataType.TDOUBLE => typeof(double),
        _ => throw new ArgumentOutOfRangeException($"Unknown FITS data type {dataType}"),
    };

    internal static unsafe partial class Native
    {
        static Native()
        {
            // cfitsio MUST be compiled to support reentrancy!
            if (FitsIsReentrant() == false)
                throw new NotImplementedException();
        }

        internal static void ThrowIfNotOk(ErrorCode status)
        {
            if (status != ErrorCode.OK)
            {
                var errorMessage = GetErrorMessage();
                throw new FitsException((int)status, errorMessage);
            }
        }

        internal static string GetErrorMessage()
        {
            string errorMessage = string.Empty;
            Span<byte> span = stackalloc byte[256];
            while (ReadErrorMsg(span) != 0)
            {
                errorMessage += span.NullTerminatedToString();
            }
            return errorMessage.Trim();
        }

        [LibraryImport("cfitsio", EntryPoint = "fits_is_reentrant")]
        [return: MarshalAs(UnmanagedType.I4)]
        internal static partial bool FitsIsReentrant();

        [LibraryImport("cfitsio", EntryPoint = "ffgmsg")]
        internal static partial int ReadErrorMsg(Span<byte> message);

        [LibraryImport("cfitsio", EntryPoint = "ffdkopn", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial ErrorCode OpenDiskFile(ref nint handle, string filename, IOMode mode, out ErrorCode status);

        [LibraryImport("cfitsio", EntryPoint = "ffclos")]
        internal static partial ErrorCode CloseFile(nint handle, out ErrorCode status);

        [LibraryImport("cfitsio", EntryPoint = "ffghps")]
        internal static partial ErrorCode GetHeaderPosition(nint handle, out int nkeys, out int keysexist, out ErrorCode status);

        [LibraryImport("cfitsio", EntryPoint = "ffgkyn")]
        internal static partial ErrorCode ReadKeyByNumber(nint handle, int keynum, Span<byte> keyname, Span<byte> value, Span<byte> comment, out ErrorCode status);

        [LibraryImport("cfitsio", EntryPoint = "ffgknjj", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial ErrorCode ReadKeyLongLong(nint handle, string keyname, int nstart, int nmax, long[] value, out int nfound, out ErrorCode status);

        [LibraryImport("cfitsio", EntryPoint = "ffpkys", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial ErrorCode WriteKeyString(nint handle, string keyname, string value, string comment, out ErrorCode status);

        [LibraryImport("cfitsio", EntryPoint = "ffdtyp", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial ErrorCode GetKeyType(string cval, out byte dtype, out ErrorCode status);

        [LibraryImport("cfitsio", EntryPoint = "ffgkys", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial ErrorCode ReadKeyString(nint handle, string keyname, Span<byte> value, Span<byte> comment, out ErrorCode status);

        [LibraryImport("cfitsio", EntryPoint = "ffgky", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial ErrorCode ReadKey(nint handle, DataType datatype, string keyname, void* value, Span<byte> comment, out ErrorCode status);

        [LibraryImport("cfitsio", EntryPoint = "ffuky", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial ErrorCode WriteKey(nint handle, DataType datatype, string keyname, void* value, string comment, out ErrorCode status);

        [LibraryImport("cfitsio", EntryPoint = "ffghdt")]
        internal static partial ErrorCode GetHduType(nint handle, out HduType exttype, out ErrorCode status);

        [LibraryImport("cfitsio", EntryPoint = "ffghprll")]
        internal static partial ErrorCode ReadImageHeader(nint handle, int maxdim, out int simple, out ImageType bitpix, out int naxis,
            long[] naxes, out CLong pcount, out CLong gcount, out int extend, out ErrorCode status);

        internal enum DataType : int
        {
            TBIT = 1,
            TBYTE = 11,
            TSBYTE = 12,
            TLOGICAL = 14,
            TSTRING = 16,
            TUSHORT = 20,
            TSHORT = 21,
            TUINT = 30,
            TINT = 31,
            TULONG = 40,
            TLONG = 41,
            TINT32BIT = 41,
            TFLOAT = 42,
            TULONGLONG = 80,
            TLONGLONG = 81,
            TDOUBLE = 82,
            TCOMPLEX = 83,
            TDBLCOMPLEX = 163,
        }

        internal enum ImageType : int
        {
            BYTE_IMG = 8,
            SHORT_IMG = 16,
            LONG_IMG = 32,
            LONGLONG_IMG = 64,
            FLOAT_IMG = -32,
            DOUBLE_IMG = -64,
            SBYTE_IMG = 10,
            USHORT_IMG = 20,
            ULONG_IMG = 40,
            ULONGLONG_IMG = 80,
        }

        internal enum HduType : int
        {
            IMAGE_HDU = 0,
            ASCII_TBL = 1,
            BINARY_TBL = 2,
            ANY_HDU = 3,
        }

        internal enum IOMode : int
        {
            READONLY = 0,
            READWRITE = 1,
        }

        internal enum ErrorCode : int
        {
            OK = 0,

            CREATE_DISK_FILE = -106, /* create disk file, without extended filename syntax */
            OPEN_DISK_FILE = -105, /* open disk file, without extended filename syntax */
            SKIP_TABLE = -104, /* move to 1st image when opening file */
            SKIP_IMAGE = -103, /* move to 1st table when opening file */
            SKIP_NULL_PRIMARY = -102, /* skip null primary array when opening file */
            USE_MEM_BUFF = -101,  /* use memory buffer when opening file */
            OVERFLOW_ERR = -11,  /* overflow during datatype conversion */
            PREPEND_PRIMARY = -9,  /* used in ffiimg to insert new primary array */
            SAME_FILE = 101,  /* input and output files are the same */
            TOO_MANY_FILES = 103,  /* tried to open too many FITS files */
            FILE_NOT_OPENED = 104,  /* could not open the named file */
            FILE_NOT_CREATED = 105,  /* could not create the named file */
            WRITE_ERROR = 106,  /* error writing to FITS file */
            END_OF_FILE = 107,  /* tried to move past end of file */
            READ_ERROR = 108,  /* error reading from FITS file */
            FILE_NOT_CLOSED = 110,  /* could not close the file */
            ARRAY_TOO_BIG = 111,  /* array dimensions exceed internal limit */
            READONLY_FILE = 112,  /* Cannot write to readonly file */
            MEMORY_ALLOCATION = 113,  /* Could not allocate memory */
            BAD_FILEPTR = 114,  /* invalid fitsfile pointer */
            NULL_INPUT_PTR = 115,  /* NULL input pointer to routine */
            SEEK_ERROR = 116,  /* error seeking position in file */
            BAD_NETTIMEOUT = 117,  /* bad value for file download timeout setting */


            BAD_URL_PREFIX = 121,  /* invalid URL prefix on file name */
            TOO_MANY_DRIVERS = 122,  /* tried to register too many IO drivers */
            DRIVER_INIT_FAILED = 123,  /* driver initialization failed */
            NO_MATCHING_DRIVER = 124,  /* matching driver is not registered */
            URL_PARSE_ERROR = 125,  /* failed to parse input file URL */
            RANGE_PARSE_ERROR = 126,  /* failed to parse input file URL */

            SHARED_ERRBASE = 150,
            SHARED_BADARG = 151,
            SHARED_NULPTR = 152,
            SHARED_TABFULL = 153,
            SHARED_NOTINIT = 154,
            SHARED_IPCERR = 155,
            SHARED_NOMEM = 156,
            SHARED_AGAIN = 157,
            SHARED_NOFILE = 158,
            SHARED_NORESIZE = 159,

            HEADER_NOT_EMPTY = 201,  /* header already contains keywords */
            KEY_NO_EXIST = 202,  /* keyword not found in header */
            KEY_OUT_BOUNDS = 203,  /* keyword record number is out of bounds */
            VALUE_UNDEFINED = 204,  /* keyword value field is blank */
            NO_QUOTE = 205,  /* string is missing the closing quote */
            BAD_INDEX_KEY = 206,  /* illegal indexed keyword name */
            BAD_KEYCHAR = 207,  /* illegal character in keyword name or card */
            BAD_ORDER = 208,  /* required keywords out of order */
            NOT_POS_INT = 209,  /* keyword value is not a positive integer */
            NO_END = 210,  /* couldn't find END keyword */
            BAD_BITPIX = 211,  /* illegal BITPIX keyword value*/
            BAD_NAXIS = 212,  /* illegal NAXIS keyword value */
            BAD_NAXES = 213,  /* illegal NAXISn keyword value */
            BAD_PCOUNT = 214,  /* illegal PCOUNT keyword value */
            BAD_GCOUNT = 215,  /* illegal GCOUNT keyword value */
            BAD_TFIELDS = 216,  /* illegal TFIELDS keyword value */
            NEG_WIDTH = 217,  /* negative table row size */
            NEG_ROWS = 218,  /* negative number of rows in table */
            COL_NOT_FOUND = 219,  /* column with this name not found in table */
            BAD_SIMPLE = 220,  /* illegal value of SIMPLE keyword  */
            NO_SIMPLE = 221,  /* Primary array doesn't start with SIMPLE */
            NO_BITPIX = 222,  /* Second keyword not BITPIX */
            NO_NAXIS = 223,  /* Third keyword not NAXIS */
            NO_NAXES = 224,  /* Couldn't find all the NAXISn keywords */
            NO_XTENSION = 225,  /* HDU doesn't start with XTENSION keyword */
            NOT_ATABLE = 226,  /* the CHDU is not an ASCII table extension */
            NOT_BTABLE = 227,  /* the CHDU is not a binary table extension */
            NO_PCOUNT = 228,  /* couldn't find PCOUNT keyword */
            NO_GCOUNT = 229,  /* couldn't find GCOUNT keyword */
            NO_TFIELDS = 230,  /* couldn't find TFIELDS keyword */
            NO_TBCOL = 231,  /* couldn't find TBCOLn keyword */
            NO_TFORM = 232,  /* couldn't find TFORMn keyword */
            NOT_IMAGE = 233,  /* the CHDU is not an IMAGE extension */
            BAD_TBCOL = 234,  /* TBCOLn keyword value < 0 or > rowlength */
            NOT_TABLE = 235,  /* the CHDU is not a table */
            COL_TOO_WIDE = 236,  /* column is too wide to fit in table */
            COL_NOT_UNIQUE = 237,  /* more than 1 column name matches template */
            BAD_ROW_WIDTH = 241,  /* sum of column widths not = NAXIS1 */
            UNKNOWN_EXT = 251,  /* unrecognizable FITS extension type */
            UNKNOWN_REC = 252,  /* unrecognizable FITS record */
            END_JUNK = 253,  /* END keyword is not blank */
            BAD_HEADER_FILL = 254,  /* Header fill area not blank */
            BAD_DATA_FILL = 255,  /* Data fill area not blank or zero */
            BAD_TFORM = 261,  /* illegal TFORM format code */
            BAD_TFORM_DTYPE = 262,  /* unrecognizable TFORM datatype code */
            BAD_TDIM = 263,  /* illegal TDIMn keyword value */
            BAD_HEAP_PTR = 264,  /* invalid BINTABLE heap address */

            BAD_HDU_NUM = 301,  /* HDU number < 1 or > MAXHDU */
            BAD_COL_NUM = 302,  /* column number < 1 or > tfields */
            NEG_FILE_POS = 304,  /* tried to move before beginning of file  */
            NEG_BYTES = 306,  /* tried to read or write negative bytes */
            BAD_ROW_NUM = 307,  /* illegal starting row number in table */
            BAD_ELEM_NUM = 308,  /* illegal starting element number in vector */
            NOT_ASCII_COL = 309,  /* this is not an ASCII string column */
            NOT_LOGICAL_COL = 310,  /* this is not a logical datatype column */
            BAD_ATABLE_FORMAT = 311,  /* ASCII table column has wrong format */
            BAD_BTABLE_FORMAT = 312,  /* Binary table column has wrong format */
            NO_NULL = 314,  /* null value has not been defined */
            NOT_VARI_LEN = 317,  /* this is not a variable length column */
            BAD_DIMEN = 320,  /* illegal number of dimensions in array */
            BAD_PIX_NUM = 321,  /* first pixel number greater than last pixel */
            ZERO_SCALE = 322,  /* illegal BSCALE or TSCALn keyword = 0 */
            NEG_AXIS = 323,  /* illegal axis length < 1 */

            NOT_GROUP_TABLE = 340,
            HDU_ALREADY_MEMBER = 341,
            MEMBER_NOT_FOUND = 342,
            GROUP_NOT_FOUND = 343,
            BAD_GROUP_ID = 344,
            TOO_MANY_HDUS_TRACKED = 345,
            HDU_ALREADY_TRACKED = 346,
            BAD_OPTION = 347,
            IDENTICAL_POINTERS = 348,
            BAD_GROUP_ATTACH = 349,
            BAD_GROUP_DETACH = 350,

            BAD_I2C = 401,  /* bad int to formatted string conversion */
            BAD_F2C = 402,  /* bad float to formatted string conversion */
            BAD_INTKEY = 403,  /* can't interprete keyword value as integer */
            BAD_LOGICALKEY = 404,  /* can't interprete keyword value as logical */
            BAD_FLOATKEY = 405,  /* can't interprete keyword value as float */
            BAD_DOUBLEKEY = 406,  /* can't interprete keyword value as double */
            BAD_C2I = 407,  /* bad formatted string to int conversion */
            BAD_C2F = 408,  /* bad formatted string to float conversion */
            BAD_C2D = 409,  /* bad formatted string to double conversion */
            BAD_DATATYPE = 410,  /* bad keyword datatype code */
            BAD_DECIM = 411,  /* bad number of decimal places specified */
            NUM_OVERFLOW = 412,  /* overflow during datatype conversion */

            DATA_COMPRESSION_ERR = 413,  /* error in imcompress routines */
            DATA_DECOMPRESSION_ERR = 414, /* error in imcompress routines */
            NO_COMPRESSED_TILE = 415, /* compressed tile doesn't exist */

            BAD_DATE = 420,  /* error in date or time conversion */

            PARSE_SYNTAX_ERR = 431,  /* syntax error in parser expression */
            PARSE_BAD_TYPE = 432,  /* expression did not evaluate to desired type */
            PARSE_LRG_VECTOR = 433,  /* vector result too large to return in array */
            PARSE_NO_OUTPUT = 434,  /* data parser failed not sent an out column */
            PARSE_BAD_COL = 435,  /* bad data encounter while parsing column */
            PARSE_BAD_OUTPUT = 436,  /* Output file not of proper type          */

            ANGLE_TOO_BIG = 501,  /* celestial angle too large for projection */
            BAD_WCS_VAL = 502,  /* bad celestial coordinate or pixel value */
            WCS_ERROR = 503,  /* error in celestial coordinate calculation */
            BAD_WCS_PROJ = 504,  /* unsupported type of celestial projection */
            NO_WCS_KEY = 505,  /* celestial coordinate keywords not found */
            APPROX_WCS_KEY = 506,  /* approximate WCS keywords were calculated */

            NO_CLOSE_ERROR = 999,  /* special value used internally to switch off */
            /* the error message from ffclos and ffchdu */
        }
    }
}
