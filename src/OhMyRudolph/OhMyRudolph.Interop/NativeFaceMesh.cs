using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OhMyRudolph.Interop;

public enum NativeStatus
{
    Success = 0,
    InvalidInput = 1,
    ResourceUnavailable = 2,
    InferenceFailed = 3,
    OutputBufferTooSmall = 4
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct NativePoint
{
    public readonly int X;
    public readonly int Y;
}

public sealed class NativeFaceMeshException : Exception
{
    public NativeFaceMeshException(NativeStatus status) : base($"Native face mesh failed (status {(int)status}).") => Status = status;
    public NativeStatus Status { get; }
}

public sealed class NativeFaceMesh : IDisposable
{
    public const uint AbiVersion = 1;
    public const int MaxFaces = 5;
    private readonly NativeHandle _handle;

    public NativeFaceMesh(string resourceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceRoot);
        uint version = NativeMethods.AbiVersion();
        if (version != AbiVersion)
            throw new InvalidOperationException("Incompatible native face mesh ABI.");
        NativeStatus status = NativeMethods.Create(resourceRoot, out nint handle);
        if (status != NativeStatus.Success || handle == 0)
        {
            if (handle != 0) NativeMethods.Destroy(handle);
            throw new NativeFaceMeshException(status == NativeStatus.Success ? NativeStatus.ResourceUnavailable : status);
        }
        _handle = new NativeHandle(handle);
    }

    // The handle is kept alive by SafeHandle marshalling until the native call returns.
    // Both spans are pinned only for that call; the native API must not retain them.
    public unsafe int Detect(ReadOnlySpan<byte> jpeg, int width, int height, Span<NativePoint> points)
    {
        if (jpeg.IsEmpty || width <= 0 || height <= 0 || points.Length < MaxFaces)
            throw new ArgumentException("Invalid native face mesh input.");
        int written = 0;
        NativeStatus status;
        fixed (byte* input = jpeg)
        fixed (NativePoint* output = points)
            status = NativeMethods.Detect(_handle, input, jpeg.Length, width, height, output, MaxFaces, &written);
        if (status != NativeStatus.Success) throw new NativeFaceMeshException(status);
        if ((uint)written > MaxFaces) throw new NativeFaceMeshException(NativeStatus.InferenceFailed);
        return written;
    }

    public void Dispose() => _handle.Dispose();
}

internal sealed class NativeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal NativeHandle(nint value) : base(true) => SetHandle(value);
    protected override bool ReleaseHandle()
    {
        NativeMethods.Destroy(handle);
        return true;
    }
}

internal static partial class NativeMethods
{
    private const string Library = "ohmyrudolph_native";

    [LibraryImport(Library, EntryPoint = "om_abi_version")]
    internal static partial uint AbiVersion();

    [LibraryImport(Library, EntryPoint = "om_create", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeStatus Create(string resourceRoot, out nint handle);

    [LibraryImport(Library, EntryPoint = "om_destroy")]
    internal static partial void Destroy(nint handle);

    [LibraryImport(Library, EntryPoint = "om_detect_jpeg")]
    internal static unsafe partial NativeStatus Detect(NativeHandle handle, byte* jpeg, int jpegSize,
        int expectedWidth, int expectedHeight, NativePoint* points, int capacity, int* written);
}
