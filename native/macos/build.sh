#!/bin/sh
set -eu
out="../../dist/Mochi.Native-macos"
rm -rf "$out"
mkdir -p "$out/Mochi.app/Contents/MacOS" "$out/Mochi.app/Contents/Resources/assets/cats"
mkdir -p "$out/Mochi.app/Contents/Resources/Mochi.iconset"
swiftc -parse-as-library Mochi.swift -framework AppKit -o "$out/Mochi.app/Contents/MacOS/Mochi"
cp ../../assets/cats/mochi-*.png ../../assets/cats/niangao-*.png "$out/Mochi.app/Contents/Resources/assets/cats/"
cp Info.plist "$out/Mochi.app/Contents/Info.plist"
sips -z 16 16 ../../assets/icons/mochi-icon.png --out "$out/Mochi.app/Contents/Resources/Mochi.iconset/icon_16x16.png" >/dev/null
sips -z 32 32 ../../assets/icons/mochi-icon.png --out "$out/Mochi.app/Contents/Resources/Mochi.iconset/icon_16x16@2x.png" >/dev/null
sips -z 32 32 ../../assets/icons/mochi-icon.png --out "$out/Mochi.app/Contents/Resources/Mochi.iconset/icon_32x32.png" >/dev/null
sips -z 64 64 ../../assets/icons/mochi-icon.png --out "$out/Mochi.app/Contents/Resources/Mochi.iconset/icon_32x32@2x.png" >/dev/null
sips -z 128 128 ../../assets/icons/mochi-icon.png --out "$out/Mochi.app/Contents/Resources/Mochi.iconset/icon_128x128.png" >/dev/null
sips -z 256 256 ../../assets/icons/mochi-icon.png --out "$out/Mochi.app/Contents/Resources/Mochi.iconset/icon_128x128@2x.png" >/dev/null
sips -z 256 256 ../../assets/icons/mochi-icon.png --out "$out/Mochi.app/Contents/Resources/Mochi.iconset/icon_256x256.png" >/dev/null
sips -z 512 512 ../../assets/icons/mochi-icon.png --out "$out/Mochi.app/Contents/Resources/Mochi.iconset/icon_256x256@2x.png" >/dev/null
sips -z 512 512 ../../assets/icons/mochi-icon.png --out "$out/Mochi.app/Contents/Resources/Mochi.iconset/icon_512x512.png" >/dev/null
sips -z 1024 1024 ../../assets/icons/mochi-icon.png --out "$out/Mochi.app/Contents/Resources/Mochi.iconset/icon_512x512@2x.png" >/dev/null
iconutil -c icns "$out/Mochi.app/Contents/Resources/Mochi.iconset" -o "$out/Mochi.app/Contents/Resources/Mochi.icns"
rm -rf "$out/Mochi.app/Contents/Resources/Mochi.iconset"
