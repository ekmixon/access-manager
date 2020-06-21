﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.DirectoryServices;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks.Sources;

namespace Lithnet.AccessManager
{
    public static class DirectoryExtensions
    {

        internal static bool TryGet<T>(this Func<T> getFunc, out T o) where T : class
        {
            o = null;
            try
            {
                o = getFunc();
                return true;
            }
            catch (ObjectNotFoundException)
            {
            }

            return false;
        }

        public static DateTime Trim(this DateTime date, long ticks)
        {
            return new DateTime(date.Ticks - (date.Ticks % ticks), date.Kind);
        }

        public static bool IsDnMatch(string dn1, string dn2)
        {
            try
            {
                X500DistinguishedName x1 = new X500DistinguishedName(dn1);
                X500DistinguishedName x2 = new X500DistinguishedName(dn2);

                return (string.Equals(x1.Decode(X500DistinguishedNameFlags.UseUTF8Encoding), x2.Decode(X500DistinguishedNameFlags.UseUTF8Encoding), StringComparison.InvariantCultureIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        public static DirectoryEntry GetParentDirectoryEntry(this IDirectoryObject o)
        {
            return o.GetDirectoryEntry().Parent;
        }

        public static DirectoryEntry GetDirectoryEntry(this IDirectoryObject o)
        {
            return new DirectoryEntry($"LDAP://{o.DistinguishedName}");
        }

        [DebuggerStepThrough]
        public static bool TryParseAsSid(this string s, out SecurityIdentifier sid)
        {
            sid = null;

            try
            {
                if (s == null)
                {
                    return false;
                }

                sid = new SecurityIdentifier(s);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string ToClaimList(this ClaimsIdentity p)
        {
            StringBuilder builder = new StringBuilder();
            foreach (Claim c in p.Claims)
            {
                builder.Append(c.Type).Append(": ").AppendLine(c.Value);
            }

            return builder.ToString();
        }

        public static DateTime? GetPropertyDateTimeFromLong(this SearchResult result, string propertyName)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return null;
            }

            long value = (long)result.Properties[propertyName][0];
            return DateTime.FromFileTimeUtc(value);
        }

        public static DateTime? GetPropertyDateTimeFromAdsLargeInteger(this DirectoryEntry result, string propertyName)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return null;
            }

            object value = result.Properties[propertyName][0];
          
            int highPart = (int)value.GetType().InvokeMember("HighPart", BindingFlags.GetProperty, null, value, null);
            int lowPart = (int)value.GetType().InvokeMember("LowPart", BindingFlags.GetProperty, null, value, null);
            
            long nv = highPart;
            nv <<= 32;
            nv += lowPart;

            if (nv != 0)
            {
                return DateTime.FromFileTimeUtc(nv);
            }

            return null;
        }

        public static DateTime? GetPropertyDateTimeFromLong(this DirectoryEntry result, string propertyName)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return null;
            }

            long? value = result.Properties[propertyName][0] as long?;

            if (value == null)
            {
                return null;
            }

            return DateTime.FromFileTimeUtc(value.Value);
        }

        public static DateTime? GetPropertyDateTime(this DirectoryEntry result, string propertyName)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return null;
            }

            return ((DateTime)result.Properties[propertyName][0]);
        }


        public static string GetPropertyString(this SearchResult result, string propertyName)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return null;
            }

            return result.Properties[propertyName][0]?.ToString();
        }

        public static string GetPropertyString(this DirectoryEntry result, string propertyName)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return null;
            }

            return result.Properties[propertyName][0]?.ToString();
        }

        public static IEnumerable<string> GetPropertyStrings(this SearchResult result, string propertyName)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return new string[] { };
            }

            return result.Properties[propertyName].OfType<object>().Select(t => t.ToString());
        }

        public static IEnumerable<string> GetPropertyStrings(this DirectoryEntry result, string propertyName)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return new string[] { };
            }

            return result.Properties[propertyName].OfType<object>().Select(t => t.ToString());
        }

        public static string GetPropertyCommaSeparatedString(this SearchResult result, string propertyName)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return null;
            }

            return string.Join(", ", result.Properties[propertyName].OfType<object>().Select(t => t.ToString()));
        }

        public static bool HasPropertyValue(this SearchResult result, string propertyName, string value)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return false;
            }

            foreach (object s in result.Properties[propertyName])
            {
                if (s.ToString().Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasPropertyValue(this DirectoryEntry result, string propertyName, string value)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return false;
            }

            foreach (object s in result.Properties[propertyName])
            {
                if (s.ToString().Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static Guid? GetPropertyGuid(this SearchResult result, string propertyName)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return null;
            }

            byte[] r = GetPropertyBytes(result, propertyName);

            if (r == null)
            {
                return null;
            }

            return new Guid(r);
        }

        public static Guid? GetPropertyGuid(this DirectoryEntry result, string propertyName)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return null;
            }

            byte[] r = GetPropertyBytes(result, propertyName);

            if (r == null)
            {
                return null;
            }

            return new Guid(r);
        }


        public static SecurityIdentifier GetPropertySid(this SearchResult result, string propertyName)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return null;
            }

            byte[] r = GetPropertyBytes(result, propertyName);

            if (r == null)
            {
                return null;
            }

            return new SecurityIdentifier(r, 0);
        }

        public static byte[] GetPropertyBytes(this SearchResult result, string propertyName)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return null;
            }

            object r = result.Properties[propertyName][0];

            return r as byte[];
        }


        public static SecurityIdentifier GetPropertySid(this DirectoryEntry result, string propertyName)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return null;
            }

            byte[] r = GetPropertyBytes(result, propertyName);

            if (r == null)
            {
                return null;
            }

            return new SecurityIdentifier(r, 0);
        }

        public static byte[] GetPropertyBytes(this DirectoryEntry result, string propertyName)
        {
            if (!result.Properties.Contains(propertyName))
            {
                return null;
            }

            object r = result.Properties[propertyName][0];

            return r as byte[];
        }

        internal static string ToOctetString(this Guid guid)
        {
            return ToOctetString((Guid?)guid);
        }

        internal static string ToOctetString(this Guid? guid)
        {
            if (!guid.HasValue)
            {
                return null;
            }

            return $"\\{string.Join("\\", guid.Value.ToByteArray().Select(t => t.ToString("X2")))}";
        }

        internal static void AddIfMissing(this StringCollection c, string value, StringComparer comparer)
        {
            if (!c.OfType<string>().Contains(value, comparer))
            {
                c.Add(value);
            }
        }
    }
}