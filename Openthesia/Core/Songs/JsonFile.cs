using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Openthesia.Core.Songs;

internal static class JsonFile
{
    public static string GetChartPath(string directory, ChartId chartId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(chartId.Value));
        return Path.Combine(
            directory,
            $"{Convert.ToHexString(hash).ToLowerInvariant()}.json");
    }

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

    public static bool ExistingDocumentCanBeOverwritten(
        string path,
        Action<string> validate)
    {
        if (!File.Exists(path))
            return true;

        try
        {
            validate(path);
            return true;
        }
        catch (Exception exception) when (IsDataFailure(exception))
        {
            return false;
        }
    }

    public static bool TryWrite(string path, object document)
    {
        try
        {
            Write(path, document);
            return true;
        }
        catch (Exception exception) when (IsDataFailure(exception))
        {
            return false;
        }
    }

    public static bool IsDataFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException;
    }
}
