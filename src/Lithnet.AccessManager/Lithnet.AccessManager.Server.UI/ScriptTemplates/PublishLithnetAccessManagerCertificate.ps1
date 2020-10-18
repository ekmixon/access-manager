﻿# Publish-LithnetAccessManagerCertificate
# 
# This script creates an object in the Configuration Naming context of the root domain in the forest with a copy
# that contains the public key of the certificate Lithnet Access Manager Agents should use to encrypt their local
# admin passwords and password history
#
# This script requires membership in the Enterprise Admin group, or the Domain Admin group on the root domain of the forest
# 
# Note, this script has been pre-populated with the information required to publish the certificate in your forest
#
# Version 1.0

#-------------------------------------------------------------------------
# Do not modify below here
#-------------------------------------------------------------------------

Import-Module ActiveDirectory

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"
$object = $null;
$lithnetContainerName = "Lithnet"
$publicKeyObjectName = "AccessManagerConfig";
$servicesContainerDN = "CN=Services,{configurationNamingContext}";
$lithnetContainerDN = "CN=$lithnetContainerName,$servicesContainerDN";
$keyContainerDN = "CN=$publicKeyObjectName,$lithnetContainerDN";
$forest = "{forest}"
$server = (Get-ADDomainController -DomainName $forest -Discover -ForceDiscover -Writable).HostName[0]

$certificateContent = @"
{certificateData}
"@;


$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
$certBytes = [System.Convert]::FromBase64String($certificateContent);

try
{
    Write-Information "Attempting to get container $lithnetContainerDN";
    $object = Get-ADObject $lithnetContainerDN -Server $server;
    Write-Information "Found container in directory $lithnetContainerDN";
}
catch [Microsoft.ActiveDirectory.Management.ADIdentityNotFoundException]
{
    Write-Warning "$lithnetContainerDN doesn't exist. Creating"
    New-ADObject -Name $lithnetContainerName -Path $servicesContainerDN -Type "container" -Server $server
    Write-Information "Created container $keyContainerDN";
}

try
{
    Write-Information "Attempting to get public key container $keyContainerDN";
    $object = Get-ADObject $keyContainerDN -Server $server;
    Write-Information "Found public key container $keyContainerDN";
    Set-ADObject -Identity $keyContainerDN -Replace @{"caCertificate"=$certBytes} -Server $server
    Write-Information "Successfully published certificate to directory";
}
catch [Microsoft.ActiveDirectory.Management.ADIdentityNotFoundException]
{
    Write-Warning "$publicKeyObjectName doesn't exist. Creating"
    New-ADObject -Name $publicKeyObjectName -Path $lithnetContainerDN -Type "lithnetAccessManagerConfig" -OtherAttributes @{"appSchemaVersion"="1"; "caCertificate"=$certBytes} -Server $server
    Write-Information "Created Public key container $keyContainerDN";
}