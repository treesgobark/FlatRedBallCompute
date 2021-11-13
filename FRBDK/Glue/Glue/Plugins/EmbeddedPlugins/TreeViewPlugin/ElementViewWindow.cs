﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using FlatRedBall.Glue.CodeGeneration;
using FlatRedBall.Glue.Controls;
using FlatRedBall.Glue.StandardTypes;
using FlatRedBall.Glue.VSHelpers.Projects;
using FlatRedBall.IO;
using FlatRedBall.Glue.IO;
using FlatRedBall.Glue.Parsing;
using Glue;
using System.Drawing;
using FlatRedBall.Glue.SaveClasses;
using System.IO;
using FlatRedBall.Utilities;
using FlatRedBall.Glue.Elements;
using FlatRedBall.Glue.VSHelpers;
using System.Diagnostics;
using FlatRedBall.Glue.Plugins;
using EditorObjects.Parsing;
using FlatRedBall.Glue.Events;
using FlatRedBall.Glue.Plugins.ExportedImplementations;
using FlatRedBall.Glue.Plugins.ExportedImplementations.CommandInterfaces;
using System.ComponentModel;
using FlatRedBall.Glue.GuiDisplay;
using FlatRedBall.Performance.Measurement;
using FlatRedBall.Glue.Navigation;
using FlatRedBall.Glue.AutomatedGlue;
using HQ.Util.Unmanaged;
using GlueFormsCore.Plugins.EmbeddedPlugins.ExplorerTabPlugin;

namespace FlatRedBall.Glue.FormHelpers
{
    public static partial class ElementViewWindow
    {
        #region Fields

        static List<string> mDirectoriesToIgnore = new List<string>();

        static TreeNode mEntityNode;
        static TreeNode mScreenNode;
        static TreeNode mGlobalContentNode;
        //static TreeNode mUnreferencedContent;

        static TreeView mTreeView;

        #region Screen Node Colors

        public static Color StartupScreenColor = Color.DarkRed;
        public static Color RequiredScreenColor = Color.DarkGreen;

        #endregion

        public static Color RegularBackgroundColor = Color.Black;
        public static Color MissingObjectColor = Color.Red;
        public static Color AutoGeneratedColor = Color.Red;

        public static Color StateCategoryColor = Color.Orange;
        public static Color FolderColor = Color.Orange;

        #region Object Node Colors

        public static Color SetByDerivedColor = Color.FromArgb(80, 100, 255);
        public static Color DefinedByBaseColor = Color.Yellow;
        public static Color LayerObjectColor = Color.GreenYellow;
        public static Color InstantiatedByBase = Color.Green;

        public static Color IsContainerColor = Color.Pink;

        public static Color DisabledColor = Color.Gray;

        #endregion


        #endregion

        #region Properties

        public static TreeNode EntitiesTreeNode
        {
            get { return mEntityNode; }
        }

        public static TreeNode ScreensTreeNode
        {
            get => mScreenNode;
        }

        public static IEnumerable<ScreenTreeNode> AllScreens
        {
            get
            {
                for (int i = 0; i < mScreenNode.Nodes.Count; i++)
                {
                    yield return (ScreenTreeNode)mScreenNode.Nodes[i];
                }
            }
        }

        public static IEnumerable<EntityTreeNode> AllEntities
        {
            get
            {
                foreach (EntityTreeNode entityTreeNode in mEntityNode.AllEntitiesIn())
                {
                    yield return entityTreeNode;
                }
            }
        }

        public static List<string> DirectoriesToIgnore
        {
            get { return mDirectoriesToIgnore; }
        }

        public static TreeNode GlobalContentFileNode
        {
            get { return mGlobalContentNode; }
        }

        public static TreeNode SelectedNodeOld
        {
            get
            {
                return MainExplorerPlugin.Self.ElementTreeView.SelectedNode;
            }
            set
            {
                MainExplorerPlugin.Self.ElementTreeView.SelectedNode = value;
            }
        }

        public static ITreeNode SelectedNode
        {
            get => TreeNodeWrapper.CreateOrNull(SelectedNodeOld);
            set 
            {
                SelectedNodeOld = ((TreeNodeWrapper)value)?.TreeNode;
            }
        }

        public static TreeNode TreeNodeDraggedOff
        {
            get;
            set;
        }

        public static bool SuppressSelectionEvents
        {
            get;
            set;
        }


        #endregion

        #region Methods

        public static void Initialize(TreeView treeView,  TreeNode entityNode, TreeNode screenNode, TreeNode globalContentNode)
        {
            mDirectoriesToIgnore.Add(".svn");
            mTreeView = treeView;
            mEntityNode = entityNode;

            
            mScreenNode = screenNode;

            
            mGlobalContentNode = globalContentNode;

            mEntityNode.SelectedImageKey = "master_entity.png";
            mEntityNode.ImageKey = "master_entity.png";

            mScreenNode.SelectedImageKey = "master_screen.png";
            mScreenNode.ImageKey = "master_screen.png";

            mGlobalContentNode.SelectedImageKey = "master_file.png";
            mGlobalContentNode.ImageKey = "master_file.png";
        }

        public static void SuspendLayout()
        {
            mTreeView.SuspendLayout();
        }

        public static void ResumeLayout()
        {
            mTreeView.ResumeLayout();
        }

        public static void AfterSelect()
        {
            // tree node click
            TreeNode node = SelectedNodeOld;

            GlueState.Self.CurrentTreeNode = TreeNodeWrapper.CreateOrNull(node);
            // Do this after taking the snapshot:
            // This should update to a plugin at some point....
            PropertyGridHelper.UpdateDisplayedPropertyGridProperties();

            bool wasFocused = mTreeView?.Focused == true;

            // ReactivelySetItemViewVisibility may add or remove controls, and as a result the
            // list view may lose focus. We dont' want that to happen so we will explicitly put
            // focus on the control:
            if(wasFocused)
            {
                mTreeView.Focus(); 
            }
        }

        // Vic asks - why is this in the ElementViewWindow? That's weird...
        public static void ShowAllElementVariablesInPropertyGrid()
        {
            var element = GlueState.Self.CurrentElement;

            PropertyGridDisplayer displayer = new PropertyGridDisplayer();
            displayer.Instance = element;
            displayer.ExcludeAllMembers();
            displayer.RefreshOnTimer = false;
            
            displayer.PropertyGrid = MainGlueWindow.Self.PropertyGrid;


            foreach (CustomVariable variable in element.CustomVariables)
            {
                Type type = variable.GetRuntimeType();
                if (type == null)
                {
                    type = typeof(string);
                }

                string name = variable.Name;
                object value = variable.DefaultValue;
                TypeConverter converter = variable.GetTypeConverter(element);


                displayer.IncludeMember(name, type,
                    delegate(object sender, MemberChangeArgs args)
                    {
                        element.GetCustomVariableRecursively(name).DefaultValue = args.Value;
                    }
                    ,
                    delegate()
                    {
                        return element.GetCustomVariableRecursively(name).DefaultValue;
                    },
                    converter);
            
            }
            MainGlueWindow.Self.PropertyGrid.SelectedObject = displayer;
        }

        [Obsolete("Use RefreshCommands.RefreshTreeNodeFor")]
        public static EntityTreeNode AddEntity(EntitySave entitySave)
        {
            EntityTreeNode treeNode = new EntityTreeNode(FileManager.RemovePath(entitySave.Name));

            treeNode.CodeFile = entitySave.Name + ".cs";
            bool succeeded = true;

            if (succeeded)
            {
                string containingDirectory = FileManager.MakeRelative(FileManager.GetDirectory(entitySave.Name));

                TreeNode treeNodeToAddTo;
                if (containingDirectory == "Entities/")
                {
                    treeNodeToAddTo = mEntityNode;
                }
                else
                {
                    string directory = containingDirectory.Substring("Entities/".Length);

                    treeNodeToAddTo = GlueState.Self.Find.TreeNodeForDirectoryOrEntityNode(
                        directory, mEntityNode);
                    if (treeNodeToAddTo == null && !string.IsNullOrEmpty(directory))
                    {
                        // If it's null that may mean the directory doesn't exist.  We should make it
                        string absoluteDirectory = ProjectManager.MakeAbsolute(containingDirectory);
                        if (!Directory.Exists(absoluteDirectory))
                        {
                            Directory.CreateDirectory(absoluteDirectory);

                        }
                        AddDirectoryNodes(FileManager.RelativeDirectory + "Entities/", mEntityNode);

                        // now try again
                        treeNodeToAddTo = GlueState.Self.Find.TreeNodeForDirectoryOrEntityNode(
                            directory, mEntityNode);
                    }
                }

                // Someone in the chat room got a crash on the Add call.  Not sure why
                // so adding these to help find out what's up.
                if (treeNodeToAddTo == null)
                {
                    throw new NullReferenceException("treeNodeToAddTo is null.  This is bad");
                }
                if (treeNode == null)
                {
                    throw new NullReferenceException("treeNode is null.  This is bad");
                }

                treeNodeToAddTo.Nodes.Add(treeNode);
                treeNodeToAddTo.Nodes.SortByTextConsideringDirectories();

                string generatedFile = entitySave.Name + ".Generated.cs";

                if (FileManager.FileExists(generatedFile))
                {
                    treeNode.GeneratedCodeFile = generatedFile;
                }

                SortEntities();

                treeNode.EntitySave = entitySave;
                treeNode.RefreshTreeNodes();

            }
            return treeNode;
        }


        public static void Invoke(Delegate method)
        {
            mTreeView.Invoke(method);
        }

        public static void RemoveEntity(EntitySave entityToRemove)
        {
            var directoryTreeNode = TreeNodeForDirectory(FileManager.GetDirectory(entityToRemove.Name));

            for (int i = 0; i < directoryTreeNode.Nodes.Count; i++)
            {
                var entityTreeNode = directoryTreeNode.Nodes[i];


                if (entityTreeNode.Tag == entityToRemove)
                {
                    directoryTreeNode.Nodes.RemoveAt(i);
                    break;
                }
            }
        }

        static TreeNode TreeNodeForDirectory(string containingDirectory)
        {
            bool isEntity = true;

            // Let's see if this thing is really an Entity


            string relativeToProject = FileManager.Standardize(containingDirectory).ToLower();

            if (FileManager.IsRelativeTo(relativeToProject, FileManager.RelativeDirectory))
            {
                relativeToProject = FileManager.MakeRelative(relativeToProject);
            }
            else if (ProjectManager.ContentProject != null)
            {
                relativeToProject = FileManager.MakeRelative(relativeToProject, ProjectManager.ContentProject.GetAbsoluteContentFolder());
            }

            if (relativeToProject.StartsWith("content/globalcontent") || relativeToProject.StartsWith("globalcontent")
                )
            {
                isEntity = false;
            }

            if (isEntity)
            {
                if (!FileManager.IsRelative(containingDirectory))
                {
                    containingDirectory = FileManager.MakeRelative(containingDirectory,
                        FileManager.RelativeDirectory + "Entities/");
                }

                return GlueState.Self.Find.TreeNodeForDirectoryOrEntityNode(containingDirectory, ElementViewWindow.EntitiesTreeNode);
            }
            else
            {
                string subdirectory = FileManager.RelativeDirectory;

                if (ProjectManager.ContentProject != null)
                {
                    subdirectory = ProjectManager.ContentProject.GetAbsoluteContentFolder();
                }
                subdirectory += "GlobalContent/";


                containingDirectory = FileManager.MakeRelative(containingDirectory, subdirectory);

                if (containingDirectory == "")
                {
                    return ElementViewWindow.GlobalContentFileNode;
                }
                else
                {

                    return GlueState.Self.Find.TreeNodeForDirectoryOrEntityNode(containingDirectory, ElementViewWindow.GlobalContentFileNode);
                }
            }
        }


        public static void RemoveScreen(ScreenSave screenToRemove)
        {
            for (int i = 0; i < mScreenNode.Nodes.Count; i++)
            {
                if ((mScreenNode.Nodes[i]).Tag == screenToRemove)
                {
                    mScreenNode.Nodes.RemoveAt(i);
                    break;
                }
            }

        }

        public static void UpdateNodeToListIndex(EntitySave entitySave)
        {

            var entityTreeNode = GlueState.Self.Find.EntityTreeNode(entitySave);

            var parentTreeNode = entityTreeNode.Parent;

            bool wasSelected = MainExplorerPlugin.Self.ElementTreeView.SelectedNode == entityTreeNode;

            parentTreeNode.SortByTextConsideringDirectories();

            if (wasSelected)
            {
                GlueState.Self.CurrentEntitySave = entitySave;
            }
        }

        public static void UpdateNodeToListIndex(ScreenSave screenSave)
        {
            ScreenTreeNode screenTreeNode = GlueState.Self.Find.ScreenTreeNode(screenSave);

            //////// Early Out////////////
            if(screenTreeNode == null)
            {
                return;
            }
            //////End Early Out///////////

            bool wasSelected = MainExplorerPlugin.Self.ElementTreeView.SelectedNode == screenTreeNode;

            int desiredIndex = ProjectManager.GlueProjectSave.Screens.IndexOf(screenSave);

            mScreenNode.Nodes.Remove(screenTreeNode);
            mScreenNode.Nodes.Insert(desiredIndex, screenTreeNode);

            if (wasSelected)
            {
                MainExplorerPlugin.Self.ElementTreeView.SelectedNode = screenTreeNode;
            }
        }

        [Obsolete("Use GlueCommands.Self.RefreshCommands.RefreshGlobalContent()")]
        public static void UpdateGlobalContentTreeNodes()
        {
            #region Loop through all referenced files.  Create a tree node if needed, or remove it from the project if the file doesn't exist.

            for (int i = 0; i < ProjectManager.GlueProjectSave.GlobalFiles.Count; i++)
            {
                ReferencedFileSave rfs = ProjectManager.GlueProjectSave.GlobalFiles[i];

                TreeNode nodeForFile = GetTreeNodeForGlobalContent(rfs, mGlobalContentNode);

                #region If there is no tree node for this file, make one

                if (nodeForFile == null)
                {
                    string fullFileName = ProjectManager.MakeAbsolute(rfs.Name, true);

                    if (FileManager.FileExists(fullFileName))
                    {
                        nodeForFile = new TreeNode(FileManager.RemovePath(rfs.Name));

                        nodeForFile.ImageKey = "file.png";
                        nodeForFile.SelectedImageKey = "file.png";

                        string absoluteRfs = ProjectManager.MakeAbsolute(rfs.Name, true);

                        TreeNode nodeToAddTo = TreeNodeForDirectory(FileManager.GetDirectory(absoluteRfs));

                        if (nodeToAddTo == null)
                        {
                            nodeToAddTo = GlobalContentFileNode;
                        }

                        nodeToAddTo.Nodes.Add(nodeForFile);

                        nodeToAddTo.Nodes.SortByTextConsideringDirectories();

                        nodeForFile.Tag = rfs;
                    }

                    else
                    {
                        ProjectManager.GlueProjectSave.GlobalFiles.RemoveAt(i);
                        // Do we want to do this?
                        // ProjectManager.GlueProjectSave.GlobalContentHasChanged = true;

                        i--;
                    }
                }

                #endregion

                #region else, there is already one

                else
                {
                    string textToSet = FileManager.RemovePath(rfs.Name);
                    if (nodeForFile.Text != textToSet)
                    {
                        nodeForFile.Text = textToSet;
                    }
                }

                #endregion
            }

            #endregion

            #region Do cleanup - remove tree nodes that exist but represent objects no longer in the project


            for (int i = mGlobalContentNode.Nodes.Count - 1; i > -1; i--)
            {
                TreeNode treeNode = mGlobalContentNode.Nodes[i];

                RemoveGlobalContentTreeNodesIfNecessary(treeNode);
            }

            #endregion
        }

        private static void RemoveGlobalContentTreeNodesIfNecessary(TreeNode treeNode)
        {
            if (treeNode.IsDirectoryNode())
            {
                string directory = treeNode.GetRelativePath();

                directory = ProjectManager.MakeAbsolute(directory, true);


                if (!Directory.Exists(directory))
                {
                    // The directory isn't here anymore, so kill it!
                    treeNode.Parent.Nodes.Remove(treeNode);

                }
                else
                {
                    // The directory is valid, but let's check subdirectories
                    for (int i = treeNode.Nodes.Count - 1; i > -1; i--)
                    {
                        RemoveGlobalContentTreeNodesIfNecessary(treeNode.Nodes[i]);
                    }
                }
            }
            else // assume content for now
            {

                ReferencedFileSave referencedFileSave = treeNode.Tag as ReferencedFileSave;

                if (!ProjectManager.GlueProjectSave.GlobalFiles.Contains(referencedFileSave))
                {
                    treeNode.Parent.Nodes.Remove(treeNode);
                }
                else
                {
                    // The RFS may be contained, but see if the file names match
                    string rfsName = FileManager.Standardize(referencedFileSave.Name, null, false).ToLower();
                    string treeNodeFile = FileManager.Standardize(treeNode.GetRelativePath(), null, false).ToLower();

                    // We first need to make sure that the file is part of GlobalContentFiles.
                    // If it is, then we may have tree node in the wrong folder, so let's get rid
                    // of it.  If it doesn't start with globalcontent/ then we shouldn't remove it here.
                    if (rfsName.StartsWith("globalcontent/") &&  rfsName != treeNodeFile)
                    {
                        treeNode.Parent.Nodes.Remove(treeNode);
                    }
                }
            }
        }

        public static TreeNode GetTreeNodeForGlobalContent(ReferencedFileSave rfs, TreeNode nodeToStartAt)
        {



            TreeNode containerTreeNode = nodeToStartAt;

            if (rfs.Name.ToLower().StartsWith("globalcontent/") && nodeToStartAt == mGlobalContentNode)
            {
                string directory = FileManager.GetDirectoryKeepRelative(rfs.Name);

                int globalContentConstLength = "globalcontent/".Length;

                if (directory.Length > globalContentConstLength)
                {

                    string directoryToLookFor = directory.Substring(globalContentConstLength, directory.Length - globalContentConstLength);

                    containerTreeNode = GlueState.Self.Find.TreeNodeForDirectoryOrEntityNode(directoryToLookFor, nodeToStartAt);
                }
            }


            if (rfs.Name.ToLower().StartsWith("content/globalcontent/") && nodeToStartAt == mGlobalContentNode)
            {
                string directory = FileManager.GetDirectoryKeepRelative(rfs.Name);

                int globalContentConstLength = "content/globalcontent/".Length;

                if (directory.Length > globalContentConstLength)
                {

                    string directoryToLookFor = directory.Substring(globalContentConstLength, directory.Length - globalContentConstLength);

                    containerTreeNode = GlueState.Self.Find.TreeNodeForDirectoryOrEntityNode(directoryToLookFor, nodeToStartAt);
                }
            }


            if (containerTreeNode != null)
            {
                for (int i = 0; i < containerTreeNode.Nodes.Count; i++)
                {
                    TreeNode subnode = containerTreeNode.Nodes[i];

                    if (subnode.Tag == rfs)
                    {
                        return subnode;
                    }
                    //else if (subnode.IsDirectoryNode())
                    //{
                    //    TreeNode foundNode = GetTreeNodeForGlobalContent(rfs, subnode);

                    //    if (foundNode != null)
                    //    {
                    //        return foundNode;
                    //    }
                    //}
                }
            }
            return null;
        }

        internal static void AddDirectoryNodes()
        {
            AddDirectoryNodes(FileManager.RelativeDirectory + "Entities/", mEntityNode);

            #region Add global content directories

            string contentDirectory = FileManager.RelativeDirectory;

            if (ProjectManager.ContentProject != null)
            {
                contentDirectory = ProjectManager.ContentProject.GetAbsoluteContentFolder();
            }

            AddDirectoryNodes(contentDirectory + "GlobalContent/", mGlobalContentNode);
            #endregion
        }

        internal static void AddDirectoryNodes(string parentDirectory, TreeNode parentTreeNode)
        {
            if(parentTreeNode == null)
            {
                throw new ArgumentNullException(nameof(parentTreeNode));
            }

            if (Directory.Exists(parentDirectory))
            {
                string[] directories = Directory.GetDirectories(parentDirectory);

                foreach (string directory in directories)
                {
                    string relativePath = FileManager.MakeRelative(directory, parentDirectory);

                    string nameOfNewNode = relativePath;

                    if (relativePath.Contains('/'))
                    {
                        nameOfNewNode = relativePath.Substring(0, relativePath.IndexOf('/'));
                    }

                    if (!mDirectoriesToIgnore.Contains(nameOfNewNode))
                    {

                        TreeNode treeNode = GlueState.Self.Find.TreeNodeForDirectoryOrEntityNode(relativePath, parentTreeNode);

                        if (treeNode == null)
                        {
                            treeNode = parentTreeNode.Nodes.Add(FileManager.RemovePath(directory));
                        }

                        treeNode.ImageKey = "folder.png";
                        treeNode.SelectedImageKey = "folder.png";

                        treeNode.ForeColor = ElementViewWindow.FolderColor;

                        AddDirectoryNodes(parentDirectory + relativePath + "/", treeNode);
                    }
                }

                // Now see if there are any directory tree nodes that don't have a matching directory

                // Let's make the directories lower case
                for (int i = 0; i < directories.Length; i++)
                {
                    directories[i] = FileManager.Standardize(directories[i]).ToLower();

                    if (!directories[i].EndsWith("/") && !directories[i].EndsWith("\\"))
                    {
                        directories[i] = directories[i] + "/";
                    }
                }

                bool isGlobalContent = parentTreeNode.Root().IsGlobalContentContainerNode();


                for (int i = parentTreeNode.Nodes.Count - 1; i > -1; i--)
                {
                    TreeNode treeNode = parentTreeNode.Nodes[i];

                    if (treeNode.IsDirectoryNode())
                    {

                        string directory = ProjectManager.MakeAbsolute(treeNode.GetRelativePath(), isGlobalContent);

                        directory = FileManager.Standardize(directory.ToLower());

                        if (!directories.Contains(directory))
                        {
                            parentTreeNode.Nodes.RemoveAt(i);
                        }
                    }
                }
            }
        }

        internal static void SortEntities()
        {
            mEntityNode.Nodes.SortByTextConsideringDirectories();
        }

        public static void ElementDoubleClicked()
        {
            TreeNode selectedNode = SelectedNodeOld;

            if (selectedNode != null)
            {
                string text = selectedNode.Text;

                var handled = PluginManager.TryHandleTreeNodeDoubleClicked(selectedNode);

                if(handled == false)
                {
                    #region Double-clicked a file
                    string extension = FileManager.GetExtension(text);
                
                    if (GlueState.Self.CurrentReferencedFileSave != null && !string.IsNullOrEmpty(extension))
                    {
                        HandleFileTreeNodeDoubleClick(text);
                        handled = true;
                    }

                    #endregion
                }

                if(!handled)
                {

                    #region Code

                    if (selectedNode.IsCodeNode())
                    {
                        var fileName = selectedNode.Text;

                        var absolute = GlueState.Self.CurrentGlueProjectDirectory + fileName;

                        if (System.IO.File.Exists(absolute))
                        {
                            var startInfo = new ProcessStartInfo();
                            startInfo.FileName = absolute;
                            startInfo.UseShellExecute = true;
                            System.Diagnostics.Process.Start(startInfo);
                        }
                        handled = true;
                    }

                    #endregion
                }
            }

        }

        private static void HandleFileTreeNodeDoubleClick(string text)
        {
            string textExtension = FileManager.GetExtension(text);
            string sourceExtension = null;

            if (GlueState.Self.CurrentReferencedFileSave != null && !string.IsNullOrEmpty(GlueState.Self.CurrentReferencedFileSave.SourceFile))
            {
                sourceExtension = FileManager.GetExtension(GlueState.Self.CurrentReferencedFileSave.SourceFile);
            }

            var effectiveExtension = sourceExtension ?? textExtension;


            string applicationSetInGlue = "";

            ReferencedFileSave currentReferencedFileSave = GlueState.Self.CurrentReferencedFileSave;
            string fileName;

            if (currentReferencedFileSave != null && currentReferencedFileSave.OpensWith != "<DEFAULT>")
            {
                applicationSetInGlue = currentReferencedFileSave.OpensWith;
            }
            else
            {
                applicationSetInGlue = EditorData.FileAssociationSettings.GetApplicationForExtension(effectiveExtension);
            }

            if (currentReferencedFileSave != null)
            {
                if (!string.IsNullOrEmpty(currentReferencedFileSave.SourceFile))
                {
                    fileName = 
                        ProjectManager.MakeAbsolute(ProjectManager.ContentDirectoryRelative + currentReferencedFileSave.SourceFile, true);
                }
                else
                {
                    fileName = ProjectManager.MakeAbsolute(ProjectManager.ContentDirectoryRelative + currentReferencedFileSave.Name);
                }
            }
            else
            {
                fileName = ProjectManager.MakeAbsolute(text);
            }

            if (string.IsNullOrEmpty(applicationSetInGlue) || applicationSetInGlue == "<DEFAULT>")
            {
                try
                {
                    var executable = WindowsFileAssociation.GetExecFileAssociatedToExtension(effectiveExtension);

                    if(string.IsNullOrEmpty(executable))
                    {
                        var message = $"Windows does not have an association for the extension {effectiveExtension}. You must set the " +
                            $"program to associate with this extension to open the file. Set the assocaition now?";

                        GlueCommands.Self.DialogCommands.ShowYesNoMessageBox(message, OpenProcess);
                    }
                    else
                    {
                        OpenProcess();
                    }

                    void OpenProcess()
                    {
                        var startInfo = new ProcessStartInfo();
                        startInfo.FileName = "\"" + fileName + "\"";
                        startInfo.UseShellExecute = true;

                        System.Diagnostics.Process.Start(startInfo);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show("Error opening " + fileName + "\nTry navigating to this file and opening it through explorer");


                }
            }
            else
            {
                bool applicationFound = true;
                try
                {
                    applicationSetInGlue = FileManager.Standardize(applicationSetInGlue);
                }
                catch
                {
                    applicationFound = false;
                }

                if (!System.IO.File.Exists(applicationSetInGlue) || applicationFound == false)
                {
                    string error = "Could not find the application\n\n" + applicationSetInGlue;

                    System.Windows.Forms.MessageBox.Show(error);
                }
                else
                {
                    MessageBox.Show("This functionality has been removed as of March 7, 2021. If you need it, please talk to Vic on Discord.");
                    //ProcessManager.OpenProcess(applicationSetInGlue, fileName);
                }
            }
        }

        public static BaseElementTreeNode GetTreeNodeFor(IElement element)
        { 
            if(element is ScreenSave screenSave)
            {
                return GetTreeNodeFor(screenSave);
            }
            else if(element is EntitySave entitySave)
            {
                return GetTreeNodeFor(entitySave);
            }
            else
            {
                return null;
            }
        }


        public static BaseElementTreeNode GetTreeNodeFor(ScreenSave screenSave) => 
            AllScreens.FirstOrDefault(item => item.Tag == screenSave);


        public static BaseElementTreeNode GetTreeNodeFor(EntitySave entitySave) =>
            AllEntities.FirstOrDefault(item => item.Tag == entitySave);


        public static TreeNode GetTreeNodeFor(NamedObjectSave nos)
        {
            var parent = nos.GetContainer();

            var parentTreeNode = GetTreeNodeFor(parent);

            return parentTreeNode?.GetTreeNodeFor(nos);
        }

        #endregion

        public static void UpdateChangedElements()
        {
            foreach (var element in from entitySave in ProjectManager.GlueProjectSave.Entities 
                                    where entitySave.HasChanged 
                                    select entitySave)
            {
                GlueCommands.Self.RefreshCommands.RefreshTreeNodeFor(element);
                GlueCommands.Self.GenerateCodeCommands.GenerateElementCode(element);
                element.HasChanged = false;
            }

            foreach (var element in from screenSave in ProjectManager.GlueProjectSave.Screens 
                                    where screenSave.HasChanged 
                                    select screenSave)
            {
                GlueCommands.Self.RefreshCommands.RefreshTreeNodeFor(element);
                GlueCommands.Self.GenerateCodeCommands.GenerateElementCode(element);
                element.HasChanged = false;
            }

            if (ProjectManager.GlueProjectSave.GlobalContentHasChanged)
            {
                GlueCommands.Self.RefreshCommands.RefreshGlobalContent();
                GlueCommands.Self.ProjectCommands.SaveProjects();

                GlobalContentCodeGenerator.UpdateLoadGlobalContentCode();
            }
        }
    }
}
