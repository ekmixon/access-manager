﻿using System.Security.Principal;

namespace Lithnet.AccessManager.Server
{
    public interface IRegistryProvider
    {
        string LogPath { get; }

        int RetentionDays { get; }

        bool IsConfigured { get; set; }

        string SqlServer { get; }
        
        string ConnectionString { get; }

        string HttpAcl { get; set; }

        string HttpsAcl { get; set; }

        string CertBinding { get; set; }

        string ConfigPath { get; }

        string BasePath { get; }

        int CacheMode { get; set; }

        bool DeleteLocalDbInstance { get; set; }

        string LastNotifiedVersion { get; set; }

        string LastNotifiedCertificateKey { get; set; }

        bool ResetScheduler { get; set; }

        bool ResetMaintenanceTaskSchedules { get; set; }

        bool ApiEnabled { get; set; }

        string LicenseData { get; }

        string AmsAdminSidString { get; set; }

        SecurityIdentifier AmsAdminSid { get; set; }
    }
}