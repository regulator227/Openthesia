namespace Openthesia.Core.Songs;

public sealed class LearnerRegistry
{
    private const int SchemaVersion = 1;
    private readonly string _learnersPath;
    private readonly string _deviceSettingsPath;

    public LearnerRegistry(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            throw new ArgumentException("A data directory is required.", nameof(dataDirectory));

        var root = Path.GetFullPath(dataDirectory);
        _learnersPath = Path.Combine(root, "Learners.json");
        _deviceSettingsPath = Path.Combine(root, "DeviceSettings.json");
    }

    public LearnerProfile GetOrCreateActive()
    {
        var profiles = ReadLearners();
        var activeId = ReadActiveLearnerId();
        if (profiles.Count == 0)
        {
            var created = new LearnerProfile(LearnerId.New(), "Default Learner");
            profiles.Add(created);
            WriteLearners(profiles);
            WriteActiveLearner(created.Id);
            return created;
        }

        var active = profiles.SingleOrDefault(profile => profile.Id == activeId);
        if (active is not null)
            return active;
        if (activeId is not null)
            throw new InvalidDataException("The active Learner is not a registered profile.");

        WriteActiveLearner(profiles[0].Id);
        return profiles[0];
    }

    public IReadOnlyList<LearnerProfile> GetAll()
    {
        return ReadLearners();
    }

    public LearnerProfile Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A learner name is required.", nameof(name));

        var profiles = ReadLearners();
        var created = new LearnerProfile(LearnerId.New(), name.Trim());
        profiles.Add(created);
        WriteLearners(profiles);
        return created;
    }

    public void SetActive(LearnerId learnerId)
    {
        if (!ReadLearners().Any(profile => profile.Id == learnerId))
            throw new ArgumentException("The active Learner must be a registered profile.", nameof(learnerId));

        WriteActiveLearner(learnerId);
    }

    private List<LearnerProfile> ReadLearners()
    {
        if (!File.Exists(_learnersPath))
            return new List<LearnerProfile>();

        var document = JsonFile.Read<LearnersDocument>(_learnersPath);
        if (document.Version != SchemaVersion)
            throw new InvalidDataException($"Unsupported Learners version {document.Version}.");
        if (document.Learners is null ||
            document.Learners.Any(learner => learner.Id == Guid.Empty || string.IsNullOrWhiteSpace(learner.Name)) ||
            document.Learners.Select(learner => learner.Id).Distinct().Count() != document.Learners.Count)
        {
            throw new InvalidDataException("The Learners document contains invalid profiles.");
        }

        return document.Learners
            .Select(learner => new LearnerProfile(new LearnerId(learner.Id), learner.Name))
            .ToList();
    }

    private LearnerId? ReadActiveLearnerId()
    {
        if (!File.Exists(_deviceSettingsPath))
            return null;

        var document = JsonFile.Read<DeviceSettingsDocument>(_deviceSettingsPath);
        if (document.Version != SchemaVersion)
            throw new InvalidDataException($"Unsupported Device Settings version {document.Version}.");
        if (document.ActiveLearnerId == Guid.Empty)
            throw new InvalidDataException("The active Learner identity is invalid.");

        return document.ActiveLearnerId is null
            ? null
            : new LearnerId(document.ActiveLearnerId.Value);
    }

    private void WriteLearners(IReadOnlyList<LearnerProfile> profiles)
    {
        var document = new LearnersDocument
        {
            Version = SchemaVersion,
            Learners = profiles
                .Select(profile => new LearnerDocument
                {
                    Id = profile.Id.Value,
                    Name = profile.Name
                })
                .ToList()
        };
        JsonFile.Write(_learnersPath, document);
    }

    private void WriteActiveLearner(LearnerId learnerId)
    {
        JsonFile.Write(
            _deviceSettingsPath,
            new DeviceSettingsDocument
            {
                Version = SchemaVersion,
                ActiveLearnerId = learnerId.Value
            });
    }

    private sealed class LearnersDocument
    {
        public int Version { get; set; }
        public List<LearnerDocument> Learners { get; set; } = null!;
    }

    private sealed class LearnerDocument
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class DeviceSettingsDocument
    {
        public int Version { get; set; }
        public Guid? ActiveLearnerId { get; set; }
    }
}
