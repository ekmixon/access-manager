﻿using Lithnet.AccessManager.Server.Configuration;

namespace Lithnet.AccessManager.Server.UI
{
    public interface IJitConfigurationViewModelFactory
    {
        JitConfigurationViewModel CreateViewModel();
    }
}