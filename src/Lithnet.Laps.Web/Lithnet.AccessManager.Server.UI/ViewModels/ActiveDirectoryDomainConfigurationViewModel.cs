﻿using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Security.Principal;
using System.Threading.Tasks;
using Accessibility;
using MahApps.Metro.Controls.Dialogs;
using Stylet;

namespace Lithnet.AccessManager.Server.UI
{
    public class ActiveDirectoryDomainConfigurationViewModel : PropertyChangedBase
    {
        private readonly Domain domain;

        private readonly SecurityIdentifier waagSid = new SecurityIdentifier("S-1-5-32-560");

        private readonly SecurityIdentifier acaoSid = new SecurityIdentifier("S-1-5-32-579");

        private readonly SecurityIdentifier serviceAccountSid;

        private readonly IDialogCoordinator dialogCoordinator;

        public ActiveDirectoryDomainConfigurationViewModel(Domain domain, IServiceSettingsProvider serviceSettings, IDirectory directory, IDialogCoordinator dialogCoordinator)
        {
            this.domain = domain;
            this.dialogCoordinator = dialogCoordinator;

            this.serviceAccountSid = serviceSettings.GetServiceAccount();

            this.RefreshGroupMembership();
        }

        public void RefreshGroupMembership()
        {
            _ = this.CheckWaagStatus();
            _ = this.CheckAcaoStatus();
        }

        public string WaagStatus { get; set; }

        public string AcaoStatus { get; set; }

        public bool IsWaagMember { get; set; }

        public bool IsNotWaagMember { get; set; }

        public bool IsAcaoMember { get; set; }

        public bool IsNotAcaoMember { get; set; }

        public string DisplayName => this.domain.Name;

        public async Task ShowADPermissionScript()
        {
            var vm = new ScriptContentViewModel(this.dialogCoordinator)
            {
                HelpText = "Run the following script with Domain Admins rights to add the service account to the correct groups",
                ScriptText = ScriptTemplates.AddDomainGroupMembershipPermissions
                    .Replace("{domainDNS}", this.domain.Name, StringComparison.OrdinalIgnoreCase)
                    .Replace("{serviceAccountSid}", this.serviceAccountSid.Value, StringComparison.OrdinalIgnoreCase)
            };

            ExternalDialogWindow w = new ExternalDialogWindow
            {
                DataContext = vm,
                SaveButtonVisible = false,
                CancelButtonName = "Close"
            };

            w.ShowDialog();

            this.RefreshGroupMembership();
        }

        private async Task CheckAcaoStatus()
        {
            await Task.Run(() =>
            {
                try
                {
                    this.AcaoLookupInProgress = true;
                    this.IsAcaoMember = false;
                    this.IsNotAcaoMember = false;
                    this.AcaoStatus = "Checking...";

                    if (this.serviceAccountSid == null)
                    {
                        this.IsNotAcaoMember = true;
                        this.AcaoStatus = "Could not determine service account";
                        return;
                    }

                    if (this.IsGroupMember(this.acaoSid, this.serviceAccountSid))
                    {
                        this.AcaoStatus = "Group membership confirmed";
                        this.IsAcaoMember = true;
                    }
                    else
                    {
                        this.AcaoStatus = "Group membership not found";
                        this.IsNotAcaoMember = true;
                    }
                }
                catch (Exception ex)
                {
                    this.AcaoStatus = "Group membership lookup error";
                    this.IsNotAcaoMember = true;
                }
                finally
                {
                    this.AcaoLookupInProgress = false;
                }
            }).ConfigureAwait(false);
        }

        public bool WaagLookupInProgress { get; set; }

        public bool AcaoLookupInProgress { get; set; }

        private async Task CheckWaagStatus()
        {
            await Task.Run(() =>
            {
                try
                {
                    this.IsWaagMember = false;
                    this.IsNotWaagMember = false;
                    this.WaagLookupInProgress = true;
                    this.WaagStatus = "Checking...";

                    if (this.serviceAccountSid == null)
                    {
                        this.WaagStatus = "Could not determine service account";
                        this.IsNotWaagMember = true;
                        return;
                    }

                    if (this.IsGroupMember(this.waagSid, this.serviceAccountSid))
                    {
                        this.WaagStatus = "Group membership confirmed";
                        this.IsWaagMember = true;
                    }
                    else
                    {
                        this.WaagStatus = "Group membership not found";
                        this.IsNotWaagMember = false;
                    }
                }
                catch (Exception ex)
                {
                    this.WaagStatus = "Group membership lookup error";
                    this.IsNotWaagMember = true;
                }
                finally
                {
                    this.WaagLookupInProgress = false;
                }
            }).ConfigureAwait(false);
        }

        private bool IsGroupMember(SecurityIdentifier groupSid, SecurityIdentifier userSid)
        {
            using PrincipalContext p = new PrincipalContext(ContextType.Domain, this.domain.Name);
            using GroupPrincipal g = GroupPrincipal.FindByIdentity(p, IdentityType.Sid, groupSid.ToString());
            return IsGroupMember(g, userSid, new HashSet<string>());
        }

        private bool IsGroupMember(GroupPrincipal g, SecurityIdentifier userSid, HashSet<string> searchedGroups)
        {
            if (!(searchedGroups.Add(g.Sid.ToString())))
            {
                return false;
            }

            foreach (var member in g.GetMembers())
            {
                if (member.Sid == userSid)
                {
                    return true;
                }

                if (member is GroupPrincipal g2)
                {
                    if (IsGroupMember(g2, userSid, searchedGroups))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
