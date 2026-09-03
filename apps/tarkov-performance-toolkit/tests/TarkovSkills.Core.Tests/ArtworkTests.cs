using System.Buffers.Binary;

namespace TarkovSkills.Core.Tests;

public sealed class ArtworkTests
{
    [Theory]
    [InlineData("StoreLogo.png", 50, 50)]
    [InlineData("Square44x44Logo.png", 44, 44)]
    [InlineData("Square150x150Logo.png", 150, 150)]
    [InlineData("Wide310x150Logo.png", 310, 150)]
    [InlineData("AppIcon.png", 256, 256)]
    public void StorePngHasExpectedDimensions(string name, int width, int height)
    {
        var bytes = File.ReadAllBytes(Asset(name));

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
        Assert.Equal(width, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(height, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
    }

    [Fact]
    public void ExecutableIconContainsAllRequiredSizes()
    {
        var bytes = File.ReadAllBytes(Asset("AppIcon.ico"));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)));

        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2));
        var sizes = Enumerable.Range(0, count)
            .Select(index => bytes[6 + (index * 16)] is 0 ? 256 : bytes[6 + (index * 16)])
            .ToArray();

        Assert.Equal([16, 20, 24, 32, 40, 48, 64, 128, 256], sizes);

        for (var index = 0; index < count; index++)
        {
            var entry = bytes.AsSpan(6 + (index * 16), 16);
            var length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entry[8..12]));
            var offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entry[12..16]));
            Assert.InRange(offset, 6 + (16 * count), bytes.Length - 8);
            Assert.InRange(length, 8, bytes.Length - offset);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[offset..(offset + 8)]);
        }
    }

    private static string Asset(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Assets", name);
}
