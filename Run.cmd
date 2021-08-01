@echo off

>nul where msbuild || (
    echo [91mMSBuild not in PATH[0m
    exit /B 1
)

setlocal

set configuration=%1
if [%configuration%]==[] set "configuration=Release"

msbuild "%~dp0src\GoToSourceBrowser\GoToSourceBrowser.csproj" /nologo /v:m /m /nr:false /restore /t:Build /p:Configuration=%configuration%;GeneratePkgDefFile=true;VSSDKTargetPlatformRegRootSuffix=Exp || exit /B 1

set "devenv_exe=C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\devenv.exe"

start "" "%devenv_exe%" /rootsuffix Exp

endlocal