﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RenderingLibrary.Math.Geometry;
using InputLibrary;
using RenderingLibrary;
using Cursors = System.Windows.Forms.Cursors;
using WinCursor = System.Windows.Forms.Cursor;
using Microsoft.Xna.Framework;
using RenderingLibrary.Math;

namespace FlatRedBall.SpecializedXnaControls.RegionSelection
{
    #region ResizeSide Enum

    public enum ResizeSide
    {
        None = -1,
        TopLeft,
        Top,
        TopRight,
        Right,
        BottomRight,
        Bottom,
        BottomLeft,
        Left,
        Middle
    }

    #endregion

    #region FloatRectangle

    struct FloatRectangle
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

    }

    #endregion

    
    public class RectangleSelector
    {
        #region Fields

        float cursorScreenXPushed;
        float cursorScreenYPushed;
        FloatRectangle rectangleCoordinatesOnPush;



        SystemManagers managers;

        List<LineCircle> mHandles;

        bool mShowHandles = true;

        LineRectangle mLineRectangle;

        // We use a separate set of coordinates so that the line rectangle can snap
        // if using unit coordinates.
        FloatRectangle mCoordinates;

        ResizeSide mSideGrabbed = ResizeSide.None;

        bool mVisible = true;

        /// <summary>
        /// Whether to raise EndRegionChanged when a mouse is released (clicked).
        /// This is set to true whenever a drag event occurs, and it's set to false
        /// whenever the mouse is released.
        /// </summary>
        bool shouldRaiseEndRegionChanged;
        #endregion

        #region Properties

        public bool RoundToUnitCoordinates
        {
            get;
            set;
        }

        public int? SnappingGridSize
        {
            get;
            set;
        }

        public float Left
        {
            get 
            { 
                // We used to return the raw value, but I think we want to round it - if it's to use unit coordinates then it should probably always return them.

                return RoundIfNecessary( mCoordinates.X); 
            }
            set 
            {
                mCoordinates.X = value;

                mLineRectangle.X = RoundIfNecessary(value);

                UpdateHandles();
            }
        }

        public float Top
        {
            get 
            { 
                return RoundIfNecessary( mCoordinates.Y); 
            }
            set
            {
                mCoordinates.Y = value;
                mLineRectangle.Y = RoundIfNecessary( value );
                UpdateHandles();

            }
        }

        public float Bottom
        {
            get 
            { 
                return Top + Height; 
            }
        }

        public float Right
        {
            get 
            { 
                return Left + Width; 
            }
        }

        public float OldLeft
        {
            get;
            private set;
        }

        public float OldRight
        {
            get;
            private set;
        }

        public float OldTop
        {
            get;
            private set;
        }

        public float OldBottom
        {
            get;
            private set;
        }


        public float CenterX
        {
            get
            { 
                return Left + Width /2.0f;
            }
        }

        public float CenterY
        {
            get
            {
                return Top + Height / 2.0f;
            }
        }

        public float Width
        {
            get 
            { 
                return RoundIfNecessary( mCoordinates.Width); 
            }
            set 
            {
                mCoordinates.Width = value;
                mLineRectangle.Width = RoundIfNecessary( value );
                UpdateHandles();
            }
        }

        public float Height
        {
            get 
            { 
                return RoundIfNecessary( mCoordinates.Height); 
            }
            set 
            {
                mCoordinates.Height = value;
                mLineRectangle.Height = RoundIfNecessary( value );
                UpdateHandles();
            }
        }

        public bool ShowHandles
        {
            get { return mShowHandles; }
            set
            {
                mShowHandles = value;

                UpdateVisibility();
            }
        }

        public bool ShowMoveCursorWhenOver { get; set; } = true;

        public bool Visible
        {
            get
            {
                return mVisible;
            }
            set
            {
                mVisible = value;
                UpdateVisibility();
            }
        }

        float HandleSize
        {
            get;
            set;
        }

        public bool AllowMoveWithoutHandles
        {
            get;
            set;
        }

        /// <summary>
        /// If true, the Windows cursor will get set back to an arrow if not over this rectangle selector
        /// </summary>
        public bool ResetsCursorIfNotOver
        {
            get;
            set;
        }

        /// <summary>
        /// Whether the rectangle selector should automatically assign the Windows cursor in its activity.
        /// Simple projectxs should set true for this, but more complex projects may want to set this to false
        /// to handle cursor setting themselves (such as to handle modifiers for adiditonal cursor assignment).
        /// </summary>
        public bool AutoSetsCursor
        {
            get; set;
        } = true;

        public object Tag
        {
            get;
            set;
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised whenever the region changes. This can happen through keyboard input, or through mouse dragging.
        /// Note that this event will be raised frequently when dragging the mouse, so it should not be used to auto-save
        /// files. See EndRegionChanged for file saving.
        /// </summary>
        public event EventHandler RegionChanged;
        public event EventHandler EndRegionChanged;
        public event EventHandler Pushed;

        #endregion


        #region Methods

        public RectangleSelector(SystemManagers managers)
        {
            this.managers = managers;
            HandleSize = 4;
            ResetsCursorIfNotOver = true;
            mShowHandles = true;
            mHandles = new List<LineCircle>();
            mLineRectangle = new LineRectangle(managers);

            for (int i = 0; i < 8; i++)
            {
                LineCircle lineCircle = new LineCircle(managers);
                lineCircle.Radius = HandleSize;
                mHandles.Add(lineCircle);
            }

            Width = 34;
            Height = 34;
        }

        public bool HasCursorOver(Cursor cursor)
        {
            float worldX = cursor.GetWorldX(managers);
            float worldY = cursor.GetWorldY(managers);
            if (this.mLineRectangle.HasCursorOver(worldX, worldY))
            {
                return true;
            }

            if (mShowHandles)
            {
                foreach (var circle in mHandles)
                {
                    if (circle.HasCursorOver(worldX, worldY))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Vic asks - why does this take a managers argument when we also take one in the constructor?
        public void AddToManagers(SystemManagers managers)
        {
            this.managers = managers;
            mLineRectangle.Z = 1;
            managers.ShapeManager.Add(mLineRectangle);

            foreach (var circle in mHandles)
            {
                circle.Z = 1;
                managers.ShapeManager.Add(circle);
            }
        }

        public void RemoveFromManagers()
        {
            mLineRectangle.Z = 1;
            managers.ShapeManager.Remove(mLineRectangle);

            foreach (var circle in mHandles)
            {
                managers.ShapeManager.Remove(circle);
            }
        }

        public void UpdateHandles()
        {
            mHandles[0].X = Left;
            mHandles[0].Y = Top;

            mHandles[1].X = CenterX;
            mHandles[1].Y = Top;

            mHandles[2].X = Right;
            mHandles[2].Y = Top;

            mHandles[3].X = Right;
            mHandles[3].Y = CenterY;

            mHandles[4].X = Right;
            mHandles[4].Y = Bottom;

            mHandles[5].X = CenterX;
            mHandles[5].Y = Bottom;

            mHandles[6].X = Left;
            mHandles[6].Y = Bottom;

            mHandles[7].X = Left;
            mHandles[7].Y = CenterY;
        }

        public void Activity(Cursor cursor, Keyboard keyboard, System.Windows.Forms.Control container)
        {
            if(AutoSetsCursor)
            {
                WinCursor cursorToSet = GetCursorToSet(cursor);

                if (WinCursor.Current != cursorToSet && cursorToSet != null)
                {
                    WinCursor.Current = cursorToSet;
                    container.Cursor = cursorToSet;
                }
            }



            MouseActivity(cursor, container);
            
            KeyboardActivity(keyboard);

            // Resize even if the cursor isn't in the window - because these may have been made visible by clicking on some winforms UI and we want
            // the size to be properly set
            ResizeCircleActivity();

        }

        private void KeyboardActivity(Keyboard keyBoard)
        {
            if (this.Visible)
            {
                bool changed = false;

                // don't do this if CTRL is held - that's reserved for camera movement
                bool isCtrlHeld =
                    keyBoard.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) ||
                    keyBoard.KeyDown(Microsoft.Xna.Framework.Input.Keys.RightControl);

                if (!isCtrlHeld)
                {

                    if (keyBoard.KeyPushed(Microsoft.Xna.Framework.Input.Keys.Left)
                        ||
                        keyBoard.KeyPushed(Microsoft.Xna.Framework.Input.Keys.Right)
                        ||
                        keyBoard.KeyPushed(Microsoft.Xna.Framework.Input.Keys.Up)
                        ||
                        keyBoard.KeyPushed(Microsoft.Xna.Framework.Input.Keys.Down))
                    {
                        // record before any changes are made
                        RecordOldValues();
                    }


                    if (keyBoard.KeyPushed(Microsoft.Xna.Framework.Input.Keys.Left))
                    {
                        this.Left--;
                        // Width should take care of this
                        //this.Right--;
                        changed = true;
                    }
                    if (keyBoard.KeyPushed(Microsoft.Xna.Framework.Input.Keys.Right))
                    {
                        this.Left++;
                        // Width should take care of this
                        //this.Right--;
                        changed = true;
                    }
                    if (keyBoard.KeyPushed(Microsoft.Xna.Framework.Input.Keys.Up))
                    {
                        this.Top--;
                        changed = true;
                    }
                    if (keyBoard.KeyPushed(Microsoft.Xna.Framework.Input.Keys.Down))
                    {
                        this.Top++;
                        changed = true;
                    }

                    if (changed )
                    {
                        RegionChanged?.Invoke(this, null);
                        EndRegionChanged?.Invoke(this, null);
                    }
                }
            }
        }

        private void RecordOldValues()
        {
            OldLeft = Left;
            OldRight = Right;
            OldTop = Top;
            OldBottom = Bottom;
        }

        private void MouseActivity(Cursor cursor, System.Windows.Forms.Control container)
        {

            if (mVisible && cursor.IsInWindow)
            {
                PushActivity(cursor);

                DragActivity(cursor);

                ClickActivity(cursor);
            }
        }

        private void ResizeCircleActivity()
        {
            if (Visible && ShowHandles)
            {

                foreach (var handle in mHandles)
                {
                    handle.Radius = HandleSize / managers.Renderer.Camera.Zoom;
                }


            }
        }


        private float RoundIfNecessary(float value)
        {
            // This function is rounding, not snapping. Also, we don't want
            // to snap on the getters which uses this.
            //if(SnappingGridSize != null)
            //{
            //    var toReturn = MathFunctions.RoundFloat(value, SnappingGridSize.Value);
            //    return toReturn;
            //}
            //else 
            if (RoundToUnitCoordinates)
            {
                return MathFunctions.RoundToInt(value);
            }
            else
            {
                return value;
            }
        }


        private void ClickActivity(Cursor cursor)

        {
            if (cursor.PrimaryClick)
            {
                mSideGrabbed = ResizeSide.None;

                this.Left = RoundIfNecessary(this.Left);
                this.Top = RoundIfNecessary(this.Top);
                this.Width = RoundIfNecessary(this.Width);
                this.Height= RoundIfNecessary(this.Height);

                if(shouldRaiseEndRegionChanged)
                {
                    EndRegionChanged?.Invoke(this, null);
                    shouldRaiseEndRegionChanged = false;
                }

                UpdateHandles();
            }
        }

        private void DragActivity(Cursor cursor)
        {
            var currentCursorScreenX = cursor.X;
            var currentCursorScreenY = cursor.Y;

            if (cursor.PrimaryDown && 
                (cursor.XChange != 0 || cursor.YChange != 0) &&
                mSideGrabbed != ResizeSide.None)
            {
                var changeSincePushX = currentCursorScreenX - cursorScreenXPushed;
                var changeSincePushY = currentCursorScreenY - cursorScreenYPushed;

                RecordOldValues();

                GetMultipliersFromSideGrabbed(out float widthMultiplier, 
                    out float heightMultiplier, 
                    out float xMultiplier, 
                    out float yMultiplier);

                xMultiplier /= managers.Renderer.Camera.Zoom;
                yMultiplier /= managers.Renderer.Camera.Zoom;
                widthMultiplier /= managers.Renderer.Camera.Zoom;
                heightMultiplier /= managers.Renderer.Camera.Zoom;

                
                this.Left = rectangleCoordinatesOnPush.X + xMultiplier * changeSincePushX;
                this.Top = rectangleCoordinatesOnPush.Y + yMultiplier * changeSincePushY;
                this.Width = rectangleCoordinatesOnPush.Width + widthMultiplier * changeSincePushX;
                this.Height = rectangleCoordinatesOnPush.Height + heightMultiplier * changeSincePushY;

                if (SnappingGridSize != null)
                {
                    //MathFunctions.RoundFloat(value, SnappingGridSize.Value);
                    this.Left = MathFunctions.RoundFloat(this.Left, SnappingGridSize.Value);
                    this.Top = MathFunctions.RoundFloat(this.Top, SnappingGridSize.Value);
                    this.Width = MathFunctions.RoundFloat(this.Width, SnappingGridSize.Value);
                    this.Height = MathFunctions.RoundFloat(this.Height, SnappingGridSize.Value);
                }

                RegionChanged?.Invoke(this, null);
                shouldRaiseEndRegionChanged = true;

            }
        }

        private void GetMultipliersFromSideGrabbed(out float widthMultiplier, out float heightMultiplier, out float xMultiplier, out float yMultiplier)
        {
            widthMultiplier = 0;
            heightMultiplier = 0;
            xMultiplier = 0;
            yMultiplier = 0;

            if (mSideGrabbed != ResizeSide.None)
            {
                switch (mSideGrabbed)
                {
                    case ResizeSide.TopLeft:
                        widthMultiplier = -1;
                        xMultiplier = 1;
                        heightMultiplier = -1;
                        yMultiplier = 1;
                        break;
                    case ResizeSide.Top:

                        heightMultiplier = -1;
                        yMultiplier = 1;
                        break;
                    case ResizeSide.TopRight:

                        heightMultiplier = -1;
                        yMultiplier = 1;
                        widthMultiplier = 1;
                        break;
                    case ResizeSide.Right:
                        widthMultiplier = 1;
                        break;
                    case ResizeSide.BottomRight:
                        widthMultiplier = 1;
                        heightMultiplier = 1;
                        break;
                    case ResizeSide.Bottom:
                        heightMultiplier = 1;
                        break;
                    case ResizeSide.BottomLeft:
                        heightMultiplier = 1;
                        widthMultiplier = -1;
                        xMultiplier = 1;
                        break;
                    case ResizeSide.Left:

                        widthMultiplier = -1;
                        xMultiplier = 1;
                        break;
                    case ResizeSide.Middle:
                        xMultiplier = 1;
                        yMultiplier = 1;
                        widthMultiplier = 0;
                        heightMultiplier = 0;
                        break;
                }
            }
        }

        private bool PushActivity(Cursor cursor)
        {
            var didActivity = false;

            if (cursor.PrimaryPush)
            {
                cursorScreenXPushed = cursor.X;
                cursorScreenYPushed = cursor.Y;

                rectangleCoordinatesOnPush = mCoordinates;

                float worldX = cursor.GetWorldX(managers);
                float worldY = cursor.GetWorldY(managers);

                var sideOver = GetSideOver(
                    worldX,
                    worldY);


                mSideGrabbed = sideOver;

                if (mSideGrabbed != ResizeSide.None && this.Pushed != null)
                {
                    Pushed(this, null);
                    didActivity = true;
                }
            }
            return didActivity;
        }


        /// <summary>
        /// Returns the cursor to set, considering the width and height of the RectangleSelector, the positiion
        /// of the cursor relative to parts of the relative selector, and whether the relative selector should
        /// reset the cursor to the arrow if not over.
        /// </summary>
        /// <param name="sideGrabbed">The side that the user has grabbed for resizing.</param>
        /// <param name="cursor">The InputLibrary.Cursor.</param>
        /// <returns>The windows Cursor to set. If null, then this does not reset the cursor.</returns>
        public WinCursor GetCursorToSet(Cursor cursor)
        {

            System.Windows.Forms.Cursor cursorToSet = null;

            if (mVisible && cursor.IsInWindow)
            {

                float worldX = cursor.GetWorldX(managers);
                float worldY = cursor.GetWorldY(managers);

                var sideOver = GetSideOver(
                    worldX,
                    worldY);


                var sideToUse = sideOver;
                if (mSideGrabbed != ResizeSide.None)
                {
                    sideToUse = mSideGrabbed;
                }

                var flipHorizontal = Width < 0;
                var flipVertical = Height < 0;

                bool flipCorners = (flipHorizontal && !flipVertical) ||
                    (!flipHorizontal && flipVertical);



                if (sideToUse != ResizeSide.None)
                {
                    switch (sideToUse)
                    {
                        case ResizeSide.TopLeft:
                        case ResizeSide.BottomRight:

                            if (flipCorners)
                            {
                                cursorToSet = Cursors.SizeNESW;
                            }
                            else
                            {
                                cursorToSet = Cursors.SizeNWSE;
                            }
                            break;
                        case ResizeSide.TopRight:
                        case ResizeSide.BottomLeft:
                            if (flipCorners)
                            {
                                cursorToSet = Cursors.SizeNWSE;
                            }
                            else
                            {
                                cursorToSet = Cursors.SizeNESW;
                            }
                            break;
                        case ResizeSide.Top:
                        case ResizeSide.Bottom:
                            cursorToSet = Cursors.SizeNS;
                            break;
                        case ResizeSide.Left:
                        case ResizeSide.Right:
                            cursorToSet = Cursors.SizeWE;
                            break;
                        case ResizeSide.Middle:
                            if (ShowMoveCursorWhenOver)
                            {
                                cursorToSet = Cursors.SizeAll;
                            }
                            break;
                        case ResizeSide.None:

                            break;
                    }

                }

            }

            if (ResetsCursorIfNotOver && cursorToSet == null)
            {
                cursorToSet = Cursors.Arrow;
            }

            return cursorToSet;
        }

        public ResizeSide GetSideOver(float x, float y)
        {
            ResizeSide toReturn = ResizeSide.None;
            if (mShowHandles)
            {

                for (int i = 0; i < this.mHandles.Count; i++)
                {
                    if (mHandles[i].HasCursorOver(x, y))
                    {
                        toReturn = (ResizeSide)i;
                    }
                }
            }

            if (mShowHandles || AllowMoveWithoutHandles)
            {
                if (this.mLineRectangle.HasCursorOver(x, y))
                {
                    toReturn = ResizeSide.Middle;
                }
            }

            return toReturn;
        }


        private void UpdateVisibility()
        {
            mLineRectangle.Visible = mVisible;

            foreach (var handle in mHandles)
            {
                handle.Visible = mShowHandles && mVisible;
            }
        }

        #endregion
    }
}
