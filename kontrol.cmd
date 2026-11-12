@echo off
rem Vernuko yerel kontrol: GitHub'daki otomatik kontrolun aynisini bu bilgisayarda yapar.
rem 1) guncel kodu indirir  2) derler  3) tum testleri calistirir  4) tum ekranlari acip kontrol eder ve basari
rem senaryosunu gercek ekranlarda yurutur: musteri, paket, randevu, tamamla (8->7), yedek, geri yukleme
rem Kullanim: cift tiklayin ya da PowerShell'de:  .\kontrol.cmd
setlocal
cd /d "%~dp0"
set OUT=%USERPROFILE%\vernuko-kontrol

echo.
echo === Vernuko kontrol basliyor (birkac dakika surebilir) ===
echo.

echo [1/4] Guncel kod indiriliyor...
git pull
if errorlevel 1 goto fail

echo.
echo [2/4] Derleniyor...
dotnet build Vernuko.sln -c Release -v minimal
if errorlevel 1 goto fail

echo.
echo [3/4] Testler calisiyor...
dotnet test tests\Vernuko.Tests\Vernuko.Tests.csproj -c Release --no-build
if errorlevel 1 goto fail

echo.
echo [4/4] Ekranlar ve kullanim senaryosu kontrol ediliyor (pencere acilmaz, 2-3 dakika)...
if exist "%OUT%" rmdir /s /q "%OUT%"
rem Microsoft imzali "dotnet" uzerinden: imzasiz Vernuko.exe, Windows 11 Akilli Uygulama Denetimi tarafindan engellenir.
dotnet "src\Vernuko.Desktop\bin\Release\net10.0-windows\Vernuko.dll" --smoke-test "%OUT%"
set SMOKE=%errorlevel%
echo.
if exist "%OUT%\smoke-report.txt" powershell -NoProfile -Command "[Console]::OutputEncoding = [Text.Encoding]::UTF8; Get-Content -Encoding UTF8 '%OUT%\smoke-report.txt'"
if not "%SMOKE%"=="0" goto fail

echo.
echo === TUM KONTROLLER BASARILI ===
echo Ekran goruntuleri: %OUT%
goto end

:fail
echo.
echo === HATA: yukaridaki son satirlari kopyalayip gonderin. ===

:end
echo.
pause
