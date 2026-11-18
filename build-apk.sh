#!/bin/sh
# Builds the signed Competition APK from this worktree. Asks for the keystore passwords (not shown, not saved).
cd "$(dirname "$0")"
KEYSTORE="$HOME/Keys/djmaster-release.keystore"
ALIAS=djmaster
printf "Keystore password: "; stty -echo; read KSP; stty echo; echo
printf "Key password (press Enter if it's the same): "; stty -echo; read KAP; stty echo; echo
[ -z "$KAP" ] && KAP="$KSP"
echo "Building... this takes a few minutes (switching to Android the first time takes longer)."
DJ_KEYSTORE="$KEYSTORE" DJ_KEYALIAS="$ALIAS" DJ_KEYSTORE_PASS="$KSP" DJ_KEYALIAS_PASS="$KAP" \
XR_RUNTIME_JSON=/nonexistent/none.json XR_SELECTED_RUNTIME_JSON=/nonexistent/none.json \
/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -buildTarget Android \
  -executeMethod DJMaster.EditorTools.BatchApk.Build -logFile Logs/apk.log
KSP=; KAP=
grep "\[APK\]" Logs/apk.log | grep -v "^UnityEngine"
ls -la Builds/*.apk 2>/dev/null && echo "Done." || echo "No APK. See Logs/apk.log"
