using System.Text.Json.Serialization;

namespace AsepriteToAchx;

public record AsepriteSheetData(
    AseSheetFrame[] Frames,
    AseMetadata Meta
    )
{
    public IEnumerable<AseSheetFrame> GetSheetsInTag(string tagName)
    {
        return Frames.Where(f => f.TagName == tagName)
            .OrderBy(f => f.TagIndex);
    }
}

public record AseSheetFrame(
    string Filename,
    AseFrameCoordinates Frame,
    bool Rotated,
    bool Trimmed,
    TrimmedSpriteSourceSize SpriteSourceSize,
    OriginalSpriteSourceSize SourceSize,
    int Duration
    )
{
    public TimeSpan SpanDuration => TimeSpan.FromMilliseconds(Duration);

    public string TagName
    {
        get
        {
            int nameLength = Filename.IndexOf(':');
            string result = Filename[..nameLength];
            return result;
        }
    }

    public int TagIndex
    {
        get
        {
            int separatorIndex = Filename.IndexOf(':');
            string indexString = Filename[(separatorIndex + 1)..];
            int result = int.Parse(indexString);
            return result;
        }
    }
}

public record AseFrameCoordinates(
    int X,
    int Y,
    int W,
    int H
);

public record TrimmedSpriteSourceSize(
    int X,
    int Y,
    int W,
    int H
);

public record OriginalSpriteSourceSize(
    int W,
    int H
);

public record AseMetadata
{
    public AseMetadata(string app,
        string version,
        string image,
        string format,
        SheetSize size,
        string scale,
        AseFrameTag[] frameTags,
        AseLayer[] layers,
        object[] slices)
    {
        App = app;
        Version = version;
        Image = image;
        Format = format;
        Size = size;
        Scale = scale;
        FrameTags = frameTags;
        Layers = layers;
        Slices = slices;

        foreach (AseFrameTag frameTag in frameTags)
        {
            TagLookup[frameTag.Name] = frameTag;
        }
    }

    public Dictionary<string, AseFrameTag> TagLookup { get; } = new();
    public string App { get; }
    public string Version { get; }
    public string Image { get; }
    public string Format { get; }
    public SheetSize Size { get; }
    public string Scale { get; }
    public AseFrameTag[] FrameTags { get; }
    public AseLayer[] Layers { get; }
    public object[] Slices { get; }

    public void Deconstruct(out string App,
        out string Version,
        out string Image,
        out string Format,
        out SheetSize Size,
        out string Scale,
        out AseFrameTag[] FrameTags,
        out AseLayer[] Layers,
        out object[] Slices)
    {
        App = this.App;
        Version = this.Version;
        Image = this.Image;
        Format = this.Format;
        Size = this.Size;
        Scale = this.Scale;
        FrameTags = this.FrameTags;
        Layers = this.Layers;
        Slices = this.Slices;
    }
}

public record SheetSize(
    int W,
    int H
);

public record AseFrameTag(
    string Name,
    int From,
    int To,
    FrameDirection Direction,
    string Color
    )
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FrameDirection Direction { get; } = Direction;
    public int FrameCount => To - From + 1;
}

public enum FrameDirection
{
    Forward,
    Reverse,
    PingPong,
}

public record AseLayer(
    string Name,
    int Opacity,
    string BlendMode
);
