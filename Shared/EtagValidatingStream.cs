using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

public class EtagInvalidException : IOException
{
    public readonly string Expected;
    public readonly string Actual;

    public EtagInvalidException(string expected, string actual)
    {
        Expected = expected;
        Actual = actual;
    }
}

public class EtagValidatingStream : Stream
{
    private readonly Stream _source;
    private readonly string _etag;
    private readonly MD5 _hash = MD5.Create();

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _source.Length;
    public override long Position
    {
        get => _source.Position;
        set => _source.Position = value;
    }

    public EtagValidatingStream(Stream source, string etag)
    {
        _source = source;
        _etag = etag;

        // Fail early if we don't understand the etag format
        if (_etag.Length != 32) Fail();
    }

    private void Fail(string actual = "ukn")
    {
        throw new EtagInvalidException(_etag, actual);
    }

    private void Validate()
    {
        _hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var result = string.Join("", _hash.Hash.Select(x => x.ToString("x2")));

        if (!result.Equals(_etag, StringComparison.InvariantCultureIgnoreCase)) Fail(result);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var c = _source.Read(buffer, offset, count);
        if (c == 0 && count > 0)
        {
            Validate();
            return 0;
        }

        _hash.TransformBlock(buffer, offset, c, null, 0);
        return c;
    }

    public override void Flush() => _source.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
