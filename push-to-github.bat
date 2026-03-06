@echo off
cd /d "%~dp0"
echo Initializing git repository...
git init
git add .
git commit -m "Initial commit: Nexus Dashboard Blazor WASM + API solution"
git branch -M main
git remote add origin https://github.com/MFM-Mike/NexusDashboard.git
git push -u origin main
echo.
echo Done! Check https://github.com/MFM-Mike/NexusDashboard
pause
