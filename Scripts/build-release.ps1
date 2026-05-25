$ErrorActionPreference = "Stop"
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .\publish
Write-Host "Published to .\publish" -ForegroundColor Green
