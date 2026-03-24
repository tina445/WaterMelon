using System.Threading.Tasks;

public class WebGLAuthService : IAuthService
{
    private readonly WebGLFirebaseProxy _proxy;

    public WebGLAuthService(WebGLFirebaseProxy proxy) => _proxy = proxy;

    public Task<AuthResult> SignInGuestAsync()
    {
        var tcs = new TaskCompletionSource<AuthResult>();

        void Ok(string uid) { Cleanup(); tcs.TrySetResult(new AuthResult { uid = uid, isGuest = true }); }
        void Err(string e)  { Cleanup(); tcs.TrySetException(new System.Exception(e)); }

        void Cleanup()
        {
            _proxy.AuthSuccess -= Ok;
            _proxy.AuthError -= Err;
        }

        _proxy.AuthSuccess += Ok;
        _proxy.AuthError += Err;
        _proxy.SignInAnon();
        return tcs.Task;
    }

    public Task<AuthResult> SignInWithGoogleAsync()
    {
        var tcs = new TaskCompletionSource<AuthResult>();

        void Ok(string uid) { Cleanup(); tcs.TrySetResult(new AuthResult { uid = uid, isGuest = false }); }
        void Err(string e)  { Cleanup(); tcs.TrySetException(new System.Exception(e)); }

        void Cleanup()
        {
            _proxy.AuthSuccess -= Ok;
            _proxy.AuthError -= Err;
        }

        _proxy.AuthSuccess += Ok;
        _proxy.AuthError += Err;
        _proxy.SignInGooglePopup();
        return tcs.Task;
    }

    public Task SignOutAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        void Ok() { Cleanup(); tcs.TrySetResult(true); }
        void Err(string e) { Cleanup(); tcs.TrySetException(new System.Exception(e)); }

        void Cleanup()
        {
            _proxy.SignOutOk -= Ok;
            _proxy.AuthError -= Err;
        }

        _proxy.SignOutOk += Ok;
        _proxy.AuthError += Err;
        _proxy.SignOut();
        return tcs.Task;
    }
}
