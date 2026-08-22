@echo off
echo Setting up Nexus Agent Python Automation environment...

if not exist venv (
    echo Creating virtual environment...
    py -3 -m venv venv
)

echo Activating virtual environment...
call venv\Scripts\activate.bat

echo Installing Python dependencies...
pip install -r requirements.txt

echo Installing Playwright browser binaries...
playwright install chromium

echo Automation environment setup complete!
