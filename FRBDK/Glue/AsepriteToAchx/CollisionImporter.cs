using AsepriteDotNet;
using AsepriteDotNet.Common;
using AsepriteDotNet.Document;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using FlatRedBall.Content.Polygon;
using System.Text.RegularExpressions;

namespace AsepriteToAchx;


public static partial class CollisionImporter
{
    // public CollisionImporter((string name, ColorComponent component) category1,
    //     (string name, ColorComponent component) category2 = default,
    //     (string name, ColorComponent component) category3 = default)
    // {
    //     List<(string name, ColorComponent component)> categories = new() { category1 };
    //     if (category2 != default)
    //     {
    //         categories.Add(category2);
    //     }
    //     if (category3 != default)
    //     {
    //         categories.Add(category3);
    //     }
    //     
    //     switch (categories.Count)
    //     {
    //         case > 3:
    //             throw new ArgumentException("Collision importer only supports 3 categories.");
    //         case <= 0:
    //             throw new ArgumentException("Collision importer requires at least one category.");
    //     }
    //
    //     var names = categories.Select(tuple => tuple.name).ToArray();
    //     if (names.GroupBy(x => x).Any(g => g.Count() > 1))
    //     {
    //         throw new ArgumentException("Category names may not be duplicates.");
    //     }
    //     if (names.Any(string.IsNullOrWhiteSpace))
    //     {
    //         throw new ArgumentException("Category names may not be null or whitespace.");
    //     }
    //     if (!names.All(IsAlphaNumeric))
    //     {
    //         throw new ArgumentException("Category names must be alphanumeric.");
    //     }
    //     
    //     var components = categories.Select(tuple => tuple.component).ToArray();
    //     if (components.GroupBy(x => x).Any(g => g.Count() > 1))
    //     {
    //         throw new ArgumentException("Category components may not be duplicates.");
    //     }
    //     if (!components.All(Enum.IsDefined))
    //     {
    //         throw new ArgumentException("You may only provide a defined component: R, G, B, or A.");
    //     }
    //     
    //     Categories = categories.Select(tuple => new CollisionCategory(tuple.name, tuple.component)).ToList();
    // }

    public static void SetFrameCollisionForChainList(AnimationChainListSave chainList, AsepriteSheetData sheetData, AsepriteFile asepriteFile)
    {
        var achFrames = chainList.AnimationChains.SelectMany(ch => ch.Frames).ToArray();
        var sheetFrames = sheetData.Frames;
        var asepriteFileFrames = asepriteFile.Frames.ToArray();
        
        SetMultipleFrameCollision(achFrames, sheetFrames, asepriteFileFrames);
    }

    private static void SetMultipleFrameCollision(IReadOnlyList<AnimationFrameSave> achChain,
        IReadOnlyList<AseSheetFrame> sheetFrames,
        IReadOnlyList<Frame> aseFileFrames)
    {
        int arraySize = achChain.Count;
        
        if (sheetFrames.Count != arraySize || aseFileFrames.Count != arraySize)
        {
            throw new ArgumentException("Frame lists must have same size");
        }
        
        for (int i = 0; i < arraySize; i++)
        {
            SetFrameCollision(achChain[i], sheetFrames[i], aseFileFrames[i]);
        }
    }

    private static void SetFrameCollision(AnimationFrameSave achFrame, AseSheetFrame sheetFrame, Frame aseFileFrame)
    {
        var rectangleCels = aseFileFrame.Cels
            .Where(cel => cel is ImageCel && cel.Layer.UserData.Text?.ToLower() == "rectangles")
            .Select(cel => (ImageCel)cel)
            .ToList();
        var circleCels = aseFileFrame.Cels
            .Where(cel => cel is ImageCel && cel.Layer.UserData.Text?.ToLower() == "circles")
            .Select(cel => (ImageCel)cel)
            .ToList();
        var polygonCels = aseFileFrame.Cels
            .Where(cel => cel is ImageCel && cel.Layer.UserData.Text?.ToLower() == "polygons")
            .Select(cel => (ImageCel)cel)
            .ToList();

        if ((rectangleCels.Count, circleCels.Count, polygonCels.Count) == (0, 0, 0))
        {
            achFrame.ShapeCollectionSave = null;
            return;
        }
        
        achFrame.ShapeCollectionSave = new ShapeCollectionSave();

        FrameCollisionData data = new();
        
        data.Rectangles.FillRelevantPoints(rectangleCels);
        achFrame.ShapeCollectionSave.AxisAlignedRectangleSaves = data.Rectangles.GetShapeList(achFrame, sheetFrame);
        
        data.Circles.FillRelevantPoints(circleCels);
        achFrame.ShapeCollectionSave.CircleSaves = data.Circles.GetShapeList(achFrame, sheetFrame);
    }
    
    public static bool IsAlphaNumeric(string strToCheck)
    {
        Regex rg = new Regex(@"^[a-zA-Z0-9\s,]*$");
        return rg.IsMatch(strToCheck);
    }
}

public enum ColorComponent
{
    R,
    G,
    B,
}
