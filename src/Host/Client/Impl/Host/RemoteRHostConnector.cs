﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.R.Host.Client.NativeMethods;

namespace Microsoft.R.Host.Client.Host {
    internal sealed class RemoteRHostConnector : RHostConnector {
        private readonly NetworkCredential _credentials;
        private readonly AutoResetEvent _credentialsValidated = new AutoResetEvent(true);
        private readonly string _authority;
        private bool _ignoreSavedCredentials;

        public RemoteRHostConnector(Uri brokerUri)
            : base(brokerUri.Fragment) {

            _credentials = new NetworkCredential();
            _authority = new UriBuilder() { Scheme = brokerUri.Scheme, Host = brokerUri.Host, Port = brokerUri.Port }.ToString();

            CreateHttpClient(brokerUri, _credentials);
        }

        protected override Task ConnectToBrokerAsync() {
            return Task.CompletedTask;
        }

        private void GetCredentials(out string userName, out string password) {
            // If there is already a GetCredentials request for which there hasn't been a validation yet, wait until it completes.
            // This can happen when two sessions are being created concurrently, and we don't want to pop the credential prompt twice -
            // the first prompt should be validated and saved, and then the same credentials will be reused for the second session.
            _credentialsValidated.WaitOne();

            var userNameBuilder = new StringBuilder(CREDUI_MAX_USERNAME_LENGTH + 1);
            var passwordBuilder = new StringBuilder(CREDUI_MAX_PASSWORD_LENGTH + 1);

            bool save = false;

            int flags = CREDUI_FLAGS_EXCLUDE_CERTIFICATES | CREDUI_FLAGS_PERSIST | CREDUI_FLAGS_EXPECT_CONFIRMATION | CREDUI_FLAGS_GENERIC_CREDENTIALS;
            if (_ignoreSavedCredentials) {
                flags |= CREDUI_FLAGS_ALWAYS_SHOW_UI;
            }

            int err = CredUIPromptForCredentials(IntPtr.Zero, _authority, IntPtr.Zero, 0, userNameBuilder, userNameBuilder.Capacity, passwordBuilder, passwordBuilder.Capacity, ref save, flags);
            if (err != 0) {
                throw new OperationCanceledException("No credentials entered.");
            }

            userName = userNameBuilder.ToString();
            password = passwordBuilder.ToString();
        }

        protected override void UpdateCredentials() {
            string userName, password;
            GetCredentials(out userName, out password);

            _credentials.UserName = userName;
            _credentials.Password = password;
        }

        protected override void OnCredentialsValidated(bool isValid) {
            CredUIConfirmCredentials(_authority, isValid);
            _ignoreSavedCredentials = !isValid;
            _credentialsValidated.Set();
        }
    }
}
