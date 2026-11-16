using System;
using UnityEngine;

namespace DJMaster
{
    // Meta account sign-in (#46). On the Quest: start the Platform SDK, run the store's entitlement check (the app quits if
    // it fails, as the store requires), then get the signed-in user's name for the greeting and the first player.
    // In the editor, offline, or before the App ID is set up, it quietly falls back to the local player: no errors.
    //
    // The App ID lives in Oculus Platform settings (Meta > Platform > Edit Settings), from the Meta developer dashboard.
    public class MetaSignIn : MonoBehaviour
    {
        public enum State { Starting, SignedIn, Offline, NotEntitled }

        public State Status { get; private set; } = State.Starting;
        public string UserName { get; private set; } = "";
        public string Reason { get; private set; } = "";

        // Fires once with the signed-in user's name.
        public event Action<string> NameReady;

        void Start()
        {
            if (Application.isEditor) { Fallback("in the editor"); return; }
            if (string.IsNullOrEmpty(Oculus.Platform.PlatformSettings.MobileAppID)) { Fallback("no App ID set yet"); return; }
            try
            {
                Oculus.Platform.Core.AsyncInitialize().OnComplete(init =>
                {
                    if (init.IsError) { Fallback("platform didn't start: " + init.GetError()?.Message); return; }
                    Oculus.Platform.Entitlements.IsUserEntitledToApplication().OnComplete(entitled =>
                    {
                        if (entitled.IsError)
                        {
                            // The store requires this: no entitlement, no game
                            Status = State.NotEntitled;
                            Reason = entitled.GetError()?.Message ?? "";
                            Debug.LogError("[DJMaster] Entitlement check failed, quitting. " + Reason);
                            Application.Quit();
                            return;
                        }
                        Oculus.Platform.Users.GetLoggedInUser().OnComplete(user =>
                        {
                            if (user.IsError || user.Data == null) { Fallback("couldn't read the user"); return; }
                            string name = !string.IsNullOrWhiteSpace(user.Data.DisplayName) ? user.Data.DisplayName : user.Data.OculusID;
                            Apply(name);
                        });
                    });
                });
            }
            catch (Exception e) { Fallback(e.Message); } // offline or no platform service: play on as the local player
        }

        void Fallback(string why)
        {
            Status = State.Offline;
            Reason = why;
            Debug.Log($"[DJMaster] Playing without Meta sign-in ({why}).");
        }

        // The signed-in name: the first player takes it, and the greeting says hi with it. Public for the tests.
        public void Apply(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) { Fallback("no name on the account"); return; }
            Status = State.SignedIn;
            UserName = name.Trim();
            Players.SignedIn(UserName);
            NameReady?.Invoke(UserName);
        }
    }
}
