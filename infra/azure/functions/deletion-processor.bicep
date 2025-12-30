// Azure Function App for GDPR Deletion Processing
// Timer-triggered function runs daily to process deletion requests after 30-day window
// FR-014c: Process deletion requests within 30 days

@description('Location for all resources')
param location string = resourceGroup().location

@description('Name of the Function App')
param functionAppName string

@description('Name of the App Service Plan')
param appServicePlanName string = '${functionAppName}-plan'

@description('Name of the Storage Account for function app')
param storageAccountName string

@description('Application Insights connection string')
param appInsightsConnectionString string

@description('Cosmos DB connection string')
param cosmosDbConnectionString string

@description('Azure Blob Storage connection string (for audit logs)')
param blobStorageConnectionString string

@description('Schedule for deletion processor (NCRONTAB format)')
param timerSchedule string = '0 0 2 * * *' // Daily at 2:00 AM UTC

@description('Environment name (dev, staging, prod)')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string = 'dev'

@description('Tags for resources')
param tags object = {
  application: 'hr-agent'
  component: 'deletion-processor'
  environment: environment
}

// App Service Plan (Consumption tier for serverless)
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  sku: {
    name: 'Y1' // Consumption (serverless) plan
    tier: 'Dynamic'
  }
  kind: 'functionapp'
  properties: {
    reserved: true // Required for Linux
  }
}

// Storage Account for Function App runtime
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

// Function App
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  properties: {
    serverFarmId: appServicePlan.id
    reserved: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'CosmosDb__ConnectionString'
          value: cosmosDbConnectionString
        }
        {
          name: 'BlobStorage__ConnectionString'
          value: blobStorageConnectionString
        }
        {
          name: 'DeletionProcessor__TimerSchedule'
          value: timerSchedule
        }
        {
          name: 'DeletionProcessor__Enabled'
          value: 'true'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      alwaysOn: false // Consumption plan doesn't support always on
    }
    httpsOnly: true
  }
}

// Outputs
output functionAppId string = functionApp.id
output functionAppName string = functionApp.name
output functionAppHostName string = functionApp.properties.defaultHostName
output appServicePlanId string = appServicePlan.id

// Note: Deployment instructions in README
