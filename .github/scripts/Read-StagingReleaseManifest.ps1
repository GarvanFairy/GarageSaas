[CmdletBinding(DefaultParameterSetName='File')]
param([Parameter(Mandatory,ParameterSetName='File')][string]$Path,
    [Parameter(Mandatory,ParameterSetName='Memory')][string]$Json,
    [Parameter(Mandatory)][string]$Commit)
$ErrorActionPreference='Stop'
if($Commit -cnotmatch '\A[0-9a-f]{40}\z' -or [string]::Equals($Commit,('0'*40),[StringComparison]::Ordinal)){throw 'Invalid exact release commit.'}
try{
    if($PSCmdlet.ParameterSetName -eq 'File'){
        if((Get-Item -LiteralPath $Path).Length -gt 65536){throw 'Oversized manifest.'}
        $raw=[IO.File]::ReadAllText([IO.Path]::GetFullPath($Path))
    }else{$raw=$Json}
    if([Text.Encoding]::UTF8.GetByteCount($raw) -gt 65536){throw 'Oversized manifest.'}
    $document=[Text.Json.JsonDocument]::Parse($raw)
    try{
        if($document.RootElement.ValueKind -ne [Text.Json.JsonValueKind]::Object){throw 'Invalid manifest root.'}
        $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $allowed=[Collections.Generic.HashSet[string]]::new([string[]]@('schemaVersion','commit','sha256','package','sdk','ciRun','repository','configuration'),[StringComparer]::Ordinal)
        foreach($p in $document.RootElement.EnumerateObject()){
            if(!$seen.Add($p.Name) -or !$allowed.Contains($p.Name) -or $p.Value.ValueKind -in @([Text.Json.JsonValueKind]::Array,[Text.Json.JsonValueKind]::Object)){throw 'Ambiguous manifest.'}
        }
        if($seen.Count -ne 8 -or $document.RootElement.GetProperty('schemaVersion').ValueKind -ne [Text.Json.JsonValueKind]::Number -or
            $document.RootElement.GetProperty('schemaVersion').GetInt32() -ne 1){throw 'Invalid manifest fields.'}
        foreach($key in @('commit','sha256','package','repository','configuration','sdk')){
            if($document.RootElement.GetProperty($key).ValueKind -ne [Text.Json.JsonValueKind]::String){throw 'Invalid manifest type.'}
        }
        $run=$document.RootElement.GetProperty('ciRun')
        if($run.ValueKind -ne [Text.Json.JsonValueKind]::Null -and
            ($run.ValueKind -ne [Text.Json.JsonValueKind]::String -or $run.GetString() -cnotmatch '\A[1-9][0-9]{0,18}\z')){throw 'Invalid run identity.'}
    }finally{$document.Dispose()}
    $manifest=$raw|ConvertFrom-Json
}catch{throw 'Missing, ambiguous or invalid release manifest.'}
if(![string]::Equals($manifest.commit,$Commit,[StringComparison]::Ordinal) -or
    ![string]::Equals($manifest.package,"GarageSaas-$Commit.zip",[StringComparison]::Ordinal) -or
    ![string]::Equals($manifest.repository,'GarvanFairy/GarageSaas',[StringComparison]::Ordinal) -or
    ![string]::Equals($manifest.configuration,'Release',[StringComparison]::Ordinal) -or
    $manifest.sha256 -cnotmatch '\A[a-f0-9]{64}\z' -or
    $manifest.sdk -cnotmatch '\A[0-9]+\.[0-9]+\.[0-9]+(?:[-+][A-Za-z0-9.-]+)?\z'){
    throw 'Release manifest identity mismatch.'
}
return $manifest
