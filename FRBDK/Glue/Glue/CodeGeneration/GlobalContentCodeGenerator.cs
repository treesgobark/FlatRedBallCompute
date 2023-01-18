﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FlatRedBall.Glue.CodeGeneration.CodeBuilder;
using FlatRedBall.Glue.SaveClasses;
using FlatRedBall.Glue.Elements;
using FlatRedBall.Glue.VSHelpers.Projects;
using FlatRedBall.IO;
using FlatRedBall.Glue.FormHelpers;
using FlatRedBall.Glue.CodeGeneration;
using FlatRedBall.Glue.AutomatedGlue;
using FlatRedBall.Glue.IO;
using FlatRedBall.Glue.Plugins.ExportedImplementations;
using FlatRedBall.Glue.Managers;

namespace FlatRedBall.Glue.Parsing
{

    // I think this is an old class which is being replaced by the ReferencedFileSaveCodeGenerator.  We should get rid of this eventually

    public static class GlobalContentCodeGenerator
    {
        #region Fields

        internal const string GlobalContentContainerName = "GeneratedGlobalContent";


        #endregion

        #region Methods

        public static void UpdateLoadGlobalContentCode()
        {
            //////////////////////Early Out////////////////////////
            if (GlueState.Self.CurrentGlueProject == null)
            {
                // early out in case Glue is closing:
                return;
            }
            ///////////////////End Early Out//////////////////////

            var classContent = GetLoadGlobalContentCode();



            var absoluteFileName = 
                new FilePath(FileManager.RelativeDirectory + "GlobalContent.Generated.cs");

            bool failedSaving = false;
            try
            {

                FileWatchManager.IgnoreNextChangeOnFile(absoluteFileName.FullPath);
                GlueCommands.Self.TryMultipleTimes(() => 
                    FileManager.SaveText(classContent.ToString(), absoluteFileName.FullPath));
            }
            catch
            {
                failedSaving = true;
            }

            if (failedSaving)
            {
                Plugins.ExportedImplementations.GlueCommands.Self.PrintError("Could not save the file\n\n" + absoluteFileName);

            }

            GlueCommands.Self.TryMultipleTimes(() =>
                GlueCommands.Self.ProjectCommands.CreateAndAddCodeFile(absoluteFileName, false));

            try
            {
                CodeWriter.InitializeStaticData(ProjectManager.GameClassFileName);
            }
            catch (CodeParseException exception)
            {
                GlueCommands.Self.PrintError(
                    "Could not add GlobalContent.Initialize to your Game class because of an error:\n\n" + 
                    exception.Message);
            }
        }

        private static ICodeBlock GetLoadGlobalContentCode()
        {

            ICodeBlock codeBlock = new CodeDocument();

            if (ProjectManager.GlueProjectSave.GlobalContentSettingsSave.LoadAsynchronously)
            {
                codeBlock
                    .Struct("", "NamedDelegate")
                    .Line("public string Name;")
                    .Line("public System.Action LoadMethod;")
                    .End()
                    ._()
                    .Line("static List<NamedDelegate> LoadMethodList = new List<NamedDelegate>();")
                    ._();
            }
            
            string className = "GlobalContent";

            #region Instantiate the ClassProperties
            ClassProperties classProperties = new ClassProperties();
            classProperties.Members = new List<FlatRedBall.Instructions.Reflection.TypedMemberBase>();
            classProperties.UntypedMembers = new Dictionary<string, string>();
            #endregion

            classProperties.NamespaceName = ProjectManager.ProjectNamespace;
            classProperties.ClassName = className;
            classProperties.IsStatic = true;
            classProperties.Partial = true;

            classProperties.UsingStatements = new List<string>();




            #region Add using statements

            // todo: remove these, we don't need them anymore and they could cause ambiguity
            classProperties.UsingStatements.Add("System.Collections.Generic");
            classProperties.UsingStatements.Add("System.Threading");
            classProperties.UsingStatements.Add("FlatRedBall");
            classProperties.UsingStatements.Add("FlatRedBall.Math.Geometry");
            classProperties.UsingStatements.Add("FlatRedBall.ManagedSpriteGroups");
            classProperties.UsingStatements.Add("FlatRedBall.Graphics.Animation");
            classProperties.UsingStatements.Add("FlatRedBall.Graphics.Particle");
            classProperties.UsingStatements.Add("FlatRedBall.AI.Pathfinding");
            classProperties.UsingStatements.Add("FlatRedBall.Utilities");
            classProperties.UsingStatements.Add("BitmapFont = FlatRedBall.Graphics.BitmapFont");
            classProperties.UsingStatements.Add("FlatRedBall.Localization");

            

            bool shouldAddUsingForDataTypes = false;

            for (int i = 0; i < ProjectManager.GlueProjectSave.GlobalFiles.Count; i++)
            {
                if (FileManager.GetExtension(ProjectManager.GlueProjectSave.GlobalFiles[i].Name) == "csv")
                {
                    shouldAddUsingForDataTypes = true;
                    break;
                }
            }

            if (shouldAddUsingForDataTypes)
            {
                classProperties.UsingStatements.Add(ProjectManager.ProjectNamespace + ".DataTypes");
                classProperties.UsingStatements.Add("FlatRedBall.IO.Csv");
            }

            #endregion

            var contents = GetGlobalContentFilesMethods();

            codeBlock.InsertBlock(contents);

            classProperties.MethodContent = codeBlock;
            
            var toReturn = CodeWriter.CreateClass(classProperties);

            var block = new CodeBlockBaseNoIndent(null);
            block.Line("#if ANDROID || IOS || DESKTOP_GL");
            block.Line("// Android doesn't allow background loading. iOS doesn't allow background rendering (which is used by converting textures to use premult alpha)");
            block.Line("#define REQUIRES_PRIMARY_THREAD_LOADING");
            block.Line("#endif");


            toReturn.PreCodeLines.Add(block);

            return toReturn;

            
        }

        private static ICodeBlock GetGlobalContentFilesMethods()
        {
            TaskManager.Self.WarnIfNotInTask();

            ICodeBlock codeBlock = new CodeDocument();
            var currentBlock = codeBlock;
            var classLevelBlock = currentBlock;

            codeBlock._();

            // Don't use foreach to make this tolerate changes to the collection while it generates
            //foreach (ReferencedFileSave rfs in ProjectManager.GlueProjectSave.GlobalFiles)
            for (int i = 0; i < ProjectManager.GlueProjectSave.GlobalFiles.Count; i++)
            {
                ReferencedFileSave rfs = ProjectManager.GlueProjectSave.GlobalFiles[i];

                ReferencedFileSaveCodeGenerator.AppendFieldOrPropertyForReferencedFile(currentBlock, rfs, null);
            }

            const bool inheritsFromElement = false;
            ReferencedFileSaveCodeGenerator.GenerateGetStaticMemberMethod(ProjectManager.GlueProjectSave.GlobalFiles, currentBlock, true, inheritsFromElement);
            ReferencedFileSaveCodeGenerator.GenerateGetFileMethodByName(
                ProjectManager.GlueProjectSave.GlobalFiles, currentBlock, false, "GetFile", false);
            if (ProjectManager.GlueProjectSave.GlobalContentSettingsSave.RecordLockContention)
            {
                currentBlock.Line("public static List<string> LockRecord = new List<string>();");
            }

            currentBlock
                .AutoProperty("public static bool", "IsInitialized", "", "private");

            currentBlock
                .AutoProperty("public static bool", "ShouldStopLoading");

            currentBlock.Line("const string ContentManagerName = \"Global\";");

            var initializeFunction =
                currentBlock.Function("public static void", "Initialize", "");

            currentBlock = initializeFunction
                    ._();

            // Vic asks - should this be in a plugin? Or should it be core FRB? Let's put it here in core for now.
            // This is needed for Tiled shapes
            //https://github.com/vchelaru/FlatRedBall/issues/892
            // But maybe we should do this for everything just to be safe?
            currentBlock.Line("bool oldShapeManagerSuppressAdd = FlatRedBall.Math.Geometry.ShapeManager.SuppressAddingOnVisibilityTrue;");
            currentBlock.Line("FlatRedBall.Math.Geometry.ShapeManager.SuppressAddingOnVisibilityTrue = true;");

            foreach (var generator in CodeWriter.GlobalContentCodeGenerators)
            {
                generator.GenerateInitializeStart(initializeFunction);
            }


            //stringBuilder.AppendLine("\t\t\tstring ContentManagerName = \"Global\";");

            // Do the high-proprity loads first
            // Update May 10, 2011
            // If loading asynchronously
            // the first Screen may load before
            // we even get to the high-priority RFS's
            // (which are localization).  This could cause
            // the first Screen to have unlocalized text.  That's
            // why we want to load it before we even start our async loading.
            foreach (ReferencedFileSave rfs in ProjectManager.GlueProjectSave.GlobalFiles)
            {
                if (ReferencedFileSaveCodeGenerator.IsRfsHighPriority(rfs))
                {
                    ReferencedFileSaveCodeGenerator.GetInitializationForReferencedFile(rfs, null, initializeFunction, LoadType.CompleteLoad);
                }
            }

            bool loadAsync = GenerateLoadAsyncCode(classLevelBlock, initializeFunction);

            if (GlobalContentCodeGenerator.SuppressGlobalContentDictionaryRefresh == false)
            {
                ReferencedFileSaveCodeGenerator.RefreshGlobalContentDictionary();
            }

            if (loadAsync)
            {
                currentBlock.Line("#if !REQUIRES_PRIMARY_THREAD_LOADING");
            }

            foreach (ReferencedFileSave rfs in ProjectManager.GlueProjectSave.GlobalFiles)
            {
                if (!ReferencedFileSaveCodeGenerator.IsRfsHighPriority(rfs) && rfs.LoadedAtRuntime)
                {
                    var blockToUse = initializeFunction;

                    if (loadAsync)
                    {
                        blockToUse = classLevelBlock
                            .Function("static void", "Load" + rfs.Name.Replace("/", "_").Replace(".", "_"), "");
                    }


                    ReferencedFileSaveCodeGenerator.GetInitializationForReferencedFile(rfs, null, blockToUse, LoadType.CompleteLoad);

                    if (loadAsync)
                    {
                        blockToUse.Line("#if !REQUIRES_PRIMARY_THREAD_LOADING");

                        blockToUse.Line("m" + rfs.GetInstanceName() + "Mre.Set();");
                        blockToUse.Line("#endif");

                        blockToUse.End();
                    }

                }
            }

            if (loadAsync)
            {
                currentBlock.Line("#endif");
            }

            currentBlock.Line("FlatRedBall.Math.Geometry.ShapeManager.SuppressAddingOnVisibilityTrue = oldShapeManagerSuppressAdd;");

            if (!loadAsync)
            {
                currentBlock = currentBlock
                        .Line("\t\t\tIsInitialized = true;")
                    .End();
            }


            ReferencedFileSaveCodeGenerator.GenerateReloadFileMethod(classLevelBlock, ProjectManager.GlueProjectSave.GlobalFiles);

            

            foreach (var generator in CodeWriter.GlobalContentCodeGenerators)
            {
                generator.GenerateInitializeEnd(initializeFunction);
                generator.GenerateAdditionalMethods(classLevelBlock);
            }



            return codeBlock;
        }

        private static bool GenerateLoadAsyncCode(ICodeBlock classLevelBlock, ICodeBlock initializeBlock)
        {
            bool loadAsync = ProjectManager.GlueProjectSave.GlobalContentSettingsSave.LoadAsynchronously;

            if (loadAsync)
            {
                GenerateInitializeAsync(initializeBlock);
            }

            loadAsync = ProjectManager.GlueProjectSave.GlobalContentSettingsSave.LoadAsynchronously;
            if (loadAsync)
            {

                classLevelBlock._();


                classLevelBlock.Line("#if !REQUIRES_PRIMARY_THREAD_LOADING");

                classLevelBlock
                    .Function("static void", "RequestContentLoad", "string contentName")
                        .Lock("LoadMethodList")
                            .Line("int index = -1;")
                            .For("int i = 0; i < LoadMethodList.Count; i++")
                                .If("LoadMethodList[i].Name == contentName")
                                    .Line("index = i;")
                                    .Line("break;")
                                .End()
                            .End()
                            .If("index != -1")
                                .Line("NamedDelegate delegateToShuffle = LoadMethodList[index];")
                                .Line("LoadMethodList.RemoveAt(index);")
                                .Line("LoadMethodList.Insert(0, delegateToShuffle);")
                            .End()

                        .End()
                    .End();
                classLevelBlock.Line("#endif");

                classLevelBlock._();

                classLevelBlock.Line("#if !REQUIRES_PRIMARY_THREAD_LOADING");
                classLevelBlock
                    .Function("static void", "AsyncInitialize", "")

                        .Line("#if XBOX360")
                        .Line("// We can not use threads 0 or 2")
                        .Line("// Async screen loading uses thread 4, so we'll use 3 here")
                        .Line("Thread.CurrentThread.SetProcessorAffinity(3);")
                        .Line("#endif")

                        .Line("bool shouldLoop = LoadMethodList.Count != 0;")

                        .While("shouldLoop")
                            .Line("System.Action action = null;")
                            .Lock("LoadMethodList")

                                .Line("action = LoadMethodList[0].LoadMethod;")
                                .Line("LoadMethodList.RemoveAt(0);")
                                .Line("shouldLoop = LoadMethodList.Count != 0 && !ShouldStopLoading;")

                            .End()
                            .Line("action();")
                        .End()
                        .Line("IsInitialized = true;")
                        ._()
                    .End();
                classLevelBlock.Line("#endif");

                //stringBuilder.AppendLine("\t\t\tstring ContentManagerName = \"Global\";");

            }

            return loadAsync;
        }

        private static void GenerateInitializeAsync(ICodeBlock currentBlock)
        {
            currentBlock.Line("#if !REQUIRES_PRIMARY_THREAD_LOADING");
            currentBlock.Line("NamedDelegate namedDelegate = new NamedDelegate();");

            foreach (ReferencedFileSave rfs in ProjectManager.GlueProjectSave.GlobalFiles)
            {
                if (!ReferencedFileSaveCodeGenerator.IsRfsHighPriority(rfs) && !rfs.LoadedOnlyWhenReferenced && rfs.LoadedAtRuntime)
                {
                    currentBlock.Line("namedDelegate.Name = \"" + rfs.Name + "\";");
                    currentBlock.Line("namedDelegate.LoadMethod = Load" + rfs.Name.Replace("/", "_").Replace(".", "_") + ";");
                    currentBlock.Line("LoadMethodList.Add( namedDelegate );");
                }
            }

            currentBlock._();

            currentBlock.Line("#if WINDOWS_8");
            currentBlock.Line("System.Threading.Tasks.Task.Run((System.Action)AsyncInitialize);");
            currentBlock.Line("#else");

            currentBlock.Line("ThreadStart threadStart = new ThreadStart(AsyncInitialize);");
            currentBlock.Line("Thread thread = new Thread(threadStart);");
            currentBlock.Line("thread.Name = \"GlobalContent Async load\";");
            currentBlock.Line("thread.Start();");
            currentBlock.Line("#endif");
            currentBlock.Line("#endif");
        }


        #endregion

        public static bool SuppressGlobalContentDictionaryRefresh { get; set; }
    }
}
