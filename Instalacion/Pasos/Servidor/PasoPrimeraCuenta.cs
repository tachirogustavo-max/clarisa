using System.Buffers;
using System.Text;
using System.Text.Json;
using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Pasos.Servidor;

// La autenticación real se comprueba después en verificacion-api: este paso solo acredita que el procedimiento terminó. Con una interfaz
// que implementa IOperadorCuentaInicial, la cuenta lleva el usuario de la solicitud y los datos llegan por la entrada estándar privada.
internal sealed class PasoPrimeraCuenta : IPasoInstalacion
{
    public const string ModoEntrada = "--crear-administrador-entrada";
    public const int Intentos = 3;
    private static readonly TimeSpan LimiteConsola = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LimiteEntrada = TimeSpan.FromMinutes(3);

    public string Id => "primera-cuenta";
    public string Nombre => "Primera cuenta de dirección";
    public string Descripcion => "Crea la cuenta de dirección con el usuario indicado; la contraseña no pasa por argumentos, archivos ni registros.";
    public bool Critico => true;
    public bool Privilegiado => true;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        if (contexto.Hechos.TryGetValue(Id, out var hecho)) return Task.FromResult(Comprobacion.Satisfecha(hecho));
        return Task.FromResult(contexto.Estado.PasosAprobados.ContainsKey(Id)
            ? Comprobacion.Satisfecha("Preparada en una ejecución anterior; se confirma con el inicio de sesión de la verificación final.")
            : Comprobacion.NoSatisfecha("Todavía no se preparó la primera cuenta."));
    }

    public Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct) => contexto.Operador is IOperadorCuentaInicial interfaz
        ? DesdeInterfazAsync(contexto, interfaz, ct)
        : DesdeConsolaAsync(contexto, ct);

    private async Task DesdeInterfazAsync(ContextoEjecucion contexto, IOperadorCuentaInicial interfaz, CancellationToken ct)
    {
        var usuario = contexto.Servidor.UsuarioPrimeraCuenta;
        string? motivo = null;
        for (var intento = 1; intento <= Intentos; intento++)
        {
            var datos = await interfaz.SolicitarCreacionPrimeraCuentaAsync(usuario, motivo, ct)
                ?? throw new FalloInstalacionException("No se indicaron los datos de la primera cuenta y no se creó ninguna. Vuelve a ejecutar " +
                                                       "el instalador para crearla.");
            var conservar = false;
            try
            {
                if (!datos.Disponible || !string.Equals(datos.Usuario, usuario, StringComparison.Ordinal))
                    throw new FalloInstalacionException($"La interfaz no devolvió datos válidos para el usuario previsto {usuario}; no se creó ninguna cuenta.");
                var clave = datos.CredencialOperador.ClaveTexto();
                contexto.Filtro.Registrar(clave);
                var rutas = contexto.RutasServidor;
                var resultado = await contexto.Sistema.Procesos.EjecutarAsync(new SolicitudProceso(rutas.EjecutableApi,
                    ["--configuracion", rutas.ArchivoConfiguracion, ModoEntrada], LimiteEntrada)
                {
                    Directorio = Path.GetDirectoryName(rutas.EjecutableApi),
                    EntradaSecreta = Serializar(datos.Usuario, datos.Nombre, clave)
                }, ct);
                contexto.Anotar(Id, $"Intento {intento}: código {resultado.Codigo}.");
                switch (resultado)
                {
                    case { VencioLimite: true }:
                        throw new FalloInstalacionException("El servidor no respondió a tiempo al crear la primera cuenta; no hay cuenta confirmada. " +
                                                            "Al reintentar, si ya se creó, se pedirá su contraseña para verificarla.");
                    case { Codigo: 0 } when Linea(resultado.Salida, "CUENTA_CREADA") is not null:
                        contexto.Hechos[Id] = $"Cuenta de dirección creada con el usuario {usuario}.";
                        contexto.ConservarCredencialPrimeraCuenta(datos.CredencialOperador);
                        conservar = true;
                        return;
                    case { Codigo: 3 }:
                        contexto.Hechos[Id] = "Ya existían cuentas en la base; no se creó otra. La verificación final pedirá el acceso de la dirección.";
                        return;
                    case { Codigo: 5 }:
                        motivo = contexto.Filtro.Limpiar(Linea(resultado.Salida, "RECHAZO ") ?? "El servidor rechazó los datos.");
                        continue;
                    default:
                        throw new FalloInstalacionException($"La preparación de la primera cuenta no terminó (código {resultado.Codigo}). No hay cuenta confirmada.");
                }
            }
            finally
            {
                if (!conservar) datos.Dispose();
            }
        }
        throw new FalloInstalacionException($"El servidor rechazó {Intentos} veces los datos de la primera cuenta ({motivo}). No se creó ninguna cuenta.");
    }

    // Consumidor anterior sin IOperadorCuentaInicial: la consola pide su propio usuario y no puede garantizar que sea el previsto.
    private async Task DesdeConsolaAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        var resultado = await contexto.Sistema.Procesos.EjecutarAsync(new SolicitudProceso(rutas.EjecutableApi,
            ["--configuracion", rutas.ArchivoConfiguracion, "--crear-administrador"], LimiteConsola)
        {
            Directorio = Path.GetDirectoryName(rutas.EjecutableApi),
            ConsolaVisible = true
        }, ct);
        contexto.Hechos[Id] = resultado switch
        {
            { VencioLimite: true } => throw new FalloInstalacionException("La consola de la primera cuenta se cerró por tiempo; no hay cuenta confirmada."),
            { Codigo: 0 } => $"Cuenta creada para la dirección en la consola (usuario previsto: {contexto.Servidor.UsuarioPrimeraCuenta}).",
            { Codigo: 3 } => "Ya existían cuentas en la base; no se creó otra.",
            _ => throw new FalloInstalacionException($"La preparación de la primera cuenta no terminó (código {resultado.Codigo}).")
        };
    }

    // El texto devuelto es la única copia en cadena; Clear borra el búfer intermedio. Los no ASCII se escapan, así no depende de la codificación.
    internal static string Serializar(string usuario, string nombre, string clave)
    {
        var bufer = new ArrayBufferWriter<byte>(4096);
        try
        {
            using (var escritor = new Utf8JsonWriter(bufer))
            {
                escritor.WriteStartObject();
                escritor.WriteString("usuario", usuario);
                escritor.WriteString("nombre", nombre);
                escritor.WriteString("clave", clave);
                escritor.WriteEndObject();
            }
            return Encoding.UTF8.GetString(bufer.WrittenSpan);
        }
        finally
        {
            bufer.Clear();
        }
    }

    private static string? Linea(string salida, string prefijo) => salida.Split('\n')
        .Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith(prefijo, StringComparison.Ordinal)) is { } linea
        ? linea[prefijo.Length..].Trim() is { Length: > 0 } resto ? resto : linea
        : null;
}
