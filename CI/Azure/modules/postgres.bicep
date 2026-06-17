@description('Prefix applied to all resource names. Max 16 characters.')
param namePrefix string

@description('Deployment environment name (e.g. dev, staging, prod).')
param environment string

@description('Azure region for the PostgreSQL Flexible Server.')
param location string

@description('PostgreSQL flexible server administrator login name.')
param adminLogin string

@description('PostgreSQL flexible server administrator password.')
@secure()
param adminPassword string

@description('Name of the initial database to create.')
param databaseName string = 'starterapp'

@description('Tags to apply to the resources.')
param tags object = {}

var pgServerName = '${namePrefix}-pg-${environment}'

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01' = {
  name: pgServerName
  location: location
  tags: tags
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    version: '16'
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    authConfig: {
      activeDirectoryAuth: 'Disabled'
      passwordAuth: 'Enabled'
    }
  }
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-12-01' = {
  parent: postgresServer
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// 0.0.0.0 → 0.0.0.0 is the Azure "allow all Azure services" sentinel for Flexible Server
resource firewallAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-12-01' = {
  parent: postgresServer
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output serverName string = postgresServer.name
output fqdn string = postgresServer.properties.fullyQualifiedDomainName
// Shape reference only — substitute <replace> with your actual password at runtime
output connectionStringShape string = 'Host=${postgresServer.properties.fullyQualifiedDomainName};Database=${databaseName};Username=${adminLogin};Password=<replace>;SslMode=Require'
