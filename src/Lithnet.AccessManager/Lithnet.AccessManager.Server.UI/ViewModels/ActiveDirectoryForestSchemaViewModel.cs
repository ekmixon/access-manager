﻿using System;
using System.DirectoryServices.ActiveDirectory;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Stylet;

namespace Lithnet.AccessManager.Server.UI
{
    public class ActiveDirectoryForestSchemaViewModel : PropertyChangedBase, IViewAware
    {
        private readonly ILogger<ActiveDirectoryForestSchemaViewModel> logger;

        public ActiveDirectoryForestSchemaViewModel(Forest forest, ILogger<ActiveDirectoryForestSchemaViewModel> logger)
        {
            this.Forest = forest;
            this.logger = logger;
            this.LithnetAccessManagerSchemaPresentText = "Checking...";
            this.LithnetSchemaLookupInProgress = true;
            this.MsLapsSchemaPresentText = "Checking...";
            this.MsLapsSchemaLookupInProgress = true;
        }

        public void RefreshSchemaStatus()
        {
            this.PopulateLithnetSchemaStatus();
            this.PopulateMsLapsSchemaStatus();
        }

        public async Task RefreshSchemaStatusAsync()
        {
            await Task.Run(this.RefreshSchemaStatus);
        }

        public string MsLapsSchemaPresentText { get; set; }

        public string LithnetAccessManagerSchemaPresentText { get; set; }

        public Forest Forest { get; }

        public void AttachView(UIElement view)
        {
            this.View = view;
        }

        public UIElement View { get; set; }

        public string Name => this.Forest.Name;

        public bool IsLithnetSchemaPresent { get; set; }

        public bool IsNotLithnetSchemaPresent { get; set; }

        public bool IsMsLapsSchemaPresent { get; set; }

        public bool MsLapsSchemaLookupInProgress { get; set; }

        public bool LithnetSchemaLookupInProgress { get; set; }

        public bool IsNotMsLapsSchemaPresent { get; set; }

        private void PopulateLithnetSchemaStatus()
        {
            try
            {
                this.LithnetAccessManagerSchemaPresentText = "Checking...";
                this.LithnetSchemaLookupInProgress = true;
                this.IsLithnetSchemaPresent = false;
                this.IsNotLithnetSchemaPresent = false;
                
                var schema = ActiveDirectorySchema.GetSchema(new DirectoryContext(DirectoryContextType.Forest, this.Forest.Name));
                schema.FindProperty("lithnetAdminPassword");
                this.IsLithnetSchemaPresent = true;
                this.LithnetAccessManagerSchemaPresentText = "Present";
            }
            catch (ActiveDirectoryObjectNotFoundException)
            {
                this.IsNotLithnetSchemaPresent = true;
                this.LithnetAccessManagerSchemaPresentText = "Not present";
            }
            catch (Exception ex)
            {
                this.logger.LogError(EventIDs.UISchemaLookupError, ex, "Could not determine Lithnet Access Manager schema status");
                this.IsNotLithnetSchemaPresent = true;
                this.LithnetAccessManagerSchemaPresentText = "Error looking up schema";
            }
            finally
            {
                this.LithnetSchemaLookupInProgress = false;
            }
        }

        private void PopulateMsLapsSchemaStatus()
        {
            try
            {
                this.MsLapsSchemaPresentText = "Checking...";
                this.MsLapsSchemaLookupInProgress = true;
                this.IsMsLapsSchemaPresent = false;
                this.IsNotMsLapsSchemaPresent = false;

                var schema = ActiveDirectorySchema.GetSchema(new DirectoryContext(DirectoryContextType.Forest, this.Forest.Name));
                schema.FindProperty("ms-Mcs-AdmPwd");
                this.IsMsLapsSchemaPresent = true;
                this.MsLapsSchemaPresentText = "Present";
            }
            catch (ActiveDirectoryObjectNotFoundException)
            {
                this.IsNotMsLapsSchemaPresent = true;
                this.MsLapsSchemaPresentText = "Not present";
            }
            catch (Exception ex)
            {
                this.logger.LogError(EventIDs.UISchemaLookupError, ex, "Could not determine Microsoft LAPS schema status");
                this.IsNotMsLapsSchemaPresent = true;
                this.MsLapsSchemaPresentText = "Error looking up schema";
            }
            finally
            {
                this.MsLapsSchemaLookupInProgress = false;
            }
        }
    }
}
