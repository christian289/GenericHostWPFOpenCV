using Microsoft.Extensions.Logging.Abstractions;
using OhMyRudolph.Core.Managers;

namespace OhMyRudolph.Tests;

public sealed class CameraManagerLifetimeTests
{
    [Fact]
    public async Task StopBeforeStartIsSafeAndDoesNotRequireAnyCamera()
    {
        await using var manager = new CameraManager(NullLogger<CameraManager>.Instance);

        await manager.CameraStopAsync();
        await manager.CameraStopAsync();

        Assert.Null(manager.SelectedCamera);
        Assert.Empty(manager.ActivatedCameras);
        Assert.Equal(CameraManagerState.Stopped, manager.State);
    }

    [Fact]
    public async Task DisposeBeforeStartAndRepeatedDisposeAreSafe()
    {
        var manager = new CameraManager(NullLogger<CameraManager>.Instance);

        await manager.DisposeAsync();
        await manager.DisposeAsync();
        await manager.CameraStopAsync();

        Assert.Null(manager.SelectedCamera);
        Assert.Empty(manager.ActivatedCameras);
        Assert.Equal(CameraManagerState.Disposed, manager.State);
    }

    [Fact]
    public async Task SwitchingWithoutAnyCameraDoesNotStartHardwareDiscovery()
    {
        await using var manager = new CameraManager(NullLogger<CameraManager>.Instance);

        Assert.False(await manager.ChangeCameraAsync());
        Assert.Null(manager.SelectedCamera);
        Assert.Empty(manager.ActivatedCameras);
    }
}
