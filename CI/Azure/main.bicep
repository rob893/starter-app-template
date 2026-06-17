targetScope = 'resourceGroup'

@description('Short prefix applied to every resource name (e.g. "myapp"). Max 16 chars keeps all derived resource names within Azure limits.')
@maxLength(16)
param namePrefix string

@description('Azure region; defaults to the resource group location.')
param location string = resourceGroup().location

@description('Deployment environment name (e.g. dev, staging, prod).')
param environment string = 'dev'

@description('PostgreSQL flexible server administrator login name.')
param postgresAdminLogin string

@description('PostgreSQL flexible server administrator password. Pass via --parameters or a GitHub secret — never store in plain text.')
@secure()
param postgresAdminPassword string

@description('App Service plan SKU name (e.g. B1, B2, S1, P1v3).')
param appServiceSku string = 'B1'

@description('Additional resource tags merged over the defaults (environment, project, managedBy).')
param tags object = {}

// Merge caller-supplied tags over opinionated defaults
var resolvedTags = union(
  {
    environment: environment
    project: 'starter-app'
    managedBy: 'bicep'
  },
  tags
)

// Map infrastructure environment name → ASP.NET Core environment name
var aspNetCoreEnv = environment == 'dev' ? 'Development' : 'Production'

// ── Log Analytics workspace ──────────────────────────────────────────────────
module logAnalytics 'modules/logAnalytics.bicep' = {
  name: 'logAnalytics'
  params: {
    namePrefix: namePrefix
    environment: environment
    location: location
    tags: resolvedTags
  }
}

// ── Application Insights (workspace-based) ──────────────────────────────────
module appInsights 'modules/appInsights.bicep' = {
  name: 'appInsights'
  params: {
    namePrefix: namePrefix
    environment: environment
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    tags: resolvedTags
  }
}

// ── Key Vault (RBAC authorization mode) ─────────────────────────────────────
module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    namePrefix: namePrefix
    environment: environment
    location: location
    tags: resolvedTags
  }
}

// ── PostgreSQL Flexible Server + database ────────────────────────────────────
module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: {
    namePrefix: namePrefix
    environment: environment
    location: location
    adminLogin: postgresAdminLogin
    adminPassword: postgresAdminPassword
    tags: resolvedTags
  }
}

// ── App Service plan + Web App (system-assigned MI, .NET 10, HTTPS-only) ────
module appService 'modules/appService.bicep' = {
  name: 'appService'
  params: {
    namePrefix: namePrefix
    environment: environment
    location: location
    sku: appServiceSku
    keyVaultUri: keyVault.outputs.keyVaultUri
    appInsightsConnectionString: appInsights.outputs.connectionString
    aspNetCoreEnvironment: aspNetCoreEnv
    tags: resolvedTags
  }
}

// ── RBAC: grant Web App MI access to Key Vault secrets + App Insights metrics
module rbac 'modules/rbac.bicep' = {
  name: 'rbac'
  params: {
    webAppPrincipalId: appService.outputs.principalId
    keyVaultName: keyVault.outputs.keyVaultName
    appInsightsName: appInsights.outputs.appInsightsName
  }
}

// ── Outputs for downstream use (pipeline variables, post-deploy config) ──────
output webAppName string = appService.outputs.webAppName
output webAppDefaultHostName string = appService.outputs.webAppDefaultHostName
output keyVaultName string = keyVault.outputs.keyVaultName
output postgresFqdn string = postgres.outputs.fqdn
output appInsightsConnectionString string = appInsights.outputs.connectionString
