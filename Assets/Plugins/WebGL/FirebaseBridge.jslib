mergeInto(LibraryManager.library, {
  FB_Init: function () {
    try {
      if (typeof window === "undefined") return;

      // GO 이름 저장소 준비 (브라우저에서만)
      window.__UNITY_FIREBASE_GO = "AppFacade";

      // index.html에서 firebase 초기화했다고 가정
      if (typeof firebase === "undefined" || !firebase.auth) {
        console.error("Firebase SDK not loaded.");
        return;
      }

      // window._auth / window._db 가 없다면 여기서 생성해도 됨
      if (!window._auth) window._auth = firebase.auth();
      if (!window._db && firebase.firestore) window._db = firebase.firestore();

      var auth = window._auth;

      // 로그인 상태 유지
      auth.setPersistence(firebase.auth.Auth.Persistence.LOCAL)
        .then(function () {
          console.log("[Firebase] Auth persistence = LOCAL");
        })
        .catch(function (e) {
          console.warn("[Firebase] setPersistence failed:", e);
        });

      // 로그인 상태 변경 통지
      auth.onAuthStateChanged(function (user) {
        if (!window.__UNITY_FIREBASE_GO) return;

        var uid = (user && user.uid) ? user.uid : "";
        SendMessage(window.__UNITY_FIREBASE_GO, "OnAuthState", uid);
      });

    } catch (e) {
      console.warn("[Firebase] FB_Init error:", e);
    }
  },

  FB_CheckAuth: function (goNamePtr) {
    try {
      if (typeof window === "undefined") return;

      var goName = UTF8ToString(goNamePtr);
      window.__UNITY_FIREBASE_GO = goName;

      if (typeof firebase === "undefined" || !firebase.auth) {
        SendMessage(goName, "OnAuthState", "");
        return;
      }

      var auth = window._auth || firebase.auth();
      window._auth = auth;

      var user = auth.currentUser;
      var uid = (user && user.uid) ? user.uid : "";

      SendMessage(goName, "OnAuthState", uid);
    } catch (e) {
      console.error("[FB_CheckAuth] error:", e);
      SendMessage(UTF8ToString(goNamePtr), "OnAuthState", "");
    }
  },

  FB_AnonSignIn: function (goNamePtr) {
    if (typeof window === "undefined") return;

    var goName = UTF8ToString(goNamePtr);
    var auth = window._auth || (typeof firebase !== "undefined" && firebase.auth ? firebase.auth() : null);
    if (!auth) {
      SendMessage(goName, "OnAuthError", "Firebase auth not ready");
      return;
    }
    window._auth = auth;

    auth.signInAnonymously()
      .then(function (res) {
        var uid = res && res.user && res.user.uid ? res.user.uid : "";
        SendMessage(goName, "OnAuthSuccess", uid);
      })
      .catch(function (e) {
        SendMessage(goName, "OnAuthError", (e && e.message) ? e.message : "AnonSignIn error");
      });
  },

  FB_GoogleSignInPopup: function (goNamePtr) {
    if (typeof window === "undefined") return;

    var goName = UTF8ToString(goNamePtr);
    if (typeof firebase === "undefined" || !firebase.auth) {
      SendMessage(goName, "OnAuthError", "Firebase auth not ready");
      return;
    }

    var auth = window._auth || firebase.auth();
    window._auth = auth;

    var provider = new firebase.auth.GoogleAuthProvider();
    auth.signInWithPopup(provider)
      .then(function (res) {
        var uid = res && res.user && res.user.uid ? res.user.uid : "";
        SendMessage(goName, "OnAuthSuccess", uid);
      })
      .catch(function (e) {
        SendMessage(goName, "OnAuthError", (e && e.message) ? e.message : "GoogleSignIn error");
      });
  },

  FB_SignOut: function (goNamePtr) {
    if (typeof window === "undefined") return;

    var goName = UTF8ToString(goNamePtr);
    var auth = window._auth;
    if (!auth) {
      SendMessage(goName, "OnSignOutOk", "");
      return;
    }

    auth.signOut()
      .then(function () {
        SendMessage(goName, "OnSignOutOk", "");
      })
      .catch(function (e) {
        SendMessage(goName, "OnAuthError", (e && e.message) ? e.message : "SignOut error");
      });
  },

  FB_GetUserProfile: function (goNamePtr, uidPtr) {
    if (typeof window === "undefined") return;

    var goName = UTF8ToString(goNamePtr);
    var uid = UTF8ToString(uidPtr);

    var db = window._db;
    if (!db) {
      SendMessage(goName, "OnFirestoreError", "Firestore not ready");
      return;
    }

    db.collection("users").doc(uid).get()
      .then(function (doc) {
        if (!doc || !doc.exists) {
          SendMessage(goName, "OnProfileJson", "");
          return;
        }
        SendMessage(goName, "OnProfileJson", JSON.stringify(doc.data()));
      })
      .catch(function (e) {
        SendMessage(goName, "OnFirestoreError", (e && e.message) ? e.message : "GetUserProfile error");
      });
  },

  FB_SaveUserProfile: function (goNamePtr, uidPtr, jsonPtr) {
    if (typeof window === "undefined") return;

    var goName = UTF8ToString(goNamePtr);
    var uid = UTF8ToString(uidPtr);
    var json = UTF8ToString(jsonPtr);

    var db = window._db;
    if (!db) {
      SendMessage(goName, "OnFirestoreError", "Firestore not ready");
      return;
    }

    var data = {};
    try { data = JSON.parse(json); } catch (e) { data = {}; }

    db.collection("users").doc(uid).set(data, { merge: true })
      .then(function () {
        SendMessage(goName, "OnSaveOk", "");
      })
      .catch(function (e) {
        SendMessage(goName, "OnFirestoreError", (e && e.message) ? e.message : "SaveUserProfile error");
      });
  }
});
