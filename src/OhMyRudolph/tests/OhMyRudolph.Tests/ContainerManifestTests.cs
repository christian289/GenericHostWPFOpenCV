using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using OhMyRudolph.Core.Containers;

namespace OhMyRudolph.Tests;

public class ContainerManifestTests
{
    [Theory]
    [InlineData("../other.tar")]
    [InlineData("sub/other.tar")]
    [InlineData("C:\\other.tar")]
    public async Task Rejects_archive_paths_outside_manifest_directory(string archive)
    {
        using var directory = new TemporaryDirectory();
        var path = await WriteManifest(directory.Path, archive, new string('0', 64));
        await Assert.ThrowsAsync<InvalidDataException>(() => ProcessingImageManifest.LoadAndVerifyAsync(path));
    }

    [Fact]
    public async Task Rejects_archive_with_wrong_digest()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(System.IO.Path.Combine(directory.Path, "processing-image.oci.tar"), "image");
        var path = await WriteManifest(directory.Path, "processing-image.oci.tar", new string('0', 64));
        await Assert.ThrowsAsync<InvalidDataException>(() => ProcessingImageManifest.LoadAndVerifyAsync(path));
    }

    [Fact]
    public async Task Accepts_verified_archive()
    {
        using var directory = new TemporaryDirectory();
        byte[] bytes = [1, 2, 3];
        await File.WriteAllBytesAsync(System.IO.Path.Combine(directory.Path, "processing-image.oci.tar"), bytes);
        var path = await WriteManifest(directory.Path, "processing-image.oci.tar", Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var manifest = await ProcessingImageManifest.LoadAndVerifyAsync(path);
        Assert.Equal("sha256:" + new string('a', 64), manifest.ImageId);
    }

    [Theory]
    [InlineData("1.0.01")]
    [InlineData("1.0.0-01")]
    public async Task Rejects_invalid_or_mismatched_package_version(string version)
    {
        using var directory = new TemporaryDirectory();
        byte[] bytes = [1, 2, 3];
        await File.WriteAllBytesAsync(System.IO.Path.Combine(directory.Path, "processing-image.oci.tar"), bytes);
        var path = await WriteManifest(directory.Path, "processing-image.oci.tar", Convert.ToHexStringLower(SHA256.HashData(bytes)), version);
        await Assert.ThrowsAsync<InvalidDataException>(() => ProcessingImageManifest.LoadAndVerifyAsync(path));
    }

    [Fact]
    public async Task Rejects_a_valid_but_different_version()
    {
        using var directory = new TemporaryDirectory();
        byte[] bytes = [1, 2, 3];
        await File.WriteAllBytesAsync(System.IO.Path.Combine(directory.Path, "processing-image.oci.tar"), bytes);
        string current = typeof(ProcessingImageManifest).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        string different = current.Contains('-') ? current + ".mismatch" : current + "-mismatch";
        var path = await WriteManifest(directory.Path, "processing-image.oci.tar", Convert.ToHexStringLower(SHA256.HashData(bytes)), different);
        await Assert.ThrowsAsync<InvalidDataException>(() => ProcessingImageManifest.LoadAndVerifyAsync(path));
    }

    [Fact]
    public void Session_string_does_not_expose_token()
    {
        string token = new('a', 64);
        var session = new ProcessingSession(new Uri("http://127.0.0.1:1234/"), token);
        Assert.DoesNotContain(token, session.ToString());
    }

    private static async Task<string> WriteManifest(string directory, string archive, string hash, string? version = null)
    {
        string path = System.IO.Path.Combine(directory, "processing-image.json");
        version ??= typeof(ProcessingImageManifest).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        var data = new { schemaVersion = 1, applicationId = "OhMyRudolph", packageVersion = version, hostVersion = version, contractVersion = 1, abiVersion = 1, platform = "linux/amd64", imageId = "sha256:" + new string('a', 64), archiveFile = archive, archiveSha256 = hash, podmanMachine = "podman-machine-default", podmanConnection = "podman-machine-default", network = "pasta", containerPort = 8080 };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(data));
        return path;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
