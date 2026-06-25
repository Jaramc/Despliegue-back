using System.Security.Cryptography;
using System.Text;

namespace RentalAI.Api.Modules.Kyc;

public static class DocumentCrypto
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    public static byte[] Encrypt(byte[] plaintext, string masterKey, Guid userId)
    {
        var key = DeriveKey(masterKey, userId);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var blob = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, blob, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, blob, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, blob, NonceSize + TagSize, ciphertext.Length);
        return blob;
    }

    private static byte[] DeriveKey(string masterKey, Guid userId) =>
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Encoding.UTF8.GetBytes(masterKey),
            KeySize,
            salt: null,
            info: userId.ToByteArray());
}
