﻿using FlatRedBall.Glue.Elements;
using FlatRedBall.Glue.Plugins;
using FlatRedBall.Glue.Plugins.ExportedImplementations;
using FlatRedBall.Glue.Plugins.Interfaces;
using FlatRedBall.Glue.SaveClasses;
using FlatRedBall.Glue.ViewModels;
using GlueFormsCore.Plugins.EmbeddedPlugins.AddScreenPlugin;
using GlueFormsCore.ViewModels;
using OfficialPluginsCore.Wizard.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using WpfDataUi;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace OfficialPluginsCore.Wizard
{
    [Export(typeof(PluginBase))]
    public class MainWizardPlugin : PluginBase
    {
        public override string FriendlyName => "New Project Wizard";

        public override Version Version => new Version(1, 1);

        public override bool ShutDown(PluginShutDownReason shutDownReason)
        {
            return true;
        }

        public override void StartUp()
        {
            AddMenuItemTo("Start New Project Wizard", RunWizard, "Plugins");
        }

        private void RunWizard(object sender, EventArgs e)
        {
            var vm = new WizardData();

            Apply(vm);
        }

        private void Apply(WizardData vm)
        {
            ScreenSave gameScreen = null;
            NamedObjectSave solidCollisionNos = null;
            NamedObjectSave cloudCollisionNos = null;

            if(vm.AddGameScreen)
            {
                gameScreen = GlueCommands.Self.GluxCommands.ScreenCommands.AddScreen("GameScreen");

                if(vm.AddTiledMap)
                {
                    MainAddScreenPlugin.AddMapObject(gameScreen);
                }

                if(vm.AddSolidCollision)
                {
                    solidCollisionNos = MainAddScreenPlugin.AddCollision(gameScreen, "SolidCollision");
                }
                if(vm.AddCloudCollision)
                {
                    cloudCollisionNos = MainAddScreenPlugin.AddCollision(gameScreen, "CloudCollision");
                }
            }

            if(vm.AddPlayerEntity)
            {
                var addEntityVm = new AddEntityViewModel();
                addEntityVm.Name = "Player";
                // todo - ask what kind of collision the user wants...
                addEntityVm.IsAxisAlignedRectangleChecked = true;
                addEntityVm.IsICollidableChecked = true;

                var playerEntity = GlueCommands.Self.GluxCommands.EntityCommands.AddEntity(addEntityVm);

                // requires the current entity be set:
                GlueState.Self.CurrentElement = playerEntity;

                if(vm.PlayerControlType == GameType.Platformer)
                {
                    // mark as platformer
                    PluginManager.CallPluginMethod("Entity Input Movement Plugin", "MakeCurrentEntityPlatformer" );

                }
                else if(vm.PlayerControlType == GameType.Topdown)
                {
                    // mark as top down
                    PluginManager.CallPluginMethod("Entity Input Movement Plugin", "MakeCurrentEntityTopDown");
                }

                NamedObjectSave playerList = null;
                if(vm.AddGameScreen && vm.AddPlayerListToGameScreen)
                {
                    {
                        AddObjectViewModel addObjectViewModel = new AddObjectViewModel();
                        addObjectViewModel.ForcedElementToAddTo = gameScreen;
                        addObjectViewModel.SourceType = SourceType.FlatRedBallType;
                        addObjectViewModel.SelectedAti = AvailableAssetTypes.CommonAtis.PositionedObjectList;
                        addObjectViewModel.SourceClassGenericType = playerEntity.Name;
                        addObjectViewModel.ObjectName = $"{playerEntity.GetStrippedName()}List";

                        playerList = GlueCommands.Self.GluxCommands.AddNewNamedObjectTo(addObjectViewModel, gameScreen, null);
                    }

                    if(vm.AddPlayerToList)
                    {
                        AddObjectViewModel addPlayerVm = new AddObjectViewModel();

                        addPlayerVm.SourceType = SourceType.Entity;
                        addPlayerVm.SourceClassType = playerEntity.Name;
                        addPlayerVm.ObjectName = "Player1";

                        GlueCommands.Self.GluxCommands.AddNewNamedObjectTo(addPlayerVm, gameScreen, playerList);
                    }
                }

                if(vm.AddGameScreen && vm.AddPlayerListToGameScreen)
                {
                    if(vm.CollideAgainstSolidCollision)
                    {
                        PluginManager.ReactToCreateCollisionRelationshipsBetween(playerList, solidCollisionNos);

                        var nos = gameScreen.GetNamedObject("PlayerListVsSolidCollision");

                        // move is 1
                        // bounce is 2
                        // PlatformerSolid is 3
                        // PlatformerCloud is 4

                        if(vm.PlayerControlType == GameType.Platformer)
                        {
                            nos.Properties.SetValue("CollisionType", 3);
                            // todo - set the masses and elasticity here
                            nos.Properties.SetValue("FirstCollisionMass", 0.0f);
                            nos.Properties.SetValue("SecondCollisionMass", 1.0f);
                            nos.Properties.SetValue("CollisionElasticity", 0.0f);
                        }
                        else
                        { 
                            nos.Properties.SetValue("CollisionType", 2);

                        }

                    }
                    if(vm.CollideAgainstCloudCollision)
                    {
                        PluginManager.ReactToCreateCollisionRelationshipsBetween(playerList, cloudCollisionNos);

                        var nos = gameScreen.GetNamedObject("PlayerListVsCloudCollision");

                        if(vm.PlayerControlType == GameType.Platformer)
                        {
                            nos.Properties.SetValue("CollisionType", 4);
                        }
                    }
                }

            }

            if(vm.CreateLevels)
            {
                for(int i= 0; i < vm.NumberOfLevels; i++)
                {
                    var levelName = "Level" + (i + 1);

                    var levelScreen = GlueCommands.Self.GluxCommands.ScreenCommands.AddScreen(levelName);
                    levelScreen.BaseScreen = gameScreen.Name;
                    levelScreen.UpdateFromBaseType();
                    GlueState.Self.CurrentScreenSave = levelScreen;


                    if(i == 0)
                    {
                        GlueCommands.Self.GluxCommands.StartUpScreenName = levelScreen.Name;
                    }

                    if(vm.AddGameScreen && vm.AddTiledMap)
                    {
                        // add a regular TMX
                        var addNewFileVm = new AddNewFileViewModel();

                        var tmxAti =
                            AvailableAssetTypes.Self.GetAssetTypeFromExtension("tmx");
                        addNewFileVm.SelectedAssetTypeInfo = tmxAti;

                        addNewFileVm.ForcedType = tmxAti;
                        addNewFileVm.FileName = levelName + "Map";
                        GlueCommands.Self.GluxCommands.CreateNewFileAndReferencedFileSave(addNewFileVm);

                        var mapObject = levelScreen.NamedObjects.FirstOrDefault(item => item.InstanceName == "Map" && item.GetAssetTypeInfo().FriendlyName.StartsWith("LayeredTileMap"));
                        if (mapObject != null)
                        {
                            mapObject.SourceType = SourceType.File;
                            mapObject.SourceFile = "Screens/Level1/" +  levelName + "Map.tmx";
                            mapObject.SourceName = "Entire File (LayeredTileMap)";
                        }

                        void SelectTmxRfs()
                        {
                            GlueState.Self.CurrentReferencedFileSave = levelScreen.ReferencedFiles
                                .FirstOrDefault(Item => Item.GetAssetTypeInfo()?.Extension == "tmx");
                        }

                        if(vm.IncludStandardTilesetInLevels)
                        {
                            SelectTmxRfs();
                            PluginManager.CallPluginMethod("Tiled Plugin", "AddStandardTilesetOnCurrentFile");
                        }
                        if(vm.IncludeGameplayLayerInLevels)
                        {
                            SelectTmxRfs();
                            PluginManager.CallPluginMethod("Tiled Plugin", "AddGameplayLayerToCurrentFile");
                        }

                    }
                }
            }

            if(vm.AddGum)
            {
                if (vm.AddFlatRedBallForms)
                {
                    PluginManager.CallPluginMethod("Gum Plugin", "CreateGumProjectWithForms");
                }
                else
                {
                    PluginManager.CallPluginMethod("Gum Plugin", "CreateGumProjectNoForms");
                }
            }

            if(vm.AddCameraController && vm.AddGameScreen)
            {
                var addCameraControllerVm = new AddObjectViewModel();
                addCameraControllerVm.ForcedElementToAddTo = gameScreen;
                addCameraControllerVm.SourceType = SourceType.FlatRedBallType;
                addCameraControllerVm.SourceClassType = "FlatRedBall.Entities.CameraControllingEntity";
                addCameraControllerVm.ObjectName = "CameraControllingEntityInstance";

                var cameraNos = GlueCommands.Self.GluxCommands.AddNewNamedObjectTo(addCameraControllerVm, gameScreen, null);

                if(vm.FollowPlayersWithCamera && vm.AddPlayerListToGameScreen)
                {
                    cameraNos.SetVariableValue(nameof(FlatRedBall.Entities.CameraControllingEntity.Targets), "PlayerList");
                }
                if(vm.KeepCameraInMap && vm.AddTiledMap)
                {
                    cameraNos.SetVariableValue(nameof(FlatRedBall.Entities.CameraControllingEntity.Map), "Map");
                }
            }
        }
    }
}