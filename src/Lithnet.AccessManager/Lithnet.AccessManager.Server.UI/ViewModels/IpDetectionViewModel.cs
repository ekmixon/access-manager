﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Documents;
using Lithnet.AccessManager.Server.Configuration;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.IconPacks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging;
using Stylet;

namespace Lithnet.AccessManager.Server.UI
{
    public class IpDetectionViewModel : Screen, IHelpLink
    {
        private readonly ForwardedHeadersAppOptions model;
        private readonly IDialogCoordinator dialogCoordinator;
        private readonly IShellExecuteProvider shellExecuteProvider;

        public IpDetectionViewModel(ForwardedHeadersAppOptions model, IDialogCoordinator dialogCoordinator, INotifyModelChangedEventPublisher eventPublisher, IShellExecuteProvider shellExecuteProvider)
        {
            this.shellExecuteProvider = shellExecuteProvider;
            this.DisplayName = "IP address detection";
            this.model = model;
            this.dialogCoordinator = dialogCoordinator;
            this.model.KnownNetworks ??= new List<string>();
            this.model.KnownProxies ??= new List<string>();
            this.KnownProxies = new BindableCollection<string>(model.KnownProxies);
            this.KnownNetworks = new BindableCollection<string>(model.KnownNetworks);
            eventPublisher.Register(this);
        }

        public string HelpLink => Constants.HelpLinkPageIPAddressDetection;

        [NotifyModelChangedProperty]
        public bool Enabled
        {
            get => this.model.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor);
            set
            {
                if (value)
                {
                    this.model.ForwardedHeaders |= ForwardedHeaders.XForwardedFor;
                }
                else
                {
                    this.model.ForwardedHeaders &= ~ForwardedHeaders.XForwardedFor;
                }
            }
        }

        [NotifyModelChangedProperty]
        public string ForwardedForHeaderName
        {
            get => this.model.ForwardedForHeaderName;
            set => this.model.ForwardedForHeaderName = value;
        }

        [NotifyModelChangedProperty]
        public int? ForwardLimit
        {
            get => this.model.ForwardLimit;
            set => this.model.ForwardLimit = value;
        }

        [NotifyModelChangedCollection]
        public BindableCollection<string> KnownProxies { get; }

        [NotifyModelChangedCollection]
        public BindableCollection<string> KnownNetworks { get; }

        public string SelectedNetwork { get; set; }

        public string NewNetwork { get; set; }

        public bool CanAddNetwork => this.Enabled && !string.IsNullOrWhiteSpace(this.NewNetwork);

        public async Task AddNetwork()
        {
            if (string.IsNullOrWhiteSpace(this.NewNetwork))
            {
                return;
            }

            if (this.KnownNetworks.Contains(this.NewNetwork))
            {
                await this.dialogCoordinator.ShowMessageAsync(this, "Validation",
                    "The specified network range already exists");
                return;
            }

            var split = this.NewNetwork.Split("/");

            if (split.Length != 2)
            {
                await this.dialogCoordinator.ShowMessageAsync(this, "Validation",
                    "The specified value was not a valid CIDR network range");
                return;
            }

            if (!int.TryParse(split[1], out int mask))
            {
                await this.dialogCoordinator.ShowMessageAsync(this, "Validation",
                    "The specified CIDR mask is not valid");
                return;
            }
            else
            {
                if (mask < 0 || mask > 128)
                {
                    await this.dialogCoordinator.ShowMessageAsync(this, "Validation",
                        "The specified CIDR mask is not valid");
                    return;

                }
            }

            if (!IPAddress.TryParse(split[0], out _))
            {
                await this.dialogCoordinator.ShowMessageAsync(this, "Validation",
                    "The specified value was not a valid IP address");
                return;
            }

            this.KnownNetworks.Add(this.NewNetwork);
            this.model.KnownNetworks.Add(this.NewNetwork);
            this.NewNetwork = null;
        }

        public void RemoveNetwork()
        {
            if (this.SelectedNetwork != null)
            {
                string value = this.SelectedNetwork;
                this.NewNetwork = value;
                this.KnownNetworks.Remove(value);
                this.model.KnownNetworks.Remove(value);
                this.SelectedNetwork = this.KnownNetworks.FirstOrDefault();
            }
        }

        public bool CanRemoveNetwork => this.Enabled && this.SelectedNetwork != null;

        public string SelectedProxy { get; set; }

        public string NewProxy { get; set; }

        public async Task AddProxy()
        {
            if (string.IsNullOrWhiteSpace(this.NewProxy))
            {
                return;
            }

            if (this.KnownProxies.Contains(this.NewProxy))
            {
                await this.dialogCoordinator.ShowMessageAsync(this, "Validation",
                    "The specified IP address already exists");
                return;
            }

            if (!IPAddress.TryParse(this.NewProxy, out _))
            {
                await this.dialogCoordinator.ShowMessageAsync(this, "Validation",
                    "The specified value was not a valid IP address");
                return;
            }

            this.KnownProxies.Add(this.NewProxy);
            this.model.KnownProxies.Add(this.NewProxy);
            this.NewProxy = null;
        }

        public bool CanAddProxy => this.Enabled && !string.IsNullOrWhiteSpace(this.NewProxy);

        public void RemoveProxy()
        {
            if (this.SelectedProxy != null)
            {
                string value = this.SelectedProxy;
                this.NewProxy = value;
                this.KnownProxies.Remove(value);
                this.model.KnownProxies.Remove(value);

                this.SelectedProxy = this.KnownProxies.FirstOrDefault();
            }
        }

        public bool CanRemoveProxy => this.Enabled && this.SelectedProxy != null;


        public PackIconPicolIconsKind Icon => PackIconPicolIconsKind.NetworkSansSecurity;

        public async Task Help()
        {
            await this.shellExecuteProvider.OpenWithShellExecute(this.HelpLink);
        }
    }
}
