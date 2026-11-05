using Deskling.Core.Simulation;

namespace Deskling.Core.Tests.Simulation;

public class PetSizeSnapTests
{
    [Theory]
    [InlineData(1.0, 64)]
    [InlineData(1.25, 64)]
    [InlineData(1.5, 128)]
    [InlineData(1.75, 128)]
    [InlineData(2.0, 128)]
    [InlineData(3.0, 192)]
    public void Size_IsAWholeMultipleOfTheArt(double scale, double expected)
    {
        Assert.Equal(expected, PetConfig.Default.SizePxAt(scale));
    }

    [Fact]
    public void ZeroArtSize_TurnsSnappingOff()
    {
        var config = new PetConfig { ArtSizePx = 0 };
        Assert.Equal(80, config.SizePxAt(1.25), 6);
    }
}

public class FurniturePixelScaleTests
{
    [Theory]
    [InlineData(1.0, 1)]
    [InlineData(1.25, 1)]
    [InlineData(1.5, 2)]
    [InlineData(1.75, 2)]
    [InlineData(2.5, 2)]
    [InlineData(3.0, 3)]
    public void PixelArt_UsesAWholeScale(double scale, double expected)
    {
        Assert.Equal(expected, Deskling.Core.Furniture.FurnitureItem.PixelScaleFor(Deskling.Core.Furniture.DefaultFurniture.All[0], scale));
    }

    [Fact]
    public void SmoothArt_KeepsTheMonitorScale()
    {
        var smooth = Deskling.Core.Furniture.DefaultFurniture.All[0] with { Smooth = true };
        Assert.Equal(1.25, Deskling.Core.Furniture.FurnitureItem.PixelScaleFor(smooth, 1.25));
    }
}
