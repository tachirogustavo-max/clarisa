using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Pasos.Comunes;
using Clarisa.Instalacion.Pasos.Servidor;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Retirada;

// Antes de quitar nada se toma una copia cifrada verificada con el mantenimiento instalado y se conserva ese mantenimiento con su
// huella: es la salida de restauración si los programas ya no están. Sin copia verificada no se retira nada.
internal sealed class PasoCopiaRetirada(ReferenciaRetirada referencia, string carpeta) : IPasoInstalacion
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(90);

    public string Id => "copia-retirada";
    public string Nombre => "Copia verificada antes de retirar";
    public string Descripcion => "Detiene el servidor, toma una copia cifrada de la base, la verifica y conserva el mantenimiento que la restaura.";
    public bool Critico => true;
    public bool Privilegiado => true;

    private string Mantenimiento => Path.Combine(carpeta, "mantenimiento", ReferenciaRetirada.NombreMantenimiento);

    public async Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        if (contexto.Hechos.TryGetValue(Id, out var hecho)) return Comprobacion.Satisfecha(hecho);
        if (contexto.Estado.Retirada is not { Copia: { } copia, CopiaSha256: { } huella, MantenimientoSha256: { } mantenimiento } retirada)
            return Comprobacion.NoSatisfecha("Falta la copia previa a la retirada.");
        // Con el servidor otra vez en marcha la copia anterior ya no describe los datos.
        if (ServiciosPropios.EnEjecucion(contexto.Sistema.Servicios.Consultar(RutasServidor.ServicioApi)))
            return Comprobacion.NoSatisfecha("El servidor volvió a iniciarse después de la copia: se toma otra.");
        await PuntoActualizacion.ExigirHuellaAsync(copia, retirada.BytesCopia, huella, ct);
        if (await PasoRegistroRetirada.HuellaAsync(Mantenimiento, ct) is not { } conservado || conservado.Sha256 != mantenimiento)
            return Comprobacion.NoSatisfecha("Falta o cambió el mantenimiento conservado.");
        return Comprobacion.Satisfecha($"Copia {copia} y mantenimiento conservado verificados.");
    }

    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        var servicios = contexto.Sistema.Servicios;
        var activa = ServiciosPropios.EnEjecucion(ServiciosPropios.ConsultarPropio(contexto, RutasServidor.ServicioApi));
        if (activa) await servicios.DetenerAsync(RutasServidor.ServicioApi, Limite, ct);
        CopiaConfirmada copia;
        try
        {
            var motor = ServiciosPropios.ConsultarPropio(contexto, RutasServidor.ServicioMotor)
                        ?? throw new FalloInstalacionException("Falta el motor de datos propio: sin él no se puede tomar la copia previa. No se retiró nada.");
            if (!ServiciosPropios.EnEjecucion(motor)) await servicios.IniciarAsync(RutasServidor.ServicioMotor, Limite, ct);
            copia = await CopiaPrevia.TomarAsync(contexto, Guid.NewGuid(), rutas.EjecutableMantenimiento, ct);

            Directory.CreateDirectory(Path.GetDirectoryName(Mantenimiento)!);
            RutasSeguras.ExigirSinPuntosReparacion(Mantenimiento);
            EscrituraSegura.Reemplazar(Mantenimiento, destino =>
            {
                using var origen = new FileStream(rutas.EjecutableMantenimiento, FileMode.Open, FileAccess.Read, FileShare.Read);
                origen.CopyTo(destino);
            });
            var conservado = await PasoRegistroRetirada.HuellaAsync(Mantenimiento, ct)
                             ?? throw new FalloInstalacionException("No se pudo conservar el mantenimiento.");
            if (await PasoRegistroRetirada.HuellaAsync(rutas.EjecutableMantenimiento, ct) != conservado)
                throw new FalloInstalacionException("La copia del mantenimiento no coincide con el instalado.");
            contexto.Estado.Retirada = new RetiradaConservada(contexto.Sistema.Reloj(), referencia.Version, referencia.HuellaManifiesto,
                VersionMotor.Leer(rutas.EjecutableMotor), copia.Archivo, copia.Bytes, copia.Sha256, conservado.Sha256, Completa: false);
            contexto.Estado.Guardar(contexto.Plan.DirectorioRegistros);
            contexto.Hechos[Id] = $"Copia {copia.Archivo} verificada; mantenimiento conservado en {Mantenimiento}.";
        }
        catch (Exception error) when (activa && error is FalloInstalacionException or IOException or OperationCanceledException
                                          or System.ComponentModel.Win32Exception or InvalidOperationException
                                          or UnauthorizedAccessException or System.Security.SecurityException or TimeoutException)
        {
            await servicios.IniciarAsync(RutasServidor.ServicioApi, Limite, CancellationToken.None);
            throw new FalloInstalacionException(contexto.Filtro.Limpiar(error.Message) + " No se retiró nada y el servidor volvió a iniciarse.", error);
        }
    }
}

// Solo un servicio que consta como propio (o ejecuta el programa de esta instalación) y cuyo comando y cuenta siguen siendo los
// previstos. Si alguien lo cambió, se detiene la retirada sin tocarlo.
internal sealed class PasoRetirarServicio(string nombre, Func<RutasServidor, DefinicionServicio> definicion) : IPasoInstalacion
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(90);

    public string Id => nombre == RutasServidor.ServicioApi ? "servicio-api" : "servicio-motor";
    public string Nombre => nombre == RutasServidor.ServicioApi ? "Retirar el servicio del servidor" : "Retirar el servicio del motor de datos";
    public string Descripcion => $"Detiene y elimina {nombre} solo si es el propio y no fue modificado. Los datos no se tocan.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public async Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        // Windows puede tardar en quitar un servicio marcado mientras alguien conserva un manejador abierto.
        for (var intento = 0; intento < 10; intento++)
        {
            if (contexto.Sistema.Servicios.Consultar(nombre) is null)
                return Comprobacion.Satisfecha($"El servicio {nombre} no está registrado.");
            if (!contexto.Hechos.ContainsKey(Id)) break;
            await contexto.Sistema.Esperar(TimeSpan.FromSeconds(1), ct);
        }
        return Comprobacion.NoSatisfecha($"El servicio {nombre} sigue registrado.");
    }

    public async Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var actual = ServiciosPropios.ConsultarPropio(contexto, nombre);
        if (actual is null) return;
        if (Modificado(actual, definicion(contexto.RutasServidor)) is { } motivo) throw new FalloInstalacionException(motivo);
        if (actual.Estado != EstadoServicio.Detenido) await contexto.Sistema.Servicios.DetenerAsync(nombre, Limite, ct);
        contexto.Sistema.Servicios.Eliminar(nombre);
        contexto.Hechos[Id] = "1";
        contexto.Estado.OlvidarArtefacto(TipoArtefacto.Servicio, nombre);
        contexto.Estado.Guardar(contexto.Plan.DirectorioRegistros);
    }

    internal static string? Modificado(InfoServicio actual, DefinicionServicio esperado) =>
        string.Equals(actual.LineaComando, esperado.LineaComando, StringComparison.OrdinalIgnoreCase)
        && string.Equals(actual.Cuenta, esperado.Cuenta, StringComparison.OrdinalIgnoreCase)
            ? null
            : $"El servicio {esperado.Nombre} tiene otro comando o cuenta que los instalados. No se detiene ni se elimina: revíselo con el " +
              "responsable técnico. No se retiró nada más.";
}

internal sealed class PasoRetirarReglaFirewall : IPasoInstalacion
{
    public string Id => "regla-firewall";
    public string Nombre => "Retirar la regla de red local";
    public string Descripcion => "Elimina la regla de entrada propia del servidor; una regla ajena o modificada se conserva.";
    public bool Critico => false;
    public bool Privilegiado => true;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var reglas = contexto.Sistema.Firewall.Consultar(PasoRedLocal.NombreRegla);
        if (reglas.Count == 0) return Task.FromResult(Comprobacion.Satisfecha("No hay regla de entrada del servidor."));
        return Task.FromResult(Conservar(contexto, reglas) is { } motivo
            ? Comprobacion.Omitida(motivo)
            : Comprobacion.NoSatisfecha("La regla de entrada propia sigue presente."));
    }

    public Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var reglas = contexto.Sistema.Firewall.Consultar(PasoRedLocal.NombreRegla);
        if (reglas.Count == 0 || Conservar(contexto, reglas) is not null) return Task.CompletedTask;
        contexto.Sistema.Firewall.Eliminar(PasoRedLocal.NombreRegla);
        contexto.Estado.OlvidarArtefacto(TipoArtefacto.ReglaFirewall, PasoRedLocal.NombreRegla);
        contexto.Estado.Guardar(contexto.Plan.DirectorioRegistros);
        return Task.CompletedTask;
    }

    private static string? Conservar(ContextoEjecucion contexto, IReadOnlyList<ReglaFirewall> reglas)
    {
        if (reglas.Count > 1) return $"Hay {reglas.Count} reglas «{PasoRedLocal.NombreRegla}»: no se decide cuál es la propia y se conservan.";
        var regla = reglas[0];
        return contexto.Estado.EsPropio(TipoArtefacto.ReglaFirewall, PasoRedLocal.NombreRegla) && regla.Grupo == ReglaFirewall.GrupoPropio
               && ProgramasAnteriores.MismoArchivo(regla.Programa, contexto.RutasServidor.EjecutableApi, contexto.RutasServidor.EjecutableApiAnterior)
            ? null
            : $"La regla «{PasoRedLocal.NombreRegla}» no consta como propia o fue modificada: se conserva para que la revise el responsable técnico.";
    }
}
