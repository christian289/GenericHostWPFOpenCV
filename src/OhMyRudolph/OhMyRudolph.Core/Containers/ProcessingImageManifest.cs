using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace OhMyRudolph.Core.Containers;

public sealed record ProcessingImageManifest(
    int SchemaVersion, string ApplicationId, string PackageVersion, string HostVersion,
    int ContractVersion, int AbiVersion, string Platform, string ImageId, string ArchiveFile,
    string ArchiveSha256, string PodmanMachine, string PodmanConnection, string Network, int ContainerPort)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static async Task<ProcessingImageManifest> LoadAndVerifyAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (file.Length > 65536) throw new InvalidDataException("Processing image manifest is too large.");
        ProcessingImageManifest manifest;
        try { manifest = await JsonSerializer.DeserializeAsync<ProcessingImageManifest>(file, Options, cancellationToken).ConfigureAwait(false) ?? throw new InvalidDataException("Empty processing image manifest."); }
        catch (JsonException ex) { throw new InvalidDataException("Invalid processing image manifest.", ex); }
        string? informationalVersion = typeof(ProcessingImageManifest).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string? buildVersion = informationalVersion?.Split('+', 2)[0];
        if (manifest.SchemaVersion != 1 || manifest.ApplicationId != "OhMyRudolph" ||
            !IsSemVer(buildVersion) || !IsSemVer(manifest.PackageVersion) ||
            manifest.PackageVersion != buildVersion || manifest.HostVersion != buildVersion ||
            manifest.ContractVersion != 1 || manifest.AbiVersion != 1 || manifest.Platform != "linux/amd64" ||
            manifest.Network != "pasta" || manifest.ContainerPort != 8080 ||
            manifest.PodmanMachine != "podman-machine-default" || string.IsNullOrWhiteSpace(manifest.PodmanConnection) ||
            !IsDigest(manifest.ImageId, true) || !IsDigest(manifest.ArchiveSha256, false) ||
            manifest.ArchiveFile != "processing-image.oci.tar")
            throw new InvalidDataException("Unsupported or invalid processing image manifest.");
        string archive = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, manifest.ArchiveFile);
        await using var stream = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] digest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(digest, Convert.FromHexString(manifest.ArchiveSha256)))
            throw new InvalidDataException("Processing image archive digest mismatch.");
        return manifest;
    }

    private static bool IsSemVer(string? version)
    {
        if (string.IsNullOrEmpty(version)) return false;
        int dash = version.IndexOf('-');
        string core = dash < 0 ? version : version[..dash];
        string[] components = core.Split('.');
        if (components.Length != 3 || components.Any(part => !IsNumericIdentifier(part))) return false;
        if (dash < 0) return true;
        string prerelease = version[(dash + 1)..];
        foreach (string identifier in prerelease.Split('.'))
        {
            if (identifier.Length == 0 || identifier.Any(c => !IsAsciiDigit(c) && c is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z' or '-')))
                return false;
            if (identifier.All(IsAsciiDigit) && !IsNumericIdentifier(identifier)) return false;
        }
        return true;
    }

    private static bool IsNumericIdentifier(string value) =>
        value.Length > 0 && (value.Length == 1 || value[0] != '0') && value.All(IsAsciiDigit);

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    private static bool IsDigest(string? value, bool prefix)
    {
        if (value is null || value.Length != (prefix ? 71 : 64) || (prefix && !value.StartsWith("sha256:", StringComparison.Ordinal))) return false;
        return value.AsSpan(prefix ? 7 : 0).IndexOfAnyExcept("0123456789abcdef") < 0;
    }
}

public sealed record ProcessingSession(Uri Address, string Token)
{
    public override string ToString() => $"ProcessingSession {{ Address = {Address}, Token = [redacted] }}";
}

public interface IProcessingRuntime : IAsyncDisposable
{
    Task<ProcessingSession> StartAsync(CancellationToken cancellationToken = default);
    void ThrowIfUnhealthy();
    Task StopAsync(CancellationToken cancellationToken = default);
}
