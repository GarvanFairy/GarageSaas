param([AllowNull()][AllowEmptyString()][string]$ResourceId,
    [AllowNull()][AllowEmptyString()][string]$TenantId,
    [AllowNull()][AllowEmptyString()][string]$SubscriptionId)
$ErrorActionPreference='Stop'
$approved='/subscriptions/d49b2f61-156a-489f-ba4c-acba574d30a0/resourceGroups/GarageSaasResourceGroup/providers/Microsoft.Web/sites/garagesaas-stg-d49b2f61-weu'
if(![string]::Equals($ResourceId,$approved,[StringComparison]::Ordinal) -or
    ![string]::Equals($TenantId,'90589b9f-6a39-4285-a798-5d72f6937826',[StringComparison]::Ordinal) -or
    ![string]::Equals($SubscriptionId,'d49b2f61-156a-489f-ba4c-acba574d30a0',[StringComparison]::Ordinal)){
    throw 'Configured Azure staging target, tenant or subscription is not exactly approved.'
}
Write-Output 'Exact staging target, tenant and subscription verified. No Azure access performed.'
