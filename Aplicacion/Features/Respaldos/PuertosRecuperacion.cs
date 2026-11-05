namespace Clarisa.Application.Features.Respaldos;

// Sin archivo de recuperación se usa la copia de la clave protegida en este servidor.
public sealed record OrigenClave(string? ArchivoRecuperacion);

// Credencial temporal del operador para crear la base nueva. Se pide por consola y no se registra ni se persiste.
public sealed class CredencialOperador(string usuario, string clave)
{
    public string Usuario { get; } = string.IsNullOrWhiteSpace(usuario)
        ? throw new ArgumentException("Indica el usuario del operador.", nameof(usuario)) : usuario;
    public string Clave { get; } = string.IsNullOrEmpty(clave)
        ? throw new ArgumentException("Indica la clave del operador.", nameof(clave)) : clave;

    public override string ToString() => "operador " + Usuario;
}

public sealed record TablaCopia(string Nombre, long Filas);

// HuellaContenido resume las huellas por tabla del manifiesto; la base recuperada debe reproducirla exactamente.
public sealed record ResumenCopia(DateTimeOffset CreadaUtc, int VersionEsquema, IReadOnlyList<TablaCopia> Tablas, string HuellaContenido)
{
    public long TotalFilas => Tablas.Sum(t => t.Filas);
}

public sealed record BaseRecuperada(string Base, ResumenCopia Copia);

public sealed record ReferenciaBase(string Base, string? Anterior, Guid? RecuperacionId, DateTimeOffset? ActivadaUtc);

public interface IRecuperadorCopias
{
    Task<ResumenCopia> ValidarAsync(string archivo, OrigenClave origen, CancellationToken ct);

    // Valida la copia completa antes de crear nada. Si falla después de crear la base, elimina solo esa base nueva.
    Task<BaseRecuperada> RecuperarEnBaseNuevaAsync(string archivo, OrigenClave origen, CredencialOperador operador, CancellationToken ct);

    Task VerificarContenidoAsync(string baseDatos, string huellaContenido, CredencialOperador operador, CancellationToken ct);

    Task VerificarEsquemaAsync(string baseDatos, CredencialOperador operador, CancellationToken ct);
}

public interface IReferenciaBaseActiva
{
    Task<ReferenciaBase> LeerAsync(CancellationToken ct);

    // Sustitución atómica: o queda la referencia anterior completa o la nueva completa.
    Task<ReferenciaBase> ActivarAsync(string nueva, Guid? recuperacionId, CancellationToken ct);
}

// Exclusión entre el servidor y el mantenimiento local. Lanza EstadoNoPermitido si el otro proceso la tiene.
public interface IExclusionInstancia
{
    IDisposable Adquirir();
}
