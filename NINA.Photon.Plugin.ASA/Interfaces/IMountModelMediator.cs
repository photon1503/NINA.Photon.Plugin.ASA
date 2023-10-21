#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Enum;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Photon.Plugin.ASA.Equipment;
using NINA.Photon.Plugin.ASA.Model;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Photon.Plugin.ASA.Interfaces
{
    public interface IMountModelMediator : IDeviceMediator<IMountModelVM, IMountModelConsumer, MountModelInfo>
    {
        string GetModelName(int modelIndex);

        int GetModelCount();

        bool LoadModel(string name);

        string[] GetModelNames();

        bool SaveModel(string name);

        bool DeleteModel(string name);

        void DeleteAlignment();

        bool DeleteAlignmentStar(int alignmentStarIndex);

        int GetAlignmentStarCount();

        AlignmentStarInfo GetAlignmentStarInfo(int alignmentStarIndex);

        AlignmentModelInfo GetAlignmentModelInfo();

        bool StartNewAlignmentSpec();

        bool FinishAlignmentSpec();

        int AddAlignmentStar(
            double mountRightAscension,
            double mountDeclination,
            PierSide sideOfPier,
            double plateSolvedRightAscension,
            double plateSolvedDeclination,
            double localSiderealTime);

        Task<LoadedAlignmentModel> GetLoadedAlignmentModel(CancellationToken ct);
    }
}