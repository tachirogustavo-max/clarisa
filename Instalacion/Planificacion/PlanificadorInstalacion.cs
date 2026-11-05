using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Planificacion;

// Sin efectos: valida y describe. El motor vuelve a comprobar cada condición en el momento de aplicarla.
public sealed class PlanificadorInstalacion
{
    private readonly IEquipo equipo;
    private readonly Func<ModoInstalacion, IReadOnlyList<IPasoInstalacion>> catalogo;
    private readonly string registrosEstacion;

    internal PlanificadorInstalacion(IEquipo equipo, Func<ModoInstalacion, IReadOnlyList<IPasoInstalacion>> catalogo, string registrosEstacion)
    {
        this.equipo = equipo;
        this.catalogo = catalogo;
        this.registrosEstacion = registrosEstacion;
    }

    public ResultadoPlanificacion Planificar(SolicitudInstalacion solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var problemas = new ValidadorSolicitud(equipo).Validar(solicitud);
        if (problemas.Count > 0) return new ResultadoPlanificacion(null, problemas);
        var pasos = catalogo(solicitud.Modo)
            .Select(p => new PasoPlaneado(p.Id, p.Nombre, p.Descripcion, p.Critico, p.Privilegiado)).ToList();
        return new ResultadoPlanificacion(new PlanInstalacion(Guid.NewGuid(), solicitud.Modo, solicitud, pasos, Resumen(solicitud),
            Registros(solicitud, registrosEstacion)), []);
    }

    internal static string Registros(SolicitudInstalacion solicitud, string registrosEstacion) => solicitud is SolicitudServidor servidor
        ? new RutasServidor(servidor).RegistrosInstalacion
        : registrosEstacion;

    private static IReadOnlyList<LineaResumen> Resumen(SolicitudInstalacion solicitud) => solicitud switch
    {
        SolicitudServidor s =>
        [
            new("Modo", "Servidor del liceo (API, motor de datos propio y mantenimiento)"),
            new("Programas", s.DirectorioPrograma),
            new("Datos, configuración y copias", s.DirectorioDatos),
            new("Dirección para las estaciones", new RutasServidor(s).DireccionPublica.AbsoluteUri),
            new("Escucha en la red local", $"{s.DireccionEscucha}:{s.PuertoHttps} (regla de firewall solo para esta dirección y puerto)"),
            new("Motor de datos", $"Solo en este equipo, 127.0.0.1:{s.PuertoMotor}"),
            new("Servicios", $"{RutasServidor.ServicioMotor} y {RutasServidor.ServicioApi}, con cuentas virtuales propias"),
            new("Primera cuenta de dirección", s.UsuarioPrimeraCuenta + " (la contraseña se define al preparar la cuenta; la cuenta solo existe cuando el servidor confirma su creación)"),
            new("Archivo de recuperación de copias", s.DestinoClaveRecuperacion),
            new("Copias automáticas", "Deshabilitadas: falta acordar la política. Las copias manuales siguen disponibles.")
        ],
        SolicitudEstacion e =>
        [
            new("Modo", "Estación de trabajo (solo la aplicación de escritorio)"),
            new("Programas", e.DirectorioPrograma),
            new("Servidor", e.DireccionServidor.Trim()),
            new("Certificado del servidor", $"{e.ArchivoCertificado} (huella {ValidadorSolicitud.NormalizarHuella(e.HuellaCertificado)})"),
            new("Acceso directo", e.AccesoEnEscritorio ? "Menú Inicio y escritorio común" : "Menú Inicio")
        ],
        _ => []
    };
}
