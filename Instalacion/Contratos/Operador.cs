using System.Security.Cryptography;

namespace Clarisa.Instalacion.Contratos;

// La interfaz pide los secretos solo cuando un paso los necesita; el motor nunca los guarda en el plan, el registro ni argumentos.
public interface IOperadorInstalacion
{
    Task<CredencialOperador?> SolicitarClaveOperadorMotorAsync(CancellationToken ct);

    Task<CredencialOperador?> SolicitarAccesoPrimeraCuentaAsync(string usuario, CancellationToken ct);

    Task<bool> ConfirmarAsync(string mensaje, CancellationToken ct);
}

public sealed class CredencialOperador : IDisposable
{
    private char[]? clave;

    public CredencialOperador(string usuario, ReadOnlySpan<char> clave)
    {
        if (string.IsNullOrWhiteSpace(usuario) || usuario.Length > 80) throw new ArgumentException("Usuario no válido.", nameof(usuario));
        if (clave.IsEmpty || clave.Length > 256) throw new ArgumentException("La clave no es válida.", nameof(clave));
        Usuario = usuario;
        this.clave = clave.ToArray();
    }

    public string Usuario { get; }

    public bool Disponible => clave is not null;

    internal string ClaveTexto() => new(clave ?? throw new ObjectDisposedException(nameof(CredencialOperador)));

    public void Dispose()
    {
        if (clave is null) return;
        CryptographicOperations.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes(clave.AsSpan()));
        clave = null;
    }

    public override string ToString() => $"Credencial de {Usuario} (protegida)";
}
