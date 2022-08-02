﻿using FlatRedBall.AnimationEditorForms.CommandsAndState;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlatRedBall.AnimationEditorForms.Managers
{
    public class ObjectFinder : Singleton<ObjectFinder>
    {
        public AnimationFrameSave GetAnimationFrameContaining(AxisAlignedRectangleSave rectangle)
        {
            foreach(var animationChain in ProjectManager.Self.AnimationChainListSave.AnimationChains)
            {
                foreach(var frame in animationChain.Frames)
                {
                    if(frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Contains(rectangle))
                    {
                        return frame;
                    }
                }
            }

            return null;
        }
    }
}