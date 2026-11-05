using System.Security.Cryptography;

namespace Clarisa.Infrastructure.Respaldos.Claves;

// Clave AES-256 con su identificador. Los bytes se borran al liberar; nunca se registran ni se devuelven a Application.
internal sealed class ClaveRespaldo : IDisposable
{
    public const int Longitud = 32;
    private readonly byte[] bytes;

    public ClaveRespaldo(Guid id, ReadOnlySpan<byte> material)
    {
        if (id == Guid.Empty || material.Length != Longitud) throw new CryptographicException("Clave de respaldo no válida.");
        Id = id;
        bytes = material.ToArray();
    }

    public Guid Id { get; }

    public ReadOnlySpan<byte> Material => bytes;

    public static ClaveRespaldo Generar()
    {
        Span<byte> material = stackalloc byte[Longitud];
        RandomNumberGenerator.Fill(material);
        try
        {
            return new ClaveRespaldo(Guid.NewGuid(), material);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material);
        }
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(bytes);

    public override string ToString() => $"clave de respaldo {Id}";
}
