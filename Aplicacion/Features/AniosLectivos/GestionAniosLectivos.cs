using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Consultas;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.AniosLectivos;

public sealed class GestionAniosLectivos(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    private const string Entidad = nameof(AnioLectivo);

    public async Task<RegistroVersionado> CrearAsync(Actor actor, CrearAnioLectivo solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var nuevo = Error.DesdeDominio(() => new AnioLectivo(datos.Id, datos.Nombre, datos.Inicio, datos.Fin,
            lapsoActualId: null, version: 1));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var existente = await unidad.AniosLectivos.ObtenerAsync(nuevo.Id, ct);
        if (existente is not null) return Reintentos.Resolver(existente.MismoContenido(nuevo), existente.Id, existente.Version);
        await ExigirNombreLibreAsync(unidad, nuevo.Nombre, null, ct);

        await unidad.AniosLectivos.AgregarAsync(nuevo, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Creacion, Entidad, nuevo.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(nuevo.Id, nuevo.Version);
    }

    public async Task<RegistroVersionado> ModificarAsync(Actor actor, ModificarAnioLectivo solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var anio = await unidad.AniosLectivos.ObtenerAsync(datos.Id, ct) ?? throw Error.NoEncontrado("El año lectivo no existe.");
        Error.ExigirVersion(anio.Version, datos.VersionEsperada);
        Error.DesdeDominio(() => anio.Modificar(datos.Nombre, datos.Inicio, datos.Fin));
        await ExigirNombreLibreAsync(unidad, anio.Nombre, anio.Id, ct);

        var lapsos = await unidad.Lapsos.ListarPorAnioAsync(anio.Id, ct);
        if (lapsos.Any(lapso => !anio.Fechas.Contiene(lapso.Fechas)))
            throw Error.EnUso("Hay momentos fuera de las nuevas fechas. Ajusta primero esos momentos.");

        var version = await unidad.AniosLectivos.ActualizarAsync(anio, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Modificacion, Entidad, anio.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(anio.Id, version);
    }

    public async Task<AnioLectivoDatos> ObtenerAsync(Actor actor, Guid id, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var anio = await unidad.AniosLectivos.ObtenerAsync(id, ct) ?? throw Error.NoEncontrado("El año lectivo no existe.");
        return AnioLectivoDatos.Desde(anio);
    }

    public async Task<Pagina<AnioLectivoDatos>> ListarAsync(Actor actor, ConsultaPagina consulta, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        var pagina = ConsultaPagina.Validar(consulta);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var resultado = await unidad.AniosLectivos.ListarAsync(pagina, ct);
        return resultado.Convertir(AnioLectivoDatos.Desde);
    }

    private static async Task ExigirNombreLibreAsync(IUnidadTrabajo unidad, string nombre, Guid? excluirId, CancellationToken ct)
    {
        if (await unidad.AniosLectivos.ExisteNombreAsync(nombre, excluirId, ct))
            throw Error.Duplicado("Ya existe un año lectivo con ese nombre.", nameof(AnioLectivo.Nombre));
    }
}
