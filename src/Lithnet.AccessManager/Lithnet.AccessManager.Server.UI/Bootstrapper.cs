﻿using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DbUp.Engine.Output;
using FluentValidation;
using Lithnet.AccessManager.Api;
using Lithnet.AccessManager.Cryptography;
using Lithnet.AccessManager.Enterprise;
using Lithnet.AccessManager.Server.Authorization;
using Lithnet.AccessManager.Server.Configuration;
using Lithnet.AccessManager.Server.Providers;
using Lithnet.AccessManager.Server.UI.AuthorizationRuleImport;
using Lithnet.AccessManager.Server.UI.Providers;
using Lithnet.Licensing.Core;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using NLog.Extensions.Logging;
using Stylet;
using StyletIoC;

namespace Lithnet.AccessManager.Server.UI
{
    public class Bootstrapper : Bootstrapper<MainWindowViewModel>
    {
        private static ILogger logger;

        public static ILogger Logger => logger;

        private static ILoggerFactory loggerFactory;

        private IApplicationConfig appconfig;

        private static UiRegistryProvider registryProvider;

        private static void SetupNLog()
        {
            var configuration = new NLog.Config.LoggingConfiguration();

            var uiLog = new NLog.Targets.FileTarget("access-manager-ui")
            {
                FileName = Path.Combine(registryProvider.LogPath, "access-manager-ui.log"),
                ArchiveEvery = NLog.Targets.FileArchivePeriod.Day,
                ArchiveNumbering = NLog.Targets.ArchiveNumberingMode.Date,
                MaxArchiveFiles = registryProvider.RetentionDays,
                Layout = "${longdate}|${level:uppercase=true:padding=5}|${logger}|${message}${onexception:inner=${newline}${exception:format=ToString}}"
            };

            configuration.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, uiLog);

            NLog.LogManager.Configuration = configuration;
        }

        static Bootstrapper()
        {
            Bootstrapper.registryProvider = new UiRegistryProvider();

            SetupNLog();

            loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddNLog();
                builder.SetMinimumLevel(Bootstrapper.registryProvider.UiLogLevel);
                builder.AddDebug();
                builder.AddEventLog(new EventLogSettings()
                {
                    SourceName = Constants.EventSourceName,
                    LogName = Constants.EventLogName,
                    Filter = (x, y) => y >= Bootstrapper.registryProvider.UiEventLogLevel
                });
            });

            logger = loggerFactory.CreateLogger<Bootstrapper>();
        }

        protected override void OnStart()
        {
            AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            Dispatcher.CurrentDispatcher.UnhandledException += CurrentDispatcher_UnhandledException;

            base.OnStart();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Dispatcher.CurrentDispatcher.UnhandledException -= CurrentDispatcher_UnhandledException;
            TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException -= AppDomain_UnhandledException;

            base.OnExit(e);
        }

        protected override void ConfigureIoC(IStyletIoCBuilder builder)
        {
            IAppPathProvider pathProvider = new AppPathProvider(registryProvider);
            
            try
            {
                try
                {
                    ClusterProvider provider = new ClusterProvider();

                    if (provider.IsClustered && !provider.IsOnActiveNode())
                    {
                        throw new ClusterNodeNotActiveException("The AMS service is not active on this cluster node. Please run this app on the currently active node");
                    }
                }
                catch (ClusterNodeNotActiveException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(EventIDs.UIGenericError, ex, "Unable to determine cluster node status");
                }

                if (!File.Exists(pathProvider.ConfigFile))
                {
                    logger.LogCritical(EventIDs.UIGenericError, "Config file was not found at path {path}", pathProvider.ConfigFile);
                    throw new MissingConfigurationException($"The appsettings.config file could not be found at path {pathProvider.ConfigFile}. Please resolve the issue and restart the application");
                }

                if (!File.Exists(pathProvider.HostingConfigFile))
                {
                    logger.LogCritical(EventIDs.UIGenericError, "Apphost file was not found at path {path}", pathProvider.HostingConfigFile);
                    throw new MissingConfigurationException($"The apphost.config file could not be found at path {pathProvider.HostingConfigFile}. Please resolve the issue and restart the application");
                }

                appconfig = ApplicationConfig.Load(pathProvider.ConfigFile);
                var hosting = HostingOptions.Load(pathProvider.HostingConfigFile);

                //Config
                builder.Bind<IApplicationConfig>().ToInstance(appconfig);
                builder.Bind<AuthenticationOptions>().ToInstance(appconfig.Authentication);
                builder.Bind<AuditOptions>().ToInstance(appconfig.Auditing);
                builder.Bind<AuthorizationOptions>().ToInstance(appconfig.Authorization);
                builder.Bind<EmailOptions>().ToInstance(appconfig.Email);
                builder.Bind<ForwardedHeadersAppOptions>().ToInstance(appconfig.ForwardedHeaders);
                builder.Bind<HostingOptions>().ToInstance(hosting);
                builder.Bind<RateLimitOptions>().ToInstance(appconfig.RateLimits);
                builder.Bind<UserInterfaceOptions>().ToInstance(appconfig.UserInterface);
                builder.Bind<JitConfigurationOptions>().ToInstance(appconfig.JitConfiguration);
                builder.Bind<LicensingOptions>().ToInstance(appconfig.Licensing);
                builder.Bind<DataProtectionOptions>().ToInstance(appconfig.DataProtection);
                builder.Bind<AdminNotificationOptions>().ToInstance(appconfig.AdminNotifications);
                builder.Bind<AzureAdOptions>().ToInstance(appconfig.AzureAd);
                builder.Bind<TokenIssuerOptions>().ToInstance(appconfig.TokenIssuer);
                builder.Bind<PasswordPolicyOptions>().ToInstance(appconfig.PasswordPolicy);
                builder.Bind<ApiAuthenticationOptions>().ToInstance(appconfig.ApiAuthentication);

                // ViewModel factories
                builder.Bind(typeof(IViewModelFactory<>)).ToAllImplementations();
                builder.Bind(typeof(IViewModelFactory<,>)).ToAllImplementations();
                builder.Bind(typeof(IViewModelFactory<,,>)).ToAllImplementations();
                builder.Bind(typeof(IAsyncViewModelFactory<,>)).ToAllImplementations();
                builder.Bind(typeof(IAsyncViewModelFactory<,,>)).ToAllImplementations();
                builder.Bind(typeof(INotificationChannelDefinitionsViewModelFactory<,>)).ToAllImplementations();
                builder.Bind(typeof(INotificationChannelDefinitionViewModelFactory<,>)).ToAllImplementations();
                builder.Bind<IFileSelectionViewModelFactory>().To<FileSelectionViewModelFactory>();

                // Services
                builder.Bind<RandomNumberGenerator>().ToInstance(RandomNumberGenerator.Create());
                builder.Bind<IRandomValueGenerator>().To<RandomValueGenerator>();

                builder.Bind<IDialogCoordinator>().To<DialogCoordinator>();
                builder.Bind<IActiveDirectory>().To<ActiveDirectory>();
                builder.Bind<ILocalSam>().To<LocalSam>();
                builder.Bind<IComputerPrincipalProviderRpc>().To<ComputerPrincipalProviderRpc>();
                builder.Bind<IComputerPrincipalProviderCsv>().To<ComputerPrincipalProviderCsv>();
                builder.Bind<IComputerPrincipalProviderLaps>().To<ComputerPrincipalProviderLaps>();
                builder.Bind<IComputerPrincipalProviderBitLocker>().To<ComputerPrincipalProviderBitLocker>();
                builder.Bind<IDiscoveryServices>().To<DiscoveryServices>();
                builder.Bind<IWindowsServiceProvider>().To<WindowsServiceProvider>();
                builder.Bind<INotificationSubscriptionProvider>().To<NotificationSubscriptionProvider>();
                builder.Bind<IEncryptionProvider>().To<EncryptionProvider>();
                builder.Bind<ICertificateProvider>().To<CertificateProvider>();
                builder.Bind<IAppPathProvider>().To<AppPathProvider>();
                builder.Bind<INotifyModelChangedEventPublisher>().To<NotifyModelChangedEventPublisher>();
                builder.Bind<IShellExecuteProvider>().To<ShellExecuteProvider>();
                builder.Bind<IDomainTrustProvider>().To<DomainTrustProvider>();
                builder.Bind<IImportProviderFactory>().To<ImportProviderFactory>();

                builder.Bind<IComputerTargetProvider>().To<ComputerTargetProviderAd>();
                builder.Bind<IComputerTargetProvider>().To<ComputerTargetProviderAzureAd>();
                builder.Bind<IComputerTargetProvider>().To<ComputerTargetProviderAms>();
                builder.Bind<IObjectSelectionProvider>().To<ObjectSelectionProvider>();
                builder.Bind<ITargetDataProvider>().To<TargetDataProvider>();
                builder.Bind<ITargetDataCache>().To<TargetDataCache>();
                builder.Bind<IAuthorizationContextProvider>().To<AuthorizationContextProvider>();
                builder.Bind<IAuthorizationInformationBuilder>().To<AuthorizationInformationBuilder>();
                builder.Bind<IPowerShellSecurityDescriptorGenerator>().To<PowerShellSecurityDescriptorGenerator>();
                builder.Bind<IAuthorizationInformationMemoryCache>().To<AuthorizationInformationMemoryCache>();
                builder.Bind<IPowerShellSessionProvider>().To<CachedPowerShellSessionProvider>();
                builder.Bind<IScriptTemplateProvider>().To<ScriptTemplateProvider>();
                builder.Bind<IRegistryProvider>().ToInstance(registryProvider);
                builder.Bind<ICertificatePermissionProvider>().To<CertificatePermissionProvider>();
                builder.Bind<ICertificateSynchronizationProvider>().To<CertificateSynchronizationProvider>();
                builder.Bind<IApplicationUpgradeProvider>().To<ApplicationUpgradeProvider>();
                builder.Bind<IFirewallProvider>().To<FirewallProvider>();
                builder.Bind<IHttpSysConfigurationProvider>().To<HttpSysConfigurationProvider>();
                builder.Bind<IAadGraphApiProvider>().To<AadGraphApiProvider>();
                builder.Bind<IRegistrationKeyProvider>().To<DbRegistrationKeyProvider>();
                builder.Bind<IDbProvider>().To<SqlDbProvider>().InSingletonScope();
                builder.Bind<SqlServerInstanceProvider>().ToSelf().InSingletonScope(); 
                builder.Bind<IUpgradeLog>().To<DbUpgradeLogger>();
                builder.Bind<IHostApplicationLifetime>().To<WpfHostLifetime>();
                builder.Bind<IDeviceProvider>().To<DbDeviceProvider>();
                builder.Bind<IDevicePasswordProvider>().To<DbDevicePasswordProvider>();
                builder.Bind<IAmsGroupProvider>().To<DbAmsGroupProvider>();

                builder.Bind<IProtectedSecretProvider>().To<ProtectedSecretProvider>().InSingletonScope();
                builder.Bind<IClusterProvider>().To<ClusterProvider>().InSingletonScope();
                builder.Bind<IProductSettingsProvider>().To<ProductSettingsProvider>().InSingletonScope();
                builder.Bind<IAmsLicenseManager>().To<AmsLicenseManager>().InSingletonScope();
                builder.Bind<ISecretRekeyProvider>().To<SecretRekeyProvider>().InSingletonScope();
                builder.Bind<ILicenseDataProvider>().To<OptionsLicenseDataProvider>().InSingletonScope();

                builder.Bind(typeof(IModelValidator<>)).To(typeof(FluentModelValidator<>));
                builder.Bind(typeof(IValidator<>)).ToAllImplementations();
                builder.Bind<ILoggerFactory>().ToInstance(Bootstrapper.loggerFactory);
                builder.Bind(typeof(ILogger<>)).To(typeof(Logger<>));
                builder.Bind(typeof(IOptions<>)).To(typeof(OptionsWrapper<>)).InSingletonScope();
                builder.Bind(typeof(IOptionsSnapshot<>)).To(typeof(OptionsManager<>));
                builder.Bind(typeof(IOptionsFactory<>)).To(typeof(OptionsFactory<>));
                builder.Bind(typeof(IOptionsMonitor<>)).To(typeof(OptionsMonitorWrapper<>));
                builder.Bind(typeof(IOptionsMonitorCache<>)).To(typeof(OptionsCache<>)).InSingletonScope();
                base.ConfigureIoC(builder);
            }
            catch (ApplicationInitializationException ex)
            {
                logger.LogCritical(EventIDs.UIInitializationError, ex, "Initialization error");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogCritical(EventIDs.UIInitializationError, ex, "Initialization error");
                throw new ApplicationInitializationException("The application failed to initialize", ex);
            }
        }


        private void CurrentDispatcher_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                this.HandleException(e.Exception);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            this.HandleException(e.Exception);
        }

        private void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            this.HandleException(e.ExceptionObject as Exception ?? new Exception("An unhandled exception occurred in the app domain, but no exception was present"));
        }

        private void HandleException(Exception ex)
        {
            logger.LogCritical(ex, "An unhandled exception occurred in the user interface");

            string errorMessage = $"An unhandled error occurred and the application will terminate.\r\n\r\n{ex.Message}\r\n\r\n Do you want to attempt to save the current configuration?";

            if (MessageBox.Show(errorMessage, "Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
            {
                try
                {
                    File.Copy(appconfig.Path, appconfig.Path + ".backup", true);
                    appconfig?.Save(appconfig.Path, true);
                }
                catch (Exception ex2)
                {
                    logger.LogCritical(ex2, "Unable to save app config");
                    MessageBox.Show("Unable to save the current configuration", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            Environment.Exit(1);
        }
    }
}
