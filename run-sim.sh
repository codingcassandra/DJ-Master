#!/bin/sh
# Launch the DJ Master macOS build inside the Meta XR Simulator.
# Rebuild first with: ./run-sim.sh build
cd "$(dirname "$0")"
UNITY=/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity
APP=Builds/SimMac/DJMaster.app
if [ "$1" = build ]; then
  rm -rf Builds/SimMac
  "$UNITY" -batchmode -projectPath "$PWD" -buildTarget OSXUniversal -executeMethod SimBuild.BuildMac -logFile Logs/simbuild.log || { echo "build failed, see Logs/simbuild.log"; exit 1; }
fi
# Unity's player asks for "openxr_loader"; the file ships as libopenxr_loader.dylib.
ln -sf libopenxr_loader.dylib "$APP/Contents/PlugIns/openxr_loader"
ln -sf libopenxr_loader.dylib "$APP/Contents/PlugIns/openxr_loader.dylib"
# Track the simulator window so the mouse finger lines up with the cursor.
[ -x Tools/simwin ] || swiftc -O Tools/simwin.swift -o Tools/simwin
pkill -f "Tools/simwin" 2>/dev/null
Tools/simwin "$PWD/Logs/simwindow.txt" &
WIN=$!
trap 'kill $WIN 2>/dev/null' EXIT INT TERM
J=/Applications/MetaXRSimulator.app/Contents/Resources/MetaXRSimulator/meta_openxr_simulator.json
XR_RUNTIME_JSON=$J XR_SELECTED_RUNTIME_JSON=$J "$APP/Contents/MacOS/DJ Master" -screen-fullscreen 0 -screen-width 640 -screen-height 360 -logFile "$PWD/Logs/player.log"
