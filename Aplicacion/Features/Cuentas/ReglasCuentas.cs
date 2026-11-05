using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Seguridad;
using Clarisa.Application.Common.Errores;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Cuentas;

// Comprobaciones que dependen de otros registros; se ejecutan dentro de la unidad de escritura que confirma el cambio.
internal static class ReglasCuentas
{
    public static async Task ExigirUsuarioLibreAsync(IUnidadSeguridad unidad, string usuario, Guid? excluirId, CancellationToken ct)
    {
        if (await unidad.Cuentas.ExisteUsuarioAsync(usuario, excluirId, ct))
            throw Error.Duplicado("Ya existe una cuenta con ese usuario.", "Usuario");
    }

    public static async Task ExigirVinculoValidoAsync(IUnidadSeguridad unidad, CuentaUsuario cuenta, CancellationToken ct)
    {
        if (cuenta.DocenteId is not { } docenteId) return;
        var docente = await unidad.Docentes.ObtenerAsync(docenteId, ct)
            ?? throw Error.ReferenciaInvalida("El docente seleccionado no existe.", "DocenteId");
        if (cuenta.Activo && !docente.Activo)
            throw Error.EstadoNoPermitido("El docente seleccionado no está activo.", "DocenteId");
        if (await unidad.Cuentas.ExisteDocenteVinculadoAsync(docenteId, cuenta.Id, ct))
            throw Error.Duplicado("Ese docente ya tiene una cuenta.", "DocenteId");
    }

    public static string UsuarioNormalizado(string? usuario) =>
        Error.DesdeDominio(() => NombresUsuario.Normalizar(usuario, "Usuario"));

    public static DateTimeOffset AhoraUtc(IReloj reloj) => Instantes.Utc(reloj.Ahora);

    public static Task AuditarAsync(IUnidadSeguridad unidad, Guid actor, AccionAuditoriaCuenta accion, Guid cuentaId,
        DateTimeOffset ahoraUtc, CancellationToken ct) =>
        unidad.Auditoria.RegistrarAsync(new EventoAuditoriaCuenta(actor, accion, cuentaId, ahoraUtc), ct);
}
