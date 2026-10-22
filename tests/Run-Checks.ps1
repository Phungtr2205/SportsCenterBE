param([switch]$DockerSmoke, [switch]$LiveGroq)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$compose = Join-Path $PSScriptRoot 'compose.yaml'
$project = 'sc-feature-check-' + [guid]::NewGuid().ToString('N').Substring(0, 10)
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$scratch = Join-Path $tempRoot $project
$apiContainer = "$project-api"
$image = "${project}:local"
$names = @('SPORTSCENTER_TEST_POSTGRES', 'SPORTSCENTER_TEST_REDIS', 'SPORTSCENTER_TEST_GROQ_KEY')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }

function Check-Exit([string]$step) {
    if ($LASTEXITCODE -ne 0) { throw "$step failed (exit $LASTEXITCODE)." }
}

try {
    if ($LiveGroq -and !$env:SPORTSCENTER_TEST_GROQ_KEY) {
        $projectXml = [xml](Get-Content -LiteralPath (Join-Path $repo 'SportsCenterAPI.csproj') -Raw)
        $secretId = [string]$projectXml.Project.PropertyGroup.UserSecretsId
        $secretPath = Join-Path $env:APPDATA "Microsoft/UserSecrets/$secretId/secrets.json"
        $secrets = Get-Content -LiteralPath $secretPath -Raw | ConvertFrom-Json
        $env:SPORTSCENTER_TEST_GROQ_KEY = if ($secrets.'AI:ApiKey') { $secrets.'AI:ApiKey' } else { $secrets.AI.ApiKey }
        if (!$env:SPORTSCENTER_TEST_GROQ_KEY) { throw 'Configure a Groq key in User Secrets before using -LiveGroq.' }
    }
    New-Item -ItemType Directory -Path $scratch | Out-Null
    docker compose -p $project -f $compose up -d --wait --wait-timeout 90
    Check-Exit 'Start isolated PostgreSQL/Redis'
    $pgAddress = (docker compose -p $project -f $compose port postgres 5432).Trim()
    Check-Exit 'Read PostgreSQL port'
    $redisAddress = (docker compose -p $project -f $compose port redis 6379).Trim()
    Check-Exit 'Read Redis port'
    $env:SPORTSCENTER_TEST_POSTGRES = "Host=127.0.0.1;Port=$($pgAddress.Split(':')[-1]);Database=sportscenter_test;Username=test;Password=test-only-password"
    $env:SPORTSCENTER_TEST_REDIS = "$redisAddress,password=test-only-password,abortConnect=false"
    # Keep the Debug executable available while Visual Studio is running the API.
    $testArguments = @((Join-Path $PSScriptRoot 'SportsCenterAPI.Tests/SportsCenterAPI.Tests.csproj'),
        '--configuration', 'Release', '--results-directory', $scratch, '--logger', 'console;verbosity=minimal')
    if (!$LiveGroq) { $testArguments += @('--filter', 'Category!=LiveGroq') }
    dotnet test @testArguments
    Check-Exit 'Feature integration tests'

    if ($DockerSmoke) {
        docker build -t $image $repo
        Check-Exit 'Build API Dockerfile'
        # These settings contain test-only values. Production settings never enter the container.
        $envFile = Join-Path $scratch 'api.env'
        @(
            'Database__InitializeOnStartup=false'
            'OpenApi__Enabled=true'
            'ConnectionStrings__DefaultConnection=Host=postgres;Database=sportscenter_test;Username=test;Password=test-only-password'
            'ConnectionStrings__Redis=redis:6379,password=test-only-password,abortConnect=false'
            'Jwt__SecretKey=test-only-signing-key-at-least-32-characters-long'
            'Jwt__Issuer=sportscenter-test'
            'Jwt__Audience=sportscenter-test'
            'PORT=9090'
        ) | Set-Content -LiteralPath $envFile -Encoding ascii
        docker run -d --name $apiContainer --network "${project}_default" --env-file $envFile -p '127.0.0.1::9090' $image | Out-Null
        Check-Exit 'Run API container'
        $address = (docker port $apiContainer 9090/tcp).Trim()
        Check-Exit 'Read API port'
        $baseUrl = "http://$address"
        $ready = $false
        for ($i = 0; $i -lt 30; $i++) {
            try {
                $swagger = Invoke-RestMethod "$baseUrl/swagger/v1/swagger.json" -TimeoutSec 2
                $ready = $true
                break
            } catch { Start-Sleep -Seconds 1 }
        }
        if (!$ready) { docker logs $apiContainer; throw 'API container did not become ready.' }
        if (!$swagger.paths.'/api/ai/chat' -or !$swagger.paths.'/api/ai/workout-recommendations') {
            throw 'AI endpoints missing from container Swagger.'
        }
        $login = Invoke-RestMethod "$baseUrl/api/Auth/login" -Method Post -ContentType 'application/json' -Body '{"email":"member-a@example.test","password":"Test-only-123!"}'
        if (!$login.token) { throw 'Container login did not return JWT.' }
        try {
            Invoke-RestMethod "$baseUrl/api/ai/chat" -Method Post -ContentType 'application/json' -Headers @{Authorization="Bearer $($login.token)"} -Body '{"question":"My schedule?"}' | Out-Null
            throw 'Expected missing-AI-config response.'
        } catch {
            if ([int]$_.Exception.Response.StatusCode -ne 503) { throw }
        }
        Write-Host 'Docker smoke passed: PORT=9090, Swagger, PostgreSQL login, JWT, missing AI config -> 503.'
    }
}
finally {
    # Only resources created by this exact invocation are removed.
    if ($DockerSmoke) {
        docker rm -f $apiContainer 2>$null | Out-Null
        docker image rm $image 2>$null | Out-Null
    }
    docker compose -p $project -f $compose down --volumes --remove-orphans
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    $resolved = [IO.Path]::GetFullPath($scratch)
    if ($resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolved) -eq $project -and (Test-Path -LiteralPath $resolved)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
