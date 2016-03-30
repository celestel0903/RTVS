﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.ObjectModel;
using Microsoft.R.Components.PackageManager.ViewModel;

namespace Microsoft.R.Components.PackageManager.Implementation.View.DesignTime {
    internal class DesignTimeRPackageManagerViewModel : IRPackageManagerViewModel {
        public ReadOnlyObservableCollection<object> Items { get; } = new ReadOnlyObservableCollection<object>(new ObservableCollection<object> {
            new DesignTimeRPackageViewModel("abbyyR", "0.3", "0.3", "abc.data, nnet, quantreg, MASS, locfit", "GPL (>= 3)", true, false),
            new DesignTimeRPackageViewModel("abctools", "1.0.4", "1.0.0", "abc, abind, parallel, plyr", "GPL (>= 2)", true, true),
            new DesignTimeRPackageViewModel("ggplot2", "2.1.0", null, "digest, grid, gtable (>= 0.1.1), MASS, plyr (>= 1.7.1), reshape2, scales (>= 0.3.0), stats", "GPL-2", false, false)
        });

        public IRPackageViewModel SelectedPackage => (IRPackageViewModel)Items[1];
        public bool IsLoading => false;

        public void SwitchToAvailablePackages() {
            
        }

        public void SwitchToInstalledPackages() {
            
        }

        public void SwitchToLoadedPackages() {
            
        }

        public void ReloadItems() {
            
        }
    }
}
