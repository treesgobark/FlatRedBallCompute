﻿using FlatRedBall.Glue.Elements;
using FlatRedBall.Glue.Errors;
using FlatRedBall.Glue.Plugins.ExportedImplementations;
using FlatRedBall.Glue.SaveClasses;
using OfficialPlugins.ErrorReportingPlugin.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OfficialPlugins.ErrorReportingPlugin
{
    internal class MainErrorReporter : IErrorReporter
    {
        public ErrorViewModel[] GetAllErrors()
        {
            List<ErrorViewModel> errors = new List<ErrorViewModel>();

            FillWithBadSetByDerived(errors);

            return errors.ToArray();
        }

        private void FillWithBadSetByDerived(List<ErrorViewModel> errors)
        {
            foreach(var screen in GlueState.Self.CurrentGlueProject.Screens)
            {
                FillWithBadSetByDerived(screen, errors);
            }

            foreach(var entity in GlueState.Self.CurrentGlueProject.Entities)
            {
                FillWithBadSetByDerived(entity, errors);
            }
        }

        private void FillWithBadSetByDerived(GlueElement derivedElement, List<ErrorViewModel> errors)
        {
            var baseElements = ObjectFinder.Self.GetAllBaseElementsRecursively(derivedElement);
            foreach(var derivedNos in derivedElement.AllNamedObjects)
            {
                if(derivedNos.DefinedByBase == false)
                {
                    // This is defined here, make sure there are no objects in base objects
                    // with the same name which are not SetByDerived

                    foreach(var baseElement in baseElements)
                    {
                        var baseNos = baseElement.AllNamedObjects
                            .FirstOrDefault(item => item.InstanceName== derivedNos.InstanceName);

                        if(baseNos != null && baseNos.SetByDerived == false && baseNos.ExposedInDerived == false)
                        {
                            var errorVm = new InvalidSetByDerivedErrorViewModel(baseNos, derivedNos);
                            errors.Add(errorVm);
                        }
                    }
                }
            }
        }
    }
}