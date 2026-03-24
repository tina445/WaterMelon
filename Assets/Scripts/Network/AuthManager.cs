using System;
using System.Threading.Tasks;
using UnityEngine;

public class AuthManager : MonoBehaviour
{
    public event Action<AuthResult> OnSignedIn;
    public event Action OnSignedOut;

    private IAuthService _auth;

    public void Initialize(IAuthService authService) => _auth = authService;

    public async Task<AuthResult> SignInGuestAsync()
    {
        var res = await _auth.SignInGuestAsync();
        OnSignedIn?.Invoke(res);
        return res;
    }

    public async Task<AuthResult> SignInWithGoogleAsync()
    {
        var res = await _auth.SignInWithGoogleAsync();
        OnSignedIn?.Invoke(res);
        return res;
    }

    public async Task SignOutAsync()
    {
        await _auth.SignOutAsync();
        OnSignedOut?.Invoke();
    }
}
