﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Lithnet.AccessManager.Server
{
    public class DbDeviceProvider : IDeviceProvider
    {
        private readonly IDbProvider dbProvider;
        private readonly ILogger<DbDeviceProvider> logger;

        public DbDeviceProvider(IDbProvider dbProvider, ILogger<DbDeviceProvider> logger)
        {
            this.dbProvider = dbProvider;
            this.logger = logger;
        }

        public async Task<IList<Device>> FindDevices(string name)
        {
            name.ThrowIfNull(nameof(name));

            await using SqlConnection con = this.dbProvider.GetConnection();

            SqlCommand command = new SqlCommand("spGetDevicesByNames", con);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ComputerNameOrDnsName", name);

            await using SqlDataReader reader = await command.ExecuteReaderAsync();

            List<Device> devices = new List<Device>();

            while (await reader.ReadAsync())
            {
                devices.Add(new Device(reader));
            }

            return devices;
        }

        public async Task<Device> GetOrCreateDeviceAsync(Microsoft.Graph.Device aadDevice, string authorityId)
        {
            authorityId.ThrowIfNull(nameof(authorityId));
            aadDevice.ThrowIfNull(nameof(aadDevice));

            string deviceId = aadDevice.Id;

            try
            {
                return await this.GetDeviceAsync(AuthorityType.AzureActiveDirectory, authorityId, deviceId);
            }
            catch (DeviceNotFoundException)
            {
                this.logger.LogTrace($"The AAD-joined computer {aadDevice.DeviceId} was not found in the AMS database and will be created");
            }

            return await this.CreateDeviceAsync(aadDevice, authorityId);
        }

        public async Task<Device> GetOrCreateDeviceAsync(IActiveDirectoryComputer principal, string authorityId)
        {
            authorityId.ThrowIfNull(nameof(authorityId));
            principal.ThrowIfNull(nameof(principal));
            
            string deviceId = principal.Sid.ToString();

            try
            {
                return await this.GetDeviceAsync(AuthorityType.ActiveDirectory, authorityId, deviceId);
            }
            catch (DeviceNotFoundException)
            {
                this.logger.LogTrace($"The AD-joined computer {principal.MsDsPrincipalName} was not found in the AMS database and will be created");
            }

            return await this.CreateDeviceAsync(principal, authorityId, deviceId);
        }

        public async Task<Device> GetDeviceAsync(AuthorityType authorityType, string authorityId, string authorityDeviceId)
        {
            authorityId.ThrowIfNull(nameof(authorityId));
            authorityDeviceId.ThrowIfNull(nameof(authorityDeviceId));

            await using SqlConnection con = this.dbProvider.GetConnection();

            SqlCommand command = new SqlCommand("spGetDeviceByAuthority", con);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@AuthorityType", (int)authorityType);
            command.Parameters.AddWithValue("@AuthorityId", authorityId);
            command.Parameters.AddWithValue("@AuthorityDeviceId", authorityDeviceId);

            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                return new Device(reader);
            }

            throw new DeviceNotFoundException($"Could not find a device with ID {authorityDeviceId} from authority {authorityId} ({authorityType})");
        }

        public async Task<Device> GetDeviceAsync(string deviceId)
        {
            deviceId.ThrowIfNull(nameof(deviceId));

            await using SqlConnection con = this.dbProvider.GetConnection();

            SqlCommand command = new SqlCommand("spGetDevice", con);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ObjectID", deviceId);

            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                return new Device(reader);
            }

            throw new DeviceNotFoundException($"Could not find a device with ID {deviceId}");
        }

        private async Task<long> GetOrCreateAuthorityKey(string authorityId, AuthorityType type)
        {
            authorityId.ThrowIfNull(nameof(authorityId));

            await using SqlConnection con = this.dbProvider.GetConnection();

            SqlCommand command = new SqlCommand("spGetOrCreateAuthority", con);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@AuthorityId", authorityId);
            command.Parameters.AddWithValue("@AuthorityType", (int)type);

            return (long)await command.ExecuteScalarAsync();
        }

        public async Task<Device> GetDeviceAsync(X509Certificate2 certificate)
        {
            certificate.ThrowIfNull(nameof(certificate));

            await using SqlConnection con = this.dbProvider.GetConnection();

            SqlCommand command = new SqlCommand("spGetDeviceByX509Thumbprint", con);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Thumbprint", certificate.Thumbprint);

            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                return new Device(reader);
            }

            throw new DeviceNotFoundException($"Could not find a device with credentials for the certificate issued to '{certificate.Subject}' with thumbprint {certificate.Thumbprint}");
        }

        public async Task<Device> CreateDeviceAsync(Device device, X509Certificate2 certificate)
        {
            device.ThrowIfNull(nameof(device));
            certificate.ThrowIfNull(nameof(certificate));

            device.ObjectID ??= Guid.NewGuid().ToString();
            device.AuthorityDeviceId = device.ObjectID;
            device.SecurityIdentifier = new System.Security.Principal.SecurityIdentifier($"{SidUtils.AmsSidPrefix}{SidUtils.GuidStringToSidString(device.ObjectID)}");

            long authorityKey = await this.GetOrCreateAuthorityKey(Constants.AmsAuthorityId, AuthorityType.Ams);

            await using SqlConnection con = this.dbProvider.GetConnection();

            SqlCommand command = new SqlCommand("spCreateDeviceWithCredentials", con);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@X509Certificate", certificate.Export(X509ContentType.Cert));
            command.Parameters.AddWithValue("@X509CertificateThumbprint", certificate.Thumbprint);
            command.Parameters.AddWithValue("@AuthorityKey", authorityKey);
            device.ToCreateCommandParameters(command);

            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            return new Device(reader);
        }

        public async Task<Device> CreateDeviceAsync(Microsoft.Graph.Device aadDevice, string authorityId)
        {
            aadDevice.ThrowIfNull(nameof(aadDevice));
            authorityId.ThrowIfNull(nameof(authorityId));

            Device device = new Device
            {
                AuthorityId = authorityId,
                AuthorityDeviceId = aadDevice.Id,
                AuthorityType = AuthorityType.AzureActiveDirectory,
                ApprovalState = ApprovalState.Approved,
                ComputerName = aadDevice.DisplayName,
                OperatingSystemFamily = aadDevice.OperatingSystem,
                OperatingSystemVersion = aadDevice.OperatingSystemVersion,
                SecurityIdentifier = new System.Security.Principal.SecurityIdentifier($"{SidUtils.AadSidPrefix}{SidUtils.GuidStringToSidString(aadDevice.Id)}")
            };

            return await this.CreateDeviceAsync(device);
        }

        public async Task<Device> CreateDeviceAsync(IActiveDirectoryComputer computer, string authorityId, string deviceId)
        {
            computer.ThrowIfNull(nameof(computer));
            authorityId.ThrowIfNull(nameof(authorityId));
            deviceId.ThrowIfNull(nameof(deviceId));

            Device device = new Device
            {
                ApprovalState = ApprovalState.Approved,
                AuthorityId = authorityId,
                AuthorityDeviceId = deviceId,
                AuthorityType = AuthorityType.ActiveDirectory,
                ComputerName = computer.SamAccountName.TrimEnd('$'),
                DnsName = computer.DnsHostName,
                SecurityIdentifier = computer.Sid
            };

            return await this.CreateDeviceAsync(device);
        }

        public async Task<Device> CreateDeviceAsync(Device device)
        {
            device.ThrowIfNull(nameof(device));

            long authorityKey = await this.GetOrCreateAuthorityKey(device.AuthorityId, device.AuthorityType);

            device.ObjectID ??= Guid.NewGuid().ToString();

            await using SqlConnection con = this.dbProvider.GetConnection();

            SqlCommand command = new SqlCommand("spCreateDevice", con);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@AuthorityKey", authorityKey);

            device.ToCreateCommandParameters(command);

            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            return new Device(reader);
        }

        public async Task<Device> UpdateDeviceAsync(Device device)
        {
            device.ThrowIfNull(nameof(device));

            if (device.ObjectID == null)
            {
                throw new InvalidOperationException("Could not update the device because the device ID was not found");
            }

            await using SqlConnection con = this.dbProvider.GetConnection();

            SqlCommand command = new SqlCommand("spUpdateDevice", con);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            device.ToUpdateCommandParameters(command);

            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            return new Device(reader);
        }
    }
}
