# PowerShell setup script for Python Automation environment
Write-Host "Setting up Nexus Agent Python Automation environment..." -ForegroundColor Cyan

if (-not (Test-Path "venv")) {
    Write-Host "Creating virtual environment..." -ForegroundColor Yellow
    py -3 -m venv venv
}

Write-Host "Activating virtual environment..." -ForegroundColor Yellow
.\venv\Scripts\Activate.ps1

Write-Host "Installing Python dependencies..." -ForegroundColor Yellow
pip install -r requirements.txt

Write-Host "Installing Playwright browser binaries..." -ForegroundColor Yellow
playwright install chromium

Write-Host "Automation environment setup complete!" -ForegroundColor Green
