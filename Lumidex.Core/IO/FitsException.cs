namespace Lumidex.Core.IO;

public class FitsException : Exception
{
    public FitsException(string message)
        : base(message)
    {
    }

    public FitsException(int errorCode)
        : base(((FitsFile.Native.ErrorCode)errorCode).ToString())
    {
    }

    public FitsException(int errorCode, string errorMessage)
        : base(((FitsFile.Native.ErrorCode)errorCode).ToString() + " | " + errorMessage)
    {
    }

    internal FitsException(FitsFile.Native.ErrorCode errorCode)
        : this((int) errorCode)
    {
    }

    internal FitsException(FitsFile.Native.ErrorCode errorCode, string message)
        : this((int)errorCode, message)
    {
    }
}