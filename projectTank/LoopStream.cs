using NAudio.Wave;

// =============================================================================
// LOOP STREAM
// A custom audio stream that makes any audio source play on an infinite loop.
// It works by inheriting from NAudio's WaveStream and overriding the Read method.
// Whenever the source reaches its end, we rewind it back to the start
// so playback continues seamlessly without any gaps or interruptions.
//
// Used for: Background music and the tank engine movement sounds.
// =============================================================================
public class LoopStream : WaveStream
{
    // The original audio source we are wrapping and looping
    private readonly WaveStream source;

    // =========================================================================
    // CONSTRUCTOR
    // Accepts any WaveStream (e.g. Mp3FileReader, WaveFileReader) and stores it.
    // All audio format information and data will be read from this source.
    // =========================================================================
    public LoopStream(WaveStream source)
    {
        this.source = source;
    }

    // =========================================================================
    // WAVE FORMAT
    // Tells NAudio what audio format this stream uses (sample rate, channels, etc.)
    // We just pass through whatever format the original source already has.
    // =========================================================================
    public override WaveFormat WaveFormat => source.WaveFormat;

    // =========================================================================
    // LENGTH
    // Returns the total byte length of the audio source.
    // NAudio uses this to know how large the audio data is.
    // =========================================================================
    public override long Length => source.Length;

    // =========================================================================
    // POSITION
    // Gets or sets the current playback position in bytes.
    // We forward this directly to the source so NAudio can seek normally.
    // =========================================================================
    public override long Position
    {
        get => source.Position;
        set => source.Position = value;
    }

    // =========================================================================
    // READ (The Core Loop Logic)
    // NAudio calls this method repeatedly to request the next chunk of audio data.
    //
    // Parameters:
    //   buffer — the byte array NAudio wants us to fill with audio data
    //   offset — where in the buffer to start writing
    //   count  — how many bytes NAudio is requesting
    //
    // Normally, when a WaveStream runs out of data it returns 0, which tells
    // NAudio to stop playback. Here we intercept that and rewind instead,
    // so the audio restarts from the beginning and keeps filling the buffer.
    // This loop continues until we have filled exactly the number of bytes
    // that NAudio requested.
    // =========================================================================
    public override int Read(byte[] buffer, int offset, int count)
    {
        int total = 0; // Tracks how many bytes we have written so far

        while (total < count) // Keep going until the buffer is completely filled
        {
            // Ask the source to fill the remaining portion of the buffer
            int read = source.Read(buffer, offset + total, count - total);

            if (read == 0)
            {
                // Source reached the end — rewind to the beginning to loop
                source.Position = 0;
            }
            else
            {
                // Got some data — add it to our running total
                total += read;
            }
        }

        return total; // Return the total bytes written, which always equals count
    }
}