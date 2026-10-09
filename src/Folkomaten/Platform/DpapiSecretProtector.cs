using System.Security.Cryptography;
using Folkomaten.Core.Tenor;

namespace Folkomaten.Platform;

/// <summary>Windows DPAPI, avgrenset til gjeldende bruker: andre kontoer på maskinen kan ikke dekryptere dataene.</summary>
internal sealed class DpapiSecretProtector : ISecretProtector
{
    public byte[] Protect(byte[] plaintext) =>
        ProtectedData.Protect(plaintext, optionalEntropy: null, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] protectedData) =>
        ProtectedData.Unprotect(protectedData, optionalEntropy: null, DataProtectionScope.CurrentUser);
}
