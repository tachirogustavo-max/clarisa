using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Consultas;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;

namespace Clarisa.Application.Features.LecturasAcademicas;

public sealed class LecturasAcademicas(IFabricaLecturasAcademicas fabrica)
{
    public async Task<Pagina<InscripcionVista>> InscripcionesAsync(Actor actor, FiltroLecturas filtro, ConsultaPagina pagina, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var consulta = Validar(filtro, exigeAnio: true);
        pagina = ConsultaPagina.Validar(pagina);
        await using var unidad = await fabrica.IniciarAsync(ct);
        return await unidad.InscripcionesAsync(consulta, pagina, ct);
    }
    public async Task<Pagina<AsignacionVista>> AsignacionesAsync(Actor actor, FiltroLecturas filtro, ConsultaPagina pagina, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        var consulta = Validar(filtro, exigeAnio: Autorizacion.EsAdministracion(actor));
        pagina = ConsultaPagina.Validar(pagina);
        await using var unidad = await fabrica.IniciarAsync(ct);
        var alcance = await AccesoLecturas.ResolverAsync(actor, unidad, ct);
        return await unidad.AsignacionesAsync(alcance, consulta, pagina, ct);
    }
    public async Task<IReadOnlyList<OpcionAcademica>> OpcionesAsync(Actor actor, TipoCatalogoAcademico tipo, Guid? anioId, Guid? seccionId, int limite, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        if (!Enum.IsDefined(tipo) || limite is < 1 or > 2000)
            throw Error.DatosInvalidos("La consulta de opciones no es válida.");
        if (tipo is TipoCatalogoAcademico.Estudiantes or TipoCatalogoAcademico.Docentes)
            Autorizacion.ExigirAdministracion(actor);
        if (anioId == Guid.Empty || seccionId == Guid.Empty)
            throw Error.DatosInvalidos("Selecciona una referencia válida.");
        await using var unidad = await fabrica.IniciarAsync(ct);
        var alcance = await AccesoLecturas.ResolverAsync(actor, unidad, ct);
        var opciones = await unidad.OpcionesAsync(tipo, alcance, anioId, seccionId, limite, ct);
        if (opciones.Count > limite)
            throw Error.EstadoNoPermitido("Hay demasiadas opciones para este formulario. Reduce la selección por año o sección.");
        return opciones;
    }
    private static FiltroLecturas Validar(FiltroLecturas filtro, bool exigeAnio)
    {
        Error.SolicitudRequerida(filtro);
        if ((exigeAnio && filtro.AnioLectivoId is null) || filtro.AnioLectivoId == Guid.Empty)
            throw Error.DatosInvalidos("Selecciona el año escolar.", nameof(filtro.AnioLectivoId));
        if (filtro.SeccionId == Guid.Empty) throw Error.DatosInvalidos("Selecciona una sección válida.", nameof(filtro.SeccionId));
        return filtro with { Texto = FiltroTexto.Validar(new(filtro.Texto, false)).Texto };
    }
}
