param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'
function ReadEntry($entry) {
    $stream=$entry.Open(); $memory=[IO.MemoryStream]::new()
    try { $stream.CopyTo($memory); return ,$memory.ToArray() } finally { $stream.Dispose(); $memory.Dispose() }
}
function ReadJson($entry) {
    try { return [Text.Encoding]::UTF8.GetString((ReadEntry $entry)) | ConvertFrom-Json -AsHashtable }
    catch { throw 'Invalid artifact JSON.' }
}
function CheckSettings($value, [string]$parent = '', [string]$path = '') {
    if ($value -is [Collections.IDictionary]) {
        foreach ($key in $value.Keys) {
            $child=$value[$key]; $next=if($path){"$path.$key"}else{$key}
            # One exact public policy identifier, at its known JSON location, with
            # B2C policy-id syntax. No general Password-named property exemption.
            $policy=$next -ceq 'AzureAdB2C.ResetPasswordPolicyId' -and $child -is [string] -and $child -match '^B2C_[A-Za-z0-9_]{1,100}$'
            if (!$policy -and ($key -match '(?i)(password|pwd$|clientsecret|apikey|access[_-]?token|refresh[_-]?token|id[_-]?token|bearer[_-]?token|^token$|privatekey|connectionstring)' -or $parent -eq 'ConnectionStrings') -and
                $null -ne $child -and $child -isnot [Collections.IDictionary] -and $child -isnot [array] -and ![string]::IsNullOrWhiteSpace([string]$child)) {
                throw 'Nonempty credential or connection configuration in artifact.'
            }
            CheckSettings $child $key $next
        }
    } elseif ($value -is [array]) { foreach ($child in $value) { CheckSettings $child $parent $path } }
}
$inventory=Get-Content (Join-Path $PSScriptRoot '../staging-native-dependencies.json') -Raw|ConvertFrom-Json -AsHashtable
$native=@{};foreach($item in $inventory.files){$native[$item.path]=$item.sha256}
$zip=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Path))
try {
    $depsEntry=$zip.GetEntry('GarageSaas.deps.json');if(!$depsEntry){throw 'Missing dependency graph.'}
    $deps=ReadJson $depsEntry
    if($deps.runtimeTarget.name -ne '.NETCoreApp,Version=v10.0'){throw 'Unexpected application target framework.'}
    $target=$deps.targets[$deps.runtimeTarget.name];if(!$target){throw 'Missing runtime dependency inventory.'}
    $expected=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($library in $target.Keys){
        foreach($section in @('runtime','native','runtimeTargets','resources')){
            $assets=$target[$library][$section];if(!$assets){continue}
            foreach($asset in $assets.Keys){
                if($asset.EndsWith('/_._')){continue}
                $name=switch($section){
                    'runtimeTargets'{$asset}
                    'resources'{ $assets[$asset].locale+'/'+[IO.Path]::GetFileName($asset) }
                    default{[IO.Path]::GetFileName($asset)}
                }
                [void]$expected.Add($name)
            }
        }
    }
    # SDK-generated apphosts for the two reviewed projects, not arbitrary EXEs.
    foreach($name in @('GarageSaas.exe','SignupAPI.exe')){[void]$expected.Add($name)}
    $package=$deps.libraries[$inventory.package]
    if(!$package -or $package.type -ne 'package' -or $package.sha512 -cne $inventory.packageSha512){throw 'Reviewed native package identity changed.'}
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in $zip.Entries){
        $name=$entry.FullName.Replace('\','/')
        $reviewedName=$false
        $named=$inventory.reviewedNamedLibrary
        if($name -ceq $named.path){
            $namedPackage=$deps.libraries[$named.package]
            $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData((ReadEntry $entry))).ToLowerInvariant()
            if(!$namedPackage -or $namedPackage.sha512 -cne $named.packageSha512 -or
                !$target[$named.package].runtime.ContainsKey($named.asset) -or $hash -cne $named.sha256){throw 'Reviewed named dependency identity changed.'}
            $reviewedName=$true
        }
        if(!$seen.Add($name) -or $name -match '(^/|^[A-Za-z]:|(^|/)\.\.?(/|$)|:)' -or
            $name -match '(^|/)\.' -or $name -match '(?i)(^|/)(uploads|artifacts|deployment|docs|node_modules|Properties)(/|$)' -or
            ($name -match '(?i)secrets' -and !$reviewedName) -or
            $name -match '(?i)(credentials|receipt|subjectverification|nameidentifier\.txt|launchsettings|appsettings\.(Development|Local|Staging)|\.env|\.pfx$|\.pem$|\.key$|\.pubxml|\.publishsettings|\.cs$|\.csproj$|\.pdb$|\.trx$)' -or
            (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000){throw 'Artifact contains a forbidden, duplicate or unsafe path.'}
        # SignupAPI is a referenced project; its two public settings files are
        # expected publish content, inspected with the same strict JSON rules.
        if($name -match '(?i)appsettings.*\.json$' -and $name -cnotin @('appsettings.json','appsettings.Production.json','signupappsettings.json','signupappsettings.Production.json')){throw 'Unexpected application configuration file.'}
        $bytes=ReadEntry $entry
        $binary=$name -match '(?i)\.(dll|exe|so|dylib)$' -or
            ($bytes.Length -ge 4 -and (($bytes[0] -eq 0x4D -and $bytes[1] -eq 0x5A) -or
             ($bytes[0] -eq 0x7F -and $bytes[1] -eq 0x45 -and $bytes[2] -eq 0x4C -and $bytes[3] -eq 0x46)))
        if($binary -and !$expected.Contains($name)){throw 'Binary is not in the runtime dependency inventory.'}
        $reviewedNative=$false
        if($native.ContainsKey($name)){
            $sha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
            if($sha -cne $native[$name] -or !$target[$inventory.package].runtimeTargets.ContainsKey($name)){throw 'Reviewed native asset content or provenance changed.'}
            $reviewedNative=$true
        }
        # All binaries still receive diagnostic/private-key scans.
        # Only six hash-pinned native assets skip broad credential-assignment matchers;
        # adding a secret changes the hash and fails before that exception applies.
        foreach($content in @([Text.Encoding]::UTF8.GetString($bytes),[Text.Encoding]::Unicode.GetString($bytes))){
            if($content -match 'StagingSubjectVerificationMiddleware|StagingVerification:CaptureOwnSubject|staging-identity-verification'){throw 'Artifact contains diagnostic behavior.'}
            if($content -match '-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----'){throw 'Artifact contains private-key material.'}
            if(!$reviewedNative -and $content -match '(?i)(?:Password|Pwd)\s*=\s*[^;\s"<>]+'){throw 'Artifact contains a password assignment.'}
            if(!$reviewedNative -and $content -match '(?i)\b(?:client[_-]?secret|api[_-]?key|access[_-]?token|refresh[_-]?token|id[_-]?token)\s*[:=]\s*["'']?[A-Za-z0-9_+/.=-]{12,}') {throw 'Artifact contains an explicit secret assignment.'}
        }
        # Structural configuration inspection, not arbitrary binary decoding as JSON.
        if($name -match '(?i)\.json$'){CheckSettings (ReadJson $entry)}
        if($name -match '(?i)\.(config|xml)$'){
            try{[xml]$xml=[Text.Encoding]::UTF8.GetString($bytes).TrimStart([char]0xFEFF)}catch{throw 'Invalid XML configuration.'}
            foreach($node in $xml.SelectNodes('//*[@name and @value]')){
                if($node.name -match '(?i)(password|pwd$|clientsecret|apikey|access[_-]?token|refresh[_-]?token|id[_-]?token|bearer[_-]?token|^token$|privatekey|connectionstring)' -and ![string]::IsNullOrWhiteSpace($node.value)){throw 'Nonempty XML credential configuration.'}
            }
        }
    }
    foreach($required in @('GarageSaas.dll','SignupAPI.dll','GarageSaas.deps.json','GarageSaas.runtimeconfig.json','web.config','appsettings.json','appsettings.Production.json','Resources/Fonts/OFL.txt')){
        if(!$seen.Contains($required)){throw 'Artifact is missing a required application file.'}
    }
    foreach($binaryName in $expected){if(!$seen.Contains($binaryName)){throw 'Runtime dependency inventory is incomplete.'}}
    $runtime=ReadJson ($zip.GetEntry('GarageSaas.runtimeconfig.json'))
    if($runtime.runtimeOptions.tfm -ne 'net10.0'){throw 'Unexpected runtime configuration.'}
    Write-Output "Artifact safety checks passed: $($zip.Entries.Count) entries; runtime dependencies and reviewed native hashes verified."
} finally {$zip.Dispose()}
