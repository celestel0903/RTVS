using System;
using Microsoft.Languages.Editor;
using Microsoft.Languages.Editor.Controller.Command;
using Microsoft.R.Support.Settings;
using Microsoft.R.Support.Settings.Definitions;
using Microsoft.VisualStudio.R.Package.Commands;
using Microsoft.VisualStudio.R.Package.Repl;
using Microsoft.VisualStudio.R.Packages.R;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.R.Package.History.Commands {
    internal class ToggleMultilineHistorySelectionCommand : ViewCommand {
        private readonly IRToolsSettings _settings;
        private readonly IRHistory _history;

        public ToggleMultilineHistorySelectionCommand(ITextView textView, IRHistoryProvider historyProvider, IRToolsSettings settings)
            : base(textView, RGuidList.RCmdSetGuid, RPackageCommandId.icmdToggleMultilineSelection, false) {
            _history = historyProvider.GetAssociatedRHistory(textView);
            _settings = settings;
        }

        public override CommandStatus Status(Guid guid, int id) {
            return ReplWindow.ReplWindowExists()
                ? RToolsSettings.Current.MultilineHistorySelection 
                    ? CommandStatus.Latched | CommandStatus.SupportedAndEnabled
                    : CommandStatus.SupportedAndEnabled
                : CommandStatus.Supported;
        }

        public override CommandResult Invoke(Guid guid, int id, object inputArg, ref object outputArg) {
            _settings.MultilineHistorySelection = !_settings.MultilineHistorySelection;
            _history.IsMultiline = _settings.MultilineHistorySelection;
            return CommandResult.Executed;
        }
    }
}