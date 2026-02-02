# Unity Package Name
Google SignIn iOS & Android

# Version 3.0 Upgrade (Aug-2023)
- Android kotlin files are now precompiled to avoid build issues over years
- Basic maintenance to accomodate new official codes

# Version 2.4 Upgrade (Aug-2022)
- Restructured so delete old files
- Resolved incompatibility issues due to recent gogole admob.
- Upgrading to use latest play services.

# Version 2.3 Upgrade (Feb-2022)
Additional name features and better error handling for edge cases

# Version 2.2 Upgrade (Sep-2021)
General Maintenance 

# Version 2.1 Upgrade (02 Oct 2019)
- Resolved issues around IL2CPP build in android for play store submission of 64 bit asset

# Version 2.0 Upgrade (25 Aug 2019)
- Resolve iOS & android build issues
- Upgraded to androidx libraries.
- You need to explicitly specify iOS & web client ID in init
- You need to explicitly provide android json & ios plist file names in LCGoogleSignInPostProcess.cs
- Android dependencies are now handled by LCGoogleLoginDependencies.xml file
- androidx upgrade
- iOS migrated to new official google pod.  version 5.0 (14th Aug 2019)

# Version 1.3 Upgrade (2017)
- Newly built aar for better support
- iOS Build issue fixes for Unity 2017
- play-services-auth hardcoded to "11.2.0" (Change it if Firebase changes it in future)
- design hardcoded to "25.3.1" (Change it if facebook changes it in future)
- New video explaining conflict management with android libraries & unity project

# Version 1.2 Upgrade
- Added server auth and capability to acquire various scopes
- Added design dependency for better compatibility
- Fixed android 4.x compatibility issue

# Version 1.1 Upgrade
- Fixed logout issue in android
- Its cumpulsory to pass web client ID in init method
- Updated console logs
- Added how to video in asset store page

