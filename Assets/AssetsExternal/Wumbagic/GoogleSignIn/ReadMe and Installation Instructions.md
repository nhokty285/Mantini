# Unity Package Name
Google SignIn iOS & Android

# Our own apps using it in production
https://play.google.com/store/apps/details?id=in.locomotion.games.dehlapakad
https://apps.apple.com/in/app/dehla-pakad-call-break/id468502067

# Prerequisite
- If you are upgrading, delete old files because of restructuring
- Google Developer account with project setup, use relevant keystore and package name
- Setup Unity -> Android Settings to use API Level 31 or higher if highest Installed (Mostly API Level 30) isn't working.
- Mac Setup -> Ensure that xcode is installed and u have opened it atleast once so it can install command line tools too. A quick check for everything functioning is by running "xcode-select --install" on terminal. Otherwise u will get non readable errors which u wont understand and it will never pin point to command line tools"

# What is this SDK Design
This SDK is a wrapper around official google iOS and android SDK so you are always using official sdk & your game is future proof. Files are kept as open code. 

If you wish to edit the implementation to support your own use case, you can 
iOS >  
- LCGoogleSignIn.h & LCGoogleSignIn.mm for iOS (Written in objective-c)
Android >
- GoogleLoginKit.kt for android (Written in kotlin) (For this you can request source code license for nomical amount of $100)

Official google dependencies are specified in folder 'GoogleSignIn/Editor' whose versions could be upgraded or downgraded as per your need. (Filename : LCGoogleLoginDependencies.cs)
- For iOS its pods file
- For Android its play services auth & related packages

# Must Read
You should have done a walk through of official google docs for your own knowledge.

Android:
https://developers.google.com/identity/sign-in/android/start-integrating

iOS:
https://developers.google.com/identity/sign-in/ios/start-integrating


# Video Walkthrough
A short video walkthrough is available (Older version 1.1) and link could be found in Asset Store page of this package to follow along. Video is old but not much hs changed. You check ChangeLogs.md file for what has changed since the old version.

Version 1.1: https://www.youtube.com/watch?v=mmLheAYQoO8&t=4s
This video by us is old and long but its essential knowledge to understand a lot about google login for unity

# Dependencies
1) External Dependency Manager / Unity Jar Resolver package: 
- Verify that you don't already have it via another third party project by looking into folder "ExternalDependencyManager". 
- If you are using firebase libs, its shipped with play services support. If its not the case, you can download latest unity packagae from
https://github.com/googlesamples/unity-jar-resolver

2) Google API Console project: 
- For Android - You need ''client_secret<<>>.apps.googleusercontent.com.json'' file
- For iOS - YOu need ''client_<<>>.apps.googleusercontent.com'' file. 
- Put it in your project Assets folder. You can have only 1 file for iOS & 1 for android at the moment and should place it in Assets folder (no subfolders). You can read about it and get it while setting up your google console project for signin 

Android:
https://developers.google.com/identity/sign-in/android/start-integrating

iOS:
https://developers.google.com/identity/sign-in/ios/start-integrating

For android you also need to setup make sure that web client id is also created (Usually 1 is auto created for you)
https://console.developers.google.com/apis/credentials

3) Initialize
You must configure logging level (set false for production) & initialize library using web client id. It has no effect on iOS and it will be used in android only. This is to make sure that R.string.default_web_client_id is configured

4) CocoaPods (iOS)
We add 'GoogleSignIn' as pod depencies to pull official google signin library for iOS.

5) Make sure that your application is signed & have correct packagaeID as also available in Google API Manager console otherwise you will get 'DEVELOPER_ERROR' in console logs after login callback. 

6) Right click anywhere on your project editor and client Play Services Resolver -> Android Resolver -> Resolve client jar
This will bring all necessary dependencies for android into your project from your SDK installation folder. Advance usage for same is given under #Advance

7) You must ensure that you are using production keystore for android or you will comes across DEVELOPER_ERROR & login failed issue.

# Configure (iOS & Android)
Open LCGoogleSignInPostProcess.cs file & change following 2 string files to your personal plist file (iOS) & json (Android) file names as added in Unity.

const string iOSFilePath = "client_<Your Apps Personal Data>.apps.googleusercontent.com.plist";
const string androidFilePath = "client_secret_<Your Apps Personal Data>.apps.googleusercontent.com.json";

# Precaution (Android)
- Always ensure that you have no duplicate aar or jar files throughout your project otherwise it will result in gradle error & its very hard to debug & locate.

# Usage
You are all set. Ensure that you have correct InitWithClientID setup in LCGoogleSignInExample.cs & checkout LCGoogleSignInExample scene and its script. Run it on device not Editor since it will be ignored.

> Always run on iOS because setup is relatively lot simpler and thr is a high chance that you got it right
> In android, sometimes first run for new project doesn't work and throws an error. We have seen it very frequently but its not documented anywhere so if your first attempt doesnt logs you in, wait for 5 mins, do a fresh install and try again. If that also doesnt work send us to our support email or check error code. If its DEVELOPER_ERROR in console, we can't help u much and u need to figure out project setup on your own.

# Silent login
Its usually used to reestablish session after user comes back to your app but we are not expert on use cases and you must refer to documents given by google for use cases.

# Server auth (V1.2 onwards)
Most of the game don't need it but if you happen to be using google APIs from your server or offline access, pass 'true' in UserLogin method for relevant variable. If user is already logged in without this permission call logout first.

# Scope (V1.2 onwards)
Mostly games don't need this feature but if you happen to be managing any other google service and need special permission as given in following doc, pass it during "UserLogin" call. For changing requests during a session, call logout and thn call login with new permissions
https://developers.google.com/identity/protocols/googlescopes


# Setup Validation
## iOS
- iOS specific Google-Services files is available in your project
- Info.plist contains 2 entries from this lib in URLs (lcgoogle & google). 
    - lcgoogle > contains your bundle identifier
    - google > contains iOS client ID
- Must have cocoapod installed for link official GoogleSignIn sdk too
- Must check if iOS & android file names are given manually at LCGoogleSignInPostProcess.cs

## Android
- Once you run plays services resolver, it brought atleast following aar files (version might be this or higher)
1) androidx.appcompat.appcompat-1.0.2.aar
2) com.google.android.gms.play-services-auth-17.0.0.aar




# Common Errors & how to solve it
## iOS
- Usually you should never see an error in iOS. If yours is an exception, let us know. Our setup was tested with xcode workspace but anything else should just work fine. 
- If you are using google play games unity library, make sure that you read iOS section properly because it have many incompatibility for new users including the version of google signin it enforces.


## Android
Errors are seen in android logs when u use './adb logcat' or attach unity debugger

### Duplicate kotlin symbol issue (#1 issue faced by almost in past. Not an issue from V3.0 onwards)
Following aar files are included in unity by default & external dependency manager also brings them into the project because we have written our code directly in kotlin which causes duplicate symbol error. Hence, ensure that 'External Dependency Manager' is not set to 'Auto Resolve' on any level. Secondly, once you have resolved dependency, delete following from project 
- kotlin-stdlib
- org.jetbrains.kotlin.kotlin-stdlib-jdk7
- org.jetbrains.kotlin.kotlin-stdlib-jdk8
- org.jetbrains.annotations
- kotlin-stdlib-common 

### Google SignIn API Exception 10
Happens due to debug / non related keystore, SHA-1, SHA-256, package name or any other google cloud setup
https://stackoverflow.com/questions/49450140/google-signin-api-exception-10

### DEVELOPER_ERROR in logs
https://stackoverflow.com/questions/37273145/error-statusstatuscode-developer-error-resolution-null

It happens for many reason like package name, incorrect project configuration, project signing, first time project etc etc. Its adviced to google and understand the specific case for your configuration. 

Try running first on iOS so that your project is set.

Try running google samples android studio project given by google, if you see same issue, you will be able to resolve it faster

Check Advance notes on replacing string "LATEST" with play services & support library version you want to use.

Verify your web client ID

Make sure you have latest Google services file for both iOS & Android

### java.lang.IllegalArgumentException
Uncaught translation error: java.lang.IllegalArgumentException: already added: Lcom/google/android/gms/iid/zzc;
Uncaught translation error: java.lang.IllegalArgumentException: already added: Lcom/google/android/gms/iid/zzd

Its usually caused by multiple aar files with different versions. Verify that your project have only 1 aar file for each android library. It might exist in different folders too.

### com.google.android.gms.auth.api.signin.internal.SignInConfiguration classNotFound
https://stackoverflow.com/questions/33583326/new-google-sign-in-android?noredirect=1&lq=1

### Dex Error 65K limit
gradle (new) seems to have less of this error and you need to optimize your project by either removing few libraries or search on how you can resolve it for your case. Many solutions in stackoverflow totally works.

### AAPT: error: resource android:attr/lStar not found.
/Users/<username>/.gradle/caches/transforms-2/files-2.1/b0b96514cd4600f35ab95e3c98f9872c/androidx.core.core-1.7.0/res/values/values.xml:105:5-114:25: AAPT: error: resource android:attr/lStar not found.

It happens due to API Level & Gradle version. Change Target API level to 31 or higher.

### Extreme resolution
Create fresh unity project and test this module

### Have suggestions for us ?
Let us know and we will update this document to help other developers




# Additional Questions / Trouble Shoot / Reach Out
You can reach us at support@wumbagic.com 

## Android logs
Use logcat with filters to attach only relevant log
./adb logcat -s Unity ActivityManager PackageManager dalvikvm DEBUG

# iOS Logs
XCode logs will work


