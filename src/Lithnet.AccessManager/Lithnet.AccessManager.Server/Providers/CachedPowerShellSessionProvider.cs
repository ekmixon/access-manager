﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Text;
using Microsoft.Extensions.Logging;
using Vanara.InteropServices;

namespace Lithnet.AccessManager.Server
{
    public class CachedPowerShellSessionProvider : IPowerShellSessionProvider
    {
        private readonly Dictionary<string, PowerShell> sessionCache;

        private readonly Dictionary<string, DateTime> lastWriteCache;

        private readonly IAppPathProvider pathProvider;

        private readonly ILogger<CachedPowerShellSessionProvider> logger;

        public CachedPowerShellSessionProvider(IAppPathProvider pathProvider, ILogger<CachedPowerShellSessionProvider> logger)
        {
            this.pathProvider = pathProvider;
            this.logger = logger;
            this.sessionCache = new Dictionary<string, PowerShell>();
            this.lastWriteCache = new Dictionary<string, DateTime>();
        }

        public PowerShell GetSession(string script, params string[] expectedFunctions)
        {
            string path = this.pathProvider.GetFullPath(script, this.pathProvider.ScriptsPath);

            if (path == null || !File.Exists(path))
            {
                throw new FileNotFoundException($"The PowerShell script was not found: {path}");
            }

            DateTime lastWrite = File.GetLastWriteTime(path);

            string key = path.ToLowerInvariant();

            if (lastWriteCache.TryGetValue(key, out DateTime cache))
            {
                if (cache == lastWrite)
                {
                    logger.LogTrace($"PowerShell script '{path}' found in the cache and it has not been modified");
                    PowerShell cachedSession = sessionCache[key];
                    cachedSession.ResetState();
                    return cachedSession;
                }
                else
                {
                    logger.LogTrace($"PowerShell script '{path}' found in the cache and it has been modified. Clearing entry and reloading");
                }

                lastWriteCache.Remove(key);
                sessionCache.Remove(key);
            }

            logger.LogTrace($"Initializing new PowerShell session from {path}");
            PowerShell p = this.InitializePowerShellSession(path, expectedFunctions);
            lastWriteCache.Add(key, lastWrite);
            sessionCache.Add(key, p);
            return p;
        }

        private PowerShell InitializePowerShellSession(string path, string[] expectedFunctions)
        {
            PowerShell powershell = PowerShell.Create();
            powershell.AddScript(File.ReadAllText(path));
            powershell.Invoke();

            if (expectedFunctions == null)
            {
                return powershell;
            }

            foreach (var f in expectedFunctions)
            {
                if (powershell.Runspace.SessionStateProxy.InvokeCommand.GetCommand(f, CommandTypes.All) == null)
                {
                    throw new NotSupportedException($"The PowerShell script must contain a function called '{f}'");
                }
            }

            powershell.Streams.Warning.DataAdded += Warning_DataAdded;
            powershell.Streams.Error.DataAdded += Error_DataAdded;
            powershell.Streams.Information.DataAdded += Information_DataAdded;
            powershell.Streams.Debug.DataAdded += Debug_DataAdded;
            powershell.Streams.Verbose.DataAdded += Verbose_DataAdded;

            this.logger.LogTrace($"The PowerShell script was successfully initialized");

            return powershell;
        }

        private void Verbose_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (((IList)sender)[e.Index] is VerboseRecord newRecord)
            {
                this.logger.LogTrace(newRecord.ToString());
            }
        }

        private void Debug_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (((IList)sender)[e.Index] is DebugRecord newRecord)
            {
                this.logger.LogDebug(newRecord.ToString());
            }
        }

        private void Information_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (((IList)sender)[e.Index] is InformationRecord newRecord)
            {
                this.logger.LogInformation(newRecord.ToString());
            }
        }

        private void Error_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (((IList)sender)[e.Index] is ErrorRecord newRecord)
            {
                this.logger.LogError(newRecord.ToString());
            }
        }

        private void Warning_DataAdded(object sender, DataAddedEventArgs e)
        {
            if (((IList)sender)[e.Index] is WarningRecord newRecord)
            {
                this.logger.LogWarning(newRecord.ToString());
            }
        }
    }
}
