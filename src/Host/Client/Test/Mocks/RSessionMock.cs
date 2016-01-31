﻿using System;
using System.Threading.Tasks;

namespace Microsoft.R.Host.Client.Test.Mocks {
    public sealed class RSessionMock : IRSession {
        private IRSessionEvaluation _eval;
        private IRSessionInteraction _inter;

        public int Id { get; set; }

        public bool IsHostRunning { get; set; }

        public string Prompt { get; set; } = ">";

        public Task<IRSessionEvaluation> BeginEvaluationAsync(bool isMutating = true) {
            _eval = new RSessionEvaluationMock();

            BeforeRequest?.Invoke(this, new RRequestEventArgs(_eval.Contexts, Prompt, 4096, true));
            if (isMutating) {
                Mutated?.Invoke(this, EventArgs.Empty);
            }
            return Task.FromResult(_eval);
        }

        public Task<IRSessionInteraction> BeginInteractionAsync(bool isVisible = true) {
            _inter = new RSessionInteractionMock();
            BeforeRequest?.Invoke(this, new RRequestEventArgs(_inter.Contexts, Prompt, 4096, true));
            return Task.FromResult(_inter);
        }

        public Task CancelAllAsync() {
            if (_eval != null) {
                AfterRequest?.Invoke(this, new RRequestEventArgs(_eval.Contexts, Prompt, 4096, true));
                _eval = null;
            }
            else if (_inter != null) {
                AfterRequest?.Invoke(this, new RRequestEventArgs(_inter.Contexts, Prompt, 4096, true));
                _inter = null;
            }
            return Task.CompletedTask;
        }

        public void Dispose() {
            StopHostAsync().Wait();
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        public void FlushLog() {
        }

        public Task StartHostAsync(RHostStartupInfo startupInfo, int timeout = 3000) {
            IsHostRunning = true;
            Connected?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task StopHostAsync() {
            IsHostRunning = false;
            Disconnected?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

#pragma warning disable 67
        public event EventHandler<RRequestEventArgs> AfterRequest;
        public event EventHandler<RRequestEventArgs> BeforeRequest;
        public event EventHandler<EventArgs> Connected;
        public event EventHandler<EventArgs> DirectoryChanged;
        public event EventHandler<EventArgs> Disconnected;
        public event EventHandler<EventArgs> Disposed;
        public event EventHandler<EventArgs> Mutated;
        public event EventHandler<ROutputEventArgs> Output;
    }
}