dotnet tool restore
dotnet publish Lumidex.Desktop\Lumidex.Desktop.csproj -r win-x64 --self-contained -c Release -o publish-win
$VERSION=(dotnet tool run dotnet-gitversion /showvariable SemVer)
& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' lumidex-installer-win.iss /DMyAppProductVersion=${VERSION}