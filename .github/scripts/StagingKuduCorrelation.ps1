# Uses Kudu's documented ZIP message parameter, preserved by CommitRepo and
# DeploymentManager.GetResult. Never follows Location=/latest or server URLs.
function Read-StagingCorrelationHistory([byte[]]$Bytes,[string]$Marker){
    $doc=$null
    try{
        if($Marker -cnotmatch '\AGarageSaas-[a-f0-9]{32}\z' -or !$Bytes.Length -or $Bytes.Length -gt 65536){throw 'Invalid history'}
        $doc=[Text.Json.JsonDocument]::Parse([Text.UTF8Encoding]::new($false,$true).GetString($Bytes),[Text.Json.JsonDocumentOptions]@{MaxDepth=8})
        if($doc.RootElement.ValueKind -ne [Text.Json.JsonValueKind]::Array -or $doc.RootElement.GetArrayLength() -gt 100){throw 'Invalid inventory'}
        $foundIds=[Collections.Generic.List[string]]::new();$ids=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach($node in $doc.RootElement.EnumerateArray()){
            if($node.ValueKind -ne [Text.Json.JsonValueKind]::Object){throw 'Invalid record'}
            $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
            foreach($p in $node.EnumerateObject()){if(!$seen.Add($p.Name)){throw 'Duplicate'} }
            $id=$node.GetProperty('id').GetString()
            if($id -cnotmatch '\A(?:[a-f0-9]{32}|temp-[a-f0-9]{8})\z' -or !$ids.Add($id)){throw 'Invalid ID'}
            if($node.GetProperty('message').GetString() -ceq $Marker){
                if($id -cnotmatch '\A[a-f0-9]{32}\z' -or $node.GetProperty('deployer').GetString() -cne 'ZipDeploy'){throw 'Invalid matching record'}
                $foundIds.Add($id)
            }
        }
        if($foundIds.Count -gt 1){throw 'Ambiguous correlation'}
        return [pscustomobject]@{available=$true;id=if($foundIds.Count -eq 1){$foundIds[0]}else{$null}}
    }catch{return [pscustomobject]@{available=$false;id=$null}}finally{if($doc){$doc.Dispose()}}
}
function Invoke-CorrelatedStagingUpload {
    param([scriptblock]$Transport,[IO.Stream]$Zip,[scriptblock]$PollWait={ [Threading.Thread]::Sleep(2000) })
    $receipt=[pscustomobject]@{outcome='Unknown';reason='MissingCorrelation';deploymentId=$null}
    try{
        $marker='GarageSaas-'+[Guid]::NewGuid().ToString('N')
        $url=$script:StagingScm+'/api/deployments'
        $before=& $Transport 'History' $url $null
        if($before.failure -cne 'None' -or $before.code -ne 200){return $receipt}
        $prior=Read-StagingCorrelationHistory $before.bytes $marker
        if(!$prior.available -or $prior.id){return $receipt}
        # One upload; a timeout/interruption may have been accepted and is Unknown.
        $r=& $Transport 'Upload' ($script:StagingScm+'/api/zipdeploy?isAsync=true&message='+$marker) $Zip
        if($r.failure -cne 'None'){
            $receipt.reason=if($r.failure -cin @('Timeout','OutputLimit','InvalidResponse')){$r.failure}else{'Interrupted'}
            return $receipt
        }
        if($r.code -notin @(200,202)){$receipt.reason='UnexpectedResponse';return $receipt}
        # Location is not proof. Reject unsafe/conflicting URLs; tolerate ONLY the
        # observed fixed Kudu latest contract or an exact same-host status URL.
        if($r.location -and !(Test-KuduEndpoint $r.location 'Status') -and
           $r.location -cnotmatch ('\A'+[regex]::Escape($script:StagingScm)+'/api/deployments/latest\?deployer=ZipDeploy&time=[0-9]{4}-[0-9]{2}-[0-9]{2}_[0-9]{2}-[0-9]{2}-[0-9]{2}Z\z')){$receipt.reason='InvalidResponse';return $receipt}
        for($i=0;$i -lt 6;$i++){
            & $PollWait|Out-Null
            $list=& $Transport 'History' $url $null
            if($list.failure -cne 'None' -or $list.code -ne 200){$receipt.reason='StatusUnavailable';return $receipt}
            $match=Read-StagingCorrelationHistory $list.bytes $marker
            if(!$match.available){$receipt.reason='InvalidResponse';return $receipt}
            if(!$match.id){continue}
            $exact=$script:StagingScm+'/api/deployments/'+$match.id
            if((Test-KuduEndpoint $r.location 'Status') -and $r.location -cne $exact){$receipt.reason='InvalidResponse';return $receipt}
            if($receipt.deploymentId -and $receipt.deploymentId -cne $match.id){$receipt.reason='InvalidResponse';return $receipt}
            $receipt.deploymentId=$match.id
            $s=& $Transport 'Status' $exact $null
            if($s.failure -cne 'None' -or $s.code -ne 200){$receipt.reason='StatusUnavailable';return $receipt}
            # Confirm request marker in the exact-ID response, independently of list.
            $single=Read-StagingCorrelationHistory ([Text.Encoding]::UTF8.GetBytes('['+[Text.UTF8Encoding]::new($false,$true).GetString($s.bytes)+']')) $marker
            if(!$single.available -or $single.id -cne $match.id){$receipt.reason='InvalidResponse';return $receipt}
            $outcome=Read-KuduStatus $s.bytes $match.id
            if($outcome -cin @('ConfirmedSuccess','ConfirmedFailure')){
                $receipt.outcome=$outcome;$receipt.reason=if($outcome -ceq 'ConfirmedSuccess'){'ExactDeploymentActive'}else{'ExactDeploymentFailed'}
                return $receipt
            }
            if($outcome -ceq 'Unknown'){$receipt.reason='InvalidResponse';return $receipt}
        }
        $receipt.reason='CompletionUnproven'
    }catch{$receipt.reason='Interrupted'}
    return $receipt
}
