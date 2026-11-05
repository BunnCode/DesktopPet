using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core.Animation;
using DesktopPet.Core.Rendering;
using DesktopPet.Core.Traits;

namespace DesktopPet.App.SpriteSheet;

/// <summary>
/// Writes the built-in rabbit art as a ready-to-edit sprite sheet: one row per animation, a matching atlas.json and a
/// short README. Copy the files into the sprites folder and paint over them.
/// </summary>
internal static class SpriteTemplateExporter
{
    public const string ImageName = "template.png";

    private const string Readme =
        """
        DesktopPet sprite template
        ==========================

        template.png holds the built-in rabbit art, one row per animation (64x64 cells), and atlas.json says which row
        is which. To use your own art:

          1. Copy template.png and atlas.json into the "sprites" folder next to this one.
          2. Paint over the frames (keep the cell grid), or point atlas.json at your own images.
          3. Save. The pet reloads the art right away; problems are shown in a notification.

        Any animation you remove from atlas.json keeps the built-in art, so you can replace one at a time.

        Conventions
          - Draw facing right (or set "facesLeft": true), with the feet at the bottom centre of each cell.
          - climb, cling and push: the hands touch the wall at the side the art faces.
          - hang, shimmy: the hands grip the top edge of the cell.
          - sit_ledge: the seat is 9/32 of the cell height above the bottom; the legs hang below it.
          - Transparent pixels are click-through. Use fully transparent, not "almost" transparent, around the pet.
          - Painted (non-pixel) art looks better with "smooth": true.

        Format details: docs/SPRITES.md in the DesktopPet repository.
        """;

    /// <summary>Writes the template into <paramref name="folder"/> (created if needed).</summary>
    public static void Export(string folder, IReadOnlyList<AnimationDef> animations)
    {
        Directory.CreateDirectory(folder);
        const int cell = DefaultArt.FrameSize;
        var columns = animations.Max(a => a.FrameCount);
        var width = columns * cell;
        var height = animations.Count * cell;
        var pixels = new uint[width * height];

        for (var row = 0; row < animations.Count; row++)
        {
            var animation = animations[row];
            for (var frame = 0; frame < animation.FrameCount; frame++)
            {
                var art = DefaultArt.Frame(Characters.Rabbit, animation.Id, frame);
                for (var y = 0; y < cell; y++)
                {
                    Array.Copy(art, y * cell, pixels, (((row * cell) + y) * width) + (frame * cell), cell);
                }
            }
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * sizeof(uint));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(Path.Combine(folder, ImageName)))
        {
            encoder.Save(file);
        }

        File.WriteAllText(Path.Combine(folder, SpriteAtlas.AtlasFileName), SpriteSheetParser.TemplateJson(animations, ImageName, cell));
        File.WriteAllText(Path.Combine(folder, "README.txt"), Readme);
    }
}
