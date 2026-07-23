@echo off
setlocal
set "PROJECT=CS2RegionPicker.csproj"
set "BASE=%~dp0release"
set "SINGLE=%BASE%\CS2RegionPicker"
echo.
echo === CS2RegionPicker build ===
echo.
echo Stopping any running CS2RegionPicker...
taskkill /IM CS2RegionPicker.exe /F >nul 2>&1
echo Cleaning bin, obj and release...
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"
if exist "%BASE%" rmdir /s /q "%BASE%"
echo.
echo === Building ===
dotnet publish "%PROJECT%" -c Release -o "%SINGLE%" -p:PublishSingleFile=true
if errorlevel 1 goto fail
echo.
echo === DONE ===
echo Output : %SINGLE%\CS2RegionPicker.exe
echo.
explorer "%BASE%"
pause
endlocal
exit /b 0
:fail
echo.
echo *** BUILD FAILED ***
echo.
pause
endlocal
exit /b 1
