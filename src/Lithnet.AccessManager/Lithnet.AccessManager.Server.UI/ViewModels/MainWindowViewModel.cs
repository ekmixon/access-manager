﻿using System;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Threading.Tasks;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Extensions.Logging;
using Stylet;

namespace Lithnet.AccessManager.Server.UI
{
    public class MainWindowViewModel : Conductor<ApplicationConfigViewModel>, IHandle<ModelChangedEvent>
    {
        private readonly IEventAggregator eventAggregator;

        private readonly IDialogCoordinator dialogCoordinator;

        private readonly ILogger<MainWindowViewModel> logger;

        private readonly IShellExecuteProvider shellExecuteProvider;
        
        public ApplicationConfigViewModel Config { get; set; }

        public MainWindowViewModel(ApplicationConfigViewModel c, IEventAggregator eventAggregator, IDialogCoordinator dialogCoordinator, ILogger<MainWindowViewModel> logger, IShellExecuteProvider shellExecuteProvider)
        {
            this.shellExecuteProvider = shellExecuteProvider;
            this.ActiveItem = c;
            this.logger = logger;
            this.dialogCoordinator = dialogCoordinator;
            this.eventAggregator = eventAggregator;
            this.eventAggregator.Subscribe(this);
            this.DisplayName = "Lithnet Access Manager Service (AMS) Configuration";
            this.Config = c;
        }

        public async Task<bool> Save()
        {
            if (await this.Config.Save())
            {
                this.IsDirty = false;
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Close()
        {
            this.RequestClose();
        }


        public async Task Help()
        {
            if (Config.HelpLink == null)
            {
                return;
            }

            await this.shellExecuteProvider.OpenWithShellExecute(Config.HelpLink);
        }

        public void About()
        {

        }

        public override async Task<bool> CanCloseAsync()
        {
            if (!this.IsDirty)
            {
                return true;
            }

            var result = await this.dialogCoordinator.ShowMessageAsync(this, "Unsaved changed",
                    "Do you want to save your changes?",
                    MessageDialogStyle.AffirmativeAndNegativeAndSingleAuxiliary, new MetroDialogSettings()
                    {
                        AffirmativeButtonText = "_Save",
                        NegativeButtonText = "_Cancel",
                        FirstAuxiliaryButtonText = "Do_n't Save",
                        DefaultButtonFocus = MessageDialogResult.Affirmative,
                        AnimateShow = false,
                        AnimateHide = false
                    });

            if (result == MessageDialogResult.Affirmative)
            {
                try
                {
                    return await this.Save();
                }
                catch (Exception ex)
                {
                    this.logger.LogError(EventIDs.UIConfigurationSaveError, ex, "Unable to save the configuration");
                    await this.dialogCoordinator.ShowMessageAsync(this, "Error", $"Unable to save the configuration\r\n{ex.Message}");
                }
            }
            else if (result == MessageDialogResult.FirstAuxiliary)
            {
                return true;
            }

            return false;
        }

        public string WindowTitle => $"{this.DisplayName}{(this.IsDirty ? "*" : "")}";

        public bool IsDirty { get; set; }

        public void Handle(ModelChangedEvent message)
        {
            //System.Diagnostics.Debug.WriteLine($"Model changed event received {message.Sender.GetType()}:{message.PropertyName}");
            this.IsDirty = true;
        }
    }
}
