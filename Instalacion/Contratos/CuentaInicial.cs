namespace Clarisa.Instalacion.Contratos;

// Si el operador no la implementa, la primera cuenta se sigue creando con la consola interactiva.
public interface IOperadorCuentaInicial
{
    // Devolver null equivale a no crear la cuenta. El motor dispone lo devuelto.
    Task<DatosCuentaInicial?> SolicitarCreacionPrimeraCuentaAsync(string usuario, string? motivoRechazo, CancellationToken ct);
}

public sealed class DatosCuentaInicial : IDisposable
{
    public const int MaximoNombre = 80;

    public DatosCuentaInicial(string usuario, string nombre, ReadOnlySpan<char> clave)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Trim().Length > MaximoNombre || nombre.Any(char.IsControl))
            throw new ArgumentException("Nombre visible no válido.", nameof(nombre));
        CredencialOperador = new CredencialOperador(usuario, clave);
        Nombre = nombre.Trim();
    }

    public string Usuario => CredencialOperador.Usuario;

    public string Nombre { get; }

    // Usuario y clave de la cuenta que se creará; la misma credencial verifica después el inicio de sesión real.
    public CredencialOperador CredencialOperador { get; }

    public bool Disponible => CredencialOperador.Disponible;

    public void Dispose() => CredencialOperador.Dispose();

    public override string ToString() => $"Primera cuenta {Usuario} (clave protegida)";
}
