#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Enum;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Photon.Plugin.ASA.Model;
using NINA.WPF.Base.Mediator;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Photon.Plugin.ASA.Equipment {

    public class MountModelMediator : DeviceMediator<IMountModelVM, IMountModelConsumer, MountModelInfo>, IMountModelMediator {

        public void DeleteAlignment() {
            handler.DeleteAlignment();
        }

        public bool DeleteAlignmentStar(int alignmentStarIndex) {
            return handler.DeleteAlignmentStar(alignmentStarIndex);
        }

        public bool DeleteModel(string name) {
            return handler.DeleteModel(name);
        }

        public bool FinishAlignmentSpec() {
            return handler.FinishAlignmentSpec();
        }

        public AlignmentModelInfo GetAlignmentModelInfo() {
            return handler.GetAlignmentModelInfo();
        }

        public int GetAlignmentStarCount() {
            return handler.GetAlignmentStarCount();
        }

        public AlignmentStarInfo GetAlignmentStarInfo(int alignmentStarIndex) {
            return handler.GetAlignmentStarInfo(alignmentStarIndex);
        }

        public int GetModelCount() {
            return handler.GetModelCount();
        }

        public string GetModelName(int modelIndex) {
            return handler.GetModelName(modelIndex);
        }

        public string[] GetModelNames() {
            return handler.GetModelNames();
        }

        public bool LoadModel(string name) {
            return handler.LoadModel(name);
        }

        public bool SaveModel(string name) {
            return handler.SaveModel(name);
        }

        public bool StartNewAlignmentSpec() {
            return handler.StartNewAlignmentSpec();
        }

        public int AddAlignmentStar(
            AstrometricTime mountRightAscension,
            CoordinateAngle mountDeclination,
            PierSide sideOfPier,
            AstrometricTime plateSolvedRightAscension,
            CoordinateAngle plateSolvedDeclination,
            AstrometricTime localSiderealTime) {
            return handler.AddAlignmentStar(mountRightAscension, mountDeclination, sideOfPier, plateSolvedRightAscension, plateSolvedDeclination, localSiderealTime);
        }

        public Task<LoadedAlignmentModel> GetLoadedAlignmentModel(CancellationToken ct) {
            return handler.GetLoadedAlignmentModel(ct);
        }
    }
}