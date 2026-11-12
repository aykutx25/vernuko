@echo off
rem Vernuko'yu kaynak koddan derleyip acar. Demo modu icin: demo.cmd (ya da "baslat.cmd demo").
rem Program Microsoft imzali "dotnet" uzerinden baslatilir; imzasiz Vernuko.exe Windows 11 Akilli Uygulama
rem Denetimi (Smart App Control) tarafindan engellenebilir. Kod imzalama sertifikasi alininca buna gerek kalmaz.
setlocal
cd /d "%~dp0"
set ARGS=
if /i "%~1"=="demo" set ARGS=--demo

echo Vernuko derleniyor...
dotnet build src\Vernuko.Desktop\Vernuko.Desktop.csproj -c Release -v quiet -nologo
if errorlevel 1 goto fail

echo Vernuko aciliyor. Bu pencere program kapanana kadar acik kalir; kapatmayin.
dotnet "src\Vernuko.Desktop\bin\Release\net10.0-windows\Vernuko.dll" %ARGS%
goto end

:fail
echo.
echo === HATA: yukaridaki satirlari kopyalayip gonderin. ===
pause

:end
