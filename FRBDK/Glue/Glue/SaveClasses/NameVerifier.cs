﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Glue;
using FlatRedBall.Glue.Elements;
using FlatRedBall.IO;
using FlatRedBall.Glue.Reflection;
using FlatRedBall.Glue.Plugins.ExportedImplementations;

namespace FlatRedBall.Glue.SaveClasses
{
	public static class NameVerifier
    {
        #region Fields

        public static char[] InvalidCharacters = 
            new char[] 
            { 
                '~', '`', '!', '@', '#', '$', '%', '^', '&', '*', 
                '(', ')', '-', '=', '+', ';', '\'', ':', '"', '<', 
                ',', '>', '.', '/', '\\', '?', '[', '{', ']', '}', 
                '|', 
                // Spaces are handled separately
            //    ' ' 
            };
        // We now allow underscore ( '_' )

        static string[] mXnaReservedWords = new string[]
        {
            "Color",
            "SpriteBatch",
            "Texture2D",
            "Vector2",
            "Vector3",
        };

        static string[] mReservedClassNames = new string[]
        {
            "AnimationChain",
            "AxisAlignedRectangle",
            "Camera",
            "Circle",
            "Cursor",
            "Emitter",
            nameof(Entities.IDamageable),
            nameof(Entities.IDamageArea),
            "Object",
            "PositionedObject",
            "Polygon",
            "Scene",
            "ShapeManager",
            "Sprite",
            "SpriteFrame",
            "SpriteManager",
            "SpriteRig",
            "Test",
            "Text",
            "TextManager",

        };


        static string[] mOtherReservedNames = new string[]
        {
            "Name",
            "Type",
            "Collision"
                
        };

        static string[] mCSharpKeyWords = new string[]
        {
            "abstract",	
            "event",
            "new",
            "struct",
            "as",
            "explicit",
            "null",
            "switch",
            "base",
            "extern",
            "object",
            "this",
            "bool",
            "false",
            "operator",
            "throw",
            "break",
            "finally",
            "out",
            "true",
            "byte",
            "fixed",
            "override",
            "try",
            "case",
            "float",
            "params",
            "typeof",
            "catch",
            "for",
            "private",
            "uint",
            "char",
            "foreach",
            "protected",
            "ulong",
            "checked",
            "goto",
            "public",
            "unchecked",
            "class",
            "if",
            "readonly",
            "unsafe",
            "const",
            "implicit",
            "ref",
            "ushort",
            "continue",
            "in",
            "return",
            "using",
            "decimal",
            "int",
            "sbyte",
            "virtual",
            "default",
            "interface",
            "sealed",
            "volatile",
            "delegate",
            "internal",
            "short",
            "void",
            "do",
            "is",
            "sizeof",
            "while",
            "double",
            "lock",
            "stackalloc",
            "else",
            "long",
            "static",
            "enum",
            "namespace",
            "string",
            "async",
            "await"
        };

        #endregion

        #region Directory

        public static bool IsDirectoryNameValid(string directory, out string whyItIsntValid)
        {
            whyItIsntValid = "";

            CheckForCommonImproperNames(directory, ref whyItIsntValid);

            bool returnValue = string.IsNullOrEmpty(whyItIsntValid);
            return returnValue;
        }

        #endregion

        #region Referenced File Save

        public static bool IsReferencedFileNameValid(string name, AssetTypeInfo ati, ReferencedFileSave rfs, IElement container, out string whyItIsntValid)
        {
            whyItIsntValid = "";

            CheckForCommonImproperNames(name, ref whyItIsntValid);

            // CheckForExistingEntity checks if the name is already used, but we can be more specific if it's part of this entity
            if(container != null)
            {
                string unqualifiedContainerName = FileManager.RemovePath(container.Name);

                if(unqualifiedContainerName.ToLowerInvariant() == FileManager.RemovePath(FileManager.RemoveExtension(name)))
                {
                    string containerType = "Entity";

                    if(container is ScreenSave)
                    {
                        containerType = "Screen";
                    }
                    else if(container is EntitySave)
                    {
                        containerType = "Entity";
                    }
                    else
                    {
                        containerType = "Container";
                    }

                    whyItIsntValid = "The " + containerType + " that you are adding the file to has the same name as the file.  This is not allowed in Glue.  Please rename the file";
                }
            }

            if (string.IsNullOrEmpty(whyItIsntValid))
            {
                CheckForExistingEntity(name, ref whyItIsntValid);
            }

            if (string.IsNullOrEmpty(whyItIsntValid))
            {
                CheckForRfsWithMatchingFileName(container, name, rfs, ref whyItIsntValid);
            }

            if (string.IsNullOrEmpty(whyItIsntValid))
            {
                if (ati != null && ati.Extension == "csv")
                {
                    // Let's see if there is already a spreadsheet by this name and if so, let's warn the user
                    // But we only want a spreadsheet that isn't using a created class:
                    var existing = ObjectFinder.Self.GetAllReferencedFiles().Where(item =>
                        // Is the item a CSV...
                        item.IsCsvOrTreatedAsCsv &&
                            // And is the item different than the rfs we're checking
                        item != rfs &&
                            // And do the names match?
                        FileManager.RemovePath(FileManager.RemoveExtension(item.Name)) == name &&
                            // And does it not use a custom class?
                        ObjectFinder.Self.GlueProject.CustomClasses.Any(customClass => customClass.CsvFilesUsingThis.Contains(item.Name)) == false)
                        .ToList();



                    if (existing.Count != 0)
                    {
                        whyItIsntValid = "There is already a CSV file using the name " + name + ". This CSV is not using a custom class:  " + existing[0].ToString();
                    }
                }
            }

            bool returnValue = string.IsNullOrEmpty(whyItIsntValid);
            return returnValue;
        }

        private static void CheckForRfsWithMatchingFileName(IElement container, string name, ReferencedFileSave rfsToSkip, ref string whyItIsntValid)
        {

            if (container == null)
            {
                // We need to see if there is already a file with the same name in Global Content
                ReferencedFileSave existingRfs = ObjectFinder.Self.GlueProject.GlobalFiles.FirstOrDefault(
                    rfs=>
                        (FileManager.RemovePath(rfs.Name) == name || 
                        rfs.GetInstanceName() == name) &&
                        rfs != rfsToSkip
                        );
                if (existingRfs != null)
                {
                    whyItIsntValid += "There is already a file in GlobalContent using the name " + name;
                }

            }
            else
            {
                // We need to see if there is already a file with the same name in Global Content
                ReferencedFileSave existingRfs = container.ReferencedFiles.FirstOrDefault(
                    rfs =>
                    {
                        return (FileManager.RemovePath(rfs.Name) == name ||
                            FileManager.RemoveExtension(FileManager.RemovePath(rfs.Name)) == name) &&
                            rfs != rfsToSkip
                            ;
                    });
                if (existingRfs != null)
                {
                    whyItIsntValid += "There is already a file in " + container.Name + " using the name " + name;
                }
            }
        }

        #endregion

        internal static bool IsCustomClassNameValid(string name, out string whyItIsntValid)
        {
            whyItIsntValid = "";

            CheckForCommonImproperNames(name, ref whyItIsntValid);

            if (ObjectFinder.Self.GetScreenSave("Screens\\" + name) != null)
            {
                whyItIsntValid = "There is already an Screen named " + name;
            }
            else if (GlueCommands.Self.GluxCommands.GetReferencedFileSaveFromFile("Screens\\" + name) != null)
            {
                whyItIsntValid = "There is already a file named " + name;
            }
            else if (mReservedClassNames.Contains(name))
            {
                whyItIsntValid = "The name " + name + " is a reserved class name, so it can't be used for a Screen";
            }
            else if (ObjectFinder.Self.GetEntitySaveUnqualified(name) != null)
            {
                whyItIsntValid = "There is already an Entity named " + name + ".\n\n" +
                    "Glue recommends naming the Class something different than existing Entities.";
            }
            else if (name == ProjectManager.ProjectNamespace)
            {
                whyItIsntValid = "The class cannot be named the same as the root namespace (which is usually the same name as the project)";
            }
            return string.IsNullOrEmpty(whyItIsntValid);
        }



        /// <summary>
        /// Returns whether the argument name is a valid Screen name. This name should not include the "Screens\" prefix.
        /// </summary>
        /// <param name="name">The name to check.</param>
        /// <param name="screenSave">The screen which may be renamed.</param>
        /// <param name="whyItIsntValid">Information about why the name is invalid which can be displayed to the user.</param>
        /// <returns>Whether the name is valid.</returns>
        public static bool IsScreenNameValid(string name, ScreenSave screenSave, out string whyItIsntValid)
		{
			whyItIsntValid = "";

			CheckForCommonImproperNames(name, ref whyItIsntValid);

            if (ObjectFinder.Self.GetScreenSave("Screens\\" + name) != null)
			{
				whyItIsntValid = "There is already an Screen named " + name;
			}
			else if (GlueCommands.Self.GluxCommands.GetReferencedFileSaveFromFile("Screens\\" + name) != null)
			{
				whyItIsntValid = "There is already a file named " + name;
			}
            else if (mReservedClassNames.Contains(name))
            {
                whyItIsntValid = "The name " + name + " is a reserved class name, so it can't be used for a Screen";
            }
            else if (ObjectFinder.Self.GetEntitySaveUnqualified(name) != null)
            {
                whyItIsntValid = "There is already an Entity named " + name + ".\n\n" +
                    "Glue recommends naming the Screen something different than existing Entities because " +
                    "generated code can get confused if you add this same-named Entity in the Screen.";
            }
            else if (name == ProjectManager.ProjectNamespace)
            {
                whyItIsntValid = "The Screen cannot be named the same as the root namespace (which is usually the same name as the project)";
            }
            return string.IsNullOrEmpty(whyItIsntValid);
        }

		public static bool IsEntityNameValid(string name, EntitySave entitySave, out string whyItIsntValid)
		{
			whyItIsntValid = "";

			CheckForCommonImproperNames(name, ref whyItIsntValid);


            CheckForExistingEntity(name, ref whyItIsntValid);

			if (ObjectFinder.Self.GetEntitySaveUnqualified(name) != null)
			{
				whyItIsntValid = "There is already an entity named " + name;
			}
			else if (GlueCommands.Self.GluxCommands.GetReferencedFileSaveFromFile("Entities\\" + name) != null)
			{
				whyItIsntValid = "There is already a file named " + name;
			}
            else if (mReservedClassNames.Contains(name))
            {
                whyItIsntValid = "The name " + name + " is a reserved class name, so it can't be used for an Entity";
            }
            else if (ObjectFinder.Self.GetScreenSaveUnqualified(name) != null)
            {
                whyItIsntValid = "There is already a Screen named " + name + ".\n\nGlue recommends not naming your Screens and Entities the same because " +
                    "adding an Entity to a Screen that has the same name may cause problems in the generated code.";

            }
            else if (name == ProjectManager.ProjectNamespace)
            {
                whyItIsntValid = "The Entity cannot be named the same as the root namespace (which is usually the same name as the project)";
            }


			return string.IsNullOrEmpty(whyItIsntValid);
		}

        public static bool IsStateCategoryNameValid(string name, out string whyItIsntValid)
        {
            whyItIsntValid = null;

            CheckForCommonImproperNames(name, ref whyItIsntValid);

            
            if (mReservedClassNames.Contains(name))
            {
                whyItIsntValid = "The name " + name + " is a reserved class name, so it can't be used for a State Category";
            }

            return string.IsNullOrEmpty(whyItIsntValid);
        }

        public static bool IsStateNameValid(string name, IElement element, StateSaveCategory category, StateSave currentStateSave, out string whyItIsntValid)
        {
            whyItIsntValid = null;

            CheckForCommonImproperNames(name, ref whyItIsntValid);

            if(!string.IsNullOrEmpty(whyItIsntValid))
                return false;

            //Check if shared
            if (element != null)
            {
                if (category != null)
                {
                    if (category.States.Any(state => state.Name == name && state != currentStateSave))
                    {
                        whyItIsntValid = "The name " + name + " is already being used in the category " + category.Name;
                        return false;
                    }
                }
            }

            if (!string.IsNullOrEmpty(element.BaseElement))
            {
                IElement baseElement = ObjectFinder.Self.GetIElement(element.BaseElement);

                if (baseElement != null)
                {
                    string categoryName = null;

                    if (category != null)
                    {
                        categoryName = category.Name;
                    }

                    if (baseElement.GetState(name, categoryName) != null)
                    {
                        string screenOrEntity = "screen";
                        if(baseElement is EntitySave)
                        {
                            screenOrEntity = "entity";
                        }

                        whyItIsntValid = "This state name \"" + name + "\" is already used in a base " + screenOrEntity;
                    }

                    
                }

            }

            return string.IsNullOrEmpty(whyItIsntValid);
        }

        public static bool IsNamedObjectNameValid(string name, out string whyItIsntValid)
        {
            return IsNamedObjectNameValid(name, GlueState.Self.CurrentNamedObjectSave, out whyItIsntValid);
        }

        public static bool IsNamedObjectNameValid(string name, NamedObjectSave namedObject, out string whyItIsntValid)
        {
            bool isDefinedInBaseButNotSetByDerived = false;

            int wasRemovedFromIndex;
            NamedObjectSave containerNos;
            IElement element;
            RemoveNosFromElementIfNecessary(namedObject, out wasRemovedFromIndex, out element, out containerNos);
            
            MembershipInfo membershipInfo = NamedObjectSaveExtensionMethodsGlue.GetMemberMembershipInfo(name);

            if (wasRemovedFromIndex != -1)
            {
                if (containerNos == null)
                {
                    element.NamedObjects.Insert(wasRemovedFromIndex, namedObject);
                }
                else
                {
                    containerNos.ContainedObjects.Insert(wasRemovedFromIndex, namedObject);
                }
            }


            if (membershipInfo == MembershipInfo.ContainedInBase)
            {
                // make sure this thing is set to be SetByDerived
                NamedObjectSave nos = GlueState.Self.CurrentElement.GetNamedObjectRecursively(name);

                if (!nos.SetByDerived)
                {
                    isDefinedInBaseButNotSetByDerived = true;
                }

            }

            whyItIsntValid = null;

            CheckForCommonImproperNames(name, ref whyItIsntValid);

            if (string.IsNullOrEmpty(whyItIsntValid))
            {
                if (isDefinedInBaseButNotSetByDerived)
                {
                    whyItIsntValid = "There is an object named " + name + " in the base Element but it is not Set By Derived.";
                }
                else if (membershipInfo == MembershipInfo.ContainedInThis)
                {
                    whyItIsntValid = "The name " + name + " is already being used";
                }
                else if (GlueState.Self.CurrentElement != null && FileManager.RemovePath(GlueState.Self.CurrentElement.Name) == name)
                {
                    if (GlueState.Self.CurrentElement is EntitySave)
                    {
                        whyItIsntValid = "You can't name your Object the same name as the Entity it is contained in.";

                    }
                    else
                    {
                        whyItIsntValid = "You can't name your Object the same name as the Screen it is contained in.";
                    }
                }
                else if (string.IsNullOrEmpty(name))
                {
                    whyItIsntValid = "The name cannot be blank.";
                }
                else if (char.IsDigit(name[0]))
                {
                    whyItIsntValid = "Object names can't start with numbers";
                }

                else if (name.Contains(' '))
                {
                    whyItIsntValid = "Object names can't have spaces";
                }
                else if (GlueState.Self.CurrentElement != null &&
                    ExposedVariableManager.GetExposableMembersFor(GlueState.Self.CurrentElement, false).Any(item => item.Member == name))
                {
                    whyItIsntValid = "The name " + name + " is an existing or exposable variable name in " +
                        GlueState.Self.CurrentElement.ToString() + " so it is not a valid object name";
                }
                else if (mOtherReservedNames.Contains(name))
                {

                    whyItIsntValid = "The name \"" + name + "\" is not an allowed name for objects. ";
                }
                else if (IsPositionedObjectMember(name))
                {
                    whyItIsntValid = "The name \"" + name + "\" is a reserved name by the PositionedObject Type.";
                }
            }
            return string.IsNullOrEmpty(whyItIsntValid);
        }

        private static bool IsPositionedObjectMember(string name)
        {
            Type type = typeof(PositionedObject);

            if (type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static) != null)
            {
                return true;
            }

            if (type.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static) != null)
            {
                return true;
            }
            return false;
        }

        private static void RemoveNosFromElementIfNecessary(NamedObjectSave namedObject, out int wasRemovedFromIndex, out IElement element, out NamedObjectSave containingNos)
        {
            // We want to see if this name is already being used by other NOS's.
            // However, if this NOS is already part of the current element, then the
            // check for whether it's being used will always return true - because it's 
            // being used by itself.  We don't want itself from returning the name as invalid
            // so we're going to remove it from the NOS list before running the check, then we'll
            // insert it back to the appropriate place.
            wasRemovedFromIndex = -1;
            element = GlueState.Self.CurrentElement;

            if (namedObject != null && element != null && element.NamedObjects.Contains(namedObject))
            {
                wasRemovedFromIndex = element.NamedObjects.IndexOf(namedObject);

                element.NamedObjects.Remove(namedObject);
            }
            containingNos = null;

            if (wasRemovedFromIndex == -1 && element != null)
            {
                foreach (NamedObjectSave containerNos in element.NamedObjects)
                {
                    if (containerNos.ContainedObjects.Contains(namedObject))
                    {
                        wasRemovedFromIndex = containerNos.ContainedObjects.IndexOf(namedObject);
                        containerNos.ContainedObjects.Remove(namedObject);
                        containingNos = containerNos;
                        break;
                    }
                }
            }
        }

        public static bool IsEventNameValid(string name, IElement currentElement, out string failureMessage)
        {
            bool didFailureOccur = false;

            string whyItIsntValid = "";
            didFailureOccur = NameVerifier.IsCustomVariableNameValid(name, null, currentElement, ref whyItIsntValid) == false;
            failureMessage = null;
            if (didFailureOccur)
            {
                failureMessage = whyItIsntValid;

            }
            if (ExposedVariableManager.IsReservedPositionedPositionedObjectMember(name) && currentElement is EntitySave)
            {
                didFailureOccur = true;
                failureMessage = "The variable\n\n" + name + "\n\nis reserved by FlatRedBall.";
            }

            return didFailureOccur;
        }



        private static void CheckForExistingEntity(string name, ref string whyItIsntValid)
        {
            if (ObjectFinder.Self.GetEntitySaveUnqualified(name) != null)
            {
                whyItIsntValid = "Your project contains an entity named " + name + " which is the same name as your file.  Glue does not allow files with the " + 
                    "same names as Entities to be added as this can result in code ambiguity.";
            }
        }

		private static void CheckForCommonImproperNames(string name, ref string whyItIsntValid)
		{
			if (string.IsNullOrEmpty(name))
			{
				whyItIsntValid = "The name can't be empty.";
			}
			else if (char.IsDigit(name[0]))
			{
				whyItIsntValid = "Names can't start with a number.";
			}
			else if(name.IndexOfAny(InvalidCharacters) != -1)
			{
                // See which ones are contained
                foreach(var invalidCharacter in InvalidCharacters)
                {
                    if(name.Contains(invalidCharacter))
                    {
                        string clarification = "";
                        if(invalidCharacter == '.')
                        {
                            clarification = " (period)";
                        }

				        whyItIsntValid += $"The name can't contain invalid character {invalidCharacter}{clarification}\n";

                    }
                }

			}
			else if(name.Contains(' '))
			{
				whyItIsntValid = "The name can't have any spaces.";
			}
            else if (mXnaReservedWords.Contains(name))
            {
                whyItIsntValid = "The word '" + name + "' is a reserved word used by the engine.  This can't be used as a name";
            }
            else if (mCSharpKeyWords.Contains(name))
            {
                whyItIsntValid = "The word '" + name + "' is a C# keyword.  This can't be used as a name";
            }
            else if (mOtherReservedNames.Contains(name))
            {
                whyItIsntValid = "The word '" + name + "' is an invalid name because it can cause ambiguity";
            }

		}

        
        internal static bool IsCustomVariableNameValid(string variableName, CustomVariable customVariable, IElement containingElement, ref string whyItIsntValid)
        {
            CheckForCommonImproperNames(variableName, ref whyItIsntValid);

            string screenOrEntity = "";

            if (containingElement is ScreenSave)
            {
                screenOrEntity = "Screen";
            }
            else
            {
                screenOrEntity = "Entity";
            }

            if(string.IsNullOrEmpty(whyItIsntValid))
            {
                if (containingElement.CustomVariables.Any(item=> item.Name == variableName && item != customVariable))
                {
                    whyItIsntValid += "This " + screenOrEntity + " already has a variable named " + variableName;
                }
            }

            // This looks like duplicate code compared to what's above.  
            //if (string.IsNullOrEmpty(whyItIsntValid))
            //{
            //    if (containingElement.ContainsCustomVariable(variableName))
            //    {
            //        string screenOrEntity = "";

            //        if (containingElement is ScreenSave)
            //        {
            //            screenOrEntity = "Screen";
            //        }
            //        else
            //        {
            //            screenOrEntity = "Entity";
            //        }

            //        whyItIsntValid += "This " + screenOrEntity + " already has a variable named " + variableName;
            //    }
            //}

            if (string.IsNullOrEmpty(whyItIsntValid))
            {
                if (containingElement.GetNamedObjectRecursively(variableName) != null)
                {
                    whyItIsntValid = "This " + screenOrEntity + 
                        " has an object named " + variableName + " so you must choose a different variable name";
                }
            }

            if(string.IsNullOrEmpty(whyItIsntValid))
            {
                if(variableName == containingElement.GetStrippedName())
                {
                    whyItIsntValid = $"The variable name {variableName} cannot be the same as its {screenOrEntity}";
                }
            }

            return string.IsNullOrEmpty(whyItIsntValid);

        }

        public static bool DoesTunneledVariableAlreadyExist(string sourceObject, string sourceObjectProperty, IElement element)
        {
            if (!string.IsNullOrEmpty(sourceObject))
            {
                if (!string.IsNullOrEmpty(sourceObjectProperty))
                {
                    foreach (CustomVariable variable in element.CustomVariables)
                    {
                        if (variable.SourceObject == sourceObject && variable.SourceObjectProperty == sourceObjectProperty)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
