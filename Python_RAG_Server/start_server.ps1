$ErrorActionPreference = "Stop"

$serverDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $serverDirectory
$webProject = Join-Path $repositoryRoot "PRN222_Project\PRN222.RazorWebApp"
$userSecretsId = "prn222-gr8-asm2-judge-7d94ab40"
$secretFile = Join-Path $env:APPDATA "Microsoft\UserSecrets\$userSecretsId\secrets.json"

if (Test-Path -LiteralPath $secretFile) {
    $secrets = Get-Content -LiteralPath $secretFile -Raw | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($env:GEMINI_API_KEY)) {
        $env:GEMINI_API_KEY = $secrets.'AIProviders:Gemini:ApiKey'
    }
    if ([string]::IsNullOrWhiteSpace($env:GITHUB_MODELS_TOKEN)) {
        $env:GITHUB_MODELS_TOKEN = $secrets.'AIProviders:OpenAI:ApiKey'
    }
}

if ([string]::IsNullOrWhiteSpace($env:GEMINI_API_KEY)) {
    throw "Missing AIProviders:Gemini:ApiKey in User Secrets for $webProject"
}

# Mac dinh dung gemini-2.5-flash (khop voi nhan "Gemini-2.5-Flash" tren
# Dashboard). Quota free-tier chi ~20 request/ngay: neu bi 429, chay
#   $env:GEMINI_MODEL = "gemini-3.5-flash"
# truoc khi goi script nay de tam thoi doi sang model con quota.
if ([string]::IsNullOrWhiteSpace($env:GEMINI_MODEL)) {
    $env:GEMINI_MODEL = "gemini-2.5-flash"
}
if ([string]::IsNullOrWhiteSpace($env:GITHUB_MODELS_TOKEN)) {
    throw "Missing AIProviders:OpenAI:ApiKey in User Secrets for $webProject"
}

$python311 = Join-Path $serverDirectory "venv311\Scripts\python.exe"
$pythonDefault = Join-Path $serverDirectory "venv\Scripts\python.exe"
$python = if (Test-Path -LiteralPath $python311) { $python311 } else { $pythonDefault }

if (-not (Test-Path -LiteralPath $python)) {
    throw "Python virtual environment not found. Run setup from run_demo.bat first."
}

Set-Location $serverDirectory
& $python -u -m uvicorn api_server:app --host 0.0.0.0 --port 8000
