﻿using System.Collections.Generic;
using Lithnet.AccessManager.Server.Configuration;
using MahApps.Metro.Controls.Dialogs;
using Stylet;

namespace Lithnet.AccessManager.Server.UI
{
    public class WebhookNotificationChannelDefinitionsViewModel : NotificationChannelDefinitionsViewModel<WebhookNotificationChannelDefinition, WebhookNotificationChannelDefinitionViewModel>
    {
        public WebhookNotificationChannelDefinitionsViewModel(IList<WebhookNotificationChannelDefinition> model, WebhookNotificationChannelDefinitionViewModelFactory factory, IDialogCoordinator dialogCoordinator, IEventAggregator eventAggregator, INotifyModelChangedEventPublisher eventPublisher) :
            base(model, factory, dialogCoordinator, eventAggregator, eventPublisher)
        {
            this.DisplayName = "Webhook";
        }
    }
}