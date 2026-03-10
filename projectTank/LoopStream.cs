using NAudio.Wave;

public class LoopStream : WaveStream
{
    private readonly WaveStream source;

    public LoopStream(WaveStream source)
    {
        this.source = source;
    }

    public override WaveFormat WaveFormat => source.WaveFormat;
    public override long Length => source.Length;

    public override long Position
    {
        get => source.Position;
        set => source.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int total = 0;

        while (total < count)
        {
            int read = source.Read(buffer, offset + total, count - total);
            if (read == 0)
                source.Position = 0; // loop
            else
                total += read;
        }

        return total;
    }
}