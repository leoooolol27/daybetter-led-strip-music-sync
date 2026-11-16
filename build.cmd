@echo off
setlocal
set "framework=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "metadata=%WINDIR%\System32\WinMetadata"
pushd "%~dp0"
"%framework%\csc.exe" /nologo /target:winexe /optimize+ /win32icon:Daybetter.ico /out:DaybetterLEDStrip.exe ^
 /r:"%framework%\System.Runtime.WindowsRuntime.dll" ^
 /r:"%framework%\System.Runtime.dll" ^
 /r:"%framework%\System.Runtime.Serialization.dll" ^
 /r:"%framework%\System.Runtime.InteropServices.WindowsRuntime.dll" ^
 /r:"%framework%\System.Windows.Forms.dll" ^
 /r:"%framework%\System.Drawing.dll" ^
 /r:"%metadata%\Windows.Devices.winmd" ^
 /r:"%metadata%\Windows.Foundation.winmd" ^
 /r:"%metadata%\Windows.Media.winmd" ^
 /r:"%metadata%\Windows.Storage.winmd" ListeningMode.cs ControllerUi.cs
set "build_exit=%errorlevel%"
popd
exit /b %build_exit%


