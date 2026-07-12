@echo off
setlocal
set "PROJECT=CS2RegionPicker.csproj"
set "BASE=%~dp0release"
set "SINGLE=%BASE%\CS2RegionPicker_Portable"
set "FOLDER=%BASE%\CS2RegionPicker"
echo.
echo === CS2RegionPicker build (both variants) ===
echo.
echo Stopping any running CS2RegionPicker...
taskkill /IM CS2RegionPicker.exe /F >nul 2>&1
echo Cleaning bin, obj and release...
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"
if exist "%BASE%" rmdir /s /q "%BASE%"
echo.
echo === [1/2] Portable build (single file) ===
dotnet publish "%PROJECT%" -c Release -o "%SINGLE%" -p:PublishSingleFile=true
if errorlevel 1 goto fail
echo.
echo === [2/2] Folder build (exe + dlls) ===
dotnet publish "%PROJECT%" -c Release -o "%FOLDER%" -p:PublishSingleFile=false
if errorlevel 1 goto fail
echo.
echo === DONE ===
echo Portable  : %SINGLE%\CS2RegionPicker.exe
echo With dlls : %FOLDER%\CS2RegionPicker.exe (+ dlls)
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
