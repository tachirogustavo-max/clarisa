using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Consultas;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Grados;

// Años de estudio. No se borran: un año que ya no se ofrece se desactiva y sus secciones anteriores siguen igual.
public sealed class GestionGrados(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    private const string Entidad = nameof(Grado);

    public async Task<RegistroVersionado> CrearAsync(Actor actor, CrearGrado solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var nuevo = Error.DesdeDominio(() => new Grado(datos.Id, datos.Nombre, datos.Orden, activo: true, version: 1));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var existente = await unidad.Grados.ObtenerAsync(nuevo.Id, ct);
        if (existente is not null) return Reintentos.Resolver(existente.MismoContenido(nuevo), existente.Id, existente.Version);
        await ExigirNombreLibreAsync(unidad, nuevo.Nombre, null, ct);

        await unidad.Grados.AgregarAsync(nuevo, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Creacion, Entidad, nuevo.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(nuevo.Id, nuevo.Version);
    }

    public async Task<RegistroVersionado> ModificarAsync(Actor actor, ModificarGrado solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var grado = await unidad.Grados.ObtenerAsync(datos.Id, ct) ?? throw Error.NoEncontrado("El año de estudio no existe.");
        Error.ExigirVersion(grado.Version, datos.VersionEsperada);
        var anterior = grado.Nombre;
        Error.DesdeDominio(() => grado.Modificar(datos.Nombre, datos.Orden, datos.Activo));
        await ExigirNombreLibreAsync(unidad, grado.Nombre, grado.Id, ct);

        var version = await unidad.Grados.ActualizarAsync(grado, ct);
        if (anterior != grado.Nombre) await unidad.Grados.RenombrarEnUsoAsync(anterior, grado.Nombre, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Modificacion, Entidad, grado.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(grado.Id, version);
    }

    public async Task<Pagina<GradoDatos>> BuscarAsync(Actor actor, FiltroTexto filtro, ConsultaPagina consulta, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        var filtroValido = FiltroTexto.Validar(filtro);
        var pagina = ConsultaPagina.Validar(consulta);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var resultado = await unidad.Grados.BuscarAsync(filtroValido, pagina, ct);
        return resultado.Convertir(GradoDatos.Desde);
    }

    // Nombre exacto del año activo que se eligió para una sección.
    internal static async Task<string> ExigirActivoAsync(IUnidadTrabajo unidad, string? nombre, CancellationToken ct)
    {
        var limpio = Error.DesdeDominio(() => Textos.Requerido(nombre, "Grado", Grado.MaximoNombre));
        var grado = await unidad.Grados.BuscarPorNombreAsync(limpio, ct);
        if (grado is null || !grado.Activo)
            throw Error.DatosInvalidos("Elige un año de estudio activo de la lista.", "Grado");
        return grado.Nombre;
    }

    private static async Task ExigirNombreLibreAsync(IUnidadTrabajo unidad, string nombre, Guid? excluirId, CancellationToken ct)
    {
        if (await unidad.Grados.ExisteNombreAsync(nombre, excluirId, ct))
            throw Error.Duplicado("Ya existe un año de estudio con ese nombre.", nameof(Grado.Nombre));
    }
}
