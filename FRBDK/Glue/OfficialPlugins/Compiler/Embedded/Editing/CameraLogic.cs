﻿{CompilerDirectives}

using {ProjectNamespace};

using FlatRedBall;
using FlatRedBall.Gui;
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using FlatRedBall.Screens;

namespace GlueControl.Editing
{
    #region CameraStateForScreen
    class CameraStateForScreen
    {
        public Vector3 Position;
        public float OrthogonalHeight;
    }
    #endregion

    class CameraLogic
    {
        #region Fields/Properties
        static int[] zoomLevels = new int[]
        {
            1600,
            1200,
            1000,
            800 ,
            700 ,
            600 ,
            500 ,
            400 ,
            350 ,
            300 ,
            250 ,
            200 ,
            175 ,
            150 ,
            125 ,
            100 ,
            87  ,
            75  ,
            63  ,
            50  ,
            33  ,
            25
        };
        // store this as a decimal, because the zoom can be inbetween when switching from game to edit mode
        static decimal currentZoomLevelIndex;

        public static float CurrentZoomRatio => zoomLevels[(int)currentZoomLevelIndex] / 100.0f;

        public static float CameraXMovement { get; private set; }
        public static float CameraYMovement { get; private set; }

        public static Dictionary<string, CameraStateForScreen> CameraStates = new Dictionary<string, CameraStateForScreen>();


        #endregion

        static CameraLogic()
        {
            currentZoomLevelIndex = Array.IndexOf(zoomLevels, 100);
        }

        public static void DoCursorCameraControllingLogic()
        {
            var cursor = GuiManager.Cursor;
            var camera = Camera.Main;

            var xBefore = camera.X;
            var yBefore = camera.Y;

            if (cursor.MiddleDown)
            {
                camera.X -= cursor.WorldXChangeAt(0);
                camera.Y -= cursor.WorldYChangeAt(0);
            }

            if (cursor.PrimaryDown || cursor.SecondaryDown)
            {
                // If near the edges, move in that direction.
                DoMouseDownScrollingLogic(cursor);
            }

            DoMouseWheelZoomingLogic(cursor);

            CameraXMovement = camera.X - xBefore;
            CameraYMovement = camera.Y - yBefore;
        }

        private static void DoMouseWheelZoomingLogic(Cursor cursor)
        {
            if (cursor.ZVelocity < 0)
            {
                DoZoomPlus(zoomAroundCursorPosition: true);
            }
            if (cursor.ZVelocity > 0)
            {
                DoZoomMinus(zoomAroundCursorPosition: true);
            }
        }

        private static void DoMouseDownScrollingLogic(Cursor cursor)
        {
            Camera camera = Camera.Main;

            const float borderInPixels = 50;
            var MaxVelocity = Camera.Main.OrthogonalHeight / 3;

            var screenX = cursor.ScreenX - camera.LeftDestination;
            var screenY = cursor.ScreenY - camera.TopDestination;

            var screenWidthInPixels = camera.DestinationRectangle.Width;
            var screenHeightInPixels = camera.DestinationRectangle.Height;

            float xMovementRatio = 0;
            if (screenX < borderInPixels)
            {
                xMovementRatio = -1 * ((borderInPixels - screenX) / borderInPixels);
            }
            else if (screenX > screenWidthInPixels - borderInPixels)
            {
                xMovementRatio = (screenX - (screenWidthInPixels - borderInPixels)) / borderInPixels;
            }

            float yMovementRatio = 0;
            if (screenY < borderInPixels)
            {
                yMovementRatio = ((borderInPixels - screenY) / borderInPixels);
            }
            else if (screenY > screenHeightInPixels - borderInPixels)
            {
                yMovementRatio = -1 * (screenY - (screenHeightInPixels - borderInPixels)) / borderInPixels;
            }

            if (xMovementRatio != 0)
            {
                camera.X += xMovementRatio * MaxVelocity * TimeManager.SecondDifference;
            }
            if (yMovementRatio != 0)
            {
                camera.Y += yMovementRatio * MaxVelocity * TimeManager.SecondDifference;
            }
        }
        public static void UpdateCameraToZoomLevel(bool zoomAroundCursorPosition = true, bool forceTo100 = false)
        {
            var cursor = GuiManager.Cursor;
            var worldXBefore = cursor.WorldX;
            var worldYBefore = cursor.WorldY;

            var makeDefaultCamera100 = false;

            float zoomLevel = 100;

            if (makeDefaultCamera100)
            {
                // In this case, 100% means whatever is the default zoom for the game.
                zoomLevel =
                    forceTo100
                    ? 100 * CameraSetup.Data.Scale / 100.0f
                    : zoomLevels[(int)currentZoomLevelIndex] * CameraSetup.Data.Scale / 100.0f;
                Camera.Main.OrthogonalHeight = (CameraSetup.Data.Scale / 100.0f) * CameraSetup.Data.ResolutionHeight / (zoomLevel / 100.0f);
            }
            else
            {
                var divisor = forceTo100
                    ? 1
                    : zoomLevels[(int)currentZoomLevelIndex] / 100.0f;

                Camera.Main.OrthogonalHeight = Camera.Main.DestinationRectangle.Height / divisor;
            }
            Camera.Main.FixAspectRatioYConstant();


            if (global::RenderingLibrary.SystemManagers.Default != null)
            {
                var windowSizeRelativeToDefault = Camera.Main.DestinationRectangle.Height / (double)CameraSetup.Data.ResolutionHeight;
                windowSizeRelativeToDefault /= (CameraSetup.Data.Scale / 100.0f);

                global::RenderingLibrary.SystemManagers.Default.Renderer.Camera.Zoom = (float)windowSizeRelativeToDefault * zoomLevel / 100.0f;
                foreach (var layer in global::RenderingLibrary.SystemManagers.Default.Renderer.Layers)
                {
                    if (layer.LayerCameraSettings != null)
                    {
                        layer.LayerCameraSettings.Zoom = (float)windowSizeRelativeToDefault * zoomLevel / 100.0f;
                    }
                }
            }

            if (zoomAroundCursorPosition)
            {
                var worldXAfterZoom = cursor.WorldX;
                var worldYAfterZoom = cursor.WorldY;

                Camera.Main.X -= worldXAfterZoom - worldXBefore;
                Camera.Main.Y -= worldYAfterZoom - worldYBefore;
            }
        }


        internal static void DoHotkeyLogic()
        {
            const int movePerPush = 16;
            var keyboard = FlatRedBall.Input.InputManager.Keyboard;
            if (keyboard.IsCtrlDown)
            {
                if (keyboard.KeyTyped(Microsoft.Xna.Framework.Input.Keys.Up))
                {
                    Camera.Main.Y += movePerPush;
                }
                if (keyboard.KeyTyped(Microsoft.Xna.Framework.Input.Keys.Down))
                {
                    Camera.Main.Y -= movePerPush;
                }
                if (keyboard.KeyTyped(Microsoft.Xna.Framework.Input.Keys.Left))
                {
                    Camera.Main.X -= movePerPush;
                }
                if (keyboard.KeyTyped(Microsoft.Xna.Framework.Input.Keys.Right))
                {
                    Camera.Main.X += movePerPush;
                }
                if (keyboard.KeyPushed(Microsoft.Xna.Framework.Input.Keys.OemPlus))
                {
                    DoZoomMinus();
                }
                if (keyboard.KeyPushed(Microsoft.Xna.Framework.Input.Keys.OemMinus))
                {
                    DoZoomPlus();
                }
            }
        }

        public static void DoZoomMinus(bool zoomAroundCursorPosition = false)
        {
            currentZoomLevelIndex = Math.Max(currentZoomLevelIndex - 1, 0);
            UpdateCameraToZoomLevel(zoomAroundCursorPosition);
            PushZoomLevelToEditor();
        }

        public static void DoZoomPlus(bool zoomAroundCursorPosition = false)
        {
            currentZoomLevelIndex = Math.Min(currentZoomLevelIndex + 1, zoomLevels.Length - 1);
            UpdateCameraToZoomLevel(zoomAroundCursorPosition);
            PushZoomLevelToEditor();
        }

        public static void RecordCameraForCurrentScreen()
        {
            var currentScreen = ScreenManager.CurrentScreen;
            if (currentScreen != null)
            {
                var state = new CameraStateForScreen();
                state.Position = Camera.Main.Position;
                state.OrthogonalHeight = Camera.Main.OrthogonalHeight;

                CameraStates[currentScreen.GetType().FullName] = state;

            }
        }

        public static void UpdateCameraValuesToScreenSavedValues(Screen screen, bool setZoom = true)
        {
            if (CameraStates.ContainsKey(screen.GetType().FullName))
            {
                var camera = Camera.Main;
                var value = CameraStates[screen.GetType().FullName];
                camera.Position = value.Position;
                if (setZoom)
                {
                    camera.OrthogonalHeight = value.OrthogonalHeight;
                    camera.FixAspectRatioYConstant();

                    UpdateZoomIndexToCamera();

                }
            }
        }

        private static void UpdateZoomIndexToCamera()
        {
            int cameraZoom;

            var heightMulitplier = FlatRedBallServices.GraphicsOptions.ResolutionHeight / (float)Camera.Main.DestinationRectangle.Height;


            //if (Camera.Main.DestinationRectangle.Height == FlatRedBallServices.GraphicsOptions.ResolutionHeight)
            //{
            cameraZoom = FlatRedBall.Math.MathFunctions.RoundToInt(100 * Camera.Main.DestinationRectangle.Height / Camera.Main.OrthogonalHeight);
            //}
            //else
            //{
            //    cameraZoom = FlatRedBall.Math.MathFunctions.RoundToInt(100 * Camera.Main.DestinationRectangle.Width / Camera.Main.OrthogonalWidth);
            //}

            for (int i = 0; i < zoomLevels.Count(); i++)
            {
                if (cameraZoom >= zoomLevels[i])
                {
                    currentZoomLevelIndex = i;
                    break;
                }
                else
                {
                    // start high, go lower
                    if (i < zoomLevels.Length - 1)
                    {
                        var nextSmallerZoom = zoomLevels[i + 1];
                        if (cameraZoom < zoomLevels[i] && cameraZoom > nextSmallerZoom)
                        {
                            currentZoomLevelIndex = i + .5m;
                            break;
                        }
                    }
                    else
                    {
                        // we got to the last so set it!
                        currentZoomLevelIndex = i;
                    }
                }
            }
        }


        public static void PushZoomLevelToEditor()
        {
            var dto = new Dtos.CurrentDisplayInfoDto();
            dto.ZoomPercentage = zoomLevels[(int)currentZoomLevelIndex];
            dto.DestinationRectangleWidth = Camera.Main.DestinationRectangle.Width;
            dto.DestinationRectangleHeight = Camera.Main.DestinationRectangle.Height;
            dto.OrthogonalWidth = Camera.Main.OrthogonalWidth;
            dto.OrthogonalHeight = Camera.Main.OrthogonalHeight;
            GlueControlManager.Self.SendToGlue(dto);
        }
    }
}
