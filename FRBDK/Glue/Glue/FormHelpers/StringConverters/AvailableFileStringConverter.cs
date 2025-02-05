﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using FlatRedBall.Glue.SaveClasses;
using FlatRedBall.Glue.Elements;
using FlatRedBall.IO;
using FlatRedBall.Glue.FormHelpers.StringConverters;
using System.Runtime.InteropServices;

namespace FlatRedBall.Glue.GuiDisplay
{
    public class AvailableFileStringConverter : TypeConverterWithNone
    {
        #region Fields
        public const string UseDefaultString = "<USE DEFAULT>";
        bool mShowNewFileOption = true;

        #endregion

        #region Properties

        public GlueElement CurrentElement
        {

            get;
            set;
        }

        public bool ShowNewFileOption
        {
            get { return mShowNewFileOption; }
            set { mShowNewFileOption = value; }
        }

        public bool RemovePathAndExtension
        {
            get;
            set;
        }


        public string QualifiedRuntimeTypeNameFilter
        {
            get;
            set;
        }

        public string UnqualifiedRuntimeTypeNameFilter
        {
            get;
            set;
        }

        #endregion

        public AvailableFileStringConverter(GlueElement element)
            : base()
        {
            IncludeNoneOption = true;
            CurrentElement = element;
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return true;
        }

        [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string x, string y);


        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            List<string> stringToReturn = GetAvailableOptions(CurrentElement, IncludeNoneOption, RemovePathAndExtension, QualifiedRuntimeTypeNameFilter, UnqualifiedRuntimeTypeNameFilter);
            return new StandardValuesCollection(stringToReturn);
        }

        public static List<string> GetAvailableOptions(GlueElement glueElement, bool includeNoneOption, bool removePathAndExtension, string qualifiedRuntimeTypeNameFilter = null, string unqualifiedRuntimeTypeNameFilter = null)
        {
            List<string> stringToReturn = new List<string>();

            if (includeNoneOption)
            {
                // 
                stringToReturn.Add("<NONE>");
            }
            else
            {
                stringToReturn.Add("");
            }


            List<string> listToSort = new List<string>();

            if (glueElement != null)
            {
                // Let's use 
                //foreach (ReferencedFileSave rfs in currentElementSave.ReferencedFiles)
                foreach (ReferencedFileSave rfs in glueElement.GetAllReferencedFileSavesRecursively())
                {
                    bool shouldInclude = (string.IsNullOrEmpty(qualifiedRuntimeTypeNameFilter) && string.IsNullOrEmpty(unqualifiedRuntimeTypeNameFilter)) ||
                        (!string.IsNullOrEmpty(qualifiedRuntimeTypeNameFilter) && IsRfsOfQualifiedRuntimeType(rfs, qualifiedRuntimeTypeNameFilter)) ||
                        (!string.IsNullOrEmpty(unqualifiedRuntimeTypeNameFilter) && IsRfsOfUnqualifiedRuntimeType(rfs, unqualifiedRuntimeTypeNameFilter));

                    if (shouldInclude)
                    {
                        if (removePathAndExtension)
                        {
                            listToSort.Add(FileManager.RemovePath(FileManager.RemoveExtension(rfs.Name)));
                        }
                        else
                        {
                            listToSort.Add(rfs.Name);
                        }
                    }
                }
            }

            listToSort.Sort(StrCmpLogicalW);

            stringToReturn.AddRange(listToSort);
            return stringToReturn;
        }

        bool IsRfsOfType(ReferencedFileSave rfs, Type type)
        {
            AssetTypeInfo ati = rfs.GetAssetTypeInfo();

            if (ati != null)
            {
                return ati.QualifiedRuntimeTypeName.QualifiedType == type.FullName;
            }
            else
            {
                return false;
            }
        }

        static bool IsRfsOfQualifiedRuntimeType(ReferencedFileSave rfs, string qualifiedRuntimeType)
        {
            AssetTypeInfo ati = rfs.GetAssetTypeInfo();

            if (ati != null)
            {
                return ati.QualifiedRuntimeTypeName.QualifiedType == qualifiedRuntimeType;
            }
            else
            {
                return false;
            }
        }

        static bool IsRfsOfUnqualifiedRuntimeType(ReferencedFileSave rfs, string unqualifiedRuntimeType)
        {
            AssetTypeInfo ati = rfs.GetAssetTypeInfo();

            if (ati != null)
            {
                return ati.RuntimeTypeName == unqualifiedRuntimeType;
            }
            else
            {
                return false;
            }
        }
    }
}
