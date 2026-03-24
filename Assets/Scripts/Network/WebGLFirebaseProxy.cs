using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class WebGLFirebaseProxy : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void FB_Init();
    [DllImport("__Internal")] private static extern void FB_CheckAuth(string goName);
    [DllImport("__Internal")] private static extern void FB_AnonSignIn(string goName);
    [DllImport("__Internal")] private static extern void FB_GoogleSignInPopup(string goName);
    [DllImport("__Internal")] private static extern void FB_SignOut(string goName);
    [DllImport("__Internal")] private static extern void FB_GetUserProfile(string goName, string uid);
    [DllImport("__Internal")] private static extern void FB_SaveUserProfile(string goName, string uid, string json);
#endif

    public event Action<string> AuthSuccess;
    public event Action<string> AuthError;

    public event Action<string> AuthStateUid;

    public event Action ProfileMissing;
    public event Action<string> ProfileJson;

    public event Action SaveOk;
    public event Action<string> FirestoreError;
    public event Action SignOutOk;

    private string GoName => gameObject.name;

    public void Init()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        FB_Init();
#endif
    }

    public void CheckAuthState()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        FB_CheckAuth(GoName);
#else
        AuthStateUid?.Invoke("");
#endif
    }

    public void SignInAnon()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        FB_AnonSignIn(GoName);
#else
        AuthError?.Invoke("Not running in WebGL build.");
#endif
    }

    public void SignInGooglePopup()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        FB_GoogleSignInPopup(GoName);
#else
        AuthError?.Invoke("Not running in WebGL build.");
#endif
    }

    public void SignOut()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        FB_SignOut(GoName);
#else
        SignOutOk?.Invoke();
#endif
    }

    public void GetProfile(string uid)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        FB_GetUserProfile(GoName, uid);
#else
        FirestoreError?.Invoke("Not running in WebGL build.");
#endif
    }

    public void SaveProfile(string uid, string json)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        FB_SaveUserProfile(GoName, uid, json);
#else
        FirestoreError?.Invoke("Not running in WebGL build.");
#endif
    }

    // JS -> C# callbacks
    public void OnAuthSuccess(string uid) => AuthSuccess?.Invoke(uid);
    public void OnAuthError(string msg) => AuthError?.Invoke(msg);

    // FirebaseBridge.jslib: SendMessage(goName, "OnAuthState", uid)
    public void OnAuthState(string uidOrEmpty)
    {
        AuthStateUid?.Invoke(uidOrEmpty ?? "");
    }

    public void OnProfileJson(string json)
    {
        if (string.IsNullOrEmpty(json)) ProfileMissing?.Invoke();
        else ProfileJson?.Invoke(json);
    }

    public void OnSaveOk(string _) => SaveOk?.Invoke();
    public void OnFirestoreError(string msg) => FirestoreError?.Invoke(msg);
    public void OnSignOutOk(string _) => SignOutOk?.Invoke();
}
