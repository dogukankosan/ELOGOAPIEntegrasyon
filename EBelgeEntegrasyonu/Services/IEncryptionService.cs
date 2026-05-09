using System.Security.Cryptography;
using System.Text;

namespace EBelgeAPI.Services;
public interface IEncryptionService
{
    string Encrypt(string plain);
    string Decrypt(string cipher);
}
public class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    public AesEncryptionService(IConfiguration config)
    {
        string? keyStr = config["Encryption:Key"]
            ?? throw new InvalidOperationException("Encryption:Key appsettings'te tanımlı değil.");
        byte[] keyBytes = Encoding.UTF8.GetBytes(keyStr);
        if (keyBytes.Length != 32)
            throw new InvalidOperationException("Encryption:Key tam 32 karakter (byte) olmalıdır.");
        _key = keyBytes;
    }
    public string Encrypt(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return string.Empty;
        byte[] nonce = new byte[12];
        byte[]tag = new byte[16];
        byte[] input = Encoding.UTF8.GetBytes(plain);
        byte[] cipher = new byte[input.Length];
        RandomNumberGenerator.Fill(nonce);
        using AesGcm aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, input, cipher, tag);
        byte[] result = new byte[12 + 16 + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, 12);
        Buffer.BlockCopy(tag, 0, result, 12, 16);
        Buffer.BlockCopy(cipher, 0, result, 12 + 16, cipher.Length);
        return Convert.ToBase64String(result);
    }
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;
        byte[] all = Convert.FromBase64String(cipherText);
        byte[] nonce = new byte[12];
        byte[] tag = new byte[16];
        byte[] cipher = new byte[all.Length - 12 - 16];
        Buffer.BlockCopy(all, 0, nonce, 0, 12);
        Buffer.BlockCopy(all, 12, tag, 0, 16);
        Buffer.BlockCopy(all, 12 + 16, cipher, 0, cipher.Length);
        byte[] plain = new byte[cipher.Length];
        using AesGcm aes = new AesGcm(_key, tag.Length);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}