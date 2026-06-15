using System.Security.Cryptography;

namespace SUP.P2FK
{
    public class RIPEMD160
    {
        // System.Security.Cryptography.RIPEMD160Managed was removed in .NET 8.
        // NBitcoin ships its own BouncyCastle RIPEMD-160 implementation; we delegate to it.
        public static byte[] Hash(byte[] data)
        {
            var digest = new NBitcoin.BouncyCastle.Crypto.Digests.RipeMD160Digest();
            digest.BlockUpdate(data, 0, data.Length);
            byte[] result = new byte[digest.GetDigestSize()];
            digest.DoFinal(result, 0);
            return result;
        }

        public static byte[] Hash(string hexData)
        {
            byte[] bytes = Hex.HexToBytes(hexData);
            return Hash(bytes);
        }
    }
}
