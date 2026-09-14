#!/bin/sh
set -eu
out="../../dist/Mochi.Native-macos"
rm -rf "$out"
mkdir -p "$out/Mochi.app/Contents/MacOS" "$out/Mochi.app/Contents/Resources/assets/cats"
swiftc Mochi.swift -framework AppKit -o "$out/Mochi.app/Contents/MacOS/Mochi"
cp ../../assets/cats/mochi-*.png "$out/Mochi.app/Contents/Resources/assets/cats/"
cp Info.plist "$out/Mochi.app/Contents/Info.plist"
