﻿using EditorObjects.IoC;
using EditorObjects.Parsing;
using FlatRedBall.Glue.Elements;
using FlatRedBall.Glue.Errors;
using FlatRedBall.Glue.IO;
using FlatRedBall.Glue.Managers;
using FlatRedBall.Glue.Plugins.ExportedInterfaces;
using FlatRedBall.Glue.Plugins.ExportedInterfaces.CommandInterfaces;
using FlatRedBall.Glue.SaveClasses;
using FlatRedBall.Glue.SetVariable;
using FlatRedBall.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FlatRedBall.Glue.Plugins.ExportedImplementations.CommandInterfaces
{
    #region Enums

    enum ProjectOrDisk
    {
        Project,
        Disk
    }

    #endregion

    class FileCommands : IFileCommands
    {

        IGlueState GlueState => Container.Get<IGlueState>();
        IGlueCommands GlueCommands => Container.Get<IGlueCommands>();

        GlueProjectSave GlueProject
        {
            get
            {
                return GlueState.CurrentGlueProject;
            }
        }

        public IEnumerable<string> GetFilesReferencedBy(ReferencedFileSave file, EditorObjects.Parsing.TopLevelOrRecursive topLevelOrRecursive)
        {
            var absolute = GlueCommands.GetAbsoluteFileName(file);

            return GetFilesReferencedBy(absolute, topLevelOrRecursive);
        }

        public IEnumerable<FilePath> GetFilePathsReferencedBy(ReferencedFileSave file, EditorObjects.Parsing.TopLevelOrRecursive topLevelOrRecursive)
        {
            var absolute = GlueCommands.GetAbsoluteFileName(file);

            return GetFilesReferencedBy(absolute, topLevelOrRecursive).Select(item => new FilePath(item));
        }

        public IEnumerable<string> GetFilesReferencedBy(string absoluteName, EditorObjects.Parsing.TopLevelOrRecursive topLevelOrRecursive)
        {
            return FileReferenceManager.Self.GetFilesReferencedBy(absoluteName, topLevelOrRecursive);
        }

        public IEnumerable<FilePath> GetFilePathsReferencedBy(string absoluteName, EditorObjects.Parsing.TopLevelOrRecursive topLevelOrRecursive)
        {
            return GetFilesReferencedBy(absoluteName, topLevelOrRecursive).Select(item => new FilePath(item));
        }

        public void ClearFileCache(string absoluteName)
        {
            FileReferenceManager.Self.ClearFileCache(absoluteName);
        }

        public IEnumerable<string> GetAllFilesNeededOnDisk()
        {
            List<string> allFiles = new List<string>();

            var allRfses = GetAllRfses();

            FillAllFilesWithFilesInList(allFiles, allRfses, TopLevelOrRecursive.Recursive, ProjectOrDisk.Disk);

            string contentProjectDirectory = GlueState.CurrentMainContentProject.ContentProject.GetAbsoluteContentFolder().ToLowerInvariant();

            for (int i = 0; i < allFiles.Count; i++)
            {
                // This fixes slashes:
                allFiles[i] = FileManager.Standardize(allFiles[i], contentProjectDirectory, makeAbsolute: false);

                // This makes the files relative to the content project:
                if (allFiles[i].ToLowerInvariant().StartsWith(contentProjectDirectory))
                {
                    allFiles[i] = allFiles[i].Substring(contentProjectDirectory.Length);
                }
            }

            return allFiles;
        }

        ReferencedFileSave[] GetAllRfses()
        {
            var allRfses =
                GlueProject.Entities.SelectMany(item => item.ReferencedFiles)
                .Concat(GlueProject.Screens.SelectMany(item2 => item2.ReferencedFiles))
                .Concat(GlueProject.GlobalFiles)
                .ToArray();

            return allRfses;
        }

        public List<string> GetAllReferencedFileNames()
        {
            return GetAllReferencedFileNames(TopLevelOrRecursive.Recursive);
        }

        public List<FilePath> GetAllReferencedFilePaths()
        {
            List<string> allFiles = new List<string>();

            var allRfses = GetAllRfses();

            FillAllFilesWithFilesInList(allFiles, allRfses, TopLevelOrRecursive.Recursive, ProjectOrDisk.Project);

            return allFiles.Select(item => new FilePath(item)).ToList();
        }

        public List<string> GetAllReferencedFileNames(TopLevelOrRecursive topLevelOrRecursive)
        {
            List<string> allFiles = new List<string>();

            var allRfses = GetAllRfses();
            
            FillAllFilesWithFilesInList(allFiles, allRfses, topLevelOrRecursive, ProjectOrDisk.Project);

            string contentProjectDirectory = GlueState.CurrentMainContentProject.GetAbsoluteContentFolder().ToLowerInvariant();

            for (int i = 0; i < allFiles.Count; i++)
            {
                // This fixes slashes:
                allFiles[i] = FileManager.Standardize(allFiles[i], contentProjectDirectory, makeAbsolute: false);

                // This makes the files relative to the content project:
                if (allFiles[i].ToLowerInvariant().StartsWith(contentProjectDirectory))
                {
                    allFiles[i] = allFiles[i].Substring(contentProjectDirectory.Length);
                }
            }

            return allFiles.Distinct().ToList();
        }

        private void AddFilesReferenced(string fileName, List<string> allFiles, TopLevelOrRecursive topLevelOrRecursive, ProjectOrDisk projectOrFile)
        {
            // The project may have been unloaded:
            if (GlueState.CurrentMainContentProject != null)
            {
                string absoluteFileName = GlueCommands.GetAbsoluteFileName(fileName, isContent:true);

                if (File.Exists(absoluteFileName))
                {
                    List<string> referencedFiles = null;

                    if (projectOrFile == ProjectOrDisk.Project)
                    {
                        referencedFiles = FlatRedBall.Glue.Managers.FileReferenceManager.Self.GetFilesReferencedBy(absoluteFileName, topLevelOrRecursive);
                    }
                    else
                    {
                        referencedFiles = FlatRedBall.Glue.Managers.FileReferenceManager.Self.GetFilesNeededOnDiskBy(absoluteFileName, topLevelOrRecursive);

                    }

                    // 12/14/2010
                    // The referencedFiles
                    // instance may be null
                    // if the absoluteFileName
                    // references a file that doesn't
                    // exist on the file system.  This
                    // happens if someone checks in a GLUX
                    // file but forgets to check in a newly-
                    // created file.  Not deadly, so Glue shouldn't
                    // crash.  Also, Glue displays warning messages in
                    // a different part of the code, so we shouldn't pester
                    // the user here with another one.
                    if (referencedFiles != null)
                    {
                        allFiles.AddRange(referencedFiles);
                    }
                }
                else
                {
                    // Do nothing?
                }
            }
        }

        private void FillAllFilesWithFilesInList(List<string> allFiles, ReferencedFileSave[] referencedFileList, 
            TopLevelOrRecursive topLevelOrRecursive, ProjectOrDisk projectOrFile)
        {
            foreach(var rfs in referencedFileList)
            {
                
                allFiles.Add(GlueCommands.GetAbsoluteFileName(rfs));

                AddFilesReferenced(rfs.Name, allFiles, topLevelOrRecursive, projectOrFile);

                for (int i = 0; i < rfs.ProjectSpecificFiles.Count; i++)
                {
                    ProjectSpecificFile psf = rfs.ProjectSpecificFiles[i];

                    allFiles.Add(psf.FilePath);

                    AddFilesReferenced(psf.FilePath, allFiles, topLevelOrRecursive, projectOrFile);
                }
            }
        }

        public string GetContentFolder(IElement element)
        {
            string contentFolder = GlueState.ContentDirectory;

            string relativeElementFolder = element.Name + "/";

            return contentFolder + relativeElementFolder;
        }

        public void IgnoreNextChangeOnFile(string absoluteFileName)
        {
            IO.FileWatchManager.IgnoreNextChangeOnFile(absoluteFileName);
        }

        public string GetFullFileName(ReferencedFileSave rfs)
        {
            return GlueState.ContentDirectory + rfs.Name;
        }

        public FilePath GetFilePath(ReferencedFileSave rfs)
        {
            return GlueState.ContentDirectory + rfs.Name;
        }

        // This replaces ObjectFinder.GetReferencedFileSaveFromFile - if any changes are made here, make the changes there too
        public ReferencedFileSave GetReferencedFile(string fileName)
        {
            ////////////////Early Out//////////////////////////////////
            var invalidPathChars = Path.GetInvalidPathChars();
            if (invalidPathChars.Any(item => fileName.Contains(item)))
            {
                // This isn't a RFS, because it's got a bad path. Early out here so that FileManager.IsRelative doesn't throw an exception
                return null;
            }

            //////////////End Early Out////////////////////////////////


            fileName = fileName.ToLower();

            if (FileManager.IsRelative(fileName))
            {

                fileName = GlueCommands.GetAbsoluteFileName(fileName, isContent:true);

            }

            fileName = FileManager.Standardize(fileName).ToLower();


            if (GlueProject != null)
            {
                foreach (ScreenSave screenSave in GlueProject.Screens)
                {
                    foreach (ReferencedFileSave rfs in screenSave.ReferencedFiles)
                    {
                        string absoluteRfsFile = FileManager.Standardize(GlueCommands.GetAbsoluteFileName(rfs)).ToLower();

                        if (absoluteRfsFile == fileName)
                        {
                            return rfs;
                        }
                    }
                }

                lock (GlueProject.Entities)
                {
                    foreach (EntitySave entitySave in GlueProject.Entities)
                    {
                        foreach (ReferencedFileSave rfs in entitySave.ReferencedFiles)
                        {
                            string absoluteRfsFile = FileManager.Standardize(GlueCommands.GetAbsoluteFileName(rfs)).ToLower();

                            if (absoluteRfsFile == fileName)
                            {
                                return rfs;
                            }
                        }
                    }
                }

                foreach (ReferencedFileSave rfs in GlueProject.GlobalFiles)
                {
                    string absoluteRfsFile = FileManager.Standardize(GlueCommands.GetAbsoluteFileName(rfs)).ToLower();

                    if (absoluteRfsFile == fileName)
                    {
                        return rfs;
                    }
                }
            }

            return null;
        }

        public GeneralResponse GetLastParseResponse(FilePath file)
        {
            // only return failure if there is an entry in the FileReferenceManager, otherwise return success:
            if(FileReferenceManager.Self.FilesWithFailedGetReferenceCalls.ContainsKey(file))
            {
                return FileReferenceManager.Self.FilesWithFailedGetReferenceCalls[file];
            }
            else
            {
                return GeneralResponse.SuccessfulResponse;
            }
        }

        public FilePath GetGlueExecutingFolder()
        {
            FilePath filePath = System.Reflection.Assembly.GetExecutingAssembly()
                .Location;
            return filePath.GetDirectoryContainingThis();
        }

        public bool RenameReferencedFileSave(ReferencedFileSave rfs, string newName)
        {
            var oldName = rfs.Name;

            string oldDirectory = FileManager.GetDirectory(oldName);
            string newDirectory = FileManager.GetDirectory(newName);

            // it's a RFS so it's gotta be content
            // Note - MakeAbsolute will do its best
            // to determine if a file is content. However,
            // a rename may change the extension to something 
            // unrecognizable. In this case we still want to have 
            // it be content
            bool forceAsContent = true;
            var oldFilePath = new FilePath(ProjectManager.MakeAbsolute(oldName, forceAsContent));
            var newFilePath = new FilePath(ProjectManager.MakeAbsolute(newName, forceAsContent));

            string instanceName = FileManager.RemovePath(FileManager.RemoveExtension(newName));
            string whyIsntValid;

            var container = ObjectFinder.Self.GetElementContaining(rfs);

            var didRename = false;

            if (oldDirectory != newDirectory)
            {
                MessageBox.Show("The old file was located in \n" + oldDirectory + "\n" +
                    "The new file is located in \n" + newDirectory + "\n" +
                    "Currently Glue does not support changing directories.", "Warning");

                //rfs.SetNameNoCall(oldName);
            }
            else if (NameVerifier.IsReferencedFileNameValid(instanceName, rfs.GetAssetTypeInfo(), rfs, container, out whyIsntValid) == false)
            {
                MessageBox.Show(whyIsntValid);
                //rfs.SetNameNoCall(oldName);
            }
            else
            {
                rfs.Name = newName;
                ReferencedFileSaveSetPropertyManager.ReactToRenamedReferencedFile(
                    oldName, rfs.Name, rfs, container);
                didRename = true;
            }

            return didRename;
        }

    }

}
