﻿using FlatRedBall.Glue.Elements;
using FlatRedBall.Glue.Errors;
using FlatRedBall.Glue.Plugins.ExportedImplementations;
using FlatRedBall.Glue.SaveClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OfficialPlugins.ErrorReportingPlugin.ViewModels
{
    internal class InvalidInstantiateByBaseErrorViewModel : ErrorViewModel
    {
        NamedObjectSave derivedNos;

        public override string UniqueId => Details;

        public InvalidInstantiateByBaseErrorViewModel(NamedObjectSave derivedNos)
        {
            this.derivedNos = derivedNos;

            this.Details = $"The object {derivedNos} is instantiated by base, but no objects in base elements " +
                $"instantiate the object (they all have SetByDerived = true).";
        }

        public override void HandleDoubleClick()
        {
            GlueState.Self.CurrentNamedObjectSave = derivedNos;
        }

        public override bool GetIfIsFixed()
        {
            var derivedElement = derivedNos.GetContainer();
            if (derivedElement == null)
            {
                return true;
            }
            else if (derivedNos.InstantiatedByBase == false)
            {
                // it instantiates itself, so it's fixed:
                return true;
            }
            else
            {
                var foundInBase = false;
                var baseElements = ObjectFinder.Self.GetAllBaseElementsRecursively(derivedElement);

                foreach (var baseElement in baseElements)
                {
                    var baseNos = baseElement.AllNamedObjects
                        .FirstOrDefault(item => item.InstanceName == derivedNos.InstanceName);

                    if (baseNos != null && baseNos.SetByDerived == false)
                    {
                        // found a match
                        foundInBase = true;
                    }
                }

                if(foundInBase)
                {
                    return true;
                }
            }


            return false;
        }
    }
}
