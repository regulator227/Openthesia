using System.Security.Cryptography;
using System.Text;

namespace Openthesia.Settings;

internal sealed record MidiDeviceDescriptor(string Token, string Name);

internal sealed class DisposableDeviceCatalog<TDevice>
    where TDevice : class, IDisposable
{
    private readonly Func<IReadOnlyList<TDevice>> _enumerate;
    private readonly Func<TDevice, string> _getName;
    private readonly object _snapshotGate = new();
    private Dictionary<string, int> _lastNameCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _nameGenerations = new(StringComparer.Ordinal);
    private bool _hasSnapshot;

    public DisposableDeviceCatalog(
        Func<IReadOnlyList<TDevice>> enumerate,
        Func<TDevice, string> getName)
    {
        ArgumentNullException.ThrowIfNull(enumerate);
        ArgumentNullException.ThrowIfNull(getName);
        _enumerate = enumerate;
        _getName = getName;
    }

    public IReadOnlyList<MidiDeviceDescriptor> Describe()
    {
        var devices = _enumerate();
        try
        {
            return Describe(devices);
        }
        finally
        {
            DisposeExcept(devices, retainedIndex: -1);
        }
    }

    public TDevice? Take(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("A device token is required.", nameof(token));

        var devices = _enumerate();
        var selectedIndex = -1;
        try
        {
            var descriptors = Describe(devices);
            for (var index = 0; index < descriptors.Count; index++)
            {
                if (StringComparer.Ordinal.Equals(descriptors[index].Token, token))
                {
                    selectedIndex = index;
                    break;
                }
            }

            return selectedIndex >= 0 ? devices[selectedIndex] : null;
        }
        finally
        {
            DisposeExcept(devices, selectedIndex);
        }
    }

    internal static string CreateToken(string name, int occurrence)
    {
        return CreateToken(name, occurrence, generation: 0);
    }

    private static string CreateToken(
        string name,
        int occurrence,
        int generation)
    {
        if (name is null)
            throw new ArgumentNullException(nameof(name));
        if (occurrence < 0)
            throw new ArgumentOutOfRangeException(nameof(occurrence));

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return $"{Convert.ToHexString(digest).ToLowerInvariant()}-{occurrence}-{generation}";
    }

    private IReadOnlyList<MidiDeviceDescriptor> Describe(
        IReadOnlyList<TDevice> devices)
    {
        var names = devices.Select(_getName).ToArray();
        lock (_snapshotGate)
        {
            var nameCounts = names
                .GroupBy(name => name, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count(),
                    StringComparer.Ordinal);
            if (_hasSnapshot)
            {
                foreach (var name in _lastNameCounts.Keys
                             .Concat(nameCounts.Keys)
                             .Distinct(StringComparer.Ordinal))
                {
                    _lastNameCounts.TryGetValue(name, out var previousCount);
                    nameCounts.TryGetValue(name, out var currentCount);
                    if (previousCount != currentCount)
                    {
                        // DryWetMIDI 8 exposes only the device name on Windows. Version a
                        // same-name group when its shape changes so stale occurrence tokens
                        // cannot silently rebind to different hardware after hot-plug.
                        _nameGenerations.TryGetValue(name, out var generation);
                        _nameGenerations[name] = generation + 1;
                    }
                }
            }
            _lastNameCounts = nameCounts;
            _hasSnapshot = true;

            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            var descriptors = new MidiDeviceDescriptor[devices.Count];
            for (var index = 0; index < devices.Count; index++)
            {
                var name = names[index];
                occurrences.TryGetValue(name, out var occurrence);
                occurrences[name] = occurrence + 1;
                _nameGenerations.TryGetValue(name, out var generation);
                descriptors[index] = new MidiDeviceDescriptor(
                    CreateToken(name, occurrence, generation),
                    name);
            }

            return descriptors;
        }
    }

    private static void DisposeExcept(
        IReadOnlyList<TDevice> devices,
        int retainedIndex)
    {
        for (var index = 0; index < devices.Count; index++)
        {
            if (index != retainedIndex)
                devices[index].Dispose();
        }
    }
}
