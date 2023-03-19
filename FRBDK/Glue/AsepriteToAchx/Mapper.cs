using AsepriteDotNet;
using AsepriteDotNet.Document;
using FlatRedBall;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Graphics;
using FlatRedBall.IO;
using System.Diagnostics;

namespace AsepriteToAchx;

public static class Mapper
{
    public static AnimationChainListSave Map(AsepriteSheetData sheet,
        bool normalizeAnimationTime,
        bool generateMirrors = true,
        bool applyAnimationDirection = true)
    {
        AnimationChainListSave chainList = new()
        {
            FileRelativeTextures = true,
            TimeMeasurementUnit = TimeMeasurementUnit.Second,
            CoordinateType = TextureCoordinateType.Pixel,
        };

        var tagGroups = sheet.Frames
            .GroupBy(f => f.TagName)
            .Select(g => (tagName: g.Key, frames: g.ToArray()));

        foreach (var tagFrames in tagGroups)
        {
            AnimationChainSave mappedChain = Map(tagFrames.frames,
                tagFrames.tagName,
                sheet.Meta.Image,
                normalizeChainDuration: normalizeAnimationTime);
            chainList.AnimationChains.Add(mappedChain);
        }

        // foreach (AseFrameTag aseFrameTag in sheet.Meta.FrameTags)
        // {
        //     AnimationChainSave mappedChain = Map(sheet.Frames,
        //         aseFrameTag.From,
        //         aseFrameTag.To,
        //         aseFrameTag.Name,
        //         sheet.Meta.Image);
        //     chainList.AnimationChains.Add(mappedChain);
        // }

        if (applyAnimationDirection)
        {
            ApplyAnimationDirectionToAllChains(chainList, sheet.Meta);
        }
        
        if (generateMirrors)
        {
            MirrorAllDirectionalChains(chainList);
        }

        return chainList;
    }

    public static AnimationChainListSave MapWithCollision(AsepriteSheetData sheet,
        AsepriteFile asepriteFile,
        bool normalizeAnimationTime,
        bool generateMirrors = true,
        bool applyAnimationDirection = true)
    {
        AnimationChainListSave chainList = Map(sheet, normalizeAnimationTime, false, false);
        CollisionImporter.SetFrameCollisionForChainList(chainList, sheet, asepriteFile);

        if (applyAnimationDirection)
        {
            ApplyAnimationDirectionToAllChains(chainList, sheet.Meta);
        }
            
        if (generateMirrors)
        {
            MirrorAllDirectionalChains(chainList);
        }

        return chainList;
    }

    private static void ApplyAnimationDirectionToAllChains(AnimationChainListSave chainList, AseMetadata jsonMetaData)
    {
        if (chainList.AnimationChains.Count > jsonMetaData.FrameTags.Length)
        {
            throw new ArgumentException("The number of chains must be less than the number of tags.");
        }

        foreach (AnimationChainSave chain in chainList.AnimationChains)
        {
            AseFrameTag tag = jsonMetaData.TagLookup[chain.Name];
            chain.Frames = ApplyAnimationDirection(chain.Frames, tag.Direction).ToList();
        }
    }

    private static AnimationChainSave Map(AseSheetFrame[] aseFrames,
        int startingIndex,
        int endingIndexInclusive,
        string chainName,
        string textureName,
        bool normalizeChainDuration,
        bool flipHorizontal = false,
        bool flipVertical = false)
    {
        AnimationChainSave chain = new()
        {
            Name = chainName,
        };

        float? frameDurationOverride = null;
        if (normalizeChainDuration)
        {
            frameDurationOverride = 1f / (endingIndexInclusive + 1 - startingIndex);
        }
        
        for (var index = startingIndex; index <= endingIndexInclusive; index++)
        {
            AseSheetFrame aseFrame = aseFrames[index];
            AnimationFrameSave mappedFrame = Map(aseFrame, textureName, flipHorizontal, flipVertical, frameDurationOverride);
            chain.Frames.Add(mappedFrame);
        }

        return chain;
    }

    private static AnimationChainSave Map(AseSheetFrame[] aseFrames,
        string chainName,
        string textureName,
        bool normalizeChainDuration,
        bool flipHorizontal = false,
        bool flipVertical = false)
    {
        AnimationChainSave chain = new()
        {
            Name = chainName,
        };

        float? frameDurationOverride = null;
        if (normalizeChainDuration)
        {
            frameDurationOverride = 1f / aseFrames.Length;
        }
        
        foreach (AseSheetFrame aseFrame in aseFrames)
        {
            AnimationFrameSave mappedFrame = Map(aseFrame, textureName, flipHorizontal, flipVertical, frameDurationOverride);
            chain.Frames.Add(mappedFrame);
        }

        return chain;
    }

    private static AnimationFrameSave Map(AseSheetFrame aseFrame,
        string textureName,
        bool flipHorizontal,
        bool flipVertical,
        float? frameDurationOverride = null)
    {
        int trimmedFromLeft = aseFrame.SpriteSourceSize.X;
        int trimmedFromRight = aseFrame.SourceSize.W - (aseFrame.SpriteSourceSize.X + aseFrame.SpriteSourceSize.W);
        int trimmedFromTop = aseFrame.SpriteSourceSize.Y;
        int trimmedFromBottom = aseFrame.SourceSize.H - (aseFrame.SpriteSourceSize.Y + aseFrame.SpriteSourceSize.H);
        AnimationFrameSave frbFrame = new()
        {
            FlipHorizontal = flipHorizontal,
            FlipVertical = flipVertical,
            TextureName = textureName,
            FrameLength = frameDurationOverride ?? (float)aseFrame.SpanDuration.TotalSeconds,
            LeftCoordinate = aseFrame.Frame.X,
            RightCoordinate = aseFrame.Frame.X + aseFrame.Frame.W,
            TopCoordinate = aseFrame.Frame.Y,
            BottomCoordinate = aseFrame.Frame.Y + aseFrame.Frame.H,
            RelativeX = (trimmedFromLeft - trimmedFromRight) / 2f,
            RelativeY = (trimmedFromBottom - trimmedFromTop) / 2f,
        };
        
        return frbFrame;
    }
    
    public static AsepriteSheetData Map(AnimationChainListSave chainList)
    {
        throw new NotImplementedException();
    }

    public static IEnumerable<T> ApplyAnimationDirection<T>(IEnumerable<T> frames, FrameDirection direction)
    {
        switch (direction)
        {
            case FrameDirection.Forward:
                return frames;
            
            case FrameDirection.Reverse:
                return frames.Reverse();
            
            case FrameDirection.PingPong:
                var newSheetFrames = frames
                    .SkipLast(1)
                    .Concat(frames.Reverse().SkipLast(1));
                return newSheetFrames;
            
            default: throw new ArgumentException("dafuq");
        }
    }

    private static void MirrorAllDirectionalChains(AnimationChainListSave chainList)
    {
        for (int i = 0; i < chainList.AnimationChains.Count; i++)
        {
            AnimationChainSave chain = chainList.AnimationChains[i];
            
            string newChainName = "";
            bool mirrorX = false;
            bool mirrorY = false;
        
            if (chain.Name.EndsWith("Left") || chain.Name.EndsWith("Right"))
            {
                newChainName = SwapEndings(chain.Name,"Left", "Right");
                mirrorX = true;
            }
        
            if (chain.Name.EndsWith("Top") || chain.Name.EndsWith("Bottom"))
            {
                newChainName = SwapEndings(chain.Name,"Top", "Bottom");
                mirrorY = true;
            }

            if (newChainName == "")
            {
                throw new ArgumentException("This needs to end in right or left");
            }

            var mirroredChain = GetMirroredChain(chain, newChainName, mirrorX, mirrorY);
            chainList.AnimationChains.Insert(i + 1, mirroredChain);
            i++;
        }
    }

    private static AnimationChainSave GetMirroredChain(AnimationChainSave chain, string newChainName, bool mirrorX, bool mirrorY)
    {
        if (!(mirrorX || mirrorY))
        {
            throw new InvalidOperationException("dafuq");
        }

        var newChain = FileManager.CloneObject(chain);
        newChain.Name = newChainName;
        foreach (var frame in newChain.Frames)
        {
            frame.FlipHorizontal = mirrorX ? !frame.FlipHorizontal : frame.FlipHorizontal;
            frame.FlipVertical = mirrorY ? !frame.FlipVertical : frame.FlipVertical;
            frame.RelativeX *= mirrorX ? -1 : 1;
            frame.RelativeY *= mirrorY ? -1 : 1;
            
            if (frame.ShapeCollectionSave is null)
            {
                continue;
            }
            
            foreach (var shape in frame.ShapeCollectionSave.AxisAlignedRectangleSaves)
            {
                shape.X *= mirrorX ? -1 : 1;
                shape.Y *= mirrorY ? -1 : 1;
            }
            foreach (var shape in frame.ShapeCollectionSave.CircleSaves)
            {
                shape.X *= mirrorX ? -1 : 1;
                shape.Y *= mirrorY ? -1 : 1;
            }
            foreach (var shape in frame.ShapeCollectionSave.PolygonSaves)
            {
                shape.X *= mirrorX ? -1 : 1;
                shape.Y *= mirrorY ? -1 : 1;
            }
            foreach (var shape in frame.ShapeCollectionSave.AxisAlignedCubeSaves)
            {
                shape.X *= mirrorX ? -1 : 1;
                shape.Y *= mirrorY ? -1 : 1;
            }
            foreach (var shape in frame.ShapeCollectionSave.SphereSaves)
            {
                shape.X *= mirrorX ? -1 : 1;
                shape.Y *= mirrorY ? -1 : 1;
            }
        }
        return newChain;
    }

    private static string SwapEndings(string input, string ending1, string otherEnding)
    {
        string newName = "";
        if (input.EndsWith(ending1))
        {
            newName = input.Remove(input.Length - ending1.Length) + otherEnding;
        }
        else if (input.EndsWith(otherEnding))
        {
            newName = input.Remove(input.Length - otherEnding.Length) + ending1;
        }
        return newName;
    }
}
