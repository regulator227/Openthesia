using Newtonsoft.Json;

namespace Openthesia.Core.Songs;

internal static class JsonFile
{
    public static T Read<T>(string path) where T : class
    {
        return JsonConvert.DeserializeObject<T>(File.ReadAllText(path))
            ?? throw new InvalidDataException("The JSON document is empty.");
    }

    public static void Write(string path, object document)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonConvert.SerializeObject(document, Formatting.Indented));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
