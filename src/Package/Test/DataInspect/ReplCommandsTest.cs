﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Common.Core;
using Microsoft.R.Debugger;
using Microsoft.R.Host.Client;
using Microsoft.R.Host.Client.Test.Script;
using Microsoft.UnitTests.Core.XUnit;
using Microsoft.VisualStudio.R.Package.DataInspect;
using Microsoft.VisualStudio.R.Package.DataInspect.Viewers;
using Microsoft.VisualStudio.R.Package.Shell;
using NSubstitute;
using Xunit;

namespace Microsoft.VisualStudio.R.Package.Test.DataInspect {
    [ExcludeFromCodeCoverage]
    [Collection(CollectionNames.NonParallel)]   // required for tests using R Host 
    public class ReplCommandsTest {
        [Test]
        [Category.Variable.Explorer]
        public async Task ViewLibraryTest() {
            var provider = VsAppShell.Current.ExportProvider.GetExportedValue<IRSessionProvider>();
            var cb = Substitute.For<IRSessionCallback>();
            cb.ViewLibrary().Returns(Task.CompletedTask);
            using (var hostScript = new RHostScript(provider, cb)) {
                using (var inter = await hostScript.Session.BeginInteractionAsync()) {
                    await inter.RespondAsync("library()" + Environment.NewLine);
                }
            }
            await cb.Received().ViewLibrary();
        }

        [Test]
        [Category.Variable.Explorer]
        public async Task ViewDataTest01() {
            var provider = VsAppShell.Current.ExportProvider.GetExportedValue<IRSessionProvider>();
            var cb = Substitute.For<IRSessionCallback>();
            cb.When(x => x.ViewObject(Arg.Any<string>(), Arg.Any<string>())).Do(x => { });
            using (var hostScript = new RHostScript(provider, cb)) {
                using (var inter = await hostScript.Session.BeginInteractionAsync()) {
                    await inter.RespondAsync("View(mtcars)" + Environment.NewLine);
                }
            }
            cb.Received().ViewObject("mtcars", "");
        }

        [Test]
        [Category.Variable.Explorer]
        public async Task ViewerExportTest() {
            var provider = VsAppShell.Current.ExportProvider.GetExportedValue<IRSessionProvider>();
            using (var hostScript = new RHostScript(provider)) {
                var aggregator = VsAppShell.Current.ExportProvider.GetExportedValue<IObjectDetailsViewerAggregator>();
                aggregator.Should().NotBeNull();

                var funcViewer = await aggregator.GetViewer("lm");
                funcViewer.Should().NotBeNull().And.BeOfType<FunctionViewer>();

                var gridViewer = await aggregator.GetViewer("airmiles");
                gridViewer.Should().NotBeNull().And.BeOfType<GridViewer>();

                gridViewer = await aggregator.GetViewer("mtcars");
                gridViewer.Should().NotBeNull().And.BeOfType<GridViewer>();

                gridViewer = await aggregator.GetViewer("AirPassengers");
                gridViewer.Should().NotBeNull().And.BeOfType<GridViewer>();

                gridViewer.Capabilities.Should().HaveFlag(ViewerCapabilities.List | ViewerCapabilities.Table);
            }
        }

        [Test]
        [Category.Variable.Explorer]
        public void ViewerTest02() {
            var aggregator = VsAppShell.Current.ExportProvider.GetExportedValue<IObjectDetailsViewerAggregator>();
            var odv = aggregator as ObjectDetailsViewerAggregator;

            var result1 = Substitute.For<IDebugValueEvaluationResult>();
            var result2 = Substitute.For<IDebugValueEvaluationResult>();
            var result3 = Substitute.For<IDebugValueEvaluationResult>();

            var viewer1 = Substitute.For<IObjectDetailsViewer>();
            viewer1.CanView(result1).Returns(true);

            var viewer2 = Substitute.For<IObjectDetailsViewer>();
            viewer2.CanView(result2).Returns(true);

            var viewers = new List<Lazy<IObjectDetailsViewer>>();
            viewers.Add(new Lazy<IObjectDetailsViewer>(() => viewer1));
            viewers.Add(new Lazy<IObjectDetailsViewer>(() => viewer2));
            odv.Viewers = viewers;

            aggregator.GetViewer(result1).Should().Be(viewer1);
            aggregator.GetViewer(result2).Should().Be(viewer2);
            aggregator.GetViewer(result3).Should().BeNull();
        }

        [Test]
        [Category.Variable.Explorer]
        public async Task ViewerTest03() {
            var viewer = Substitute.For<IObjectDetailsViewer>();
            viewer.ViewAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);

            var evaluator = Substitute.For<IDataObjectEvaluator>();

            var aggregator = Substitute.For<IObjectDetailsViewerAggregator>();
            aggregator.GetViewer(Arg.Any<string>()).Returns(viewer);

            var provider = new ObjectDetailsViewerProvider(aggregator, evaluator);
            await provider.ViewObjectDetails("mtcars", null);

            await viewer.Received().ViewAsync("mtcars", null);
        }

        [Test]
        [Category.Variable.Explorer]
        public async Task FunctionViewerTest() {
            var provider = VsAppShell.Current.ExportProvider.GetExportedValue<IRSessionProvider>();
            using (var hostScript = new RHostScript(provider)) {
                var aggregator = VsAppShell.Current.ExportProvider.GetExportedValue<IObjectDetailsViewerAggregator>();
                aggregator.Should().NotBeNull();

                var funcViewer = await aggregator.GetViewer("lm") as FunctionViewer;
                funcViewer.Should().NotBeNull();

                funcViewer.GetFileName("lm", null).Should().Be(Path.Combine(Path.GetTempPath(), "~lm.r"));
                funcViewer.GetFileName("lm", "abc").Should().Be(Path.Combine(Path.GetTempPath(), "~abc.r"));

                var code = await funcViewer.GetFunctionCode("lm");
                code.StartsWithOrdinal("function(formula, data, subset, weights, na.action").Should().BeTrue();
            }
        }
        [Test]
        [Category.Variable.Explorer]
        public async Task GridViewerTest() {
            var provider = VsAppShell.Current.ExportProvider.GetExportedValue<IRSessionProvider>();
            using (var hostScript = new RHostScript(provider)) {
                var aggregator = VsAppShell.Current.ExportProvider.GetExportedValue<IObjectDetailsViewerAggregator>();
                aggregator.Should().NotBeNull();

                var gridViewer = await aggregator.GetViewer("mtcars") as GridViewer;
                gridViewer.Should().NotBeNull();

                var eval = Substitute.For<IDebugValueEvaluationResult>();
                eval.Classes.Returns(new List<string>() { "foo" });

                gridViewer.CanView(null).Should().BeFalse();
                gridViewer.CanView(eval).Should().BeFalse();

                eval.Dim.Count.Returns(0);
                gridViewer.CanView(eval).Should().BeFalse();

                foreach (var c in new string[] { "matrix", "data.frame", "table" }) {
                    eval.Classes.Returns(new List<string>() { c });
                    eval.Dim.Count.Returns(3);
                    gridViewer.CanView(eval).Should().BeFalse();
                    eval.Dim.Count.Returns(2);
                    gridViewer.CanView(eval).Should().BeTrue();
                    eval.Dim.Count.Returns(1);
                    gridViewer.CanView(eval).Should().BeTrue();
                    eval.Dim.Count.Returns(0);
                    gridViewer.CanView(eval).Should().BeFalse();
                }

                eval.Dim.Returns((IReadOnlyList<int>)null);
                foreach (var c in new string[] { "list", "ts" }) {
                    eval.Classes.Returns(new List<string>() { c });
                    gridViewer.CanView(eval).Should().BeTrue();
                }

                eval.Dim.Returns((IReadOnlyList<int>)null);
                foreach (var c in new string[] { "a", "b" }) {
                    eval.Classes.Returns(new List<string>() { c });
                    gridViewer.CanView(eval).Should().BeFalse();
                }

                eval.Classes.Returns(new List<string>() { "foo", "bar" });
                gridViewer.CanView(eval).Should().BeFalse();

                eval.Classes.Returns(new List<string>() { "blah" });
                eval.Length.Returns(1);
                gridViewer.CanView(eval).Should().BeFalse();
                eval.Length.Returns(2);
                gridViewer.CanView(eval).Should().BeTrue();
            }
        }
    }
}
