﻿using System.Collections.Generic;
using Lithnet.AccessManager.Server.Configuration;
using MahApps.Metro.Controls.Dialogs;
using Stylet;

namespace Lithnet.AccessManager.Server.UI
{
    public class WebhookNotificationChannelDefinitionsViewModelFactory : NotificationChannelDefinitionsViewModelFactory<WebhookNotificationChannelDefinition, WebhookNotificationChannelDefinitionViewModel>
    {
        private readonly WebhookNotificationChannelDefinitionViewModelFactory factory;
        private readonly IDialogCoordinator dialogCoordinator;
        private readonly IEventAggregator eventAggregator;

        public WebhookNotificationChannelDefinitionsViewModelFactory(WebhookNotificationChannelDefinitionViewModelFactory factory, IDialogCoordinator dialogCoordinator, IEventAggregator eventAggregator) : base(factory)
        {
            this.factory = factory;
            this.dialogCoordinator = dialogCoordinator;
            this.eventAggregator = eventAggregator;
        }

        public override NotificationChannelDefinitionsViewModel<WebhookNotificationChannelDefinition, WebhookNotificationChannelDefinitionViewModel> CreateViewModel(IList<WebhookNotificationChannelDefinition> model)
        {
            return new WebhookNotificationChannelDefinitionsViewModel(model, this.factory, this.dialogCoordinator, this.eventAggregator);
        }
    }
}
