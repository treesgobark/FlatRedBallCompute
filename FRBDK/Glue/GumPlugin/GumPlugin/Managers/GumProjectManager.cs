﻿using FlatRedBall.Glue;
using FlatRedBall.Glue.Managers;
using FlatRedBall.Glue.Plugins.ExportedImplementations;
using FlatRedBall.Glue.SaveClasses;
using FlatRedBall.IO;
using GumPlugin.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GumPlugin.Managers
{
    public class GumProjectManager : Singleton<GumProjectManager>
    {
        public ReferencedFileSave GetRfsForGumProject()
        {
            var glueProject = GlueState.Self.CurrentGlueProject;

            if (glueProject != null)
            {

                return glueProject.GetAllReferencedFiles().FirstOrDefault
                 (item => FileManager.GetExtension(item.Name) == "gumx");
            }
            else
            {
                return null;
            }
        }

        bool GetIsGumProjectAlreadyInGlueProject()
        {
         
            return GetRfsForGumProject() != null;
        }

        public FilePath GetGumProjectFileName()
        {
            // Let's get all the available Screens:
            ReferencedFileSave gumxRfs = GumProjectManager.Self.GetRfsForGumProject();
            if (gumxRfs != null)
            {
                return GlueCommands.Self.GetAbsoluteFilePath(gumxRfs);
            }
            return null;
        }

        public void ReloadGumProject()
        {
            var gumProjectFileName = GetGumProjectFileName();
            if (gumProjectFileName != null)
            {
                string gumxDirectory = null;

                gumxDirectory = gumProjectFileName.GetDirectoryContainingThis().FullPath;

                FileReferenceTracker.Self.LoadGumxIfNecessaryFromDirectory(gumxDirectory, force:true);
            }
            else
            {
                // Set the project to null so we don't have code generation happening:
                AppState.Self.GumProjectSave = null;
            }
        }

        internal bool TryAddNewGumProject()
        {
            bool added = false;

            if (GlueState.Self.CurrentGlueProject == null)
            {
                MessageBox.Show("You must first create a Glue project before adding a Gum project");
            }

            else if (GetIsGumProjectAlreadyInGlueProject())
            {
                MessageBox.Show("A Gum project already exists");
            }
            else
            {
                string gumProjectDirectory = GlueState.Self.ContentDirectory + "GumProject/";
                EmbeddedResourceManager.Self.SaveEmptyProject(gumProjectDirectory);

                GlueState.Self.CurrentTreeNode = GlueState.Self.Find.GlobalContentTreeNode;

                bool userCancelled = false;

                // ignore changes while this is being added, because we don't want to add then remove files:


                //var rfs = FlatRedBall.Glue.FormHelpers.RightClickHelper.AddSingleFile(
                //    gumProjectDirectory + "GumProject.gumx", ref userCancelled);
                //var rfs = GlueCommands.Self.GluxCommands.AddSingleFileTo(gumProjectDirectory + "GumProject.gumx",
                //    "GumProject.gumx", null, null, false, null, null, null);
                var absoluteGumFile = gumProjectDirectory + "GumProject.gumx";
                var relativeFile = FileManager.MakeRelative(absoluteGumFile, GlueState.Self.ContentDirectory);

                var rfs = GlueCommands.Self.GluxCommands.AddReferencedFileToGlobalContent(
                    relativeFile,
                    false);
                SetGumxReferencedFileSaveDefaults(rfs);

                added = !userCancelled;

                if (added)
                {
                    GlueState.Self.CurrentReferencedFileSave = rfs;
                    ReloadGumProject();

                    GumPluginCommands.Self.UpdateGumToGlueResolution();

                }
            }

            return added;
        }

        /// <summary>
        /// Modifies the properties on the Gumx ReferencedFileSave to have the common
        /// defaults needed for most games.
        /// </summary>
        /// <param name="rfs">The gum project (.gumx) ReferencedFileSave.</param>
        public static void SetGumxReferencedFileSaveDefaults(ReferencedFileSave rfs)
        {
            TaskManager.Self.AddAsync(() =>
            {
                rfs.Properties.SetValue(
                    nameof(FileAdditionBehavior),
                    (int)FileAdditionBehavior.IncludeNoFiles);


                rfs.Properties.SetValue(
                    nameof(GumViewModel.AutoCreateGumScreens),
                    true);

                rfs.Properties.SetValue(
                    nameof(GumViewModel.ShowMouse),
                    true);

                rfs.Properties.SetValue(
                    nameof(GumViewModel.MakeGumInstancesPublic),
                    true);


                rfs.Properties.SetValue(
                    nameof(GumViewModel.IsMatchGameResolutionInGumChecked),
                    true);

                GlueCommands.Self.DoOnUiThread(() => GumPluginCommands.Self.RefreshGumViewModel());

                GlueCommands.Self.GluxCommands.SaveGlujFile(TaskExecutionPreference.AddOrMoveToEnd);

            }, nameof(SetGumxReferencedFileSaveDefaults));
        }
    }
}
