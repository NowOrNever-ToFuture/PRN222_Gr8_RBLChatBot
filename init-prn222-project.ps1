param(
    [string]$RootDir = (Get-Location).Path,
    [switch]$SkipPythonVenv,
    [switch]$SkipDotNetRestore
)

$ErrorActionPreference = "Stop"

$SolutionName = "PRN222_Project"
$SolutionDir = Join-Path -Path $RootDir -ChildPath $SolutionName
$PythonDir = Join-Path -Path $RootDir -ChildPath "Python_RAG_Server"

function Test-Command {
    param([Parameter(Mandatory = $true)][string]$Name)
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

Write-Host "PRN222 setup started at: $RootDir" -ForegroundColor Green

if (-not (Test-Command "dotnet")) {
    throw "dotnet CLI was not found. Install .NET 8 SDK before running this setup."
}

if (-not (Test-Command "python")) {
    throw "python was not found. Install Python 3.10+ before running this setup."
}

if (-not (Test-Path $SolutionDir)) {
    Write-Host "Creating .NET solution at: $SolutionDir" -ForegroundColor Cyan

    New-Item -ItemType Directory -Path $SolutionDir -Force | Out-Null
    Push-Location $SolutionDir

    dotnet new sln -n $SolutionName

    dotnet new classlib -n PRN222.Models
    dotnet new classlib -n PRN222.Repositories
    dotnet new classlib -n PRN222.Services
    dotnet new webapp -n PRN222.RazorWebApp

    dotnet sln "$SolutionName.sln" add PRN222.Models/PRN222.Models.csproj
    dotnet sln "$SolutionName.sln" add PRN222.Repositories/PRN222.Repositories.csproj
    dotnet sln "$SolutionName.sln" add PRN222.Services/PRN222.Services.csproj
    dotnet sln "$SolutionName.sln" add PRN222.RazorWebApp/PRN222.RazorWebApp.csproj

    dotnet add PRN222.Repositories/PRN222.Repositories.csproj reference PRN222.Models/PRN222.Models.csproj
    dotnet add PRN222.Services/PRN222.Services.csproj reference PRN222.Repositories/PRN222.Repositories.csproj
    dotnet add PRN222.RazorWebApp/PRN222.RazorWebApp.csproj reference PRN222.Services/PRN222.Services.csproj

    dotnet add PRN222.Repositories/PRN222.Repositories.csproj package Microsoft.EntityFrameworkCore --version 8.0.0
    dotnet add PRN222.Repositories/PRN222.Repositories.csproj package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.0
    dotnet add PRN222.RazorWebApp/PRN222.RazorWebApp.csproj package Microsoft.EntityFrameworkCore.Design --version 8.0.*

    Pop-Location
} else {
    Write-Host ".NET solution already exists. Skipping scaffold." -ForegroundColor Yellow
}

if (-not $SkipDotNetRestore) {
    Write-Host "Restoring .NET packages..." -ForegroundColor Cyan
    dotnet restore (Join-Path $SolutionDir "$SolutionName.sln")
}

if (-not (Test-Path $PythonDir)) {
    Write-Host "Creating Python_RAG_Server directory..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $PythonDir -Force | Out-Null
}

$RequirementsPath = Join-Path $PythonDir "requirements.txt"
if (-not (Test-Path $RequirementsPath)) {
    Write-Host "Creating Python requirements.txt..." -ForegroundColor Cyan
    @(
        "fastapi",
        "uvicorn",
        "pydantic",
        "python-multipart",
        "sentence-transformers",
        "pandas",
        "openpyxl",
        "python-docx",
        "python-pptx",
        "pypdf",
        "tabulate"
    ) | Set-Content -Path $RequirementsPath -Encoding UTF8
}

if (-not $SkipPythonVenv) {
    $VenvDir = Join-Path $PythonDir "venv"
    if (-not (Test-Path $VenvDir)) {
        Write-Host "Creating Python virtual environment..." -ForegroundColor Cyan
        python -m venv $VenvDir
    } else {
        Write-Host "Python virtual environment already exists. Skipping creation." -ForegroundColor Yellow
    }

    $PipExe = Join-Path $VenvDir "Scripts\pip.exe"
    if (Test-Path $PipExe) {
        Write-Host "Installing Python packages from requirements.txt..." -ForegroundColor Cyan
        & $PipExe install -r $RequirementsPath
    } else {
        Write-Host "Could not find venv pip.exe. Activate venv manually and run pip install -r requirements.txt." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Setup completed." -ForegroundColor Green
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Review PRN222_Project\PRN222.RazorWebApp\appsettings.json for SQL Server and API keys." -ForegroundColor White
Write-Host "  2. Run EF migration in Visual Studio Package Manager Console: Update-Database" -ForegroundColor White
Write-Host "  3. Start Python AI server:" -ForegroundColor White
Write-Host "     cd /d $PythonDir" -ForegroundColor Gray
Write-Host "     venv\Scripts\activate" -ForegroundColor Gray
Write-Host "     python -m uvicorn api_server:app --port 8000" -ForegroundColor Gray
Write-Host "  4. Start the .NET web app from Visual Studio or dotnet run." -ForegroundColor White
