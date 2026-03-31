using GRPCRemote.Input;
using GRPCRemote.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GRPCRemote.Tests;

public sealed class VirtualKeyServiceTests
{
    [Fact]
    public void Media_keys_are_not_supported_by_hvdK()
    {
        Assert.True(RemoteKeyMap.IsMediaKey(RemoteKey.KeyMediaPlayPause));
        Assert.True(RemoteKeyMap.IsMediaKey(RemoteKey.KeyMediaStop));
        Assert.True(RemoteKeyMap.IsMediaKey(RemoteKey.KeyMediaPrevTrack));
        Assert.True(RemoteKeyMap.IsMediaKey(RemoteKey.KeyMediaNextTrack));
        Assert.True(RemoteKeyMap.IsMediaKey(RemoteKey.KeyVolumeMute));
        Assert.True(RemoteKeyMap.IsMediaKey(RemoteKey.KeyVolumeUp));
        Assert.True(RemoteKeyMap.IsMediaKey(RemoteKey.KeyVolumeDown));
        Assert.True(RemoteKeyMap.IsMediaKey(RemoteKey.KeyBrowserBack));
        Assert.True(RemoteKeyMap.IsMediaKey(RemoteKey.KeyBrowserForward));
        Assert.True(RemoteKeyMap.IsMediaKey(RemoteKey.KeyBrowserRefresh));
    }

    [Fact]
    public void Get_media_key_vk_code_returns_correct_values()
    {
        Assert.Equal(0xB3, RemoteKeyMap.GetMediaKeyVkCode(RemoteKey.KeyMediaPlayPause));
        Assert.Equal(0xB2, RemoteKeyMap.GetMediaKeyVkCode(RemoteKey.KeyMediaStop));
        Assert.Equal(0xB1, RemoteKeyMap.GetMediaKeyVkCode(RemoteKey.KeyMediaPrevTrack));
        Assert.Equal(0xB0, RemoteKeyMap.GetMediaKeyVkCode(RemoteKey.KeyMediaNextTrack));
        Assert.Equal(0xAD, RemoteKeyMap.GetMediaKeyVkCode(RemoteKey.KeyVolumeMute));
        Assert.Equal(0xAF, RemoteKeyMap.GetMediaKeyVkCode(RemoteKey.KeyVolumeUp));
        Assert.Equal(0xAE, RemoteKeyMap.GetMediaKeyVkCode(RemoteKey.KeyVolumeDown));
        Assert.Equal(0xA6, RemoteKeyMap.GetMediaKeyVkCode(RemoteKey.KeyBrowserBack));
        Assert.Equal(0xA7, RemoteKeyMap.GetMediaKeyVkCode(RemoteKey.KeyBrowserForward));
        Assert.Equal(0xA8, RemoteKeyMap.GetMediaKeyVkCode(RemoteKey.KeyBrowserRefresh));
    }

    [Fact]
    public async Task Send_media_key_press_does_not_throw()
    {
        var service = new VirtualKeyService(NullLogger<VirtualKeyService>.Instance);

        await service.SendMediaKeyAsync(RemoteKey.KeyVolumeUp, RemoteActionType.Press, CancellationToken.None);
    }

    [Fact]
    public async Task Send_media_key_down_and_up_does_not_throw()
    {
        var service = new VirtualKeyService(NullLogger<VirtualKeyService>.Instance);

        await service.SendMediaKeyAsync(RemoteKey.KeyVolumeMute, RemoteActionType.Down, CancellationToken.None);
        await Task.Delay(50);
        await service.SendMediaKeyAsync(RemoteKey.KeyVolumeMute, RemoteActionType.Up, CancellationToken.None);
    }

    [Theory]
    [InlineData(RemoteKey.KeyMediaPlayPause)]
    [InlineData(RemoteKey.KeyMediaStop)]
    [InlineData(RemoteKey.KeyMediaPrevTrack)]
    [InlineData(RemoteKey.KeyMediaNextTrack)]
    [InlineData(RemoteKey.KeyVolumeMute)]
    [InlineData(RemoteKey.KeyVolumeUp)]
    [InlineData(RemoteKey.KeyVolumeDown)]
    [InlineData(RemoteKey.KeyBrowserBack)]
    [InlineData(RemoteKey.KeyBrowserForward)]
    [InlineData(RemoteKey.KeyBrowserRefresh)]
    public async Task All_media_keys_can_be_sent(RemoteKey key)
    {
        var service = new VirtualKeyService(NullLogger<VirtualKeyService>.Instance);

        await service.SendMediaKeyAsync(key, RemoteActionType.Press, CancellationToken.None);
    }

    [Fact]
    public async Task Holding_volume_up_repeats_until_released()
    {
        var events = new List<(ushort VkCode, uint Flags, DateTime Timestamp)>();
        var service = new VirtualKeyService(
            NullLogger<VirtualKeyService>.Instance,
            (vkCode, flags) =>
            {
                lock (events)
                {
                    events.Add((vkCode, flags, DateTime.UtcNow));
                }
            });

        await service.SendMediaKeyAsync(RemoteKey.KeyVolumeUp, RemoteActionType.Down, CancellationToken.None);
        await Task.Delay(420);

        List<(ushort VkCode, uint Flags, DateTime Timestamp)> beforeRelease;
        lock (events)
        {
            beforeRelease = events.ToList();
        }

        await service.SendMediaKeyAsync(RemoteKey.KeyVolumeUp, RemoteActionType.Up, CancellationToken.None);
        await Task.Delay(160);

        List<(ushort VkCode, uint Flags, DateTime Timestamp)> afterRelease;
        lock (events)
        {
            afterRelease = events.ToList();
        }

        Assert.True(beforeRelease.Count >= 4, $"Expected repeated events before release, got {beforeRelease.Count}.");
        Assert.Equal(beforeRelease.Count, afterRelease.Count);
        Assert.All(afterRelease, e => Assert.Equal((ushort)0xAF, e.VkCode));
        Assert.Equal(0u, afterRelease[0].Flags);
        Assert.Equal(0x0002u, afterRelease[1].Flags);
    }

    [Fact]
    public async Task Releasing_held_volume_key_stops_future_repeat_events()
    {
        var events = new List<(ushort VkCode, uint Flags, DateTime Timestamp)>();
        var service = new VirtualKeyService(
            NullLogger<VirtualKeyService>.Instance,
            (vkCode, flags) =>
            {
                lock (events)
                {
                    events.Add((vkCode, flags, DateTime.UtcNow));
                }
            });

        await service.SendMediaKeyAsync(RemoteKey.KeyVolumeDown, RemoteActionType.Down, CancellationToken.None);
        await Task.Delay(140);
        await service.SendMediaKeyAsync(RemoteKey.KeyVolumeDown, RemoteActionType.Up, CancellationToken.None);

        List<(ushort VkCode, uint Flags, DateTime Timestamp)> atRelease;
        lock (events)
        {
            atRelease = events.ToList();
        }

        await Task.Delay(220);

        List<(ushort VkCode, uint Flags, DateTime Timestamp)> finalEvents;
        lock (events)
        {
            finalEvents = events.ToList();
        }

        Assert.Equal(atRelease.Count, finalEvents.Count);
        Assert.Equal(2, finalEvents.Count);
        Assert.Collection(
            finalEvents,
            first =>
            {
                Assert.Equal((ushort)0xAE, first.VkCode);
                Assert.Equal(0u, first.Flags);
            },
            second =>
            {
                Assert.Equal((ushort)0xAE, second.VkCode);
                Assert.Equal(0x0002u, second.Flags);
            });
    }
}
