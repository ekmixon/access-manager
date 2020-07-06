﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Lithnet.AccessManager.Configuration
{
    public class AuthenticationOptions
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public AuthenticationMode Mode { get; set; }

        public IwaAuthenticationProviderOptions Iwa { get; set; }

        public OidcAuthenticationProviderOptions Oidc { get; set; }

        public WsFedAuthenticationProviderOptions WsFed { get; set; }
    }
}