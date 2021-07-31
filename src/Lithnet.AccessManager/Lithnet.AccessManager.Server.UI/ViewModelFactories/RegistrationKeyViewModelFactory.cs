﻿using System;
using Lithnet.AccessManager.Api;
using Lithnet.AccessManager.Server.Providers;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Extensions.Logging;
using Stylet;

namespace Lithnet.AccessManager.Server.UI
{
    public class RegistrationKeyViewModelFactory : IViewModelFactory<RegistrationKeyViewModel, IRegistrationKey>
    {
        private readonly Func<IModelValidator<RegistrationKeyViewModel>> validator;
        private readonly ILogger<RegistrationKeyViewModel> logger;
        private readonly IDialogCoordinator dialogCoordinator;
        private readonly IViewModelFactory<AmsGroupSelectorViewModel> groupSelectorFactory;
        private readonly IRegistrationKeyProvider registrationKeyProvider;

        public RegistrationKeyViewModelFactory(Func<IModelValidator<RegistrationKeyViewModel>> validator, ILogger<RegistrationKeyViewModel> logger, IDialogCoordinator dialogCoordinator, IViewModelFactory<AmsGroupSelectorViewModel> groupSelectorFactory, IRegistrationKeyProvider registrationKeyProvider)
        {
            this.validator = validator;
            this.logger = logger;
            this.dialogCoordinator = dialogCoordinator;
            this.groupSelectorFactory = groupSelectorFactory;
            this.registrationKeyProvider = registrationKeyProvider;
        }

        public RegistrationKeyViewModel CreateViewModel(IRegistrationKey model)
        {
            return new RegistrationKeyViewModel(model, this.logger, this.validator.Invoke(), dialogCoordinator, groupSelectorFactory, registrationKeyProvider);
        }
    }
}
