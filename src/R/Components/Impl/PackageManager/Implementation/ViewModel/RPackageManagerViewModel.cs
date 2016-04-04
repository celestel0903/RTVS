﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Common.Core;
using Microsoft.Common.Core.Shell;
using Microsoft.Common.Core.Threading;
using Microsoft.Common.Wpf;
using Microsoft.Common.Wpf.Collections;
using Microsoft.R.Components.PackageManager.Model;
using Microsoft.R.Components.PackageManager.ViewModel;

namespace Microsoft.R.Components.PackageManager.Implementation.ViewModel {
    internal class RPackageManagerViewModel : BindableBase, IRPackageManagerViewModel {
        private readonly IRPackageManager _packageManager;
        private readonly ICoreShell _coreShell;
        private readonly BinaryAsyncLock _availableLock;
        private readonly BinaryAsyncLock _installedAndLoadedLock;
        private readonly BatchObservableCollection<object> _items;

        private volatile IList<IRPackageViewModel> _availablePackages;
        private volatile IList<IRPackageViewModel> _installedPackages;
        private volatile IList<IRPackageViewModel> _loadedPackages;
        private volatile string _searchString;

        private SelectedTab _selectedTab;
        private bool _isLoading;
        private IRPackageViewModel _selectedPackage;

        public RPackageManagerViewModel(IRPackageManager packageManager, ICoreShell coreShell) {
            _packageManager = packageManager;
            _coreShell = coreShell;
            _selectedTab = SelectedTab.None;
            _availablePackages = new List<IRPackageViewModel>();
            _installedPackages = new List<IRPackageViewModel>();
            _loadedPackages = new List<IRPackageViewModel>();
            _availableLock = new BinaryAsyncLock();
            _installedAndLoadedLock = new BinaryAsyncLock();
            _items = new BatchObservableCollection<object>();
            Items = new ReadOnlyObservableCollection<object>(_items);
        }

        public ReadOnlyObservableCollection<object> Items { get; }

        public IRPackageViewModel SelectedPackage {
            get { return _selectedPackage; }
            private set { SetProperty(ref _selectedPackage, value); }
        }

        public bool IsLoading {
            get { return _isLoading; }
            private set { SetProperty(ref _isLoading, value); }
        }

        public void SwitchToAvailablePackages() {
            _coreShell.AssertIsOnMainThread();
            if (_selectedTab == SelectedTab.AvailablePackages) {
                return;
            }

            _selectedTab = SelectedTab.AvailablePackages;
            DispatchOnMainThread(SwitchToAvailablePackagesAsync);
        }

        public void SwitchToInstalledPackages() {
            _coreShell.AssertIsOnMainThread();
            if (_selectedTab == SelectedTab.InstalledPackages) {
                return;
            }

            _selectedTab = SelectedTab.InstalledPackages;
            DispatchOnMainThread(SwitchToInstalledPackagesAsync);
        }

        public void SwitchToLoadedPackages() {
            _coreShell.AssertIsOnMainThread();
            if (_selectedTab == SelectedTab.LoadedPackages) {
                return;
            }

            _selectedTab = SelectedTab.LoadedPackages;
            DispatchOnMainThread(SwitchToLoadedPackagesAsync);
        }

        public void ReloadItems() {
            _coreShell.AssertIsOnMainThread();
            switch (_selectedTab) {
                case SelectedTab.AvailablePackages:
                    //LoadAvailablePackages();
                    break;
                case SelectedTab.InstalledPackages:
                    //LoadInstalledPackages();
                    break;
                case SelectedTab.LoadedPackages:
                    break;
                case SelectedTab.None:
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void SelectPackage(IRPackageViewModel package) {
            _coreShell.AssertIsOnMainThread();
            if (package == _selectedPackage) {
                return;
            }

            SelectedPackage = package;
            if (package == null) {
                return;
            }

            if (!package.HasDetails) {
                DispatchOnMainThread(() => AddPackageDetailsAsync(package));
            }
        }

        private async Task AddPackageDetailsAsync(IRPackageViewModel package) {
            _coreShell.AssertIsOnMainThread();
            var details = await GetAdditionalPackageInfoAsync(package);
            package.AddDetails(details, false);
        }

        private async Task<RPackage> GetAdditionalPackageInfoAsync(IRPackageViewModel package) {
            await TaskUtilities.SwitchToBackgroundThread();
            return await _packageManager.GetAdditionalPackageInfoAsync(package.Name, package.Repository);
        }

        private async Task SwitchToAvailablePackagesAsync() {
            _coreShell.AssertIsOnMainThread();
            if (_selectedTab == SelectedTab.AvailablePackages) {
                IsLoading = !_availableLock.IsCompleted;
            }
            
            await EnsureAvailablePackagesLoadedAsync();
            if (_selectedTab == SelectedTab.AvailablePackages) {
                IsLoading = false;
            }

            if (_availablePackages == null) {
                return;
            }
            _items.ReplaceWith(_availablePackages);
        }

        private async Task EnsureAvailablePackagesLoadedAsync() {
            var availablePackagesLoaded = await _availableLock.WaitAsync();
            if (!availablePackagesLoaded) {
                try {
                    await LoadAvailablePackagesAsync();
                } catch (RPackageManagerException ex) { 
                } finally {
                    _availableLock.Release();
                }
            }
        }

        private async Task LoadAvailablePackagesAsync() {
            await TaskUtilities.SwitchToBackgroundThread();
            var availablePackages = await _packageManager.GetAvailablePackagesAsync();
            var vmAvailablePackages = new List<IRPackageViewModel>();

            var installedPackages = _installedPackages.ToDictionary(p => p.Name, p => p);
            foreach (var package in availablePackages) {
                IRPackageViewModel installedPackage;
                if (installedPackages.TryGetValue(package.Package, out installedPackage)) {
                    installedPackage.UpdateAvailablePackageDetails(package);
                    vmAvailablePackages.Add(installedPackage);
                } else {
                    vmAvailablePackages.Add(RPackageViewModel.CreateAvailable(package));
                }
            }

            _availablePackages = vmAvailablePackages.OrderBy(p => p.Name).ToList();
        }

        private async Task SwitchToInstalledPackagesAsync() {
            _coreShell.AssertIsOnMainThread();
            if (_selectedTab == SelectedTab.InstalledPackages) {
                IsLoading = true;
            }

            await ReloadInstalledAndLoadedPackagesAsync();
            if (_selectedTab == SelectedTab.InstalledPackages) {
                IsLoading = false;
            }
            
            _items.ReplaceWith(_installedPackages);
        }

        private async Task ReloadInstalledAndLoadedPackagesAsync() {
            _installedAndLoadedLock.ResetIfNotWaiting();
            var areLoaded = await _installedAndLoadedLock.WaitAsync();
            if (areLoaded) {
                return;
            }

            try {
                await LoadInstalledAndLoadedPackagesAsync();
            } catch (RPackageManagerException ex) {
            } finally {
                _installedAndLoadedLock.Release();
            }
        }

        private async Task LoadInstalledAndLoadedPackagesAsync() {
            await TaskUtilities.SwitchToBackgroundThread();

            var markUninstalledAndUnloadedTask =  _coreShell.DispatchOnMainThreadAsync(MarkUninstalledAndUnloaded);
            var getInstalledPackagesTask = _packageManager.GetInstalledPackagesAsync();
            await Task.WhenAll(markUninstalledAndUnloadedTask, getInstalledPackagesTask);

            var installedPackages = getInstalledPackagesTask.Result;
            if (!_availableLock.IsCompleted) {
                _installedPackages = installedPackages
                    .Select(RPackageViewModel.CreateInstalled)
                    .OrderBy(p => p.Name)
                    .ToList<IRPackageViewModel>();
                DispatchOnMainThread(EnsureAvailablePackagesLoadedAsync);
            } else {
                var vmAvailablePackages = _availablePackages.ToDictionary(k => k.Name);
                var vmInstalledPackages = new List<IRPackageViewModel>();

                foreach (var installedPackage in installedPackages) {
                    IRPackageViewModel vmPackage;
                    if (vmAvailablePackages.TryGetValue(installedPackage.Package, out vmPackage)) {
                        vmPackage.AddDetails(installedPackage, true);
                        vmInstalledPackages.Add(vmPackage);
                    } else {
                        vmInstalledPackages.Add(RPackageViewModel.CreateInstalled(installedPackage));
                    }
                }

                _installedPackages = vmInstalledPackages.OrderBy(p => p.Name).ToList();
            }

            var loadedPackageNames = await _packageManager.GetLoadedPackagesAsync();
            var vmLoadedPackages = _installedPackages.Where(p => loadedPackageNames.Contains(p.Name)).ToList();
            foreach (var package in vmLoadedPackages) {
                package.IsLoaded = true;
            }

            _loadedPackages = vmLoadedPackages;
        }

        private void MarkUninstalledAndUnloaded() {
            _coreShell.AssertIsOnMainThread();
            foreach (var package in _installedPackages) {
                package.IsInstalled = false;
                package.IsLoaded = false;
            }
        }

        private async Task SwitchToLoadedPackagesAsync() {
            _coreShell.AssertIsOnMainThread();
            if (_installedAndLoadedLock.IsCompleted) {
                _items.ReplaceWith(_loadedPackages);
                return;
            }

            if (_selectedTab == SelectedTab.LoadedPackages) {
                IsLoading = true;
            }

            await ReloadInstalledAndLoadedPackagesAsync();
            if (_selectedTab == SelectedTab.LoadedPackages) {
                IsLoading = false;
            }

            _items.ReplaceWith(_loadedPackages);
        }

        private void DispatchOnMainThread(Func<Task> callback) {
            _coreShell.DispatchOnMainThreadAsync(callback)
                .Unwrap()
                .SilenceException<OperationCanceledException>()
                .DoNotWait();
        }

        public async Task<int> Search(string searchString, CancellationToken cancellationToken) {
            _searchString = searchString;
            switch (_selectedTab) {
                case SelectedTab.AvailablePackages:
                    await EnsureAvailablePackagesLoadedAsync();
                    return Search(_availablePackages, searchString, cancellationToken);
                case SelectedTab.InstalledPackages:
                    await ReloadInstalledAndLoadedPackagesAsync();
                    return Search(_installedPackages, searchString, cancellationToken);
                case SelectedTab.LoadedPackages:
                    return Search(_loadedPackages, searchString, cancellationToken);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private int Search(IList<IRPackageViewModel> packages, string searchString, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(searchString)) {
                _coreShell.DispatchOnUIThread(() => ApplySearch(packages, cancellationToken));
                return packages.Count;
            }

            var filteredPackages = new List<IRPackageViewModel>();
            foreach (var package in packages){
                if (cancellationToken.IsCancellationRequested) {
                    return filteredPackages.Count;
                }

                if (package.Name.StartsWithIgnoreCase(searchString)) {
                    filteredPackages.Add(package);
                }
            }

            _coreShell.DispatchOnUIThread(() => ApplySearch(filteredPackages, cancellationToken));
            return filteredPackages.Count;
        }

        private void ApplySearch(IList<IRPackageViewModel> packages, CancellationToken cancellationToken) {
            _coreShell.AssertIsOnMainThread();
            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            _items.ReplaceWith(packages);
        }

        private enum SelectedTab {
            None,
            AvailablePackages,
            InstalledPackages,
            LoadedPackages,
        }
    }
}
