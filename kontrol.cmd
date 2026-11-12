@echo off
rem Vernuko yerel kontrol: GitHub'daki otomatik kontrolun aynisini bu bilgisayarda yapar.
rem 1) guncel kodu indirir  2) derler  3) tum testleri calistirir  4) tum ekranlari acip kontrol eder (smoke test)
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
echo [4/4] Ekranlar kontrol ediliyor (pencere acilmaz, 1-2 dakika)...
if exist "%OUT%" rmdir /s /q "%OUT%"
start "" /wait "src\Vernuko.Desktop\bin\Release\net10.0-windows\Vernuko.exe" --smoke-test "%OUT%"
set SMOKE=%errorlevel%
echo.
if exist "%OUT%\smoke-report.txt" type "%OUT%\smoke-report.txt"
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
