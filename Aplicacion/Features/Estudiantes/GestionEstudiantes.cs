using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Consultas;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Estudiantes;

public sealed class GestionEstudiantes(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    private const string Entidad = nameof(Estudiante);

    public async Task<RegistroVersionado> CrearAsync(Actor actor, CrearEstudiante solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var nuevo = Error.DesdeDominio(() => new Estudiante(datos.Id, datos.Identificacion, datos.Nombres, datos.Apellidos,
            datos.FechaNacimiento, activo: true, version: 1));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var existente = await unidad.Estudiantes.ObtenerAsync(nuevo.Id, ct);
        if (existente is not null) return Reintentos.Resolver(existente.MismoContenido(nuevo), existente.Id, existente.Version);
        await ExigirIdentificacionLibreAsync(unidad, nuevo.Identificacion, null, ct);

        await unidad.Estudiantes.AgregarAsync(nuevo, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Creacion, Entidad, nuevo.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(nuevo.Id, nuevo.Version);
    }

    public async Task<RegistroVersionado> ModificarAsync(Actor actor, ModificarEstudiante solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var estudiante = await unidad.Estudiantes.ObtenerAsync(datos.Id, ct) ?? throw Error.NoEncontrado("El estudiante no existe.");
        Error.ExigirVersion(estudiante.Version, datos.VersionEsperada);
        Error.DesdeDominio(() => estudiante.Modificar(datos.Identificacion, datos.Nombres, datos.Apellidos, datos.FechaNacimiento,
            datos.Activo));
        await ExigirIdentificacionLibreAsync(unidad, estudiante.Identificacion, estudiante.Id, ct);

        var version = await unidad.Estudiantes.ActualizarAsync(estudiante, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Modificacion, Entidad, estudiante.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(estudiante.Id, version);
    }

    public async Task<EstudianteDatos> ObtenerAsync(Actor actor, Guid id, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var estudiante = await unidad.Estudiantes.ObtenerAsync(id, ct) ?? throw Error.NoEncontrado("El estudiante no existe.");
        return EstudianteDatos.Desde(estudiante);
    }

    public async Task<Pagina<EstudianteDatos>> BuscarAsync(Actor actor, FiltroTexto filtro, ConsultaPagina consulta,
        CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var filtroValido = FiltroTexto.Validar(filtro);
        var pagina = ConsultaPagina.Validar(consulta);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var resultado = await unidad.Estudiantes.BuscarAsync(filtroValido, pagina, ct);
        return resultado.Convertir(EstudianteDatos.Desde);
    }

    private static async Task ExigirIdentificacionLibreAsync(IUnidadTrabajo unidad, string? identificacion, Guid? excluirId,
        CancellationToken ct)
    {
        if (identificacion is not null && await unidad.Estudiantes.ExisteIdentificacionAsync(identificacion, excluirId, ct))
            throw Error.Duplicado("Ya existe un estudiante con esa identificación.", nameof(Estudiante.Identificacion));
    }
}
