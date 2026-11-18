# Changelog

All notable changes to this package will be documented in this file.

## [2.1.1] - 2026-10-01

### Changes & Improvements:

- Removed the restriction that prevented setting the Local Maven Repo Directory to a path under `Assets/Plugins/Android`. The restriction was based on an outdated concern that no longer applies.
- Replaced legacy IMGUI-based resolver dialogs with UIElements windows. The following windows have been reworked: Android resolution result (success, failure, and non-Android platform warning), Android auto-resolution prompt, Android Libraries display, and CocoaPods integration prompt for iOS. The Android Libraries window now auto-refreshes after resolution and when dependency XML files change, and displays a warning when Android is not the active platform.

### Fixes:

- Fixed Android resolution failing with a `DirectoryNotFoundException` when a package is installed from a folder on disk (`file:` dependency). Package paths are now resolved using Unity's Package Manager API.

## [2.1.0] - 2026-09-14

### Changes & Improvements:
- Added a possibility to set SPM dependency target
- Improved UI
- Settings moved to Project Settings
- Removed AAR Resolution mode and related settings
- Improved Cocoapods and Ruby detection & installation

### Fixes:
- Fixed an error on Android where Resolution would not run due to Assets/Plugins/Android folder missing. That folder is now created automatically.

## [2.0.0] - 2026-02-20

### Changes & Improvements:
- First version

### Fixes:
- Nothing
