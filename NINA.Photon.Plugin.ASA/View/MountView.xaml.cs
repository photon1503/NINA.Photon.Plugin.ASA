#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Photon.Plugin.ASA.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace NINA.Photon.Plugin.ASA.View {

    /// <summary>
    /// Interaction logic for MountView.xaml
    /// </summary>
    public partial class MountView : UserControl {

        public MountView() {
            InitializeComponent();
        }

        private void UnattendedFlipEnabled_Unchecked(object sender, RoutedEventArgs e) {
            var checkBox = (CheckBox)sender;
            var mountVM = (MountVM)checkBox.DataContext;
            mountVM.DisableUnattendedFlip();
        }

        private void DualAxisTrackingEnabled_Toggled(object sender, RoutedEventArgs e) {
            var checkBox = (CheckBox)sender;
            var mountVM = (MountVM)checkBox.DataContext;
            var isChecked = checkBox.IsChecked;
            if (isChecked.HasValue) {
                mountVM.SetDualAxisTracking(isChecked.Value);
            }
        }
    }
}