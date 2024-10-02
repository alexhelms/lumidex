#!/bin/bash
set -e

APP_NAME="Lumidex"
VERSION=${1:-"0.0.0"}
INFO_PLIST="./macos/Info.plist"
ICON_FILE="./macos/Lumidex.icns"
PROJECT_NAME="Lumidex.Desktop"
OUTPUT_DIR="publish-osx-universal"

LIPO_URL="https://github.com/konoui/lipo/releases/latest/download/lipo_Linux_amd64"
RCODESIGN_URL="https://github.com/indygreg/apple-platform-rs/releases/download/apple-codesign%2F0.27.0/apple-codesign-0.27.0-x86_64-unknown-linux-musl.tar.gz"

build_for_arch() {
	local arch=$1
	echo "Building for $arch..."
	dotnet publish \
		--self-contained \
		-r osx-$arch\
		-c Release \
		-p:UseAppHost=true \
		-p:PublishSingleFile=true \
		-o "publish-osx-${arch}" \
		"${PROJECT_NAME}/${PROJECT_NAME}.csproj"

	# some cleanup
	cp -a "publish-osx-${arch}/runtimes/osx-${arch}/native/." "publish-osx-${arch}/"
	rm -rf "publish-osx-${arch}/runtimes/"
}

echo "Getting lipo..."
curl -L -o /tmp/lipo "${LIPO_URL}"
chmod +x /tmp/lipo
sudo mv /tmp/lipo /usr/local/bin

echo "Getting rcodesign..."
wget -O /tmp/codesign.tar.gz "${RCODESIGN_URL}"
tar zxvf /tmp/codesign.tar.gz -C /tmp
mv /tmp/apple-codesign-*/rcodesign .

rm -rf "publish-osx-*"
build_for_arch "x64"
build_for_arch "arm64"

# start with the x64 we will replace the binary with a universal one
mkdir -p "${OUTPUT_DIR}"
cp -rf "publish-osx-x64/." "${OUTPUT_DIR}"
rm "${OUTPUT_DIR}/Lumidex.Desktop"

echo "Creating universal binary..."
lipo -create \
	"publish-osx-x64/Lumidex.Desktop" \
	"publish-osx-arm64/Lumidex.Desktop" \
	-output "${OUTPUT_DIR}/Lumidex.Desktop"
# sqlite is not universal, so we have to make it universal
lipo -create \
	"publish-osx-x64/libe_sqlite3.dylib" \
	"publish-osx-arm64/libe_sqlite3.dylib" \
	-output "${OUTPUT_DIR}/libe_sqlite3.dylib"
echo "Universal binary created"

echo "Creating app bundle..."
APP_BUNDLE="${APP_NAME}.app"
APP_OUTPUT="dist/${APP_BUNDLE}"
APP_OUTPUT_ZIP="${APP_NAME}-${VERSION}-macos.app.zip"

rm -rf "${APP_OUTPUT}"

mkdir -p "${APP_OUTPUT}"
mkdir -p "${APP_OUTPUT}/Contents"
mkdir -p "${APP_OUTPUT}/Contents/MacOS"
mkdir -p "${APP_OUTPUT}/Contents/Resources"

cp "${INFO_PLIST}" "${APP_OUTPUT}/Contents/Info.plist"
cp "${ICON_FILE}" "${APP_OUTPUT}/Contents/Resources/Lumidex.icns"
cp -a "${OUTPUT_DIR}/." "${APP_OUTPUT}/Contents/MacOS/"

chmod -R 755 "${APP_OUTPUT}/Contents/MacOS"

echo "Adhoc signing app bundle..."
./rcodesign sign "${APP_OUTPUT}"

echo "Zipping app bundle..."
rm -rf "${APP_OUTPUT_ZIP}"
(cd dist && zip -r - "${APP_BUNDLE}") > "dist/${APP_OUTPUT_ZIP}"
zipinfo "dist/${APP_OUTPUT_ZIP}"

echo "Done!"