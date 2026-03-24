using System.Threading.Tasks;

public struct AuthResult
{
    public string uid;
    public bool isGuest;
}

public interface IAuthService
{
    Task<AuthResult> SignInGuestAsync();
    Task<AuthResult> SignInWithGoogleAsync();
    Task SignOutAsync();
}
