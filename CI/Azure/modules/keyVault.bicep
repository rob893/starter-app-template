@description('Prefix applied to all resource names. KV name = <namePrefix>-kv-<env> (must be 3–24 chars total).')
param namePrefix string

@description('Deployment environment name (e.g. dev, staging, prod).')
param environment string

@description('Azure region for the Key Vault.')
param location string

@description('Tags to apply to the resource.')
param tags object = {}

// KV names must be 3–24 chars, start with a letter, alphanumeric/hyphen, no trailing hyphen.
// Cap the composed name at 24 chars, then strip a trailing hyphen so truncation stays valid.
var rawVaultName = take('${namePrefix}-kv-${environment}', 24)
var vaultName = endsWith(rawVaultName, '-') ? take(rawVaultName, 23) : rawVaultName

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: vaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    // RBAC authorization: role assignments control access; no legacy access policies
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

output keyVaultName string = vault.name
output keyVaultUri string = vault.properties.vaultUri
