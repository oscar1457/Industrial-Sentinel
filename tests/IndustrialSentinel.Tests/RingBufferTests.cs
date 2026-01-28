using IndustrialSentinel.Core.Utilities;
using Xunit;

namespace IndustrialSentinel.Tests;

public class RingBufferTests
{
    [Fact]
    public void Snapshot_ReturnsInOrder()
    {
        var buffer = new RingBuffer<int>(3);
        buffer.Write(1);
        buffer.Write(2);
        buffer.Write(3);

        Assert.Equal(new[] { 1, 2, 3 }, buffer.Snapshot());
    }

    [Fact]
    public void Snapshot_WrapsCorrectly()
    {
        var buffer = new RingBuffer<int>(3);
        buffer.Write(1);
        buffer.Write(2);
        buffer.Write(3);
        buffer.Write(4);

        Assert.Equal(new[] { 2, 3, 4 }, buffer.Snapshot());
    }
}
