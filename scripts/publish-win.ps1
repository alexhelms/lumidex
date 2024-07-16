dotnet publish Lumidex.Desktop\Lumidex.Desktop.csproj -r win-x64 --self-contained -c Release -o publish
& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' lumidex-installer-win.iss