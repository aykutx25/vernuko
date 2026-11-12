using Vernuko.Licensing;

namespace Vernuko.Application.Services;

// Members that are not remote calls: on a connected computer they are implemented locally by the client adapters
// (the session and the cached license status live on that computer; the host enforces the real checks).
public partial interface IAuthService
{
    void Lock();

    void Logout();
}

public partial interface ILicenseService
{
    LicenseStatus Current { get; }

    bool CanWrite { get; }

    event EventHandler? Changed;
}
