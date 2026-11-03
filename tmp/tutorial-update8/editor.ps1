param([string]$Command='eval',[string]$Code,[string]$Arguments='{}')
$descriptor = Get-Content -LiteralPath 'Library/Pipeline/.unity-pipeline-port' -Raw | ConvertFrom-Json
$parameters = if ($Code) { @{ code=$Code; timeout=30000 } } else { ConvertFrom-Json -InputObject $Arguments -AsHashtable }
$body = @{ command=$Command; parameters=$parameters; caller='plugin'; skill='unity-cli' } | ConvertTo-Json -Depth 12
Invoke-RestMethod -Uri ('http://127.0.0.1:' + $descriptor.port + '/api/exec') -Method Post -ContentType 'application/json' -Headers @{Authorization='Bearer ' + $descriptor.evalToken} -Body $body -TimeoutSec 45 | ConvertTo-Json -Depth 20
