using GRPCRemote.Input;
using GRPCRemote.Services;

namespace GRPCRemote.Tests;

public sealed class VirtualKeyServiceTests
{
    [Fact]
    public async Task Media_keys_are_not_supported_by_hvdK()
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
        var service = new VirtualKeyService();

        await service.SendMediaKeyAsync(RemoteKey.KeyVolumeUp, RemoteActionType.Press, CancellationToken.None);
    }

    [Fact]
    public async Task Send_media_key_down_and_up_does_not_throw()
    {
        var service = new VirtualKeyService();

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
        var service = new VirtualKeyService();

        await service.SendMediaKeyAsync(key, RemoteActionType.Press, CancellationToken.None);
    }
}
