﻿using System;
using Lithnet.AccessManager.Server.Configuration;

namespace Lithnet.AccessManager.Server.Authorization
{
    public class LapsAuthorizationResponse : AuthorizationResponse
    {
        /// <summary>
        /// If the user was successfully authorized, then this TimeSpan will be used to determine the new expiry date of the LAPS password. If it is set to zero, then no alteration to the LAPS password expiry date will be made.
        /// </summary>
        public TimeSpan ExpireAfter { get; set; }

        public PasswordStorageLocation RetrievalLocation { get; set; }

        public override AccessMask EvaluatedAccess => AccessMask.LocalAdminPassword;
    }
}