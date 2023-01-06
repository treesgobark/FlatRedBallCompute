using System.Runtime.Serialization;
using System.Text.Json.Serialization;

public record AsepriteSheetData(
    AseSheetFrame[] Frames,
    AseMetadata Meta
);

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

public record AseMetadata(
    string App,
    string Version,
    string Image,
    string Format,
    SheetSize Size,
    string Scale,
    AseFrameTag[] FrameTags,
    AseLayer[] Layers,
    object[] Slices
);

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
