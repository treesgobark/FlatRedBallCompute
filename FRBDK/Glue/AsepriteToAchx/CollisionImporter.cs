using AsepriteDotNet;
using AsepriteDotNet.Common;
using AsepriteDotNet.Document;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using FlatRedBall.Content.Polygon;
using System.Text.RegularExpressions;

namespace AsepriteToAchx;


public class CollisionImporter
{
    private class CollisionCategory
    {
        public CollisionCategory(string name, ColorComponent colorComponent)
        {
            Name = name;
            ColorComponent = colorComponent;
        }

        public string Name { get; }
        public ColorComponent ColorComponent { get; }
    }

    private abstract class CollisionShapeCollection<T>
    {
        public required 
            
        public abstract List<T> GetShapeList();
    }

    private class RectangleCollisionCollection : CollisionShapeCollection<AxisAlignedRectangleSave>
    {
        public override List<AxisAlignedRectangleSave> GetShapeList()
        {
            throw new NotImplementedException();
        }
    }

    private class CircleCollisionCollection : CollisionShapeCollection<CircleSave>
    {
        public override List<CircleSave> GetShapeList()
        {
            throw new NotImplementedException();
        }
    }

    private class PolygonCollisionCollection : CollisionShapeCollection<PolygonSave>
    {
        public override List<PolygonSave> GetShapeList()
        {
            throw new NotImplementedException();
        }
    }

    private class FrameCollisionData
    {
        public RectangleCollisionCollection Rectangles { get; } = new();
        public CircleCollisionCollection Circles { get; } = new();
        public PolygonCollisionCollection Polygons { get; } = new();

        public void FillChainFrame(AnimationFrameSave frame)
        {
            frame.ShapeCollectionSave ??= new ShapeCollectionSave();
            
            frame.ShapeCollectionSave.AxisAlignedRectangleSaves = Rectangles.GetShapeList();
            frame.ShapeCollectionSave.CircleSaves = Circles.GetShapeList();
            frame.ShapeCollectionSave.PolygonSaves = Polygons.GetShapeList();
        }
    }
    
    private IReadOnlyList<CollisionCategory> Categories { get; }

    public CollisionImporter(params (string name, ColorComponent component)[] categories)
    {
        if (categories.Length > 4)
        {
            throw new ArgumentException("Collision importer only supports 4 categories.");
        }
        
        var names = categories.Select(tuple => tuple.name).ToArray();
        if (names.GroupBy(x => x).Any(g => g.Count() > 1))
        {
            throw new ArgumentException("Category names may not be duplicates.");
        }
        if (names.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Category names may not be null or whitespace.");
        }
        if (!names.All(IsAlphaNumeric))
        {
            throw new ArgumentException("Category names must be alphanumeric.");
        }
        
        var components = categories.Select(tuple => tuple.component).ToArray();
        if (components.GroupBy(x => x).Any(g => g.Count() > 1))
        {
            throw new ArgumentException("Category components may not be duplicates.");
        }
        if (!components.All(Enum.IsDefined))
        {
            throw new ArgumentException("You may only provide a defined component: R, G, B, or A.");
        }
        
        Categories = categories.Select(tuple => new CollisionCategory(tuple.name, tuple.component)).ToList();
    }

    public void SetFrameCollisionForChainList(AnimationChainListSave chainList, AsepriteSheetData sheetData, AsepriteFile asepriteFile)
    {
        var achFrames = chainList.AnimationChains.SelectMany(ch => ch.Frames).ToArray();
        var sheetFrames = sheetData.Frames;
        var asepriteFileFrames = asepriteFile.Frames.ToArray();
        
        SetMultipleFrameCollision(achFrames, sheetFrames, asepriteFileFrames);
    }

    private void SetMultipleFrameCollision(IReadOnlyList<AnimationFrameSave> achChain,
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

    private void SetFrameCollision(AnimationFrameSave achFrame, AseSheetFrame sheetFrame, Frame aseFileFrame)
    {
        var rectangleCel = aseFileFrame.Cels
            .FirstOrDefault(cel => cel is ImageCel && cel.Layer.Name.ToLower() == "rectangles") as ImageCel;
        var circleCel = aseFileFrame.Cels
            .FirstOrDefault(cel => cel is ImageCel && cel.Layer.Name.ToLower() == "circles") as ImageCel;
        var polygonCel = aseFileFrame.Cels
            .FirstOrDefault(cel => cel is ImageCel && cel.Layer.Name.ToLower() == "polygons") as ImageCel;

        if ((rectangleCel, circleCel, polygonCel) == (null, null, null))
        {
            achFrame.ShapeCollectionSave = null;
            return;
        }
        
        achFrame.ShapeCollectionSave = new ShapeCollectionSave();

        var rectanglePixels = GetRelevantPixels(rectangleCel);
        HandleRectangles(achFrame, sheetFrame, rectangleCel, rectanglePixels);
        var circlePixels = GetRelevantPixels(circleCel);
        HandleCircles(achFrame, sheetFrame, circleCel, circlePixels);
        var polygonPixels = GetRelevantPixels(polygonCel);
        HandlePolygons(achFrame, sheetFrame, polygonCel, polygonPixels);
    }

    private void HandleRectangles(AnimationFrameSave achFrame,
        AseSheetFrame sheetFrame,
        ImageCel? rectangleCel,
        Dictionary<string, List<Point>> relevantPixels)
    {
        if (rectangleCel is null)
        {
            return;
        }

        foreach (var kvp in relevantPixels)
        {
            
        }
        
        Rectangle rectangleCelBounds = new Rectangle
        {
            X = rectangleCel.Position.X - sheetFrame.SpriteSourceSize.X,
            Y = rectangleCel.Position.Y - sheetFrame.SpriteSourceSize.Y,
            Width = rectangleCel.Size.Width,
            Height = rectangleCel.Size.Height,
        };
        var achRectangle = Map(rectangleCelBounds, Color.FromRGBA(255, 0, 0, 255), achFrame, sheetFrame);
        achFrame.ShapeCollectionSave.AxisAlignedRectangleSaves.Add(achRectangle);
    }

    private (int left, int top, int right, int bottom) GetExtremesOfPointCloud(IEnumerable<Point> points)
    {
        (int left, int top, int right, int bottom) = (int.MinValue, int.MinValue, int.MaxValue, int.MaxValue);
        foreach (Point point in points)
        {
            left = point.X > left ? point.X : left;
            top = point.Y > top ? point.Y : top;
            right = point.X < right ? point.X : right;
            bottom = point.Y < bottom ? point.Y : bottom;
        }
        return (left, top, right, bottom);
    }

    private void HandleCircles(AnimationFrameSave achFrame,
        AseSheetFrame sheetFrame,
        ImageCel? circleCel,
        Dictionary<string, List<Point>> relevantPixels)
    {
        if (circleCel is null)
        {
            return;
        }
    }

    private void HandlePolygons(AnimationFrameSave achFrame,
        AseSheetFrame sheetFrame,
        ImageCel? polygonsCel,
        Dictionary<string, List<Point>> relevantPixels)
    {
        if (polygonsCel is null)
        {
            return;
        }
    }

    private void FillCategories()
    {
        
    }

    private byte GetColorValueForComponent(Color pixel, ColorComponent component)
    {
        return component switch
        {
            ColorComponent.R => pixel.R,
            ColorComponent.G => pixel.G,
            ColorComponent.B => pixel.B,
            ColorComponent.A => pixel.A,
            _ => throw new InvalidOperationException("dafuq"),
        };
    }

    private Point GetCelCoordsFromIndex(ImageCel cel, int index)
    {
        int x = index % cel.Size.Width;
        int y = index / cel.Size.Width;
        return new Point(x, y);
    }

    private static AxisAlignedRectangleSave Map(Rectangle rectangle, Color color, AnimationFrameSave achFrame, AseSheetFrame sheetFrame)
    {
        var center = GetCenter(rectangle);
        AxisAlignedRectangleSave achRect = new()
        {
            X = center.x - sheetFrame.SpriteSourceSize.W / 2f,
            Y = -center.y + sheetFrame.SpriteSourceSize.H / 2f,
            Z = 0,
            ScaleX = rectangle.Width / 2f,
            ScaleY = rectangle.Height / 2f,
            Alpha = color.A / 255f,
            Red = color.R / 255f,
            Green = color.G / 255f,
            Blue = color.B / 255f,
        };

        achRect.X = achFrame.FlipHorizontal ? -achRect.X : achRect.X;
        achRect.Y = achFrame.FlipVertical ? -achRect.Y : achRect.Y;

        return achRect;
    }

    private static (float x, float y) GetCenter(Rectangle rect)
    {
        float x = rect.X + rect.Width / (float)2;
        float y = rect.Y + rect.Height / (float)2;
        return (x, y);
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
    A,
}
