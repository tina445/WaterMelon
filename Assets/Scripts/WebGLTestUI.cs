using TMPro;
using UnityEngine;

public class WebGLAuthTestUI : MonoBehaviour
{
    [SerializeField] private TMP_Text status;

    private void Start()
    {
        if (status) status.text = "Ready";
    }

    public async void OnClickGuest()
    {
        status.text = "Guest login...";
        await AppFacade.I.Auth.SignInGuestAsync();
        var p = AppFacade.I.UserData.CurrentProfile;
        status.text = $"OK Guest uid={p.uid} points={p.points}";
    }

    public async void OnClickGoogle()
    {
        status.text = "Google popup login...";
        try
        {
            await AppFacade.I.Auth.SignInWithGoogleAsync();
            var p = AppFacade.I.UserData.CurrentProfile;
            status.text = $"OK Google uid={p.uid} points={p.points}";
        }
        catch (System.Exception e)
        {
            status.text = $"FAIL: {e.Message}";
        }
    }

    public async void OnClickGivePoints()
    {
        await AppFacade.I.UserData.AddPointsAsync(10);
        var p = AppFacade.I.UserData.CurrentProfile;
        status.text = $"Points={p.points} (saved)";
    }
}
