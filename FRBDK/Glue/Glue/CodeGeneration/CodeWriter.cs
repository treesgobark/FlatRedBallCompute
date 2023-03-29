﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using FlatRedBall.Glue.CodeGeneration.CodeBuilder;
using FlatRedBall.IO;
using FlatRedBall.Utilities;
using FlatRedBall.Instructions.Reflection;
using FlatRedBall.Glue.Elements;
using FlatRedBall.Glue.SaveClasses;
using FlatRedBall.Content.Instructions;
using FlatRedBall.Glue.Controls;
using FlatRedBall.Glue.CodeGeneration;
using FlatRedBall.Glue.Events;
using FlatRedBall.Glue.Plugins.Performance;
using FlatRedBall.Glue.Plugins;
using FlatRedBall.Glue.Plugins.Interfaces;
using FlatRedBall.Glue.IO;
using System.IO;
using FlatRedBall.Glue.AutomatedGlue;
using FlatRedBall.Glue.FormHelpers;
using System.Windows.Forms;
using Glue;
using FlatRedBall.Glue.Plugins.ExportedImplementations;
using System.Threading.Tasks;

namespace FlatRedBall.Glue.Parsing
{
    #region Class Properties Struct
    public struct ClassProperties
    {
        public string NamespaceName;
        public string ClassName;
        public List<TypedMemberBase> Members;
        public Dictionary<string, string> UntypedMembers;

        public bool IsStatic;
        public bool Partial;
        public List<string> UsingStatements;

        public ICodeBlock MethodContent;
    }
    #endregion



    public static class CodeWriter
    {
        #region Fields

        private static string mScreenTemplateCode =
@"using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using FlatRedBall;
using FlatRedBall.Input;
using FlatRedBall.Instructions;
using FlatRedBall.AI.Pathfinding;
using FlatRedBall.Graphics.Animation;
using FlatRedBall.Gui;
using FlatRedBall.Math;
using FlatRedBall.Math.Geometry;
using FlatRedBall.Localization;
using Microsoft.Xna.Framework;



namespace FlatRedBallAddOns.Screens
{
    public partial class ScreenTemplate
    {

        void CustomInitialize()
        {


        }

        void CustomActivity(bool firstTimeCalled)
        {


        }

        void CustomDestroy()
        {


        }

        static void CustomLoadStaticContent(string contentManagerName)
        {


        }

    }
}
";

        private static string mEntityTemplateCode =
@"using System;
using System.Collections.Generic;
using System.Text;
using FlatRedBall;
using FlatRedBall.Input;
using FlatRedBall.Instructions;
using FlatRedBall.AI.Pathfinding;
using FlatRedBall.Graphics.Animation;
using FlatRedBall.Graphics.Particle;
using FlatRedBall.Math.Geometry;
using Microsoft.Xna.Framework;

namespace FlatRedBallAddOns.Entities
{
    public partial class GlueEntityTemplate
    {
        /// <summary>
        /// Initialization logic which is executed only one time for this Entity (unless the Entity is pooled).
        /// This method is called when the Entity is added to managers. Entities which are instantiated but not
        /// added to managers will not have this method called.
        /// </summary>
        private void CustomInitialize()
        {


        }

        private void CustomActivity()
        {


        }

        private void CustomDestroy()
        {


        }

        private static void CustomLoadStaticContent(string contentManagerName)
        {


        }
    }
}
";



        #endregion

        #region Properties

        public static string ScreenTemplateCode
        {
            get { return mScreenTemplateCode; }
        }

        public static string EntityTemplateCode
        {
            get { return mEntityTemplateCode; }
        }

        public static List<ElementComponentCodeGenerator> CodeGenerators
        {
            get;
            set;
        }

        public static List<GlobalContentCodeGeneratorBase> GlobalContentCodeGenerators
        {
            get;
            private set;
        } = new List<GlobalContentCodeGeneratorBase>();


        #endregion

        #region Methods

        static CodeWriter()

        {
            CodeGenerators = new List<ElementComponentCodeGenerator>();

            CodeGenerators.Add(new ErrorCheckingCodeGenerator());
            CodeGenerators.Add(new ScrollableListCodeGenerator());
            CodeGenerators.Add(new StateCodeGenerator());

            CodeGenerators.Add(new FactoryCodeGeneratorEarly());
            CodeGenerators.Add(new FactoryCodeGenerator());
            CodeGenerators.Add(new ReferencedFileSaveCodeGenerator());
            CodeGenerators.Add(new NamedObjectSaveCodeGenerator());
            CodeGenerators.Add(new CustomVariableCodeGenerator());
            CodeGenerators.Add(new EventCodeGenerator());
            CodeGenerators.Add(new PooledCodeGenerator());
            CodeGenerators.Add(new IVisibleCodeGenerator());
            CodeGenerators.Add(new IWindowCodeGenerator());
            CodeGenerators.Add(new ITiledTileMetadataCodeGenerator());
            CodeGenerators.Add(new PauseCodeGenerator());
            CodeGenerators.Add(new LoadingScreenCodeGenerator());
        }

        public static async Task GenerateCode(GlueElement element)
        {

            #region Prepare for generation

            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            // Since anything can modify an enumeration value we want to make sure that
            // it's proper before generating code for it:

            // If enumeration values don't work property let's just print some output and carry on
            try
            {
                element.FixEnumerationValues();
            }
            catch (Exception e)
            {
                PluginManager.ReceiveError("Error fixing enumerations for " + element + ": " + e.ToString());
            }


            // Do this before doing anything else since 
            // these reusable entire file RFS's are used 
            // in other code.
            RefreshReusableEntireFileRfses(element);
            #endregion

            #region Event Generation

            EventCodeGenerator.GenerateEventGeneratedFile(element);

            if (element.Events.Count != 0)
            {
                var sharedCodeFullFileName =
                    EventResponseSave.GetSharedCodeFullFileName(element, FileManager.GetDirectory(GlueState.Self.GlueProjectFileName.FullPath));

                EventCodeGenerator.CreateEmptyCodeIfNecessary(element,
                    sharedCodeFullFileName, false);
            }



            EventCodeGenerator.AddStubsForCustomEvents(element);

            #endregion

            CreateGeneratedFileIfNecessary(element);

            foreach (PluginManager pluginManager in PluginManager.GetInstances())
            {
                CodeGeneratorPluginMethods.CallCodeGenerationStart(pluginManager, element);
            }

            if (GlobalContentCodeGenerator.SuppressGlobalContentDictionaryRefresh == false)
            {
                ReferencedFileSaveCodeGenerator.RefreshGlobalContentDictionary();
            }

            if (ReferencedFileSaveCodeGenerator.GlobalContentFilesDictionary == null)
            {
                throw new Exception("Global content files dictionary should not be null");
            }
            string classNamespace = GetGlueElementNamespace(element);

            var rootBlock = new CodeDocument(0);

            GenerateDefines(rootBlock);

            UsingsCodeGenerator.GenerateUsingStatements(rootBlock, element);


            var namespaceBlock = rootBlock.Namespace(classNamespace);

            var codeBlock = GenerateClassHeader(element, namespaceBlock);


            GenerateFieldsAndProperties(element, codeBlock);

            GenerateConstructors(element, codeBlock);

            GenerateInitialize(element, codeBlock);

            GenerateAddToManagers(element, codeBlock);

            GenerateActivity(codeBlock, element);

            if(GlueState.Self.CurrentGlueProject.FileVersion >= (int)GlueProjectSave.GluxVersions.ScreensHaveActivityEditMode)
            {
                GenerateActivityEditMode(codeBlock, element);
            }

            GenerateDestroy(element, codeBlock);

            GenerateMethods(codeBlock, element);

            foreach (var codeGenerator in CodeGenerators)
            {
                codeGenerator.GenerateAdditionalClasses(namespaceBlock, element);
            }

            string generatedCodeFileName = element.Name + ".Generated.cs";
            var contentsToSave = rootBlock.ToString();

            CodeWriter.SaveFileContents(contentsToSave, FileManager.RelativeDirectory + generatedCodeFileName, true);

            #region Extra stuff if it's an EntitySave

            if (element is EntitySave entitySave)
            {
                var fileContents = contentsToSave;
                string fileName = FileManager.RelativeDirectory + element.Name + ".Generated.cs";
                bool shouldSave = false;

                #region Ok, the code is generated, but we may still need to give it a base class


                bool inheritsFromEntity = element.InheritsFromEntity();



                EntitySave rootEntitySave;
                List<string> inheritanceList = InheritanceCodeWriter.Self.GetInheritanceList(entitySave, out rootEntitySave);
                InheritanceCodeWriter.Self.RemoveCallsForInheritance(entitySave, inheritsFromEntity, rootEntitySave, ref fileContents, ref shouldSave);

                #endregion

                #region If this thing is created by other entities, then we should make it IPoolable

                if (entitySave.CreatedByOtherEntities)
                {
                    var isAbstract = entitySave.AllNamedObjects.Any(item => item.SetByDerived);
                    if(!isAbstract)
                    {
                        FactoryCodeGenerator.GenerateAndAddFactoryToProjectClass(entitySave);
                    }
                }

                #endregion

                #region If this uses global content, then make it use global content regardless of what is passed in

                if (entitySave.UseGlobalContent)
                {
                    fileContents = fileContents.Replace("ContentManagerName = contentManagerName;", "ContentManagerName = FlatRedBall.FlatRedBallServices.GlobalContentManager;");
                    shouldSave = true;
                }

                #endregion


                #region If a change was made to the fileContents, let's save it

                if (shouldSave)
                {
                    bool tryAgain = true;

                    CodeWriter.SaveFileContents(fileContents, fileName, tryAgain);
                }

                #endregion
            }
            #endregion

            // This code will create and add above, but if the file already exists, the code above won't re-add it to the 
            // project. This is a last chance to add it if necessary:
            await GlueCommands.Self.ProjectCommands.TryAddCodeFileToProjectAsync(GetAbsoluteGeneratedCodeFileFor(element), saveOnAdd: true);
        }
        
        #region Predefines like #if ANDROID

        public static void GenerateDefines(ICodeBlock rootBlock)
        {
            rootBlock.Line("#if ANDROID || IOS || DESKTOP_GL");
            rootBlock.Line("#define REQUIRES_PRIMARY_THREAD_LOADING");
            rootBlock.Line("#endif");

            var project = GlueState.Self.CurrentGlueProject;
            if(project.FileVersion >= (int)GlueProjectSave.GluxVersions.SupportsEditMode)
            {
                rootBlock.Line("#define SUPPORTS_GLUEVIEW_2");
            }
            else
            {
                rootBlock.Line($"// The project is not new enough to support GlueView 2. It is on version {project.FileVersion}");
                rootBlock.Line("//#define SUPPORTS_GLUEVIEW_2");

            }
        }

        #endregion

        #region Namespace

        public static string GetGlueElementNamespace(GlueElement element)
        {
            string classNamespace = ProjectManager.ProjectNamespace;

            if (element is EntitySave)
            {
                string directory = FileManager.MakeRelative(FileManager.GetDirectory(element.Name));

                if (directory.ToLower() != "Entities/".ToLower())
                {
                    string relativeDirectory = FileManager.MakeRelative(directory);
                    relativeDirectory = relativeDirectory.Substring(0, relativeDirectory.Length - 1);
                    relativeDirectory = relativeDirectory.Replace('/', '.');

                    classNamespace += "." + relativeDirectory;
                }
                else
                {
                    classNamespace += ".Entities";
                }
            }
            else if (element is ScreenSave)
            {
                classNamespace += ".Screens";
            }

            return classNamespace;
        }

        #endregion

        #region Class Header

        private static ICodeBlock GenerateClassHeader(IElement element, ICodeBlock namespaceBlock)
        {
            var inheritance = GetInheritance(element);

            if(!string.IsNullOrEmpty(inheritance))
            {
                inheritance = " : " + inheritance;
            }

            var isAbstract = element.AllNamedObjects.Any(item => item.SetByDerived);

            string optionalAbstractString = isAbstract ? "abstract " : string.Empty;

            var classCodeblock = namespaceBlock.Class($"public {optionalAbstractString}partial", FileManager.RemovePath( element.Name), inheritance);

            return classCodeblock;
        }

        #endregion

        #region Load Static Content

        public static void GenerateLoadStaticContent(ICodeBlock codeBlock, IElement saveObject)
        {
            var curBlock = codeBlock;

            bool inheritsFromElement = saveObject.InheritsFromElement();

            curBlock = curBlock.Function(StringHelper.SpaceStrings(
                                                        "public",
                                                        "static",
                                                        inheritsFromElement ? "new" : null,
                                                        "void"),
                                         "LoadStaticContent",
                                         "string contentManagerName");


            PerformancePluginCodeGenerator.GenerateStart(saveObject, curBlock, "LoadStaticContent");

            curBlock.Line("bool oldShapeManagerSuppressAdd = FlatRedBall.Math.Geometry.ShapeManager.SuppressAddingOnVisibilityTrue;");
            curBlock.Line("FlatRedBall.Math.Geometry.ShapeManager.SuppressAddingOnVisibilityTrue = true;");

            curBlock.If("string.IsNullOrEmpty(contentManagerName)")
                .Line("throw new System.ArgumentException(\"contentManagerName cannot be empty or null\");")
                .End();

            #region Set the ContentManagerName ( do this BEFORE checking for IsStaticContentLoaded )

            if (saveObject is EntitySave)
            {
                if ((saveObject as EntitySave).UseGlobalContent)
                {
                    curBlock.Line("// Set to use global content");
                    curBlock.Line("contentManagerName = FlatRedBall.FlatRedBallServices.GlobalContentManager;");
                }
                curBlock.Line("ContentManagerName = contentManagerName;");
            }

            #endregion


            if (inheritsFromElement)
            {
                curBlock.Line(ProjectManager.ProjectNamespace + "." + saveObject.BaseElement.Replace("\\", ".")  + ".LoadStaticContent(contentManagerName);");
            }

            foreach (ElementComponentCodeGenerator codeGenerator in CodeGenerators
                .OrderBy(item=>(int)item.CodeLocation))
            {
                curBlock = codeGenerator.GenerateLoadStaticContent(curBlock, saveObject);
            }




            #region Register the unload for EntitySaves

            // Vic says - do we want this for Screens too?
            // I don't think we do because Screens can just null- out stuff in their Destroy method.  There's only one of them around at a time.

            if (saveObject is EntitySave)
            {
                if (!(saveObject as EntitySave).UseGlobalContent)
                {
                    var ifBlock = curBlock.If("registerUnload && ContentManagerName != FlatRedBall.FlatRedBallServices.GlobalContentManager");
                    ReferencedFileSaveCodeGenerator.AppendAddUnloadMethod(ifBlock, saveObject);
                }
            }

            #endregion




            curBlock.Line("CustomLoadStaticContent(contentManagerName);");

            curBlock.Line("FlatRedBall.Math.Geometry.ShapeManager.SuppressAddingOnVisibilityTrue = oldShapeManagerSuppressAdd;");

            PerformancePluginCodeGenerator.GenerateEnd(saveObject, curBlock, "LoadStaticContent");
        }

        #endregion

        private static void GenerateConstructors(GlueElement element, ICodeBlock codeBlock)
        {
            ICodeBlock constructor;

            var whatToInheritFrom = GetInheritance(element);

            var elementName = FileManager.RemovePath(element.Name);

            if (element is EntitySave)
            {
                codeBlock.Constructor("public", elementName, "", "this(FlatRedBall.Screens.ScreenManager.CurrentScreen.ContentManagerName, true)");

                codeBlock.Constructor("public", elementName, "string contentManagerName", "this(contentManagerName, true)");

                constructor = codeBlock.Constructor("public", elementName, "string contentManagerName, bool addToManagers", "base()");
                constructor.Line("ContentManagerName = contentManagerName;");

                // See below on why we do this here
                CallElementComponentCodeGeneratorGenerateConstructor(element, constructor);

                // The base will handle this
                if (element.InheritsFromEntity() == false)
                {
                    constructor.Line("InitializeEntity(addToManagers);");
                }
            }
            else // screen save
            {
                string contentManagerName = $"\"{elementName}\"";

                if (element.UseGlobalContent)
                {
                    contentManagerName = "\"Global\"";
                }

                // Feb 13, 2022
                // This constructor enables the old FRB code to call in to this with no parameters without breaking reflection
                // No need to change it since screens are almost never explicitly constructed
                codeBlock.Constructor("public", elementName, "", $"this ({contentManagerName})");

                constructor = codeBlock.Constructor("public", elementName, "string contentManagerName", $"base (contentManagerName)");

                CallElementComponentCodeGeneratorGenerateConstructor(element, constructor);
            }

            // October 27 2021
            // We used to call the code here, but that means it will get called after initialize on entityes.
            // This can cause crashes with lists in entities, so we probably want this called before calling the 
            // entity's Initialize
            //CallElementComponentCodeGeneratorGenerateConstructor(element, constructor);

            static void CallElementComponentCodeGeneratorGenerateConstructor(IElement element, ICodeBlock constructor)
            {
                foreach (ElementComponentCodeGenerator codeGenerator in CodeGenerators)
                {
                    try
                    {
                        codeGenerator.GenerateConstructor(constructor, element);
                    }
                    catch (Exception e)
                    {
                        GlueCommands.Self.PrintError(
                            $"Error calling GenerateConstructor on {codeGenerator.GetType()}:\n{e}");
                    }
                }
            }
        }

        static string GetInheritance(IElement element)
        {
            string whatToInheritFrom = null;

            if (element is EntitySave)
            {
                bool inheritsFromEntity = element.InheritsFromEntity();
                var entitySave = element as EntitySave;

                EntitySave rootEntitySave;
                List<string> inheritanceList = 
                    InheritanceCodeWriter.Self.GetInheritanceList(entitySave, out rootEntitySave);

                foreach (string inheritance in inheritanceList)
                {
                    if (string.IsNullOrEmpty(whatToInheritFrom))
                    {
                        whatToInheritFrom = inheritance;
                    }
                    else
                    {
                        whatToInheritFrom += ", " + inheritance;
                    }
                }

            }
            else // Screen
            {
                bool inherits = !string.IsNullOrEmpty(element.BaseElement) && element.BaseElement != "<NONE>";
                if (inherits)
                {

                    whatToInheritFrom = FileManager.RemovePath(element.BaseElement);
                }
                else
                {
                    whatToInheritFrom = "FlatRedBall.Screens.Screen";
                }
            }

            return whatToInheritFrom;
        }


        public static bool SaveFileContents(string fileContents, string fileName, bool tryAgain, bool standardizeNewlines = true)
        {
            if(standardizeNewlines && !string.IsNullOrEmpty(fileContents))
            {
                // from: https://stackoverflow.com/questions/31053/regex-c-replace-n-with-r-n
                // for: https://github.com/vchelaru/FlatRedBall/issues/103
                fileContents = System.Text.RegularExpressions.Regex.Replace(fileContents, "(?<!\r)\n", "\r\n");
            }

            bool isReadOnly = System.IO.File.Exists(fileName) && new FileInfo(fileName).IsReadOnly;

            if (isReadOnly)
            {
                GlueGui.ShowMessageBox("Could not save file\n\n" + fileName + "\n\nbecause it is marked read-only.");
            }
            else
            {
                if(fileName.Contains(".generated.", StringComparison.InvariantCultureIgnoreCase))
                {
                    fileContents = $"#pragma warning disable\r\n{fileContents}";
                }
                FileWatchManager.IgnoreNextChangeOnFile(fileName);
                if (!tryAgain)
                {
                    FileManager.SaveText(fileContents, fileName);
                }
                else
                {
                    try
                    {
                        GlueCommands.Self.TryMultipleTimes(() =>FileManager.SaveText(fileContents, fileName), numberOfTimesToTry:10);
                    }
                    catch(Exception e)
                    {
                        string errorMessage = "Could not generate the file " + fileName + "\n\n" +
                            "Try manually re-generating this through Glue.  This is not a fatal error.";

                        GlueCommands.Self.PrintOutput(errorMessage);
                    }
                }
            }
            return tryAgain;
        }

        public static FilePath GetAbsoluteGeneratedCodeFileFor(GlueElement saveObject)
        {
            string fileName = saveObject.Name + ".Generated.cs";

            FilePath absoluteFileName = fileName;

            if (FileManager.IsRelative(fileName))
            {
                absoluteFileName = GlueState.Self.CurrentGlueProjectDirectory + fileName;
            }

            return absoluteFileName;
        }

        public static void CreateGeneratedFileIfNecessary(GlueElement saveObject)
        {
            var absoluteFilePath = GetAbsoluteGeneratedCodeFileFor(saveObject);
            if (absoluteFilePath.Exists() == false)
            {
                CreateAndAddGeneratedFile(saveObject);
            }
        }

        public static void CreateAndAddGeneratedFile(IElement saveObject)
        {
            // let's make a generated file
            string fileName = saveObject.Name + ".Generated.cs";
            ProjectManager.CodeProjectHelper.CreateAndAddPartialCodeFile(fileName, true);
            PluginManager.ReceiveOutput("Glue has created the generated file " + FileManager.RelativeDirectory + saveObject.Name + ".cs");
        }

        public static Dictionary<string, string> ReusableEntireFileRfses { get; } = new Dictionary<string, string>();

        private static void RefreshReusableEntireFileRfses(IElement element)
        {
            ReusableEntireFileRfses.Clear();

            // Fill the mReusableEntireFileRfses
            for (int i = 0; i < element.NamedObjects.Count; i++)
            {
                NamedObjectSave nos = element.NamedObjects[i];

                if (nos.IsEntireFile && nos.SourceFile != null && ReusableEntireFileRfses.ContainsKey(nos.SourceFile) == false)
                {
                    ReusableEntireFileRfses.Add(nos.SourceFile, nos.FieldName);
                }
            }
            IVisibleCodeGenerator.ReusableEntireFileRfses = ReusableEntireFileRfses;
            NamedObjectSaveCodeGenerator.ReusableEntireFileRfses = ReusableEntireFileRfses;
        }

        internal static ICodeBlock GenerateFieldsAndProperties(IElement glueElement, ICodeBlock codeBlock)
        {
            if(glueElement is EntitySave)
            {
                if(glueElement.InheritsFromElement())
                {
                    string baseQualifiedName = ProjectManager.ProjectNamespace + "." + glueElement.BaseElement.Replace("\\", ".");

                    codeBlock.Line("// This is made static so that static lazy-loaded content can access it.");
                    codeBlock.Property("public static new string", "ContentManagerName")
                        .Get().Line($"return {baseQualifiedName}.ContentManagerName;").End()
                        .Set().Line($"{baseQualifiedName}.ContentManagerName = value;").End();
                }
                else
                {
                    codeBlock.Line("// This is made static so that static lazy-loaded content can access it.");
                    codeBlock.AutoProperty("public static string", "ContentManagerName");
                }
            }

            foreach (var codeGenerator in CodeWriter.CodeGenerators)
            {
                if (codeGenerator == null)
                {
                    throw new Exception("The CodeWriter contains a null code generator.  A plugin must have added this");
                }

                try
                {
                    codeGenerator.GenerateFields(codeBlock, glueElement);
                }
                catch (Exception e)
                {
                    throw new Exception("Error generating fields in generator " + codeGenerator.GetType().Name + 
                        "\n\n" + e.ToString());

                }
            }

            PerformancePluginCodeGenerator.GenerateFields(glueElement, codeBlock);

            // No need to create LayerProvidedByContainer if this inherits from another object.
            if (glueElement is EntitySave && !glueElement.InheritsFromEntity())
            {
                // Add the layer that is going to get assigned in generated code
                codeBlock.Line("protected FlatRedBall.Graphics.Layer LayerProvidedByContainer = null;");
            }
            

            return codeBlock;


        }
        
        internal static ICodeBlock GenerateInitialize(GlueElement saveObject, ICodeBlock codeBlock)
        {
            string initializePre = null;
            string initializeMethodCall = null;
            if (saveObject is ScreenSave)
            {
                initializePre = "public override void";
                initializeMethodCall = "Initialize";
            }
            else
            {
                initializeMethodCall = "InitializeEntity";

                if (saveObject.InheritsFromElement())
                {
                    initializePre = "protected override void";
                }
                else
                {
                    initializePre = "protected virtual void";
                }
            }

            codeBlock = codeBlock.Function(initializePre, initializeMethodCall, "bool addToManagers");

            // Start measuring performance before anything else
            PerformancePluginCodeGenerator.GenerateStartTimingInitialize(saveObject, codeBlock);

            PerformancePluginCodeGenerator.SaveObject = saveObject;
            PerformancePluginCodeGenerator.CodeBlock = codeBlock;

            PerformancePluginCodeGenerator.GenerateStart("CustomLoadStaticContent from Initialize");

            // Load static content before looping through the CodeGenerators
            // The reason for this is there is a ReferencedFileSaveCodeGenerator
            // which needs to work with static RFS's which are instantiated here
            codeBlock.Line("LoadStaticContent(ContentManagerName);");

            PerformancePluginCodeGenerator.GenerateEnd();


            PerformancePluginCodeGenerator.GenerateStart("General Initialize internals");

            foreach (ElementComponentCodeGenerator codeGenerator in CodeGenerators)
            {
                try
                {
                    codeGenerator.GenerateInitialize(codeBlock, saveObject);
                }
                catch (Exception e)
                {
                    GlueCommands.Self.PrintError($"Error calling GenerateInitialize on {codeGenerator.GetType()}:\n{e}");
                }
            }



            foreach (ElementComponentCodeGenerator codeGenerator in CodeGenerators)
            {
                try
                {
                    codeGenerator.GenerateInitializeLate(codeBlock, saveObject);
                }
                catch(Exception e)
                {
                    GlueCommands.Self.PrintError($"Error calling GenerateInitializeLate on {codeGenerator.GetType()}:\n{e}");
                }
            }

            NamedObjectSaveCodeGenerator.GenerateCollisionRelationships(codeBlock, saveObject);

            if (saveObject is ScreenSave)
            {
                ScreenSave asScreenSave = saveObject as ScreenSave;
                codeBlock._();

                if (asScreenSave.IsRequiredAtStartup)
                {
                    string startupScreen = GlueCommands.Self.GluxCommands.StartUpScreenName;

                    string qualifiedName = ProjectManager.ProjectNamespace + "." + startupScreen.Replace("\\", ".");

                    codeBlock.Line(string.Format("this.NextScreen = typeof({0}).FullName;", qualifiedName));
                }

                if (asScreenSave.UseGlobalContent)
                {
                    // no need to do anything here because Screens are smart enough to know to not load if they
                    // are using global content
                }
            }


            codeBlock._();
            PerformancePluginCodeGenerator.GenerateEnd();

            #region PostInitializeCode

            PerformancePluginCodeGenerator.GenerateStart("Post Initialize");


            if (saveObject.InheritsFromElement() == false)
            {
                codeBlock.Line("PostInitialize();");
            }
            PerformancePluginCodeGenerator.GenerateEnd();

            #endregion

            PerformancePluginCodeGenerator.GenerateStart("Base.Initialize");


            InheritanceCodeWriter.Self.WriteBaseInitialize(saveObject, codeBlock);

            // This needs to happen after calling WriteBaseInitialize so that the derived overwrites the base
            if (saveObject is ScreenSave)
            {
                ScreenSave asScreenSave = saveObject as ScreenSave;

                if (!string.IsNullOrEmpty(asScreenSave.NextScreen))
                {
                    string nameToUse = ProjectManager.ProjectNamespace + "." + asScreenSave.NextScreen.Replace("\\", ".");

                    codeBlock.Line(string.Format("this.NextScreen = typeof({0}).FullName;", nameToUse));
                }

            }
            PerformancePluginCodeGenerator.GenerateEnd();

            // I think we want to set this after calling base.Initialize so that the base
            // has a chance to set values on derived objects
            PerformancePluginCodeGenerator.GenerateStart("Reset Variables");
            // Now that variables are set, we can record reset variables
            NamedObjectSaveCodeGenerator.AssignResetVariables(codeBlock, saveObject);
            PerformancePluginCodeGenerator.GenerateEnd();

            PerformancePluginCodeGenerator.GenerateStart("AddToManagers");


            #region If shouldCallAddToManagers, call AddToManagers
            bool shouldCallAddToManagers = !saveObject.InheritsFromElement();
            if (shouldCallAddToManagers)
            {
                var ifBlock = codeBlock
                    .If("addToManagers");
                if (saveObject is ScreenSave)
                {
                    ifBlock.Line("AddToManagers();");
                }
                else
                {
                    ifBlock.Line("AddToManagers(null);");
                }
            }

            #endregion
            PerformancePluginCodeGenerator.GenerateEnd();

            PerformancePluginCodeGenerator.GenerateEndTimingInitialize(saveObject, codeBlock);

            return codeBlock;
        }

        
        internal static void GenerateAddToManagers(IElement saveObject, ICodeBlock codeBlock)
        {
            ICodeBlock currentBlock = codeBlock;

            bool isEntity = saveObject is EntitySave;
            bool isScreen = !isEntity;

            bool inheritsFromNonFrbType = 
                !string.IsNullOrEmpty(saveObject.BaseElement) && !saveObject.InheritsFromFrbType();
            GenerateReAddToManagers(saveObject, currentBlock);


            #region Generate the method header

            if (isScreen)
            {
                currentBlock = currentBlock
                    .Function("public override void", "AddToManagers", "");
            }
            else if (saveObject.InheritsFromElement()) // it's an EntitySave
            {
                currentBlock = currentBlock
                    .Function("public override void", "AddToManagers", "FlatRedBall.Graphics.Layer layerToAddTo");
            }
            else // It's a base EntitySave
            {
                currentBlock = currentBlock
                    .Function("public virtual void", "AddToManagers", "FlatRedBall.Graphics.Layer layerToAddTo");
            }
            #endregion



            PerformancePluginCodeGenerator.SaveObject = saveObject;
            PerformancePluginCodeGenerator.CodeBlock = currentBlock;

            PerformancePluginCodeGenerator.GenerateStart("Pooled PostInitialize");

            #region Call PostInitialize *again* if this is a pooled, base Entity

            // May 24, 2022
            // This code is quite
            // old, but I believe this
            // is necessary because it re-initializes
            // the entity after being destroyed. "old" recycled
            // entities may have their internal objects shifted around,
            // so a post-init will reset them. 
            FactoryCodeGenerator.CallPostInitializeIfNecessary(saveObject, currentBlock);


            #endregion

            PerformancePluginCodeGenerator.GenerateEnd();

            PerformancePluginCodeGenerator.GenerateStart("Layer for this code");


            #region Generate layer if a screen

            if (IsOnOwnLayer(saveObject))
            {
                // Only Screens need to define a layer.  Otherwise, the layer is fed to the Entity
                if (isScreen)
                {
                    currentBlock.Line("mLayer = SpriteManager.AddLayer();");
                }
            }

            #endregion

            #region Assign the layer so that custom code can get to it

            if (isEntity)
            {
                currentBlock.Line("LayerProvidedByContainer = layerToAddTo;");
            }


            #endregion

            PerformancePluginCodeGenerator.GenerateEnd();

            GenerateAddThisEntityToManagers(saveObject, currentBlock);

            const string addFilesToManagers = "Add Files to Managers";
            PerformancePluginCodeGenerator.GenerateStart(saveObject, currentBlock, addFilesToManagers);


            // Add referenced files before adding objects because the objects
            // may be aliases for the files (if using Entire File) and may add them
            // to layers.
            ReferencedFileSaveCodeGenerator.GenerateAddToManagersStatic(
                currentBlock, saveObject);

            PerformancePluginCodeGenerator.GenerateEnd(saveObject, currentBlock, addFilesToManagers);
            PerformancePluginCodeGenerator.GenerateStart("Create layer instances");

            #region First generate all code for Layers before other stuff
            // We want the code for Layers to be generated before other stuff
            // since Layes may be used when generating the objects
            for (int i = 0; i < saveObject.NamedObjects.Count; i++)
            {
                NamedObjectSave nos = saveObject.NamedObjects[i];

                if ( nos.SourceType == SourceType.FlatRedBallType && nos.GetAssetTypeInfo()?.FriendlyName == "Layer")
                {
                    NamedObjectSaveCodeGenerator.WriteAddToManagersForNamedObject(saveObject, nos, currentBlock);

                    foreach (CustomVariable customVariable in saveObject.CustomVariables)
                    {
                        if (customVariable.SourceObject == nos.InstanceName)
                        {
                            CustomVariableCodeGenerator.AppendAssignmentForCustomVariableInElement(currentBlock, customVariable, saveObject);
                        }
                    }    
                }
            }
            #endregion
            PerformancePluginCodeGenerator.GenerateEnd();

            PerformancePluginCodeGenerator.GenerateStart("General AddToManagers code");

            foreach (ElementComponentCodeGenerator codeGenerator in CodeGenerators
                .OrderBy(item => (int)item.CodeLocation)
                .Where(item => item.CodeLocation != CodeLocation.AfterStandardGenerated))
            {
                codeGenerator.GenerateAddToManagers(currentBlock, saveObject);
            }
            PerformancePluginCodeGenerator.GenerateEnd();

            PerformancePluginCodeGenerator.GenerateStart("Add to managers base and bottom up");

            if ( saveObject.InheritsFromElement())
            {
                if (saveObject is ScreenSave)
                {
                    currentBlock.Line("base.AddToManagers();");

                }
                else
                {
                    currentBlock.Line("base.AddToManagers(layerToAddTo);");
                }
            }
            else
            {
                if (isScreen)
                {
                    if (! saveObject.InheritsFromElement())
                    {
                        // Screen will always call base.AddToManagers so that
                        // Screen.cs gets a chance to set up its timing
                        currentBlock.Line("base.AddToManagers();");
                    }
                    currentBlock.Line("AddToManagersBottomUp();");

                    if(!saveObject.InheritsFromElement())
                    {
                        if (GlueState.Self.CurrentGlueProject.FileVersion >= (int)GlueProjectSave.GluxVersions.SupportsEditMode)
                        {
                            currentBlock.Line("BeforeCustomInitialize?.Invoke();");
                        }
                    }
                }
                else
                {
                    currentBlock.Line("AddToManagersBottomUp(layerToAddTo);");
                }
            }


            foreach (ElementComponentCodeGenerator codeGenerator in CodeGenerators
                .OrderBy(item => (int)item.CodeLocation)
                .Where(item => item.CodeLocation == CodeLocation.AfterStandardGenerated))
            {
                codeGenerator.GenerateAddToManagers(currentBlock, saveObject);
            }



            PerformancePluginCodeGenerator.GenerateEnd();


            // The code for custom variables
            // used to be up in Initialize, but
            // it probably belongs in AddToManagers.
            // See the note in Initialize for more information.

            // UPDATE:  Nevermind, we don't want custom variable
            // setting to be done here because if so then the variables
            // won't be available to the user in CustomInitialize
            PerformancePluginCodeGenerator.GenerateStart("Custom Initialize");
            currentBlock.Line("CustomInitialize();");
            PerformancePluginCodeGenerator.GenerateEnd();
            
        }

        public static bool IsOnOwnLayer(IElement element)
        {
            if (element is EntitySave)
            {
                // The AddToManagers for EntitySaves takes a layer.  We should always
                // use this argument, but make sure all methods that take layered arguments
                // can work with null
                return true;

            }
            else
            {
                return (element as ScreenSave).IsOnOwnLayer;
            }
        }

        private static void GenerateReAddToManagers(IElement saveObject, ICodeBlock currentBlock)
        {
            bool isEntity = saveObject is EntitySave;

            bool inheritsFromNonFrbType =
                !string.IsNullOrEmpty(saveObject.BaseElement) && !saveObject.InheritsFromFrbType();

            if (isEntity)
            {
                ICodeBlock reAddToManagers = null;

                if (inheritsFromNonFrbType)
                {
                    reAddToManagers = currentBlock.Function("public override void", "ReAddToManagers", "FlatRedBall.Graphics.Layer layerToAddTo");
                    reAddToManagers.Line("base.ReAddToManagers(layerToAddTo);");
                }
                else
                {
                    reAddToManagers = currentBlock.Function("public virtual void", "ReAddToManagers", "FlatRedBall.Graphics.Layer layerToAddTo");
                    reAddToManagers.Line("LayerProvidedByContainer = layerToAddTo;");

                }

                // add "this" to managers:
                GenerateAddThisEntityToManagers(saveObject, reAddToManagers);

                for (int i = 0; i < saveObject.NamedObjects.Count; i++)
                {
                    NamedObjectSave nos = saveObject.NamedObjects[i];

                    bool setVariables = false;
                    NamedObjectSaveCodeGenerator.WriteAddToManagersForNamedObject(saveObject, nos, reAddToManagers, false, setVariables);
                }
            }
        }

        private static void GenerateAddThisEntityToManagers(IElement saveObject, ICodeBlock currentBlock)
        {
            bool isEntity = saveObject is EntitySave;
            if (isEntity)
            {
                var entitySave = saveObject as EntitySave;

                PerformancePluginCodeGenerator.GenerateStart("Add this to managers");

                if (saveObject.InheritsFromFrbType())
                {
                    AssetTypeInfo ati = AvailableAssetTypes.Self.GetAssetTypeFromRuntimeType(saveObject.BaseObject, saveObject);

                    if (ati != null)
                    {
                        int addMethodIndex = 0;

                        var isContainerNos = saveObject.AllNamedObjects.FirstOrDefault(item => item.IsContainer);

                        if (isContainerNos != null && isContainerNos.IsZBuffered &&
                            (isContainerNos.SourceClassType == "Sprite" || isContainerNos.SourceClassType == "SpriteFrame"))
                        {
                            addMethodIndex = 1;
                        }

                        if(entitySave.IsManuallyUpdated)
                        {
                            if (!string.IsNullOrEmpty(ati.AddManuallyUpdatedMethod))
                            {
                                var line = ati.AddManuallyUpdatedMethod
                                    .Replace("{THIS}", "this")
                                    .Replace("{LAYER}", "layerToAddTo") + ';';
                                currentBlock.Line(line);
                            }
                            else
                            {
                                // not adding this to managers 
                            }

                        }
                        else if(ati.AddToManagersFunc != null)
                        {
                            currentBlock.Line(ati.AddToManagersFunc(saveObject, null, null, "layerToAddTo"));
                        }
                        else if (ati.LayeredAddToManagersMethod.Count != 0)
                        {
                            // just use the method as-is, because the template is already using "this"
                            currentBlock.Line(ati.LayeredAddToManagersMethod[addMethodIndex].Replace("mLayer", "layerToAddTo") + ";");
                        }
                        else if (ati.AddToManagersMethod.Count != 0)
                        {
                            currentBlock.Line(ati.AddToManagersMethod[addMethodIndex] + ";");
                        }
                    }
                }
                else if (!saveObject.InheritsFromElement())
                {
                    if(entitySave.IsManuallyUpdated)
                    {
                        currentBlock.Line("// This entity skips adding itself to FRB Managers because it has its IsManuallyUpdated property set to true");
                    }
                    else
                    {
                        currentBlock.Line("FlatRedBall.SpriteManager.AddPositionedObject(this);");
                    }
                }


                IWindowCodeGenerator.TryGenerateAddToManagers(currentBlock, saveObject);

                PerformancePluginCodeGenerator.GenerateEnd();
            }
        }


        internal static void GenerateActivity(ICodeBlock codeBlock, IElement saveObject)
        {

            string activityPre = "public virtual void";
            string activityParameters = "";

            if (saveObject is ScreenSave)
            {
                activityPre = "public override void";
                activityParameters = "bool firstTimeCalled";
            }
            else if (saveObject.InheritsFromElement())
            {
                activityPre = "public override void";
            }
            codeBlock = codeBlock.Function(activityPre, "Activity", activityParameters);

            #region Plugin code generation before standard generation

            List<PluginManagerBase> pluginManagers = PluginManager.GetInstances();
            var currentBlock = codeBlock;

            foreach (PluginManager pluginManager in pluginManagers)
            {

                CodeLocation codeLocation = CodeLocation.BeforeStandardGenerated;
                CodeGeneratorPluginMethods.GenerateActivityPluginCode(codeLocation, pluginManager, codeBlock, saveObject);
            }
            #endregion

            if (saveObject is ScreenSave)
            {


                currentBlock = currentBlock
                    .If("!IsPaused");

                CodeWriter.GenerateGeneralActivity(currentBlock, saveObject);

                currentBlock = currentBlock
                    .End();


                currentBlock = currentBlock
                    .Else();

                GeneratePauseIgnoringActivity(currentBlock, saveObject);

                currentBlock = currentBlock
                    .End();

                currentBlock.Line("base.Activity(firstTimeCalled);");
                currentBlock
                    .If("!IsActivityFinished")
                        .Line("CustomActivity(firstTimeCalled);");

            }
            else
            {
                CodeWriter.GenerateGeneralActivity(currentBlock, saveObject);

                currentBlock.Line("CustomActivity();");

            }


            CodeWriter.GenerateAfterActivity(codeBlock, saveObject);
            
        }

        static void GenerateActivityEditMode(ICodeBlock codeBlock, GlueElement saveObject)
        {

            string activityPre = "public virtual void";
            string activityParameters = "";

            var inherits = saveObject is ScreenSave || saveObject.InheritsFromElement();

            if (inherits)
            {
                activityPre = "public override void";
            }
            

            var currentBlock = codeBlock.Function(activityPre, "ActivityEditMode", activityParameters);

            if(saveObject is ScreenSave)
            {
                currentBlock = currentBlock.If("FlatRedBall.Screens.ScreenManager.IsInEditMode");

            }

            if(GlueState.Self.CurrentGlueProject.FileVersion >= (int)GlueProjectSave.GluxVersions.IEntityInFrb)
            {
                if(saveObject is ScreenSave && !saveObject.InheritsFromElement())
                {
                    var foreachBlock = currentBlock.ForEach($"var item in FlatRedBall.SpriteManager.ManagedPositionedObjects");
                    var foreachIfBlock = foreachBlock.If("item is FlatRedBall.Entities.IEntity entity");
                    foreachIfBlock.Line("entity.ActivityEditMode();");
                }
            }
            else
            {
                // Old version (before file version 10 in Dec 24 2021) required code gen to call custom activity.
                foreach(var nos in saveObject.NamedObjects)
                {
                    if(!nos.DefinedByBase && !nos.IsDisabled)
                    {
                        if(nos.SourceType == SourceType.Entity)
                        {
                            currentBlock.Line($"{nos.InstanceName}.ActivityEditMode();");
                        }
                        else if(nos.IsList && ObjectFinder.Self.GetEntitySave(nos.SourceClassGenericType) != null)
                        {
                            var foreachBlock = currentBlock.ForEach($"var item in {nos.InstanceName}");
                            foreachBlock.Line($"item.ActivityEditMode();");
                        }
                    }
                }
            }

            foreach (var codeGenerator in CodeGenerators)
            {
                codeGenerator.GenerateActivityEditMode(currentBlock, saveObject);
            }

            currentBlock.Line("CustomActivityEditMode();");



            if(inherits)
            {
                currentBlock.Line("base.ActivityEditMode();");
            }
        }

        internal static ICodeBlock GenerateGeneralActivity(ICodeBlock codeBlock, IElement saveObject)
        {


            bool isEntity = saveObject is EntitySave;

            EntitySave entitySave = saveObject as EntitySave;

            // This code might seem a little weird.  The reason we do this
            // is because when an Entity is paused, it has a method that is
            // called.  However, when it is unpaused, there's just an instruction
            // that is executed - there is no event.  But if a Screen is paused, then
            // objects within that Screen don't get unpaused....so we're going to bet on
            // the Activity function only being called in unpaused Screens.  If this causes
            // probelsm we may have to make something a little more standard like an Unpause
            // method.
            if (isEntity &&
                (entitySave.ImplementsIClickable || entitySave.ImplementsIWindow)
                && !entitySave.GetInheritsFromIWindowOrIClickable()
                )
            {
                codeBlock.Line("mIsPaused = false;");
            }

            #region Call base.Activity if it has a derived object

            // We only need to do this for EntitySaves.  Screens inherit from the
            // Screen class so they ALWAYS call base.Activity.  It's in the generated
            // Screen template.  
            if ( saveObject.InheritsFromEntity())
            {
                codeBlock.Line("base.Activity();");
            }

            #endregion

            codeBlock._();

            // Eventually do we want to move this in the generate activity for custom variable code gen.
            CustomVariableCodeGenerator.WriteVelocityForCustomVariables(saveObject.CustomVariables, codeBlock);


            foreach (ElementComponentCodeGenerator codeGenerator in CodeWriter.CodeGenerators)
            {

                codeGenerator.GenerateActivity(codeBlock, saveObject);
            }


            return codeBlock;
        }

        internal static void GenerateDestroy(IElement saveObject, ICodeBlock codeBlock)
        {

            string destroyPre = "public virtual void";

            bool destroyInherits = saveObject is ScreenSave || saveObject.InheritsFromElement();

            if (destroyInherits)
            {
                destroyPre = "public override void";
            }

            codeBlock = codeBlock.Function(destroyPre, "Destroy", "");

            bool isScreen = saveObject is ScreenSave;
            var currentBlock = codeBlock;


            foreach (ElementComponentCodeGenerator codeGenerator in CodeWriter.CodeGenerators
                // eventually split these up:
                .Where(item => item.CodeLocation == CodeLocation.BeforeStandardGenerated))
            {
                codeGenerator.GenerateDestroy(currentBlock, saveObject);
            }


            #region Call base.Destroy if it has a derived object

            if (saveObject.InheritsFromEntity() || saveObject is ScreenSave)
            {
                currentBlock.Line("base.Destroy();");
            }

            #endregion


            #region If Entity, remove from managers (SpriteManager, GuiManager)

            if (saveObject is EntitySave)
            {
                if (saveObject.InheritsFromFrbType())
                {
                    AssetTypeInfo ati = AvailableAssetTypes.Self.GetAssetTypeFromRuntimeType(saveObject.BaseObject, saveObject);

                    if (ati != null)
                    {
                        currentBlock.Line(ati.DestroyMethod + ";");
                    }
                }
                else if (!saveObject.InheritsFromElement())
                {
                    currentBlock.Line("FlatRedBall.SpriteManager.RemovePositionedObject(this);");
                }

                if ((saveObject as EntitySave).ImplementsIWindow && !(saveObject as EntitySave).GetInheritsFromIWindow())
                {
                    currentBlock.Line("FlatRedBall.Gui.GuiManager.RemoveWindow(this);");
                }

            }

            #endregion

            foreach (ElementComponentCodeGenerator codeGenerator in CodeWriter.CodeGenerators
                // eventually split these up:
                .Where(item => item.CodeLocation == CodeLocation.AfterStandardGenerated || item.CodeLocation == CodeLocation.StandardGenerated))
            {
                codeGenerator.GenerateDestroy(currentBlock, saveObject);
            }

            // Sept 9, 2022
            // Not sure if this should be at the beginning or end, but adding this
            // at the end so it doesn't interrupt any other unload code:
            GenerateUnloadContentManager(saveObject as GlueElement, currentBlock);

            codeBlock.Line("CustomDestroy();");
        }

        private static void GenerateUnloadContentManager(GlueElement saveObject, ICodeBlock currentBlock)
        {
            var shouldUnload = 
                saveObject is ScreenSave screenSave && 
                screenSave.UseGlobalContent == false &&
                screenSave.ReferencedFiles.Any(item => item.LoadedOnlyWhenReferenced);

            // This code could be in a screen that is the base
            // for a derived screen (such as GameScreen for Level1)
            // In that case, Level1 would use its own content manager,
            // but when the LoadedOnlyWhenReferenced property is accessed,
            // the base content manager would get used. Screens automatically
            // clean up their content managers at the engine level, but only the
            // content manager speicified by the most derived.

            if (shouldUnload)
            {
                currentBlock.Line($"FlatRedBall.FlatRedBallServices.Unload(\"{saveObject.ClassName}\");");
            }
        }

        public static ICodeBlock CreateClass(ClassProperties classProperties)
        {
            return CreateClass(classProperties.NamespaceName, classProperties.ClassName, classProperties.Partial, classProperties.Members,
                classProperties.IsStatic, classProperties.UsingStatements, classProperties.UntypedMembers,
                classProperties.MethodContent);

        }

        public static ICodeBlock CreateClass(string namespaceName, string className, List<TypedMemberBase> members)
        {
            return CreateClass(namespaceName, className, false, members, false,
                new List<string>(), new Dictionary<string, string>(), null);
        }

        public static ICodeBlock CreateClass(string namespaceName, string className, bool isPartial, List<TypedMemberBase> members,
            bool isStatic, List<string> usingStatements, Dictionary<string, string> untypedMembers, ICodeBlock methodContent)
        {
            var codeBlock = new CodeDocument();

            #region Append Using Statements
            foreach(var usingStatement in usingStatements.Distinct())
            {
                codeBlock.Line("using " + usingStatement + ";");
            }
            #endregion

            #region Append Namespace

            codeBlock._();

            ICodeBlock currentBlock = codeBlock;

            currentBlock = currentBlock.Namespace(namespaceName);

            #endregion

            #region Append class header

            currentBlock = currentBlock.Class(className, Public: true, Static: isStatic, Partial: isPartial);

            #endregion

            for (int i = 0; i < members.Count; i++)
            {
                TypedMemberBase member = members[i];


                bool isPublic = member.Modifier == Modifier.Public;
                bool isPrivate = member.Modifier == Modifier.Private;
                bool isInternal = member.Modifier == Modifier.Internal;

                string memberType = member.MemberType.ToString();

                memberType = PrepareTypeToBeWritten(member, memberType);

                // We used to remove whitespace here,
                // but the member name may contain an assignment.
                // In that case we want spaces preserved.  Whatever
                // calls this method is in charge of removing whitespace.
                string memberName = member.MemberName;

                currentBlock.Line(StringHelper.Modifiers(
                    Public: isPublic, 
                    Private: isPrivate,
                    Internal: isInternal,
                    Static: isStatic, 
                    Type: memberType, 
                    Name: memberName) + ";");
            }

            foreach (KeyValuePair<string, string> kvp in untypedMembers)
            {
                string memberName = kvp.Key;
                string type = kvp.Value;


                bool isPublic = !memberName.StartsWith("m");

                currentBlock.Line(StringHelper.Modifiers(Public: isPublic, Static: isStatic, Type: type, Name: memberName) + ";");
            }


            if (methodContent == null)
            {
                currentBlock.Tag("Methods");
            }
            else
            {
                currentBlock.InsertBlock(methodContent);
            }

            currentBlock._()._();

            currentBlock.Replace(" System.Single ", " float ");
            currentBlock.Replace(" System.Boolean ", " bool ");
            currentBlock.Replace(" System.Int32 ", " int ");
            currentBlock.Replace(" System.String ", " string ");

            if(members.Any(item => item.MemberName == "Name" && item.MemberType == typeof(string)))
            {
                currentBlock.Line("public override string ToString() => Name;");
            }

            return codeBlock;
        }

        private static string PrepareTypeToBeWritten(TypedMemberBase member, string memberType)
        {
            if (memberType.Contains("`1"))
            {
                // This is generic
                string name = memberType.Substring(0, memberType.IndexOf('`'));

                // We want to use FullName rather than Name so we don't rely on using's in generated code
                //string genericContents = PrepareTypeToBeWritten(null, member.MemberType.GetGenericArguments()[0].Name);
                string genericContents = PrepareTypeToBeWritten(null, member.MemberType.GetGenericArguments()[0].FullName);

                memberType = string.Format("{0}<{1}>", name, genericContents);
            }
            else if (memberType.Contains("<"))
            {
                string name = memberType.Substring(0, memberType.IndexOf('<'));

                // See above
                //string genericContents = PrepareTypeToBeWritten(null, member.MemberType.GetGenericArguments()[0].Name);
                string genericContents = PrepareTypeToBeWritten(null, member.MemberType.GetGenericArguments()[0].FullName);

                memberType = string.Format("{0}<{1}>", name, genericContents);
            }
            else
            {
                memberType = TypeManager.GetCommonTypeName(memberType);
            }

            // Fully qualify FRB names to prevent clashes with 
            if(memberType.Contains("FlatRedBall."))
            {
                memberType = "global::" + memberType;
            }


            return memberType;
        }



        public static void SetBaseClass(string fileName, string baseClass)
        {
            string fileContents;

            SetBaseClass(fileName, baseClass, out fileContents);

            FileManager.SaveText(fileContents, fileName + ".Generated.cs");
        }

        public static void SetBaseClass(string fileName, string baseClass, out string fileContents)
        {
            SetBaseClass(fileName, baseClass, true, out fileContents);
        }

        public static void SetBaseClass(string fileName, string baseClass, bool overwrite, out string fileContents)
        {
            fileContents = FileManager.FromFileText(fileName + ".Generated.cs");

            SetBaseClass(ref fileContents, baseClass, overwrite);

        }

        public static void SetBaseClass(ref string fileContents, string baseClass, bool overwrite)
        {
            string wordAfter = StringFunctions.GetWordAfter(" : ", fileContents);

            if (overwrite)
            {
                fileContents = fileContents.Replace(" : " + wordAfter, " : " + baseClass);
            }
            else
            {
                string contents = " : " + wordAfter;
                int index = fileContents.IndexOf(contents) + contents.Length;
                fileContents = fileContents.Insert(index, ", " + baseClass);
            }

        }

        public static void InitializeStaticData(string relativeGameFileName)
        {
            if (string.IsNullOrEmpty(relativeGameFileName))
            {
                return;
            }
            var gameFilePath = new FilePath(GlueState.Self.CurrentMainProject.Directory + relativeGameFileName);

            string contents = FileManager.FromFileText(gameFilePath.FullPath);
            var contentsBeforeChange = contents;
            var gluxVersion = GlueState.Self.CurrentGlueProject.FileVersion;
            if(gluxVersion < (int)GlueProjectSave.GluxVersions.HasGame1GenerateEarly)
            {
                AddGlobalContentInitializeInCustomCode(ref contents);
            }

            if(contents != contentsBeforeChange)
            {
                if (new FileInfo(gameFilePath.FullPath).IsReadOnly)
                {
                    GlueGui.ShowMessageBox("The file\n\n" + gameFilePath + "\n\nis read-only, so Glue can't generate code");
                }
                else
                {
                    FileWatchManager.IgnoreNextChangeOnFile(gameFilePath.Standardized);
                    try
                    {
                        GlueCommands.Self.TryMultipleTimes(() =>
                        {
                            FileManager.SaveText(contents, gameFilePath.FullPath);
                        });
                    }
                    catch(Exception e)
                    {
                        // If we failed, save a backup
                        FileManager.SaveText(contents, gameFilePath.FullPath + ".Backup");
                        throw e;
                    }
                }
            }
        }

        private static void AddGlobalContentInitializeInCustomCode(ref string contents)
        {
            string lineToReplaceWith = "            " + "GlobalContent.Initialize();";

            if (contents.Contains("GlobalContent.Initialize"))
            {
                StringFunctions.ReplaceLine(ref contents, "GlobalContent.Initialize", lineToReplaceWith);
            }
            else
            {
                // We gotta find where to put the start call.  This should be after 
                // FlatRedBallServices.InitializeFlatRedBall

                int index = CodeParser.GetIndexAfterFlatRedBallInitialize(contents);

                if (index == -1)
                {
                    throw new CodeParseException("Could not find FlatRedBall.Initialize in the Game file.  Did you delete this?  " +
                        "Glue requires this call to be in the Game class. You must manually add this call and reload Glue.");
                }
                contents = contents.Insert(index, lineToReplaceWith + Environment.NewLine);
            }

        }

        internal static string ReplaceNamespace(string fileContents, string newNamespace)
        {
            string throwaway;
            return ReplaceNamespace(fileContents, newNamespace, out throwaway);
        }

        internal static string ReplaceNamespace(string fileContents, string newNamespace, out string oldNamespace)
        {
            int indexOfNamespaceKeyword = fileContents.IndexOf("namespace ");
            oldNamespace = "";
            if(indexOfNamespaceKeyword != -1)
            {


                int indexOfNamespaceStart = indexOfNamespaceKeyword + "namespace ".Length;

                int indexOfSlashR = fileContents.IndexOf("\r", indexOfNamespaceStart);
                int indexOfSlashN = fileContents.IndexOf("\n", indexOfNamespaceStart);

                int indexOfEndOfNamespace = indexOfNamespaceStart;

                if (indexOfSlashR == -1)
                {
                    indexOfEndOfNamespace = indexOfSlashN;
                }
                else if(indexOfSlashN == -1)
                {
                    indexOfEndOfNamespace = indexOfSlashR;
                }
                else
                {
                    indexOfEndOfNamespace = System.Math.Min(indexOfSlashR, indexOfSlashN);
                }

                oldNamespace = fileContents.Substring(indexOfNamespaceStart, indexOfEndOfNamespace - indexOfNamespaceStart);


                fileContents = fileContents.Remove(indexOfNamespaceStart, indexOfEndOfNamespace - indexOfNamespaceStart);

                fileContents = fileContents.Insert(indexOfNamespaceStart, newNamespace);

            }
            return fileContents;
        }


        public static void SetClassNameAndNamespace(string projectNamespace, string elementName, StringBuilder templateCode)
        {
            SetClassNameAndNamespace(projectNamespace, elementName, templateCode, false, "\"Global\"", null);
        }

        public static void SetClassNameAndNamespace(string classNamespace, string elementName, StringBuilder templateCode, bool useGlobalContent, string replacementContentManagerName, string inheritance)
        {

            string namespaceToReplace = StringFunctions.GetWordAfter("namespace ", templateCode);
            bool isScreen = namespaceToReplace.Contains("Screen");
                
            string classNameToReplace = StringFunctions.GetWordAfter("public partial class ", templateCode);
            if (isScreen)
            {
                templateCode.Replace("namespace " + namespaceToReplace,
                    "namespace " + classNamespace);





                if (useGlobalContent)
                {
                    // replace the content mangaer name with the global content manager
                    templateCode.Replace("\"" + classNameToReplace + "\"", replacementContentManagerName);

                }
            }
            else
            {
                string whatToReplaceWith = "";

                //if (projectNamespace.Contains(".Entities."))
                // Not sure why we require the period at the end of Entities
                if(classNamespace.Contains(".Entities") && classNamespace.IndexOf('.') == classNamespace.IndexOf(".Entities"))
                {
                    // This is a full namespace.  Okay, let's just use that
                    whatToReplaceWith = "namespace " + classNamespace;
                }
                else
                {
                    // We gotta put Entities at the end ourselves
                    whatToReplaceWith = "namespace " + classNamespace + ".Entities";
                }

                templateCode.Replace("namespace " + namespaceToReplace,
                    whatToReplaceWith);
            }

            if (!string.IsNullOrEmpty(inheritance))
            {
                templateCode.Replace(classNameToReplace,
                 elementName);

                var indexOfClass = templateCode.IndexOf("class " + elementName);

                if (indexOfClass != -1)
                {
                    int length = ("class " + elementName).Length;
                    templateCode.Insert(indexOfClass + length, " : " + inheritance);
                }

            }
            else
            {
                templateCode.Replace(classNameToReplace,
                    elementName);
            }
        }

        internal static void RefreshStartupScreenCode()
        {
            // If there is a required screen, then use that
            ScreenSave requiredScreen = null;

            for (int i = 0; i < ProjectManager.GlueProjectSave.Screens.Count; i++)
            {
                ScreenSave screenSave = ProjectManager.GlueProjectSave.Screens[i];

                if (screenSave.IsRequiredAtStartup)
                {
                    requiredScreen = screenSave;
                    break;
                }
            }

            var screenName = requiredScreen?.Name ?? 
                GlueCommands.Self.GluxCommands.StartUpScreenName;

            CodeWriter.SetStartUpScreen(
                ProjectManager.GameClassFileName,
                screenName);
        }

        private static void SetStartUpScreen(string gameFileName, string startUpScreen)
        {
            bool success = true;

            string contents = null;
            try
            {
                contents = FileManager.FromFileText(FileManager.RelativeDirectory + gameFileName);
            }
            catch(Exception e)
            {

                PluginManager.ReceiveError(e.ToString());
                success = false;
            }

            if (success)
            {

                #region Get the lineThatStartsEverything

                // isEmpty needs to get set
                // *before* prepending the ProjectNamespace
                bool isEmpty = string.IsNullOrEmpty(startUpScreen);

                if(!isEmpty)
                {
                    startUpScreen = ProjectManager.ProjectNamespace + "." + startUpScreen.Replace("\\", ".");
                }
                string lineThatStartsEverything =
                    $"            FlatRedBall.Screens.ScreenManager.Start(typeof({startUpScreen}));";

                if (isEmpty)
                {
                    lineThatStartsEverything = "            //FlatRedBall.Screens.ScreenManager.Start(typeof(YourScreenClass));";
                }

                #endregion

                if(contents.Contains("Type startScreenType = "))
                {
                    var line = isEmpty ?
                        $"            Type startScreenType = null;" :
                        $"            Type startScreenType = typeof({startUpScreen});";
                    // new projects (as of October 25 2019) use this multi-line approach
                    StringFunctions.ReplaceLine(ref contents, "Type startScreenType = ", line);

                }
                else if (contents.Contains("ScreenManager.Start"))
                {
                    StringFunctions.ReplaceLine(ref contents, "ScreenManager.Start", lineThatStartsEverything);
                }
                else
                {
                    // We gotta find where to put the start call.  This should be after 
                    // FlatRedBallServices.InitializeFlatRedBall

                    int index = CodeParser.GetIndexAfterFlatRedBallInitialize(contents);
                    contents = contents.Insert(index, lineThatStartsEverything + Environment.NewLine);
                }

                try
                {
                    SaveFileContents(contents, FileManager.RelativeDirectory + gameFileName, true);
                }
                catch (Exception e)
                {
                    PluginManager.ReceiveError(e.ToString());
                    success = false;
                }
            }
        }

        private static bool IsCustomVariableAssignedInAddToManagers(CustomVariable customVariable, IElement saveObject)
        {
            if (!string.IsNullOrEmpty(customVariable.SourceObject))
            {
                NamedObjectSave nos = saveObject.GetNamedObjectRecursively(customVariable.SourceObject);

                if (nos != null)
                {
                    AssetTypeInfo ati = nos.GetAssetTypeInfo();

                    if (ati != null && ati.IsInstantiatedInAddToManagers)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static void GeneratePostInitialize(ICodeBlock codeBlock, IElement saveObject)
        {
            // PostInitialize is a method which can be called multiple times if an entity is pooled. Therefore, any "add" calls here must
            // be protected with if-checks.

            var currentBlock = codeBlock;
            bool inheritsFromElement = saveObject.InheritsFromElement();
            currentBlock = currentBlock
                .Function("PostInitialize", "", Public: true, Override: inheritsFromElement, Virtual: !inheritsFromElement, Type: "void");

            // PostInitialize may happen async - but setting Visible = true on a shape
            // adds it to the ShapeManager.  This is bad because:
            // 1.  It's not thread safe
            // 2.  Another Screen may be visible
            // 3.  The ScreenManager checks for the presence of objects in the managers after a Screen is destroyed.  An addition would (and has) cause a crash here
            currentBlock.Line("bool oldShapeManagerSuppressAdd = FlatRedBall.Math.Geometry.ShapeManager.SuppressAddingOnVisibilityTrue;");
            currentBlock.Line("FlatRedBall.Math.Geometry.ShapeManager.SuppressAddingOnVisibilityTrue = true;");


            // Events need to come first here
            // in case other generators set properties
            // that raise events.
            EventCodeGenerator.GeneratePostInitialize(currentBlock, saveObject);

            if (inheritsFromElement)
            {
                currentBlock.Line("base.PostInitialize();");
            }


            // Do attachments before setting any variables (which may call events)
            NamedObjectSaveCodeGenerator.GetPostInitializeForNamedObjectList(null, 
                // There may be a race condition so handle it by to-listing it
                saveObject.NamedObjects.ToList(), 
                currentBlock, saveObject);

            // July 24, 2013
            // Victor Chelaru
            // Why do we initialize here in PostInitialize?  The variable gets set in AddToManagersBottomUp. 
            // I'm going to remove this to see if it causes problems:
            //for (int i = 0; i < saveObject.CustomVariables.Count; i++)
            //{
            //    CustomVariable customVariable = saveObject.CustomVariables[i];

            //    if (!IsCustomVariableAssignedInAddToManagers(customVariable, saveObject))
            //    {
            //        CustomVariableCodeGenerator.AppendAssignmentForCustomVariableInElement(currentBlock, customVariable, saveObject);
            //    }
            //}
            foreach (ElementComponentCodeGenerator codeGenerator in CodeGenerators
                .OrderBy(item => (int)item.CodeLocation))
            {
                currentBlock = codeGenerator.GeneratePostInitialize(currentBlock, saveObject);
            }
            foreach (ReferencedFileSave rfs in saveObject.ReferencedFiles)
            {
                AssetTypeInfo ati = rfs.GetAssetTypeInfo();

                if (!rfs.IsSharedStatic && ati != null && !string.IsNullOrEmpty(ati.PostInitializeCode))
                {
                    currentBlock.InsertBlock(ReferencedFileSaveCodeGenerator.GetPostInitializeForReferencedFile(rfs));
                }
            }
            currentBlock.Line("FlatRedBall.Math.Geometry.ShapeManager.SuppressAddingOnVisibilityTrue = oldShapeManagerSuppressAdd;");
        }



        public static ICodeBlock GenerateAfterActivity(ICodeBlock codeBlock, IElement saveObject)
        {
            #region Loop through all ReferenceFiles to get their post custom activity code

            for (int i = 0; i < saveObject.ReferencedFiles.Count; i++)
            {
                codeBlock.InsertBlock(ReferencedFileSaveCodeGenerator.GetPostCustomActivityForReferencedFile(saveObject.ReferencedFiles[i]));
            }

            #endregion

            #region Loop through all NamedObjectSaves to get their post custom activity code

            for (int i = 0; i < saveObject.NamedObjects.Count; i++)
            {
                NamedObjectSaveCodeGenerator.GetPostCustomActivityForNamedObjectSave(saveObject, saveObject.NamedObjects[i], codeBlock);
            }


            #endregion



            foreach (PluginManager pluginManager in PluginManager.GetInstances())
            {

                CodeGeneratorPluginMethods.GenerateActivityPluginCode(CodeLocation.AfterStandardGenerated,
                    pluginManager, codeBlock, saveObject);
                
            }
            

            return codeBlock;
        }


        public static string MakeLocalizedIfNecessary(NamedObjectSave namedObject, string variableName, object valueAsObject, string valueAsString, CustomVariable customVariable)
        {
            // This code will convert something like
            //      someVariable = "Hello";
            // to
            //      someVariable = LocalizationManager.Translate("Hello");

            // This code gets called on states.
            // If it's a state, then we find the 
            // custom variable that the state represents
            // and pass it as the last argument in the method.
            // We can look at that custom variable to see if it's
            // an AnimationChain

            var shouldTranslate = false;

            if (ObjectFinder.Self.GlueProject != null && 
                ObjectFinder.Self.GlueProject.UsesTranslation && 
                valueAsObject is string && (customVariable == null || customVariable.Type != "Color"))//&& !namedObject.IsAnimationChain)
            {
                if (customVariable != null && customVariable.GetIsAnimationChain())
                {
                    // do nothing
                }
                else if (namedObject != null && namedObject.SourceType == SourceType.File)
                {
                    if (variableName != "CurrentChain" // CurrentChain is used by some FRB types
                        && variableName != "CurrentChainName" // and CurrentChainName is used by others....sucky.
                        )
                    {
                        shouldTranslate = true;
                    }
                }
                else if (namedObject != null && namedObject.SourceType == SourceType.Entity)
                {
                    EntitySave entitySave = ObjectFinder.Self.GetEntitySave(namedObject.SourceClassType);

                    if (entitySave != null)
                    {
                        CustomVariable variableInEntity = entitySave.GetCustomVariable(variableName);

                        if (variableInEntity == null || variableInEntity.GetIsAnimationChain() == false)
                        {
                            shouldTranslate = true;
                        }
                    }
                }
                else if (namedObject != null && namedObject.SourceType == SourceType.FlatRedBallType)
                {
                    if (namedObject.GetAssetTypeInfo() == AvailableAssetTypes.CommonAtis.Text && variableName == "DisplayText")
                    {
                        shouldTranslate = true;
                    }
                }
                else // March 18, 2022 - 
                {

                }
            }

            if(shouldTranslate)
            {
                valueAsString = "FlatRedBall.Localization.LocalizationManager.Translate(" + valueAsString + ")";
            }

            return valueAsString;
        }



        internal static void GenerateAndAddElementCustomCode(IElement element)
        {
            string fileName = element.Name + ".cs";

            string elementNamespace = ProjectManager.ProjectNamespace;
            string namespaceToAppend = FileManager.GetDirectory(fileName, RelativeType.Relative);
            // remove the trailing '.'
            namespaceToAppend = namespaceToAppend.Substring(0, namespaceToAppend.Length - 1);

            StringBuilder stringBuilder;

            if (element is EntitySave)
            {
                if (namespaceToAppend.Length > "Entities".Length)
                {
                    namespaceToAppend = ".Entities." + namespaceToAppend.Substring("Entities\\".Length).Replace("/", ".").Replace("\\", ".");
                }
                else
                {
                    // I don't know why the code is appending
                    // a '.' at the end of "Entities", this makes
                    // code not compile:
                    //namespaceToAppend = ".Entities.";
                    namespaceToAppend = ".Entities";
                }

                stringBuilder = new StringBuilder(CodeWriter.EntityTemplateCode);


                CodeWriter.SetClassNameAndNamespace(
                    elementNamespace + namespaceToAppend,
                    FileManager.RemovePath(element.Name),
                    stringBuilder);
            }
            else // it's a Screen
            {
                if (namespaceToAppend.Length > "Screens".Length)
                {
                    namespaceToAppend = ".Screens." + namespaceToAppend.Substring("Screens\\".Length).Replace("/", ".").Replace("\\", ".");
                }
                else
                {
                    // See above comment for why this was changed
                    //namespaceToAppend = ".Screens.";
                    namespaceToAppend = ".Screens";
                }

                stringBuilder = new StringBuilder(ScreenTemplateCode);

                CodeWriter.SetClassNameAndNamespace(
                    elementNamespace + namespaceToAppend,
                    FileManager.RemovePath(element.Name),
                    stringBuilder);

            }

            FileManager.SaveText(stringBuilder.ToString(), FileManager.RelativeDirectory + fileName);

        }

        internal static void AddEventCustomCodeFileForElement(IElement element)
        {

            string fileName = element.Name + ".Event.cs";
            string fullFileName = GlueState.Self.CurrentMainProject.Directory + fileName;

            bool save = false; // we'll be doing manual saving after it's created
            ProjectManager.CodeProjectHelper.CreateAndAddPartialCodeFile(fileName, save);

            FileWatchManager.IgnoreNextChangeOnFile(fullFileName);
            FileManager.SaveText("// Empty event file - code will be added here if events are added in Glue", fullFileName);
        }

        internal static void AddEventGeneratedCodeFileForElement(IElement element)
        {

            string fileName = element.Name + ".Generated.Event.cs";
            string fullFileName = GlueState.Self.CurrentMainProject.Directory + fileName;

            bool save = false; // we'll be doing manual saving after it's created
            ProjectManager.CodeProjectHelper.CreateAndAddPartialCodeFile(fileName, save);

            FileWatchManager.IgnoreNextChangeOnFile(fullFileName);
            FileManager.SaveText("// Empty event file - code will be added here if events are added in Glue", fullFileName);
        }


        internal static int GetIndexAfter(string stringToSearchFor, string entireStringToSearchIn)
        {
            int indexOfString = entireStringToSearchIn.IndexOf(stringToSearchFor);
            if (indexOfString == -1)
            {
                return -1;
            }
            else
            {
                int returnValue = indexOfString + stringToSearchFor.Length + 1;
                if (entireStringToSearchIn[returnValue] == '\n')
                {
                    returnValue++;
                }

                return returnValue;
            }
        }

        internal static int GetIndexAfter(string stringToSearchFor, StringBuilder entireStringToSearchIn)
        {
            int indexOfString = entireStringToSearchIn.IndexOf(stringToSearchFor);
            if (indexOfString == -1)
            {
                return -1;
            }
            else
            {
                int returnValue = indexOfString + stringToSearchFor.Length + 1;
                if (returnValue < entireStringToSearchIn.Length && entireStringToSearchIn[returnValue] == '\n')
                {
                    returnValue++;
                }

                return returnValue;
            }
        }


        private static bool IsAnyNamedObjectAttachedToCamera(List<NamedObjectSave> namedObjectList)
        {
            foreach (NamedObjectSave nos in namedObjectList)
            {
                if (nos.AttachToCamera)
                {
                    return true;
                }

                if (IsAnyNamedObjectAttachedToCamera(nos.ContainedObjects))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void GenerateAddToManagersBottomUp(ICodeBlock codeBlock, IElement element, Dictionary<string, string> reusableEntireFileRfses)
        {
            bool isEntity = element is EntitySave;
            bool isScreen = element is ScreenSave;

            bool inheritsFromElement = element.InheritsFromElement();

            string layerArgs = "";

            if (isEntity)
            {
                layerArgs = "FlatRedBall.Graphics.Layer layerToAddTo";
            }

            var currentBlock = codeBlock;

            currentBlock = currentBlock
                .Function("AddToManagersBottomUp", layerArgs, Public: true, Override: inheritsFromElement,
                          Virtual: !inheritsFromElement, Type: "void");




            if (inheritsFromElement)
            {
                if (isEntity)
                {
                    currentBlock.Line("base.AddToManagersBottomUp(layerToAddTo);");
                }
                else
                {
                    currentBlock.Line("base.AddToManagersBottomUp();");
                }
            }
            foreach (ElementComponentCodeGenerator codeGenerator in CodeWriter.CodeGenerators
                .Where(item =>item.CodeLocation == CodeLocation.BeforeStandardGenerated))
            {
                codeGenerator.GenerateAddToManagersBottomUp(currentBlock,element);
            }

            foreach (ElementComponentCodeGenerator codeGenerator in CodeWriter.CodeGenerators
                .Where(item => item.CodeLocation == CodeLocation.StandardGenerated))
            {
                codeGenerator.GenerateAddToManagersBottomUp(currentBlock, element);
            }


            if (isScreen && string.IsNullOrEmpty(element.BaseElement))
            {
                currentBlock.Line("CameraSetup.ResetCamera(SpriteManager.Camera);");
            }

            if (!element.InheritsFromElement())
            {
                currentBlock.Line("AssignCustomVariables(false);");
            }

            foreach (ElementComponentCodeGenerator codeGenerator in CodeWriter.CodeGenerators
                .Where(item => item.CodeLocation == CodeLocation.AfterStandardGenerated))
            {
                codeGenerator.GenerateAddToManagersBottomUp(currentBlock, element);
            }
        }

        public static List<FilePath> GetAllCodeFilesFor(IElement element)
        {
            string directory = FileManager.GetDirectory(GlueCommands.Self.GetAbsoluteFileName(element.Name + "/", false));


            List<FilePath> foundCsFiles = FileManager.GetAllFilesInDirectory(directory, "cs")
                .Select(item => new FilePath(item)).ToList();

            for (int i = foundCsFiles.Count - 1; i > -1; i--)
            {
                var file = foundCsFiles[i];
                string relativeFile = FileManager.MakeRelative(file.Original).Replace('/', '\\');
                bool isValid = relativeFile.StartsWith(element.Name) && relativeFile[element.Name.Length] == '.';

                if (!isValid)
                {
                    foundCsFiles.RemoveAt(i);
                }
            }

            if(element is EntitySave)
            {
                var asEntitySave = element as EntitySave;

                if(asEntitySave.CreatedByOtherEntities)
                {
                    string strippedName = FileManager.RemovePath(element.Name);

                    // This also has a factory, so check for that.
                    var fullName = GlueState.Self.CurrentGlueProjectDirectory + "Factories/" + strippedName + "Factory.Generated.cs";

                    foundCsFiles.Add(fullName);
                }
            }

            return foundCsFiles;

        }

        static void GeneratePauseIgnoringActivity(ICodeBlock codeBlock, IElement saveObject)
        {
            for (int i = 0; i < saveObject.NamedObjects.Count; i++)
            {
                if (saveObject.NamedObjects[i].IgnoresPausing)
                {
                    NamedObjectSaveCodeGenerator.GetActivityForNamedObject(saveObject.NamedObjects[i], codeBlock);
                }
            }
        }


        internal static ICodeBlock GenerateUnloadStaticContent(ICodeBlock codeBlock, IElement saveObject)
        {
            var currentBlock = codeBlock;

            #region Generate UnloadStaticContent

            // Vic says - originally the code would only
            // generate UnloadStaticContent for entities IF they had
            // static content.  Well, we want to simplify the interface 
            // so all entities will always generate this method.  That way
            // all Entities can just clone elements in the loaded data.

            if (saveObject is EntitySave)
            {
                currentBlock = currentBlock
                    .Function("UnloadStaticContent", "", Public: true, Static: true,
                              New: saveObject.InheritsFromElement(), Type: "void");

                // We only want to unload if this isn't using global content
                // If so, then unloading should be a no-op
                if (saveObject.UseGlobalContent == false)
                {

                    foreach (ElementComponentCodeGenerator codeGenerator in CodeWriter.CodeGenerators)
                    {
                        currentBlock = codeGenerator.GenerateUnloadStaticContent(currentBlock, saveObject);
                    }

                }
                else
                {
                    currentBlock.Line("// Intentionally left blank because this element uses global content, so it should never be unloaded");
                }


                // June 21, 2011
                // Previously elements
                // used to call the base
                // UnloadStaticContent, but
                // this is no longer needed now
                // because each Entity will add its
                // own call to UnloadStaticContent to
                // the given ContentManager.
                //if (DoesSaveObjectInherit)
                //{
                //    if (SaveObject is EntitySave)
                //    {
                //        string baseName = (SaveObject as EntitySave).BaseEntity;

                //        stringBuilder.AppendLine(tabs + FileManager.RemovePath(baseName) + ".UnloadStaticContent();");
                //        stringBuilder.AppendLine();
                //    }
                //}
            }

            #endregion
            return codeBlock;
        }

        internal static void GenerateConvertToManuallyUpdated(ICodeBlock codeBlock, IElement saveObject, Dictionary<string, string> reusableEntireFileRfses)
        {
            bool hasBase = saveObject.InheritsFromElement();

            ICodeBlock currentBlock = codeBlock;

            currentBlock = currentBlock
                .Function("ConvertToManuallyUpdated", "", Public: true, Override: hasBase, Virtual: !hasBase,
                          Type: "void");

            if (hasBase)
            {
                currentBlock.Line("base.ConvertToManuallyUpdated();");
            }

            if (saveObject is EntitySave)
            {

                // It's possible that an Entity may be converted to ManuallyUpdated before
                // any Draw calls get made - this means that UpdateDependencies will never get called.
                // This should happen before the other manual updates are called so that everything is positioned
                // right when verts are created.
                currentBlock.Line("this.ForceUpdateDependenciesDeep();");

                if (saveObject.InheritsFromFrbType())
                {
                    AssetTypeInfo ati = AvailableAssetTypes.Self.GetAssetTypeFromRuntimeType(saveObject.BaseElement, saveObject);

                    if (ati != null)
                    {
                        currentBlock.Line(ati.MakeManuallyUpdatedMethod + ";");
                    }
                }
                else
                {

                    // Convert the Entity itself to manually updated
                    currentBlock.Line("FlatRedBall.SpriteManager.ConvertToManuallyUpdated(this);");
                }
            }

            foreach (ReferencedFileSave rfs in saveObject.ReferencedFiles)
            {
                ReferencedFileSaveCodeGenerator.GenerateConvertToManuallyUpdated(currentBlock, rfs);
            }

            NamedObjectSaveCodeGenerator.WriteConvertToManuallyUpdated(currentBlock, saveObject, reusableEntireFileRfses);
        }


        internal static void GenerateMethods(ICodeBlock codeBlock, IElement element)
        {
            var currentBlock = codeBlock;


            CodeWriter.GeneratePostInitialize(codeBlock, element);

            CodeWriter.GenerateAddToManagersBottomUp(currentBlock, element, ReusableEntireFileRfses);

            CodeWriter.GenerateRemoveFromManagers(currentBlock, element);

            GenerateAssignCustomVariables(codeBlock, element);

            CodeWriter.GenerateConvertToManuallyUpdated(currentBlock, element, ReusableEntireFileRfses);

            CodeWriter.GenerateLoadStaticContent(currentBlock, element);

            currentBlock = CodeWriter.GenerateUnloadStaticContent(currentBlock, element);

            if(element is ScreenSave)
            {
                GeneratePauseThisScreen(currentBlock, element);

                GenerateUnpauseThisScreen(currentBlock, element);
            }

            GenerateUpdateDependencies(currentBlock, element);

            foreach (ElementComponentCodeGenerator codeGenerator in CodeWriter.CodeGenerators)
            {
                // I see no reason to take the code block
                //currentBlock = codeGenerator.GenerateAdditionalMethods(currentBlock, element);
                codeGenerator.GenerateAdditionalMethods(currentBlock, element);
            }

            currentBlock.Line("partial void CustomActivityEditMode();");

            foreach (PluginManager pluginManager in PluginManager.GetInstances())
            {
                CodeGeneratorPluginMethods.GenerateAdditionalMethodsPluginCode(pluginManager, codeBlock, element);
            }

        }

        private static void GeneratePauseThisScreen(ICodeBlock currentBlock, IElement element)
        {
            var methodBlock = currentBlock.Function("public override void", "PauseThisScreen", "");

            foreach(var generator in CodeWriter.CodeGenerators)
            {
                generator.GeneratePauseThisScreen(methodBlock, element);
            }

            methodBlock.Line("base.PauseThisScreen();");
        }

        private static void GenerateUnpauseThisScreen(ICodeBlock currentBlock, IElement element)
        {
            var methodBlock = currentBlock.Function("public override void", "UnpauseThisScreen", "");

            foreach (var generator in CodeWriter.CodeGenerators)
            {
                generator.GenerateUnpauseThisScreen(methodBlock, element);
            }


            methodBlock.Line("base.UnpauseThisScreen();");

        }

        private static void GenerateUpdateDependencies(ICodeBlock currentBlock, IElement element)
        {
            // screens will need this too:
            

            var innerBlock = new CodeBlockBase(null);

            foreach(var generator in CodeWriter.CodeGenerators)
            {
                generator.GenerateUpdateDependencies(innerBlock, element);
            }

            if(innerBlock.BodyCodeLines.Any())
            {
                var methodBlock = currentBlock.Function("public override void", "UpdateDependencies", "double currentTime");

                methodBlock.InsertBlock(innerBlock);

                methodBlock.Line("CustomUpdateDependencies(currentTime);");

                currentBlock.Line("partial void CustomUpdateDependencies(double currentTime);");

            }
        }

        

        private static void GenerateRemoveFromManagers(ICodeBlock currentBlock, IElement saveObject)
        {
            if (saveObject.InheritsFromElement())
            {
                currentBlock = currentBlock.Function("public override void", "RemoveFromManagers", "");
                currentBlock.Line("base.RemoveFromManagers();");
            }
            else
            {
                currentBlock = currentBlock.Function("public virtual void", "RemoveFromManagers", "");

            }

            if (saveObject is EntitySave)
            {
                if (saveObject.InheritsFromFrbType())
                {
                    AssetTypeInfo ati = AvailableAssetTypes.Self.GetAssetTypeFromRuntimeType(saveObject.BaseObject, saveObject);

                    if (ati != null)
                    {
                        EntitySave asEntitySave = saveObject as EntitySave;

                        if (asEntitySave.CreatedByOtherEntities && !string.IsNullOrEmpty(ati.RecycledDestroyMethod))
                        {
                            currentBlock.Line(ati.RecycledDestroyMethod + ";");
                        }
                        else
                        {
                            currentBlock.Line(ati.DestroyMethod + ";");
                        }
                    }
                }

                else if (!saveObject.InheritsFromElement())
                {
                    currentBlock.Line("FlatRedBall.SpriteManager.ConvertToManuallyUpdated(this);");
                }

                if ((saveObject as EntitySave).ImplementsIWindow && !(saveObject as EntitySave).GetInheritsFromIWindow())
                {
                    currentBlock.Line("FlatRedBall.Gui.GuiManager.RemoveWindow(this);");
                }
            }

            foreach (ElementComponentCodeGenerator codeGenerator in CodeWriter.CodeGenerators)
            {
                codeGenerator.GenerateRemoveFromManagers(currentBlock, saveObject);
            }
        }

        private static void GenerateAssignCustomVariables(ICodeBlock codeBlock, IElement element)
        {
            bool inherits = !string.IsNullOrEmpty(element.BaseElement) && !element.InheritsFromFrbType();

            
            if (inherits)
            {
                codeBlock = codeBlock.Function("public override void", "AssignCustomVariables", "bool callOnContainedElements");
                codeBlock.Line("base.AssignCustomVariables(callOnContainedElements);");
            }
            else
            {
                codeBlock = codeBlock.Function("public virtual void", "AssignCustomVariables", "bool callOnContainedElements");
            }

            // call AssignCustomVariables on all contained objects before assigning custom variables on "this"
            var ifCallOnContainedElements = codeBlock.If("callOnContainedElements");

            var listOfItems = element.NamedObjects.Where(item=>
                item.IsFullyDefined &&
                !item.IsDisabled &&
                item.Instantiate &&
                !item.SetByContainer 
                // November 4, 2020
                // If we don't consider
                // SetByDerived, then variables
                // that are set on the base object
                // won't get assigned here. Shouldn't 
                // they?
                //&& !item.SetByDerived
                ).ToList();


            GenerateAssignmentForListOfObjects(codeBlock, element, ifCallOnContainedElements, listOfItems);

            


            foreach (CustomVariable customVariable in element.CustomVariables)
            {


                CustomVariableCodeGenerator.AppendAssignmentForCustomVariableInElement(codeBlock, customVariable, element);
            }

            EventCodeGenerator.GenerateAddToManagersBottomUp(codeBlock, element);
        }

        private static void GenerateAssignmentForListOfObjects(ICodeBlock codeBlock, IElement element, ICodeBlock ifCallOnContainedElements, List<NamedObjectSave> listOfItems)
        {
            foreach (var item in listOfItems)
            {
                if (item.DefinedByBase == false && item.SourceType == SourceType.Entity)
                {
                    NamedObjectSaveCodeGenerator.AddIfConditionalSymbolIfNecesssary(ifCallOnContainedElements, item);

                    ifCallOnContainedElements.Line(item.InstanceName + ".AssignCustomVariables(true);");
                    NamedObjectSaveCodeGenerator.AddEndIfIfNecessary(ifCallOnContainedElements, item);
                }


                NamedObjectSaveCodeGenerator.AddIfConditionalSymbolIfNecesssary(codeBlock, item);

                var innerBlock = codeBlock;
                if(item.SetByDerived)
                {
                    innerBlock = codeBlock.If($"{item.InstanceName} != null");
                }
                NamedObjectSaveCodeGenerator.AssignInstanceVaraiblesOn(element, item, innerBlock);


                var containedItems = item.ContainedObjects.Where(containedObject =>
                    containedObject.IsFullyDefined &&
                    !containedObject.IsDisabled &&
                    containedObject.Instantiate &&
                    !containedObject.SetByContainer &&
                    !containedObject.SetByDerived
                    ).ToList();

                GenerateAssignmentForListOfObjects(codeBlock, element, ifCallOnContainedElements, containedItems);
                NamedObjectSaveCodeGenerator.AddEndIfIfNecessary(codeBlock, item);

            }
        }

        public static bool EliminateCall(string call, ref string contents)
        {
            var removed = false;
            if (contents.Contains(call))
            {
                removed = true;
                contents = contents.Replace(call, "");
            }

            return removed;
        }

        internal static bool IsVariableHandledByCustomCodeGenerator(CustomVariable customVariable, IElement element)
        {
            foreach (var codeGenerator in CodeGenerators)
            {
                if (codeGenerator.HandlesVariable(customVariable, element))
                {
                    return true;

                }
            }

            return false;
        }

        #endregion
    }
}
