# Run this script from inside D:\Claude\NexusDashboard\
# It initializes git, commits all files, and pushes to GitHub.

Set-Location $PSScriptRoot

git init
git add .
git commit -m "Initial commit: Nexus Dashboard Blazor WASM + API solution"
git branch -M main
git remote add origin https://github.com/MFM-Mike/NexusDashboard.git
git push -u origin main

Write-Host ""
Write-Host "Done! Visit: https://github.com/MFM-Mike/NexusDashboard" -ForegroundColor Green
