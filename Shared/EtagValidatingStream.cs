using System;
using System.IO;

public class EtagValidatingStream : Stream
{
    
    public override bool CanRead => true;
    public override bool CanWrite => false;
    public override bool CanSeek => false;
    public override long Length => _source.Length;
    public override long Position
    {
        get => _source.Position;
        set => throw new NotSupportedException();
    }
    
    private readonly Stream _source;
    private readonly EtagValidation _validation;
    private readonly EtagValidation.IDigest _digest;
    private readonly string _etag;
    
    public EtagValidatingStream(Stream src, EtagValidation validation, string etag)
    {
        _source = src;
        _validation = validation;
        _digest = validation.NewDigest();
        _etag = etag;
    }
    
    //
    
    public override void Flush()
    {
        _source.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0) return 0;
        int c = _source.Read(buffer, offset, count);
        if (c == 0)
        {
            bool ok = _validation.Check(_etag, _digest);
            if (!ok) throw new IOException("checksum failed (etag: " + _etag + ")");
            return 0;
        }
        _digest.Update(buffer, offset, c);
        return c;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
    
}
