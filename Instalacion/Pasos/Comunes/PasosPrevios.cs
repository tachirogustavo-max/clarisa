using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Paquete;
using Clarisa.Instalacion.Pasos.Servidor;

namespace Clarisa.Instalacion.Pasos.Comunes;

internal sealed class PasoRequisitos : PasoVerificacion
{
    private const long MiB = 1024 * 1024;
    private const long MargenPrograma = 200 * MiB;
    private const long MinimoDatosServidor = 4096 * MiB;
    private static readonly Version WindowsMinimo = new(10, 0, 17763);

    public override string Id => "requisitos";
    public override string Nombre => "Requisitos del equipo y espacio";
    public override string Descripcion => "Windows de 64 bits compatible y espacio para programas, motor, datos y temporales.";

    public override Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var equipo = contexto.Sistema.Equipo;
        var datos = equipo.Describir();
        if (!datos.Windows64 || datos.VersionWindows < WindowsMinimo)
            return Task.FromResult(Comprobacion.NoSatisfecha($"Se requiere Windows 10 1809 o posterior de 64 bits; este equipo tiene {datos.VersionWindows}."));
        if (!datos.Administrador)
            return Task.FromResult(Comprobacion.NoSatisfecha("El instalador debe ejecutarse como administrador. No se modificó nada."));
        var plan = contexto.Plan;
        var manifiesto = contexto.Paquete;
        var necesarioPrograma = manifiesto.Del(plan.Modo).Sum(a => a.Bytes) + MargenPrograma;
        var faltas = new List<string>();
        Revisar(faltas, equipo, plan.Solicitud.DirectorioPrograma, necesarioPrograma, "programas");
        if (contexto.Plan.Solicitud is SolicitudServidor servidor)
            Revisar(faltas, equipo, servidor.DirectorioDatos, MinimoDatosServidor, "datos, copias y temporales");
        return Task.FromResult(faltas.Count == 0
            ? Comprobacion.Satisfecha($"Windows {datos.VersionWindows} de 64 bits; espacio suficiente.")
            : Comprobacion.NoSatisfecha(string.Join(" ", faltas)));
    }

    private static void Revisar(List<string> faltas, Sistema.IEquipo equipo, string ruta, long necesario, string uso)
    {
        var libres = equipo.BytesLibres(ruta);
        if (libres < necesario)
            faltas.Add($"Espacio insuficiente para {uso} en {Path.GetPathRoot(ruta)}: hay {libres / MiB} MiB y se necesitan {necesario / MiB} MiB.");
    }
}

internal sealed class PasoPaquete : PasoVerificacion
{
    public override string Id => "paquete";
    public override string Nombre => "Integridad del paquete";
    public override string Descripcion => "Cada archivo del paquete coincide con el tamaño y la huella SHA-256 de su manifiesto.";

    public override async Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var origen = contexto.Plan.Solicitud.OrigenPaquete;
        var manifiesto = contexto.Paquete;
        if (manifiesto.Faltantes(contexto.Plan.Modo) is { Count: > 0 } faltan)
            return Comprobacion.NoSatisfecha("El paquete no incluye: " + string.Join(", ", faltan));
        var archivos = manifiesto.Del(contexto.Plan.Modo);
        foreach (var archivo in archivos)
        {
            if (await ManifiestoPaquete.DiferenciaAsync(origen, archivo, ct) is { } diferencia)
                return Comprobacion.NoSatisfecha("Paquete dañado o incompleto: " + diferencia + ". No se copió nada.");
        }
        return Comprobacion.Satisfecha($"Versión {manifiesto.Version}: {archivos.Count} archivos verificados.");
    }
}

internal sealed class PasoPuertos : PasoVerificacion
{
    public override string Id => "puertos";
    public override string Nombre => "Puertos del servidor y del motor";
    public override string Descripcion => "Los puertos elegidos están libres o los usa esta misma instalación; nunca se detiene otro programa.";

    public override Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var s = contexto.Servidor;
        var rutas = contexto.RutasServidor;
        var equipo = contexto.Sistema.Equipo;
        var problemas = new List<string>();
        if (!equipo.PuertoDisponible(s.DireccionEscucha, s.PuertoHttps)
            && !(Propio(contexto, RutasServidor.ServicioApi) && ConfiguracionEfectiva.Url(contexto) == ConfiguracionServidor.UrlEscucha(rutas)))
            problemas.Add($"El puerto {s.PuertoHttps} en {s.DireccionEscucha} lo usa otro programa.");
        if (!equipo.PuertoDisponible(System.Net.IPAddress.Loopback, s.PuertoMotor)
            && !(Propio(contexto, RutasServidor.ServicioMotor) && ConfiguracionMotor.Vigente(rutas)))
            problemas.Add($"El puerto {s.PuertoMotor} del motor lo usa otro programa (posiblemente otro MariaDB/MySQL); no se detiene.");
        return Task.FromResult(problemas.Count == 0
            ? Comprobacion.Satisfecha($"Puertos {s.PuertoHttps} y {s.PuertoMotor} disponibles para esta instalación.")
            : Comprobacion.NoSatisfecha(string.Join(" ", problemas) + " Elige otro puerto."));
    }

    // Solo se acepta un puerto ocupado si lo usa el servicio propio, en ejecución y configurado con ese mismo puerto.
    private static bool Propio(ContextoEjecucion contexto, string nombre)
    {
        var servicio = contexto.Sistema.Servicios.Consultar(nombre);
        return ServiciosPropios.EnEjecucion(servicio) && ServiciosPropios.Ajeno(contexto, servicio) is null;
    }
}
