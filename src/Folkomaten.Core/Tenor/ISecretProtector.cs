namespace Folkomaten.Core.Tenor;

/// <summary>Krypterer data slik at bare den gjeldende Windows-brukeren kan lese dem tilbake.</summary>
public interface ISecretProtector
{
    byte[] Protect(byte[] plaintext);

    /// <exception cref="System.Security.Cryptography.CryptographicException">Dataene kan ikke dekrypteres av denne brukeren.</exception>
    byte[] Unprotect(byte[] protectedData);
}
