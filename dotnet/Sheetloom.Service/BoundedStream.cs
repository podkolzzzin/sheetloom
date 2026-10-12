public static class BoundedStream
{
    public static async Task<byte[]> ReadAsync(
        Stream stream, long? length, int limit, string errorCode, CancellationToken token)
    {
        if (length > limit)
        {
            throw new ServiceError(413, errorCode, "Size limit exceeded");
        }

        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        int count;
        while ((count = await stream.ReadAsync(buffer, token)) != 0)
        {
            if (output.Length + count > limit)
            {
                throw new ServiceError(413, errorCode, "Size limit exceeded");
            }

            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }

        return output.ToArray();
    }
}
