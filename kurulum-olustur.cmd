@echo off
rem Vernuko kurulum dosyasini bu bilgisayarda olusturur ve kurulumu baslatir.
rem Kullanim: cift tiklayin ya da PowerShell'de:  .\kurulum-olustur.cmd
setlocal
cd /d "%~dp0"

set VER=
for /f "tokens=3 delims=<>" %%v in ('findstr /c:"<Version>" Directory.Build.props') do set VER=%%v
if "%VER%"=="" set VER=0.0.1

echo.
echo === Vernuko %VER% kurulum dosyasi hazirlaniyor (birkac dakika surebilir) ===
echo.

if exist publish rmdir /s /q publish
if exist releases rmdir /s /q releases

echo [1/3] Program derleniyor...
dotnet publish src\Vernuko.Desktop\Vernuko.Desktop.csproj -c Release -r win-x64 --self-contained -o publish
if errorlevel 1 goto fail

echo [2/3] Paketleme araci hazirlaniyor...
dotnet tool restore
if errorlevel 1 goto fail

echo [3/3] Kurulum dosyasi olusturuluyor...
dotnet vpk pack --packId VernukoApp --packVersion %VER% --packDir publish --mainExe Vernuko.exe --packTitle Vernuko --packAuthors Vernuko --icon src\Vernuko.Desktop\Assets\vernuko.ico --runtime win-x64 --outputDir releases
if errorlevel 1 goto fail
if not exist "releases\VernukoApp-win-Setup.exe" goto fail

echo.
echo === HAZIR: %~dp0releases\VernukoApp-win-Setup.exe ===
echo Kurulum baslatiliyor. "Windows bilgisayarinizi korudu" uyarisi cikarsa: Ek bilgi - Yine de calistir.
start "" "releases\VernukoApp-win-Setup.exe"
goto end

:fail
echo.
echo === HATA: yukaridaki son satirlari kopyalayip gonderin. ===

:end
echo.
pause
