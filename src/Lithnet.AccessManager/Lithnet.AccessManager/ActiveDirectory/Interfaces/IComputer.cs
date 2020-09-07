﻿using System;
using System.Collections.Generic;
using System.DirectoryServices;

namespace Lithnet.AccessManager
{
    public interface IComputer : ISecurityPrincipal
    {
        string Description { get; }

        string DisplayName { get; }

        string DnsHostName { get; }

        DirectoryEntry DirectoryEntry { get; }

        IEnumerable<Guid> GetParentGuids();
    }
}
