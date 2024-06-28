namespace Lumidex.Core.Data;

public enum ImageType
{
    Unknown = 0,
    Light = 1,
    Flat = 2,
    Dark = 3,
    Bias = 4,
}

public enum ImageKind
{
    Unknown = 0,
    Raw = 1,
    Intermediate = 2,
    Calibration = 3,
    Master = 4,
    Processed = 5,
}