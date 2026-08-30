using CustomerSupport.Application.Interfaces;
using CustomerSupport.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using System.Net;
using System.Text;
using Xunit;

namespace CustomerSupport.Tests.Unit.Services;

public class DistributedCacheServiceTests
{
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<IConnectionMultiplexer> _multiplexerMock;
    private readonly Mock<ILogger<DistributedCacheService>> _loggerMock;
    private readonly IDistributedCacheService _service;

    public DistributedCacheServiceTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _multiplexerMock = new Mock<IConnectionMultiplexer>();
        _loggerMock = new Mock<ILogger<DistributedCacheService>>();
        _service = new DistributedCacheService(
            _cacheMock.Object,
            _multiplexerMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnDeserializedValue_WhenKeyExists()
    {
        var expected = new TestObject { Id = 1, Name = "Test" };
        var json = """{"Id":1,"Name":"Test"}""";
        _cacheMock.Setup(c => c.GetAsync("key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(json));

        var result = await _service.GetAsync<TestObject>("key");

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnDefault_WhenKeyMissing()
    {
        _cacheMock.Setup(c => c.GetAsync("key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var result = await _service.GetAsync<TestObject>("key");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ShouldStoreSerializedValue_WithAbsoluteExpiry()
    {
        var value = new TestObject { Id = 2, Name = "Item" };
        var expiry = TimeSpan.FromMinutes(5);

        await _service.SetAsync("key", value, expiry);

        _cacheMock.Verify(c => c.SetAsync(
            "key",
            It.Is<byte[]>(b => Encoding.UTF8.GetString(b).Contains("\"Id\":2")),
            It.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == expiry),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetSlidingAsync_ShouldStoreSerializedValue_WithSlidingExpiry()
    {
        var value = new TestObject { Id = 3, Name = "Slide" };
        var sliding = TimeSpan.FromMinutes(10);

        await _service.SetSlidingAsync("key", value, sliding);

        _cacheMock.Verify(c => c.SetAsync(
            "key",
            It.IsAny<byte[]>(),
            It.Is<DistributedCacheEntryOptions>(o => o.SlidingExpiration == sliding),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_ShouldCallRemoveAsync_OnCache()
    {
        await _service.RemoveAsync("key");

        _cacheMock.Verify(c => c.RemoveAsync("key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_ShouldCallRefreshAsync_OnCache()
    {
        await _service.RefreshAsync("key");

        _cacheMock.Verify(c => c.RefreshAsync("key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenKeyExists()
    {
        _cacheMock.Setup(c => c.GetAsync("key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("data"));

        var result = await _service.ExistsAsync("key");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenKeyMissing()
    {
        _cacheMock.Setup(c => c.GetAsync("key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var result = await _service.ExistsAsync("key");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrCreateAsync_ShouldReturnCachedValue_WhenKeyExists()
    {
        var cached = new TestObject { Id = 5, Name = "Cached" };
        _cacheMock.Setup(c => c.GetAsync("key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("""{"Id":5,"Name":"Cached"}"""));

        var factoryCalled = false;
        var result = await _service.GetOrCreateAsync<TestObject>("key", () =>
        {
            factoryCalled = true;
            return Task.FromResult(new TestObject { Id = 99, Name = "New" });
        });

        result.Should().BeEquivalentTo(cached);
        factoryCalled.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrCreateAsync_ShouldCreateAndCache_WhenKeyMissing()
    {
        _cacheMock.Setup(c => c.GetAsync("key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var created = new TestObject { Id = 6, Name = "Created" };
        var result = await _service.GetOrCreateAsync<TestObject>("key", () => Task.FromResult(created), TimeSpan.FromMinutes(2));

        result.Should().BeEquivalentTo(created);
        _cacheMock.Verify(c => c.SetAsync(
            "key",
            It.IsAny<byte[]>(),
            It.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(2)),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveByPrefixAsync_ShouldDeleteMatchingKeys()
    {
        var endpoint = new DnsEndPoint("localhost", 6379);
        var mockServer = new Mock<IServer>();
        var mockDb = new Mock<IDatabase>();

        _multiplexerMock.Setup(m => m.GetEndPoints(false)).Returns(new EndPoint[] { endpoint });
        _multiplexerMock.Setup(m => m.GetServer(endpoint)).Returns(mockServer.Object);
        _multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(mockDb.Object);

        mockServer.Setup(s => s.Keys(
            It.IsAny<int>(),
            It.Is<RedisValue>(v => v == (RedisValue)"prefix*"),
            It.IsAny<int>(),
            It.IsAny<long>(),
            It.IsAny<int>(),
            It.IsAny<CommandFlags>()))
            .Returns(new RedisKey[] { "prefix:1", "prefix:2" });

        mockDb.Setup(d => d.KeyDeleteAsync(
            It.Is<RedisKey[]>(keys => keys.Length == 2),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(2L);

        await _service.RemoveByPrefixAsync("prefix");

        mockDb.Verify(d => d.KeyDeleteAsync(
            It.Is<RedisKey[]>(keys => keys.Length == 2),
            It.IsAny<CommandFlags>()),
            Times.Once);
    }

    private class TestObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
