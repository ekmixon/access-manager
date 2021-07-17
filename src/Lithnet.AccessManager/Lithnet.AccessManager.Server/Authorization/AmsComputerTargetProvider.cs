﻿using Lithnet.AccessManager.Server.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using Lithnet.AccessManager.Server.Providers;

namespace Lithnet.AccessManager.Server.Authorization
{
    public class AmsComputerTargetProvider : IComputerTargetProvider
    {
        private readonly ITargetDataProvider targetDataProvider;
        private readonly ILogger logger;
        private readonly IAmsGroupProvider groupProvider;

        public AmsComputerTargetProvider(ITargetDataProvider targetDataProvider, ILogger<AmsComputerTargetProvider> logger, IAmsGroupProvider groupProvider)
        {
            this.logger = logger;
            this.groupProvider = groupProvider;
            this.targetDataProvider = targetDataProvider;
        }

        public bool CanProcess(IComputer computer)
        {
            return computer is IDevice d && d.AuthorityType == AuthorityType.Ams;
        }

        public async Task<IList<SecurityDescriptorTarget>> GetMatchingTargetsForComputer(IComputer computer, IEnumerable<SecurityDescriptorTarget> targets)
        {
            if (!(computer is IDevice d) || (d.AuthorityType != AuthorityType.Ams))
            {
                throw new InvalidOperationException("The object passed to the method was of an incorrect type");
            }

            List<SecurityDescriptorTarget> matchingTargets = new List<SecurityDescriptorTarget>();

            List<SecurityIdentifier> computerTokenSids = await this.groupProvider.GetGroupSidsForDevice(d).ToListAsync();

            foreach (var target in targets.OrderBy(t => (int)t.Type).ThenByDescending(this.targetDataProvider.GetSortOrder))
            {
                TargetData targetData = this.targetDataProvider.GetTargetData(target);

                try
                {
                    if (target.IsInactive())
                    {
                        continue;
                    }

                    if (target.Type == TargetType.AmsComputer)
                    {
                        if (targetData.Sid == d.SecurityIdentifier)
                        {
                            this.logger.LogTrace($"Matched {d.FullyQualifiedName} to target {target.Id}");
                            matchingTargets.Add(target);
                        }
                    }
                    else if (target.Type == TargetType.AmsGroup)
                    {
                        if (computerTokenSids.Any(t => t == targetData.Sid))
                        {
                            this.logger.LogTrace($"Matched {d.FullyQualifiedName} to target {target.Id}");
                            matchingTargets.Add(target);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError(EventIDs.TargetRuleProcessingError, ex, $"An error occurred processing the target {target.Id}:{target.Type}:{target.Target}");
                }
            }

            return (IList<SecurityDescriptorTarget>)matchingTargets;
        }
    }
}
