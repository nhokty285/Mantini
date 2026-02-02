using System;
using UnityEngine;
using System.Runtime.InteropServices;

namespace LCGoogleLogin
{
    public static class LCGoogleLoginAndroid
    {
#pragma warning disable 0414
        static AndroidJavaObject curGoogleLoginObj = null;
        private const string packageClass = "in.locomotion.plugins.login.GoogleLoginKit";
        public static void InitiateWithClientID(string clientID)
        {
            using (var javaClass = new AndroidJavaClass(packageClass))
            {
                javaClass.CallStatic("InitiateWithClientID", clientID);
            }

            // AndroidJavaObject androidPlugin = AndroidPluginObj(true);
            // if (androidPlugin != null)
            // {
            //     androidPlugin.CallStatic("InitiateWithClientID", clientID);
            // }
        }

        public const string kUserLogin = "UserLogin";
        public const string kUserLogout = "UserLogout";

        public static bool CallAndroidLoginMethod(string method, bool isSilent, bool enableServerAuth,
            bool forceCodeForRefreshToken, string[] requestedScopes)
        {
            using (var javaClass = new AndroidJavaClass(packageClass))
            {
                javaClass.CallStatic<bool>(method, isSilent, enableServerAuth, forceCodeForRefreshToken, requestedScopes);
            }

            // AndroidJavaObject androidPlugin = AndroidPluginObj(true);
            // if (androidPlugin != null)
            // {
            //     return androidPlugin.CallStatic<bool>(method, isSilent, enableServerAuth, forceCodeForRefreshToken, requestedScopes);
            // }

            return false;
        }

        public static bool CallAndroidBoolMethod(string method)
        {
            using (var javaClass = new AndroidJavaClass(packageClass))
            {
                javaClass.CallStatic<bool>(method);
            }

            // AndroidJavaObject androidPlugin = AndroidPluginObj(true);
            // if (androidPlugin != null)
            // {
            //     return androidPlugin.CallStatic<bool>(method);
            // }

            return false;
        }

        public const string kStrUserDisplayName = "UserDisplayName";
        public const string kStrUserGivenName = "UserGivenName";
        public const string kStrUserFamilyName = "UserFamilyName";
        public const string kStrUserActualID = "UserActualID";
        public const string kStrUserEmail = "UserEmail";
        public const string kStrUserPhotoUrl = "UserPhotoUrl";
        public const string kStrUserIDToken = "UserIDToken";
        public const string kStrUserAccessToken = "UserAccessToken";
        public const string kStrRefreshToken = "RefreshToken";
        public const string kStrServerAuthCode = "ServerAuthCode";

        public static string CallAndroidStringMethod(string method)
        {
            using (var javaClass = new AndroidJavaClass(packageClass))
            {
                return javaClass.CallStatic<string>(method);
            }

            // AndroidJavaObject androidPlugin = AndroidPluginObj(true);
            // if (androidPlugin != null)
            // {
            //     return androidPlugin.CallStatic<string>(method);
            // }

            // return null;
        }

        public const string kStrArrScopes = "AvalableScopes";
        public static string[] CallAndroidStringArrayMethod(string method)
        {
            using (var javaClass = new AndroidJavaClass(packageClass))
            {
                return javaClass.CallStatic<string[]>(method);
            }

            // AndroidJavaObject androidPlugin = AndroidPluginObj(true);
            // if (androidPlugin != null)
            // {
            //     return androidPlugin.CallStatic<string[]>(method);
            // }

            // return null;
        }

        public const string kInBoolChangeLogLevel = "ChangeLogLevel";
        public const string kInBoolChangeDevLogLevel = "ChangeDevLogLevel";

        public static bool CallAndroidInBoolMethod(string method, bool newVal)
        {
            using (var javaClass = new AndroidJavaClass(packageClass))
            {
                javaClass.CallStatic(method, newVal);
                return true;
            }

            // AndroidJavaObject androidPlugin = AndroidPluginObj(true);
            // if (androidPlugin != null)
            // {
            //     androidPlugin.CallStatic(method, newVal);
            //     return true;
            // }
            // return false;
        }


        //Android Utils
        // static AndroidJavaObject AndroidPluginObj(bool forceNew)
        // {
        //     if (curGoogleLoginObj != null && forceNew == false)
        //     {
        //         return curGoogleLoginObj;
        //     }

        //     if (Application.platform == RuntimePlatform.Android)
        //     {
        //         string unityPackage = "com.unity3d.player.UnityPlayer";
        //         using (var javaUnityPlayer = new AndroidJavaClass(unityPackage))
        //         {
        //             using (var currentActivity = javaUnityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        //             {
        //                 string googleLoginPackage = packageClass;
        //                 curGoogleLoginObj = new AndroidJavaObject(googleLoginPackage, currentActivity);
        //                 return curGoogleLoginObj;
        //             }
        //         }
        //     }
        //     return null;
        // }

        static AndroidJavaObject JavaArrayFromCS(string[] values)
        {
            AndroidJavaClass arrayClass = new AndroidJavaClass("java.lang.reflect.Array");
            AndroidJavaObject arrayObject = arrayClass.CallStatic<AndroidJavaObject>("newInstance",
                new AndroidJavaClass("java.lang.String"),
                values.Length);
            for (int i = 0; i < values.Length; ++i)
            {
                arrayClass.CallStatic("set", arrayObject, i, new AndroidJavaObject("java.lang.String", values[i]));
            }
            return arrayObject;
        }
    }

}

