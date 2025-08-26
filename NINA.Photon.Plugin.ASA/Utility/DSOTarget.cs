#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Astrometry.Interfaces;
using NINA.Astrometry;
using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Photon.Plugin.ASA.Utility
{
    public class DSOTarget
    {
        public static InputTarget _FindTarget(ISequenceContainer parent)
        {
            ISequenceContainer sc = ItemUtility.GetRootContainer(parent);
            if (sc != null && sc.Items.Count == 3)
            {
                return FindRunningItem((ISequenceContainer)sc.Items[1]);
            }
            return RetrieveTarget(parent);
        }

        public static InputTarget FindTarget(ISequenceContainer parent)
        {
            InputTarget t = RetrieveTarget(parent);
            if (t != null) return t;
            ISequenceContainer sc = ItemUtility.GetRootContainer(parent);
            if (sc != null && sc.Items.Count == 3)
            {
                return FindRunningItem((ISequenceContainer)sc.Items[1]);
            }
            else
            {
                return null;
            }
        }

        public static InputTarget RetrieveTarget(ISequenceContainer parent)
        {
            if (parent != null)
            {
                var container = parent as IDeepSkyObjectContainer;
                if (container != null && container.Target != null && container.Target.InputCoordinates != null && container.Target.DeepSkyObject != null)
                {
                    Logger.Debug("DSOTarget, found above: " + container.Target.TargetName);
                    Coordinates c = container.Target.InputCoordinates.Coordinates;
                    if (c != null && c.RA == 0 && c.Dec == 0)
                    {
                        IDeepSkyObject dso = container.Target.DeepSkyObject;
                        if (dso != null && dso.Coordinates.RA != 0 && dso.Coordinates.Dec != 0)
                        {
                            Logger.Debug("DSOTarget, using DSO coordinates instead");
                            container.Target.InputCoordinates.Coordinates = dso.Coordinates;
                            return container.Target;
                        }
                        Logger.Debug("Found target has 0/0 coordinates; failing search");
                        return null;
                    }
                    return container.Target;
                }
                else
                {
                    return RetrieveTarget(parent.Parent);
                }
            }
            else
            {
                Logger.Debug("DSOTarget, Not found");
                return null;
            }
        }

        public static InputTarget FindRunningItem(ISequenceContainer c)
        {
            if (c != null)
            {
                foreach (var item in c.Items)
                {
                    if (item is ISequenceContainer sc && (item.Status == SequenceEntityStatus.RUNNING || item.Status == SequenceEntityStatus.CREATED))
                    {
                        if (item is IDeepSkyObjectContainer dso)
                        {
                            return dso.Target;
                        }
                        else if (item is ISequenceContainer cont)
                        {
                            foreach (ISequenceItem item2 in cont.Items)
                            {
                                if (item2.Status == SequenceEntityStatus.RUNNING || item2.Status == SequenceEntityStatus.CREATED)
                                {
                                    if (item2 is IDeepSkyObjectContainer dso2)
                                    {
                                        Logger.Debug("DSOTarget, found running target: " + dso2.Target.TargetName);
                                        if (dso2.Target.InputCoordinates == null)
                                        {
                                            Logger.Debug("DSO Target, running target has no InputCoordinates");
                                        }
                                        else if (dso2.Target.InputCoordinates.Coordinates == null)
                                        {
                                            Logger.Debug("DSO Target, running target InputCoordinates has no Coordinates");
                                        }
                                        else if (dso2.Target.InputCoordinates.Coordinates.RA == 0 && dso2.Target.InputCoordinates.Coordinates.Dec == 0)
                                        {
                                            IDeepSkyObject dsot = dso2.Target.DeepSkyObject;
                                            if (dsot != null && dsot.Coordinates.RA != 0 && dsot.Coordinates.Dec != 0)
                                            {
                                                Logger.Debug("DSO Target, using DSO coordinates instead");
                                                dso2.Target.InputCoordinates.Coordinates = dsot.Coordinates;
                                                return dso2.Target;
                                            }
                                            Logger.Debug("DSO Target, running target Coordinates are 0/0");
                                        }
                                        else
                                        {
                                            return dso2.Target;
                                        }
                                        Logger.Debug("DSO Target, looking inside running target...");
                                        InputTarget rt = FindRunningItem(dso2);
                                        if (rt != null)
                                        {
                                            //SPLogger.Debug("DSO Target, found by looking deeper");
                                            return rt;
                                        }
                                    }
                                    else if (item2 is ISequenceContainer cont2)
                                    {
                                        InputTarget rt = FindRunningItem(cont2);
                                        if (rt != null)
                                        {
                                            return rt;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }
    }
}