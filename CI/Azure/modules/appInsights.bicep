@description('Prefix applied to all resource names. Max 16 characters.')
param namePrefix string

@description('Deployment environment name (e.g. dev, staging, prod).')
param environment string

@description('Azure region for Application Insights.')
param location string

@description('Resource ID of the Log Analytics workspace to link (workspace-based App Insights).')
param logAnalyticsWorkspaceId string

@description('Tags to apply to the resource.')
param tags object = {}

var aiName = '${namePrefix}-ai-${environment}'

resource appInsightsComp 'Microsoft.Insights/components@2020-02-02' = {
  name: aiName
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspaceId
    RetentionInDays: 90
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

output appInsightsId string = appInsightsComp.id
output appInsightsName string = appInsightsComp.name
output connectionString string = appInsightsComp.properties.ConnectionString
output instrumentationKey string = appInsightsComp.properties.InstrumentationKey
