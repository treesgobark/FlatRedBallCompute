﻿#define SupportsEditMode
#define IncludeSetVariable
using GlueControl.Dtos;
using GlueControl.Editing;
using Microsoft.Xna.Framework;

using FlatRedBall;
using FlatRedBall.Graphics;
using FlatRedBall.Math.Collision;
using FlatRedBall.Math.Geometry;
using FlatRedBall.Screens;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using {ProjectNamespace};


namespace GlueControl
{
    static class CommandReceiver
    {
        #region Supporting Methods/Properties

        static System.Reflection.MethodInfo[] AllMethods;

        static CommandReceiver()
        {
            AllMethods = typeof(CommandReceiver).GetMethods(
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic)
                .ToArray();
        }

        public static string Receive(string message, Func<object, bool> runPredicate = null)
        {
            string dtoTypeName = null;
            string dtoSerialized = null;
            if (message.Contains(":"))
            {
                dtoSerialized = message.Substring(message.IndexOf(":") + 1);
                dtoTypeName = message.Substring(0, message.IndexOf(":"));
            }
            else
            {
                throw new Exception();
            }

            var matchingMethod =
                AllMethods
                .FirstOrDefault(item =>
                {
                    if (item.Name == nameof(HandleDto))
                    {
                        var parameters = item.GetParameters();
                        return parameters.Length == 1 && parameters[0].ParameterType.Name == dtoTypeName;
                    }
                    return false;
                });

            if (matchingMethod == null)
            {
                throw new InvalidOperationException(
                    $"Could not find a HandleDto method for type {dtoTypeName}");
            }

            var dtoType = matchingMethod.GetParameters()[0].ParameterType;

            var dto = JsonConvert.DeserializeObject(dtoSerialized, dtoType);

            if (runPredicate == null || runPredicate(dto))
            {
                var response = ReceiveDto(dto);

                if (response != null)
                {
                    return JsonConvert.SerializeObject(response);
                }
            }
            return null;
        }

        public static object ReceiveDto(object dto)
        {
            var type = dto.GetType();

            var method = AllMethods
                .FirstOrDefault(item =>
                {
                    if (item.Name == nameof(HandleDto))
                    {
                        var parameters = item.GetParameters();
                        return parameters.Length == 1 && parameters[0].ParameterType == type;
                    }
                    return false;
                });


            object toReturn = null;

            if (method != null)
            {
                toReturn = method.Invoke(null, new object[] { dto });
            }

            return toReturn;
        }

        private static bool GetIfMatchesCurrentScreen(string elementName, out System.Type ownerType, out Screen currentScreen)
        {
            var game1FullName = typeof(Game1).FullName;
            var topNamespace = game1FullName.Substring(0, game1FullName.IndexOf('.'));
            //var ownerTypeName = "WhateverNamespace." + elementName.Replace("\\", ".");
            var ownerTypeName = $"{topNamespace}.{elementName.Replace("\\", ".")}";

            ownerType = typeof(CommandReceiver).Assembly.GetType(ownerTypeName);
            currentScreen = ScreenManager.CurrentScreen;
            var currentScreenType = currentScreen.GetType();

            return currentScreenType == ownerType || ownerType?.IsAssignableFrom(currentScreenType) == true;
        }

        static Dictionary<string, Vector3> CameraPositions = new Dictionary<string, Vector3>();

        // todo - move this to some type manager
        public static bool DoTypesMatch(PositionedObject positionedObject, string qualifiedTypeName, Type possibleType = null)
        {
            if (possibleType == null)
            {
                possibleType = typeof(CommandReceiver).Assembly.GetType(qualifiedTypeName);
            }

            if (positionedObject.GetType() == possibleType)
            {
                return true;
            }
            else if (positionedObject is GlueControl.Runtime.DynamicEntity dynamicEntity)
            {
                return dynamicEntity.EditModeType == qualifiedTypeName;
            }
            else
            {
                // here we need to do reflection to get the EditModeType, but that's not implemented yet.
                // This is needed for inherited entities
                return false;
            }
        }

        #endregion

        #region Message Queue

        /// <summary>
        /// Stores all commands that have been sent from Glue to game 
        /// which should always be re-run.
        /// </summary>
        public static List<object> GlobalGlueToGameCommands = new List<object>();

        #endregion

        #region Set Variable

        private static GlueVariableSetDataResponse HandleDto(GlueVariableSetData dto)
        {
            GlueVariableSetDataResponse response = null;

            if (dto.AssignOrRecordOnly == AssignOrRecordOnly.Assign)
            {
                response = GlueControl.Editing.VariableAssignmentLogic.SetVariable(dto);
            }
            else
            {
                // If it's a record-only, then we'll always want to enqueue it
                // need to change the record only back to assign so future re-runs will assign
                dto.AssignOrRecordOnly = AssignOrRecordOnly.Assign;
            }

            GlobalGlueToGameCommands.Add(dto);

            return response;
        }

        #endregion

        #region Set Camera Position

        private static void HandleDto(SetCameraPositionDto dto)
        {
            Camera.Main.Position = dto.Position;
        }

        #endregion

        #region Select Object
        private static void HandleDto(SelectObjectDto selectObjectDto)
        {
            bool matchesCurrentScreen =
                GetIfMatchesCurrentScreen(selectObjectDto.ElementName, out System.Type ownerType, out Screen currentScreen);

            var elementNameGlue = selectObjectDto.ElementName;
            string ownerTypeName = GlueToGameElementName(elementNameGlue);
            ownerType = typeof(CommandReceiver).Assembly.GetType(ownerTypeName);

            bool isOwnerScreen = false;


            if (matchesCurrentScreen)
            {
                Editing.EditingManager.Self.Select(selectObjectDto.ObjectName);
                Editing.EditingManager.Self.ElementEditingMode = GlueControl.Editing.ElementEditingMode.EditingScreen;
                isOwnerScreen = true;
            }
            else
            {
                // it's a different screen. See if we can select that screen:
                CameraPositions[currentScreen.GetType().FullName] = Camera.Main.Position;

                bool selectedNewScreen = ownerType != null && typeof(Screen).IsAssignableFrom(ownerType);
                if (selectedNewScreen)
                {
#if SupportsEditMode

                    void BeforeCustomInitializeLogic(Screen newScreen)
                    {
                        GlueControlManager.Self.ReRunAllGlueToGameCommands();
                        ScreenManager.BeforeScreenCustomInitialize -= BeforeCustomInitializeLogic;
                    }

                    void AfterInitializeLogic(Screen screen)
                    {
                        // Select this even if it's null so the EditingManager deselects 
                        EditingManager.Self.Select(selectObjectDto.ObjectName);
                        screen.ScreenDestroy += HandleScreenDestroy;
                        if (CameraPositions.ContainsKey(screen.GetType().FullName))
                        {
                            Camera.Main.Position = CameraPositions[screen.GetType().FullName];
                        }
                        ScreenManager.ScreenLoaded -= AfterInitializeLogic;
                    }
                    FlatRedBall.Screens.ScreenManager.BeforeScreenCustomInitialize += BeforeCustomInitializeLogic;
                    ScreenManager.ScreenLoaded += AfterInitializeLogic;

                    ScreenManager.CurrentScreen.MoveToScreen(ownerType);

                    isOwnerScreen = true;
                    EditingManager.Self.ElementEditingMode = GlueControl.Editing.ElementEditingMode.EditingScreen;
#endif
                }
            }

            if (!isOwnerScreen)
            {
                var isEntity = typeof(PositionedObject).IsAssignableFrom(ownerType) ||
                    InstanceLogic.Self.CustomGlueElements.ContainsKey(ownerTypeName);

                if (isEntity)
                {
                    var isAlreadyViewingThisEntity = ScreenManager.CurrentScreen.GetType().Name == "EntityViewingScreen" &&
                        SpriteManager.ManagedPositionedObjects.Count > 0 &&
                        DoTypesMatch(SpriteManager.ManagedPositionedObjects[0], ownerTypeName, ownerType);

                    if (!isAlreadyViewingThisEntity)
                    {
#if SupportsEditMode
                        void BeforeCustomInitializeLogic(Screen newScreen)
                        {
                            GlueControlManager.Self.ReRunAllGlueToGameCommands();
                            ScreenManager.BeforeScreenCustomInitialize -= BeforeCustomInitializeLogic;
                        }

                        void AfterInitializeLogic(Screen newScreen)
                        {
                            newScreen.ScreenDestroy += HandleScreenDestroy;

                            FlatRedBall.Screens.ScreenManager.ScreenLoaded -= AfterInitializeLogic;
                        }

                        FlatRedBall.Screens.ScreenManager.ScreenLoaded += AfterInitializeLogic;
                        FlatRedBall.Screens.ScreenManager.BeforeScreenCustomInitialize += BeforeCustomInitializeLogic;


                        Screens.EntityViewingScreen.GameElementTypeToCreate = GlueToGameElementName(elementNameGlue);
                        Screens.EntityViewingScreen.InstanceToSelect = selectObjectDto.ObjectName;
                        ScreenManager.CurrentScreen.MoveToScreen(typeof(Screens.EntityViewingScreen));
#endif
                    }
                    else
                    {
                        EditingManager.Self.Select(selectObjectDto.ObjectName);
                    }
                }
            }
        }

        #endregion

        #region Rename

        static string topNamespace = null;
        public static string GlueToGameElementName(string elementName)
        {
            if (topNamespace == null)
            {
                var game1FullName = typeof(Game1).FullName;
                topNamespace = game1FullName.Substring(0, game1FullName.IndexOf('.'));
            }
            return $"{topNamespace}.{elementName.Replace("\\", ".")}";
        }

        public static string GameElementTypeToGlueElement(string gameType)
        {
            var strings = gameType.Split('.');

            return string.Join("\\", strings.Skip(1).ToArray());
        }

        #endregion

        #region Destroy Screen

        private static void HandleScreenDestroy()
        {
            GlueControl.InstanceLogic.Self.DestroyDynamicallyAddedInstances();
        }

        #endregion

        #region Remove (Destroy) NamedObjectSave

        private static RemoveObjectDtoResponse HandleDto(RemoveObjectDto removeObjectDto)
        {
            var response = InstanceLogic.Self.HandleDeleteInstanceCommandFromGlue(removeObjectDto);

            CommandReceiver.GlobalGlueToGameCommands.Add(removeObjectDto);

            return response;
        }

        #endregion

        #region Add Entity

        private static void HandleDto(CreateNewEntityDto createNewEntityDto)
        {
            var entitySave = createNewEntityDto.EntitySave;

            // convert the entity save name (which is the glue name) to a type name:
            string elementName = GlueToGameElementName(entitySave.Name);


            InstanceLogic.Self.CustomGlueElements[elementName] = entitySave;
        }

        #endregion

        #region Add Object

        private static AddObjectDtoResponse HandleDto(AddObjectDto dto)
        {
            AddObjectDtoResponse valueToReturn = new AddObjectDtoResponse();

            var createdObject =
                GlueControl.InstanceLogic.Self.HandleCreateInstanceCommandFromGlue(dto, GlobalGlueToGameCommands.Count, forcedItem: null);
            valueToReturn.WasObjectCreated = createdObject != null;

            // internally this decides what to add to, so we don't have to sort the DTOs
            //CommandReceiver.EnqueueToOwner(dto, dto.ElementNameGame);
            GlobalGlueToGameCommands.Add(dto);

            return valueToReturn;
        }

        #endregion

        #region Edit vs Play

        private static void HandleDto(SetEditMode setEditMode)
        {
            var value = setEditMode.IsInEditMode;
#if SupportsEditMode
            if (value != FlatRedBall.Screens.ScreenManager.IsInEditMode)
            {
                FlatRedBall.Screens.ScreenManager.IsInEditMode = value;
                FlatRedBall.Gui.GuiManager.Cursor.RequiresGameWindowInFocus = !value;

                if (value)
                {
                    FlatRedBallServices.Game.IsMouseVisible = true;
                }

                FlatRedBall.TileEntities.TileEntityInstantiator.CreationFunction =
                    InstanceLogic.Self.CreateEntity;

                RestartScreenRerunCommands(applyRestartVariables: true);
            }
#endif
        }

        #endregion

        #region Move to Container

        private static MoveObjectToContainerDtoResponse HandleDto(MoveObjectToContainerDto dto)
        {
            var toReturn = new MoveObjectToContainerDtoResponse();

            var matchesCurrentScreen = GetIfMatchesCurrentScreen(
                dto.ElementName, out System.Type ownerType, out Screen currentScreen);
            if (matchesCurrentScreen)
            {
                toReturn.WasObjectMoved = GlueControl.Editing.MoveObjectToContainerLogic.TryMoveObjectToContainer(
                    dto.ObjectName, dto.ContainerName, EditingManager.Self.ElementEditingMode);
            }
            else
            {
                // we don't know if it can be moved. We'll assume it can, and when that screen is loaded, it will re-run that and...if it 
                // fails, then I guess we'll figure out a way to communicate back to Glue that it needs to restart. Actually this may never
                // happen because moving objects is done in the current screen, but I gues it's technically a possibility so I'll leave this
                // comment here.
            }

            CommandReceiver.GlobalGlueToGameCommands.Add(dto);


            return toReturn;
        }

        #endregion

        #region Restart Screen

        private static void HandleDto(RestartScreenDto dto)
        {
            RestartScreenRerunCommands(applyRestartVariables: true);
        }

        private static void RestartScreenRerunCommands(bool applyRestartVariables)
        {
            var screen =
                FlatRedBall.Screens.ScreenManager.CurrentScreen;
            // user may go into edit mode after moving through a level and wouldn't want it to restart fully....or would they? What if they
            // want to change the Player start location. Need to think that through...

            // Vic says - We run all Glue commands before running custom initialize. The reason is - custom initialize
            // may make modifications to objects that are created by glue commands (such as assigning acceleration to objects
            // in a list), but it is unlikely that scripts will make modifications to objects created in CustomInitialize because
            // objects created in CustomInitialize cannot be modified by level editor.
            void BeforeCustomInitializeLogic(Screen newScreen)
            {
                GlueControlManager.Self.ReRunAllGlueToGameCommands();
                ScreenManager.BeforeScreenCustomInitialize -= BeforeCustomInitializeLogic;
            }

            void AfterInitializeLogic(Screen newScreen)
            {
                newScreen.ScreenDestroy += HandleScreenDestroy;

                if (CameraPositions.ContainsKey(newScreen.GetType().FullName))
                {
                    Camera.Main.Position = CameraPositions[newScreen.GetType().FullName];
                }

                FlatRedBall.Screens.ScreenManager.ScreenLoaded -= AfterInitializeLogic;
            }

            FlatRedBall.Screens.ScreenManager.BeforeScreenCustomInitialize += BeforeCustomInitializeLogic;
            FlatRedBall.Screens.ScreenManager.ScreenLoaded += AfterInitializeLogic;

            CameraPositions[screen.GetType().FullName] = Camera.Main.Position;


            screen?.RestartScreen(reloadContent: true, applyRestartVariables: applyRestartVariables);
        }

        #endregion

        private static void HandleDto(ReloadGlobalContentDto dto)
        {
            GlobalContent.Reload(GlobalContent.GetFile(dto.StrippedGlobalContentFileName));
        }

        private static void HandleDto(TogglePauseDto dto)
        {
            var screen = ScreenManager.CurrentScreen;

            if (screen.IsPaused)
            {
                screen.UnpauseThisScreen();
            }
            else
            {
                screen.PauseThisScreen();
            }
        }

        private static void HandleDto(AdvanceOneFrameDto dto)
        {
            var screen = ScreenManager.CurrentScreen;

            screen.UnpauseThisScreen();
            var delegateInstruction = new FlatRedBall.Instructions.DelegateInstruction(() =>
            {
                screen.PauseThisScreen();
            });
            delegateInstruction.TimeToExecute = FlatRedBall.TimeManager.CurrentTime + .001;

            FlatRedBall.Instructions.InstructionManager.Instructions.Add(delegateInstruction);
        }

        private static void HandleDto(SetSpeedDto dto)
        {
            FlatRedBall.TimeManager.TimeFactor = dto.SpeedPercentage / 100.0f;
        }
    }
}
