using AsepriteDotNet.Document;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using FlatRedBall.Content.Polygon;
using FlatRedBall.Math.Geometry;
using Microsoft.Xna.Framework;
using Color = AsepriteDotNet.Common.Color;
using Point = AsepriteDotNet.Common.Point;
using Rectangle = AsepriteDotNet.Common.Rectangle;

namespace AsepriteToAchx;

public class CollisionCategory
{
    public CollisionCategory(string name, ColorComponent colorComponent)
    {
        Name = name;
        ColorComponent = colorComponent;
    }

    public string Name { get; }
    public ColorComponent ColorComponent { get; }
}

public readonly record struct PartialColor(ColorComponent Component, byte Value)
{
    public Color GetColor()
    {
        return Component switch
        {
            ColorComponent.R => Color.FromRGBA(Value, 0, 0, 0),
            ColorComponent.G => Color.FromRGBA(0, Value, 0, 0),
            ColorComponent.B => Color.FromRGBA(0, 0, Value, 0),
            _ => throw new InvalidOperationException("dafuq"),
        };
    }
}

public abstract class CollisionShapeCollection<T>
{
    protected Dictionary<(string name, Color color), List<Point>> RelevantPoints { get; } = new();

    // public abstract void FillRelevantPoints(ImageCel cel, IReadOnlyList<CollisionCategory> categories);
    
    public virtual void FillRelevantPoints(IEnumerable<ImageCel> cels)
    {
        RelevantPoints.Clear();

        foreach (ImageCel cel in cels)
        {
            var celPixels = cel.Pixels;
            for (int i = 0; i < celPixels.Length; i++)
            {
                Color pixel = celPixels[i];
                byte alphaValue = pixel.A;
            
                if (alphaValue == 0)
                {
                    continue;
                }
            
                Point pixelCoords = GetCelCoordsFromIndex(cel, i);
            
                if (!RelevantPoints.ContainsKey((cel.Layer.Name, pixel)))
                {
                    RelevantPoints[(cel.Layer.Name, pixel)] = new List<Point>();
                }
                RelevantPoints[(cel.Layer.Name, pixel)].Add(pixelCoords);
            }
        }
    }
    
    public abstract List<T> GetShapeList(AnimationFrameSave achFrame, AseSheetFrame sheetFrame);

    public static (int left, int top, int right, int bottom) GetExtremesOfPointCloud(IEnumerable<Point> points)
    {
        (int left, int top, int right, int bottom) = (int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
        foreach (Point point in points)
        {
            left = point.X < left ? point.X : left;
            top = point.Y < top ? point.Y : top;
            right = point.X > right ? point.X : right;
            bottom = point.Y > bottom ? point.Y : bottom;
        }
        return (left, top, right, bottom);
    }

    public static AxisAlignedRectangleSave MapRectangle(string name, Rectangle rectangle, Color color, AnimationFrameSave achFrame, AseSheetFrame sheetFrame)
    {
        var center = GetCenter(rectangle);
        AxisAlignedRectangleSave achRect = new()
        {
            Name = name,
            X = center.X - sheetFrame.SpriteSourceSize.W / 2f + achFrame.RelativeX,
            Y = -center.Y + sheetFrame.SpriteSourceSize.H / 2f + achFrame.RelativeY,
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

    public static CircleSave MapCircle(string name,
        Vector2 center,
        float radius,
        Color color,
        AnimationFrameSave achFrame,
        AseSheetFrame sheetFrame)
    {
        CircleSave achRect = new()
        {
            Name = name,
            X = center.X - sheetFrame.SpriteSourceSize.W / 2f + achFrame.RelativeX,
            Y = -center.Y + sheetFrame.SpriteSourceSize.H / 2f + achFrame.RelativeY,
            Z = 0,
            Radius = radius,
            Alpha = color.A / 255f,
            Red = color.R / 255f,
            Green = color.G / 255f,
            Blue = color.B / 255f,
        };

        achRect.X = achFrame.FlipHorizontal ? -achRect.X : achRect.X;
        achRect.Y = achFrame.FlipVertical ? -achRect.Y : achRect.Y;

        return achRect;
    }

    public static (float X, float Y) GetCenter(Rectangle rect)
    {
        float x = rect.X + rect.Width / 2f;
        float y = rect.Y + rect.Height / 2f;
        return (x, y);
    }
    
    public static PartialColor GetPartialColor(Color pixel, ColorComponent component)
    {
        return component switch
        {
            ColorComponent.R => new PartialColor(component, pixel.R),
            ColorComponent.G => new PartialColor(component, pixel.G),
            ColorComponent.B => new PartialColor(component, pixel.B),
            _ => throw new InvalidOperationException("dafuq"),
        };
    }

    public static Point GetCelCoordsFromIndex(ImageCel cel, int index)
    {
        int x = cel.Position.X + index % cel.Size.Width;
        int y = cel.Position.Y + index / cel.Size.Width;
        return new Point(x, y);
    }
}

public class RectangleCollisionCollection : CollisionShapeCollection<AxisAlignedRectangleSave>
{
    public override List<AxisAlignedRectangleSave> GetShapeList(AnimationFrameSave achFrame, AseSheetFrame sheetFrame)
    {
        List<AxisAlignedRectangleSave> rectangleSaves = new();
        
        foreach (KeyValuePair<(string name, Color color), List<Point>> kvp in RelevantPoints)
        {
            (int left, int top, int right, int bottom) = GetExtremesOfPointCloud(kvp.Value);
            Rectangle celSpaceRectangle = new Rectangle
            {
                X = left - sheetFrame.SpriteSourceSize.X,
                Y = top - sheetFrame.SpriteSourceSize.Y,
                Width = right - left + 1,
                Height = bottom - top + 1,
            };
            
            var mappedRect = MapRectangle(kvp.Key.name, celSpaceRectangle, kvp.Key.color, achFrame, sheetFrame);
            rectangleSaves.Add(mappedRect);
        }
        
        return rectangleSaves;
    }
}

public class CircleCollisionCollection : CollisionShapeCollection<CircleSave>
{
    public override List<CircleSave> GetShapeList(AnimationFrameSave achFrame, AseSheetFrame sheetFrame)
    {
        List<CircleSave> rectangleSaves = new();
        
        foreach (KeyValuePair<(string name, Color color), List<Point>> kvp in RelevantPoints)
        {
            (int left, int top, int right, int bottom) = GetExtremesOfPointCloud(kvp.Value);

            Vector2 center = new()
            {
                X = (left + right + 1) / 2f - sheetFrame.SpriteSourceSize.X,
                Y = (top + bottom + 1) / 2f - sheetFrame.SpriteSourceSize.Y,
            };
            
            (float xRadius, float yRadius) = ((right - left + 1) / 2f, (bottom - top + 1) / 2f);
            float majorRadius = xRadius > yRadius ? xRadius : yRadius;
            
            var mappedRect = MapCircle(kvp.Key.name, center, majorRadius, kvp.Key.color, achFrame, sheetFrame);
            rectangleSaves.Add(mappedRect);
        }
        
        return rectangleSaves;
    }
}

public class PolygonCollisionCollection : CollisionShapeCollection<PolygonSave>
{
    public override List<PolygonSave> GetShapeList(AnimationFrameSave achFrame, AseSheetFrame sheetFrame)
    {
        throw new NotImplementedException();
    }
}

public class FrameCollisionData
{
    public RectangleCollisionCollection Rectangles { get; } = new();
    public CircleCollisionCollection Circles { get; } = new();
    public PolygonCollisionCollection Polygons { get; } = new();

    public void PopulateRelevantPoints()
    {
        
    }
    
    public void FillChainFrame(AnimationFrameSave frame, AseSheetFrame sheetFrame)
    {
        frame.ShapeCollectionSave ??= new ShapeCollectionSave();
        
        frame.ShapeCollectionSave.AxisAlignedRectangleSaves = Rectangles.GetShapeList(frame, sheetFrame);
        frame.ShapeCollectionSave.CircleSaves = Circles.GetShapeList(frame, sheetFrame);
        frame.ShapeCollectionSave.PolygonSaves = Polygons.GetShapeList(frame, sheetFrame);
    }
}