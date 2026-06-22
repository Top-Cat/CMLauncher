using System;
using System.Security.Cryptography;

/**
 * Scheme to handle ETag header
 * validation for HTTP transfers
 */
public abstract class EtagValidation
{

    public static readonly EtagValidation None = new NullImpl();
    public static readonly EtagValidation HexMd5 = new Md5HexImpl();
    
    //
    
    public abstract IDigest NewDigest();

    public abstract bool Check(string etag, IDigest hash);
    
    //
    
    public interface IDigest : IDisposable
    {
        void Update(byte[] buffer, int offset, int count);
    }
    
    private class NullImpl : EtagValidation
    {
        
        public override IDigest NewDigest()
        {
            return new Digest();
        }

        public override bool Check(string etag, IDigest hash)
        {
            return true;
        }
        
        //

        private class Digest : IDigest
        {
            
            public void Update(byte[] buffer, int offset, int count)
            {
                // NO-OP
            }

            public void Dispose()
            {
                // NO-OP
            }
            
        }
        
    }
    
    private class Md5HexImpl : EtagValidation
    {
        
        public override IDigest NewDigest()
        {
            return new Digest();
        }

        public override bool Check(string etag, IDigest hash)
        {
            etag = etag.Trim('"');
            if (etag.Length != 32) return false;
            if (hash is not Digest) return false;
            byte[] a = ((Digest) hash).Complete();
            byte[] b;
            try { b = Convert.FromHexString(etag); } catch (FormatException) { return false; }
            return CryptographicOperations.FixedTimeEquals(a, b);
        }
        
        //
        
        private class Digest : IDigest
        {

            public readonly MD5 Hash;

            public Digest()
            {
                Hash = MD5.Create();
            }
            
            //

            public void Update(byte[] buffer, int offset, int count)
            {
                Hash.TransformBlock(buffer, offset, count, null, 0);
            }

            public byte[] Complete()
            {
                Hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return Hash.Hash;
            }

            public void Dispose()
            { 
                Hash.Dispose();
            }
            
        }
        
    }
    
}