param([Parameter(Mandatory)][ValidateSet('A','B','C')][string]$Arm,
      [Parameter(Mandatory)][string]$ApplicationRoot,
      [Parameter(Mandatory)][string]$ExpectedController)
$ErrorActionPreference='Stop'
$policy=Get-Content "$PSScriptRoot/sql-abc/policy.json" -Raw | ConvertFrom-Json -AsHashtable
if($ExpectedController -cnotmatch '\A[0-9a-f]{40}\z' -or $ExpectedController -ceq ('0'*40)){
    throw 'Controller enrollment required after review and merge. No experiment is authorized by this PR.'
}
if($env:GITHUB_REF -cne 'refs/heads/master' -or $env:GITHUB_EVENT_NAME -cne 'workflow_dispatch' -or
   $env:GITHUB_RUN_ATTEMPT -cne '1' -or $env:GITHUB_REPOSITORY -cne 'GarvanFairy/GarageSaas') {throw 'Unapproved execution context.'}
foreach($pair in @(@((Join-Path $PSScriptRoot '../..'),$ExpectedController),@($ApplicationRoot,$policy.arms[$Arm].revision))){
    $actual=(& git -C $pair[0] rev-parse HEAD)
    if($LASTEXITCODE -ne 0 -or $actual -cne $pair[1]){throw 'Checkout SHA mismatch.'}
    $dirty=& git -C $pair[0] status --porcelain --untracked-files=all
    if($LASTEXITCODE -ne 0 -or $dirty){throw 'Checkout is not clean.'}
}
& git -C (Join-Path $PSScriptRoot '../..') merge-base --is-ancestor $ExpectedController origin/master
if($LASTEXITCODE -ne 0){throw 'Controller is not on the fetched default branch.'}
if($env:GARAGESAAS_ALLOW_SHARED_LOCAL_SQLSERVER){throw 'Shared SQL opt-in forbidden.'}
