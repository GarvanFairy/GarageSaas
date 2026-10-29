param([Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][AllowEmptyString()][string]$ExpectedFingerprint)
$ErrorActionPreference='Stop'
if($Commit -cnotmatch '\A[0-9a-f]{40}\z' -or [string]::Equals($Commit,('0'*40),[StringComparison]::Ordinal)){
    throw 'Build identity requires an exact non-placeholder application commit.'
}
& (Join-Path $PSScriptRoot 'Get-CiRuntimeFingerprint.ps1') -Path $Path -Expected $ExpectedFingerprint | Out-Null
foreach($name in @('GarageSaas.dll','SignupAPI.dll')){
    # Read PE version metadata without loading/executing the application assembly.
    $version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path ([IO.Path]::GetFullPath($Path)) $name)).ProductVersion
    if([string]::IsNullOrEmpty($version) -or !$version.EndsWith('+'+$Commit,[StringComparison]::Ordinal)){
        throw 'Application binary declared source identity does not match the selected commit.'
    }
}
Write-Output 'Tested runtime fingerprint and both application binary source identities verified.'
