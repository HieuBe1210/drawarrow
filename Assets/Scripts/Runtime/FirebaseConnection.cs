using Firebase;
using Firebase.Extensions;
using UnityEngine;

namespace DrawArrow
{
    static class FirebaseConnection
    {
        public static bool Available { get; private set; }
        public static bool Finished { get; private set; }

#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
        static FirebaseApp app;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            Available = Finished = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                Finished = true;
                if (task.IsCanceled)
                {
                    Debug.LogWarning("Firebase initialization was canceled.");
                    return;
                }
                if (task.IsFaulted)
                {
                    Debug.LogException(task.Exception);
                    return;
                }
                if (task.Result != DependencyStatus.Available)
                {
                    Debug.LogWarning($"Firebase dependencies unavailable: {task.Result}");
                    return;
                }
                try
                {
                    app = FirebaseApp.DefaultInstance;
                    Available = app != null;
                    if (Available) Debug.Log("Firebase initialized.");
                }
                catch (System.Exception error)
                {
                    Debug.LogException(error);
                }
            });
        }
#endif
    }
}
