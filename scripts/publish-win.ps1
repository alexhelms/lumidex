dotnet tool restore
dotnet publish Lumidex\Lumidex.csproj -r win-x64 --self-contained -c Release -o publish-win

# Remove large pdb files for external dependencies
Remove-Item "publish-win/libSkiaSharp.pdb" -ErrorAction SilentlyContinue
Remove-Item "publish-win/libHarfBuzzSharp.pdb" -ErrorAction SilentlyContinue

$VERSION=(dotnet tool run dotnet-gitversion /showvariable SemVer)
& 'C:/Program Files (x86)/Inno Setup 6/ISCC.exe' lumidex-installer-win.iss /DMyAppProductVersion=${VERSION}