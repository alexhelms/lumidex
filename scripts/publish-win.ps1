# This is intended for quickly creating a dev installer, not used in CI for production.

dotnet tool install -g vpk
dotnet tool restore
dotnet publish Lumidex\Lumidex.csproj -r win-x64 --self-contained -c Release -o publish

# Remove large pdb files for external dependencies
Remove-Item "publish/libSkiaSharp.pdb" -ErrorAction SilentlyContinue
Remove-Item "publish/libHarfBuzzSharp.pdb" -ErrorAction SilentlyContinue

$VERSION=(dotnet tool run dotnet-gitversion /showvariable SemVer)
vpk pack `
    --yes `
    --packId LumidexApp `
    --mainExe Lumidex.exe `
    --packVersion $VERSION `
    --packDir publish `
    --packTitle Lumidex `
    --icon Lumidex/Assets/lumidex-icon.ico `
    --splashImage Lumidex/Assets/lumidex-icon.png `
    --runtime win-x64 `
    --channel win-x64-beta `
    --outputDir dist
