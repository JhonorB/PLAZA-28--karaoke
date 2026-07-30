# Script para cerrar la instancia anterior y lanzar el Karaoke correctamente
Write-Host "🔄 Cerrando instancia anterior de Karaoke..." -ForegroundColor Yellow
Stop-Process -Name "Karaoke.Desktop" -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 800

Write-Host "🚀 Iniciando Karaoke Plaza 28..." -ForegroundColor Cyan
dotnet run --project src/Karaoke.Desktop/Karaoke.Desktop.csproj
