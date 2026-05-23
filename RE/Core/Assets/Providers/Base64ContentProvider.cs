using System.Buffers;
using System.Buffers.Text;
using System.Text;

namespace RE.Core.Assets.Providers;

public class Base64ContentProvider : IContentProvider
{
    public static string Encode(byte[] data)
    {
        var length = Base64.GetMaxDecodedFromUtf8Length(data.Length);

        byte[] output = ArrayPool<byte>.Shared.Rent(length);
        Base64.EncodeToUtf8(data, output, out _, out var written);
        byte[] result = output[..written];
        ArrayPool<byte>.Shared.Return(output);

        return Encoding.UTF8.GetString(result);
    }

    private static byte[] Decode(string path)
    {
        var input = Encoding.UTF8.GetBytes(path);

        var length = Base64.GetMaxDecodedFromUtf8Length(input.Length);
        byte[] output = ArrayPool<byte>.Shared.Rent(length);

        var status = Base64.DecodeFromUtf8(input, output, out _, out var written);

        if (status != OperationStatus.Done)
        {
            ArrayPool<byte>.Shared.Return(output);
            throw new FormatException("Invalid base64 string.");
        }

        byte[] result = output[..written];

        ArrayPool<byte>.Shared.Return(output);

        return result;
    }

    /// <inheritdoc />
    public byte[] GetBytes(string path, int offset, int count)
    {
        return Decode(path)[offset..(offset + count)];
    }

    /// <inheritdoc />
    public byte[] GetBytes(string path)
    {
        return Decode(path);
    }

    /// <inheritdoc />
    public bool Exists(string path)
    {
        try
        {
            Decode(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool DirectoryExists(string path)
    {
        return false;
    }

    /// <inheritdoc />
    public Stream Open(string path)
    {
        return new MemoryStream(Decode(path), writable: false);
    }

    /// <inheritdoc />
    public string[] GetFiles(string path, bool recursive = false)
    {
        return [];
    }

    /// <inheritdoc />
    public string[] GetDirectories(string path, bool recursive = false)
    {
        return [];
    }

    /// <inheritdoc />
    public string Prefix => "base64:";
}