dotnet publish Lumidex.Desktop\Lumidex.Desktop.csproj -r win-x64 --self-contained -c Release -o publish-win
$VERSION = (nbgv get-version)[3].Split(":")[1].Trim()
& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' lumidex-installer-win.iss /DMyAppProductVersion=${VERSION}