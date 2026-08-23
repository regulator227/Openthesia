namespace Openthesia.Core.Audio;

internal static class AudioOutputStartup
{
    internal static bool TryStart<TOutput>(
        Func<TOutput> create,
        Action<TOutput> initialize,
        Action<TOutput> play,
        Action<TOutput> dispose,
        out TOutput output,
        out Exception? error)
        where TOutput : class
    {
        TOutput? candidate = null;

        try
        {
            candidate = create();
            initialize(candidate);
            play(candidate);

            output = candidate;
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            if (candidate is not null)
            {
                try
                {
                    dispose(candidate);
                }
                catch
                {
                    // Preserve the startup error; cleanup failure cannot make this output usable.
                }
            }

            output = null!;
            error = ex;
            return false;
        }
    }
}
