using System;
using System.Runtime.InteropServices;

public class xdelta3
{
    /// <summary>
    /// Sets the maximum buffer size that xdelta3 is allowed to write to.
    /// </summary>
    static readonly int MAX_BUFFER = 32 * 1024 * 1024; // 32 MB

    /// <summary>
    /// Creates xdelta3 patch from source to target.
    /// </summary>
    /// <param name="target">The target of the patch (the outcome of patching).</param>
    /// <param name="source">The source of the patch (what will be patched).</param>
    /// <returns>Xdelta3 patch data.</returns>
    public static byte[] CreatePatch(string file, byte[] target, byte[] source)
    {
        byte[] obuf = new byte[MAX_BUFFER];
        UInt32 obufSize;

        // Call xdelta3 library
        int result = xd3_encode_memory(target, (UInt32)target.Length,
            source, (UInt32)source.Length,
            obuf, out obufSize,
            (UInt32)obuf.Length, 0);

        // Check result
        if (result != 0)
        {
            throw new xdelta3Exception(result, file);
        }

        // Trim the output
        byte[] output = new byte[obufSize];
        Buffer.BlockCopy(obuf, 0, output, 0, (int)obufSize);

        return output;
    }

    /// <summary>
    /// Applies xdelta3 patch to source.
    /// </summary>
    /// <param name="patch">xdelta3 patch data.</param>
    /// <param name="source">The data to be patched.</param>
    /// <returns>Patched data.</returns>
    public static byte[] ApplyPatch(string file, byte[] patch, byte[] source)
    {
        byte[] obuf = new byte[patch.Length + source.Length + MAX_BUFFER];
        UInt32 obufSize;

        // Call xdelta3 library
        int result = xd3_decode_memory(patch, (UInt32)patch.Length,
            source, (UInt32)source.Length,
            obuf, out obufSize,
            (UInt32)obuf.Length, 0);

        // Check result
        if (result != 0)
        {
            throw new xdelta3Exception(result, file);
        }

        // Trim the output
        byte[] output = new byte[obufSize];
        Buffer.BlockCopy(obuf, 0, output, 0, (int)obufSize);

        return output;
    }

    #region PInvoke wrappers

    [DllImport("xdelta3.dll", EntryPoint = "xd3_encode_memory", CallingConvention = CallingConvention.Cdecl)]
    static extern int xd3_encode_memory(
        byte[] input,
        UInt32 input_size,
        byte[] source,
        UInt32 source_size,
        byte[] output_buffer,
        out UInt32 output_size,
        UInt32 avail_output,
        int flags);

    [DllImport("xdelta3.dll", EntryPoint = "xd3_decode_memory", CallingConvention = CallingConvention.Cdecl)]
    static extern int xd3_decode_memory(
        byte[] input,
        UInt32 input_size,
        byte[] source,
        UInt32 source_size,
        byte[] output_buffer,
        out UInt32 output_size,
        UInt32 avail_output,
        int flags);

    #endregion

}

#region Exceptions

public class xdelta3Exception : Exception
{
    public int ExceptionCode { get; }
    public string File { get; }
    public override string Message => $"xdelta3Exception: Code {ExceptionCode} while patching {File}";

    public xdelta3Exception(int rCode, string file)
    {
        ExceptionCode = rCode;
        File = file;
    }
}

#endregion