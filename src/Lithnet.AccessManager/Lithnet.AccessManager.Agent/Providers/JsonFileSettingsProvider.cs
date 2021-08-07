﻿using System;
using System.Collections.Generic;
using Lithnet.AccessManager.Agent.Configuration;
using Lithnet.AccessManager.Api.Shared;
using Microsoft.Extensions.Options;

namespace Lithnet.AccessManager.Agent.Providers
{
    public class JsonFileSettingsProvider : IAgentSettings
    {
        private readonly IOptionsMonitor<AgentOptions> agentOptions;
        private readonly IWritableOptions<AppState> appState;

        public JsonFileSettingsProvider(IOptionsMonitor<AgentOptions> agentOptions, IWritableOptions<AppState> appState)
        {
            this.agentOptions = agentOptions;
            this.appState = appState;
        }

        public int Interval => this.agentOptions.CurrentValue.Interval;

        public bool AmsPasswordStorageEnabled => true;

        public bool AmsServerManagementEnabled => this.agentOptions.CurrentValue.Enabled;

        public bool Enabled => this.agentOptions.CurrentValue.Enabled;

        public AgentAuthenticationMode AuthenticationMode => AgentAuthenticationMode.Ams;

        public string Server => this.agentOptions.CurrentValue.Server;

        public IEnumerable<string> AzureAdTenantIDs => new List<string>();

        public bool RegisterSecondaryCredentialsForAadr => false;

        public bool RegisterSecondaryCredentialsForAadj => false;

        public int CheckInIntervalHours => this.agentOptions.CurrentValue.CheckInIntervalHours;

        public bool EnableAdminAccount => this.agentOptions.CurrentValue.EnableAdminAccount;

        public bool Reset { get; set; } = false;

        public void Clear()
        {
            this.ClientId = null;
            this.RegistrationState = 0;
            this.AuthCertificate = null;
            this.LastCheckIn = new DateTime(0);
            this.HasRegisteredSecondaryCredentials = false;
        }

        public string RegistrationKey
        {
            get => this.appState.Value.RegistrationKey;
            set
            {
                this.appState.Value.RegistrationKey = value;
                this.appState.Update(t => t.RegistrationKey = value);
            }
        }

        public string ClientId
        {
            get => this.appState.Value.ClientId;
            set
            {
                this.appState.Value.ClientId = value;
                this.appState.Update(t => t.ClientId = value);
            }
        }

        public RegistrationState RegistrationState
        {
            get => this.appState.Value.RegistrationState;
            set
            {
                this.appState.Value.RegistrationState = value;
                this.appState.Update(t => t.RegistrationState = value);
            }
        }

        public string AuthCertificate
        {
            get => this.appState.Value.AuthCertificate;
            set
            {
                this.appState.Value.AuthCertificate = value;
                this.appState.Update(t => t.AuthCertificate = value);
            }
        }

        public string CheckInDataHash
        {
            get => this.appState.Value.CheckInDataHash;
            set
            {
                this.appState.Value.CheckInDataHash = value;
                this.appState.Update(t => t.CheckInDataHash = value);
            }
        }

        public DateTime LastCheckIn
        {
            get => this.appState.Value.LastCheckIn;
            set
            {
                this.appState.Value.LastCheckIn = value;
                this.appState.Update(t => t.LastCheckIn = value);
            }
        }

        public bool HasRegisteredSecondaryCredentials
        {
            get => this.appState.Value.HasRegisteredSecondaryCredentials;
            set
            {
                this.appState.Value.HasRegisteredSecondaryCredentials = value;
                this.appState.Update(t => t.HasRegisteredSecondaryCredentials = value);
            }
        }
    }
}
