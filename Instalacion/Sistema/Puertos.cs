using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Clarisa.Instalacion.Sistema;

internal sealed record SolicitudProceso(string Ejecutable, IReadOnlyList<string> Argumentos, TimeSpan Limite)
{
    public string? Directorio { get; init; }
    // Se escribe en la entrada estándar y se cierra; nunca va en los argumentos.
    public string? EntradaSecreta { get; init; }
    // Consola propia para que el operador escriba directamente; no se captura su salida.
    public bool ConsolaVisible { get; init; }
    public IReadOnlyDictionary<string, string>? VariablesEntorno { get; init; }

    // El ToString de un record mostraría la entrada secreta y las variables.
    public override string ToString() =>
        $"{Path.GetFileName(Ejecutable)} {string.Join(' ', Argumentos)} (entrada secreta: {EntradaSecreta is not null})";
}

internal sealed record ResultadoProceso(int Codigo, string Salida, bool VencioLimite);

internal interface IProcesos
{
    Task<ResultadoProceso> EjecutarAsync(SolicitudProceso solicitud, CancellationToken ct);
}

internal enum EstadoServicio
{
    Detenido,
    Iniciando,
    EnEjecucion,
    Deteniendose,
    Otro
}

internal sealed record DefinicionServicio(string Nombre, string Visible, string Descripcion, string Ejecutable,
    IReadOnlyList<string> Argumentos, string Cuenta, IReadOnlyList<string> Dependencias)
{
    // Comando tal como lo guarda el administrador de servicios: ruta y argumentos entrecomillados.
    public string LineaComando => string.Join(' ', new[] { Ejecutable }.Concat(Argumentos).Select(Entrecomillar));

    public static string Entrecomillar(string valor)
    {
        if (valor.Contains('"')) throw new ArgumentException("Un argumento de servicio no puede contener comillas.");
        return $"\"{valor}\"";
    }
}

internal sealed record InfoServicio(string Nombre, string LineaComando, string Cuenta, bool InicioAutomatico, EstadoServicio Estado)
{
    // Nombre que muestra Windows; null si el adaptador no lo informa.
    public string? Visible { get; init; }
    // Sin comillas no se puede saber dónde termina una ruta con espacios: se toma hasta el primer espacio y no coincidirá con la propia.
    public string Ejecutable => LineaComando.StartsWith('"')
        ? LineaComando[1..].Split('"')[0]
        : LineaComando.Split(' ')[0];
}

internal interface IServiciosWindows
{
    InfoServicio? Consultar(string nombre);
    void CrearOActualizar(DefinicionServicio definicion);
    Task IniciarAsync(string nombre, TimeSpan limite, CancellationToken ct);
    Task DetenerAsync(string nombre, TimeSpan limite, CancellationToken ct);
    EntornoServicio LeerEntorno(string nombre);
    void GuardarEntorno(string nombre, IReadOnlyDictionary<string, string> valores);
    // Marca el servicio para eliminarlo; quien llama ya comprobó que es propio, que su comando no cambió y que está detenido.
    void Eliminar(string nombre);
}

internal sealed record EntornoServicio(IReadOnlyDictionary<string, string> Valores, bool Protegido)
{
    public override string ToString() => $"Entorno de servicio: {Valores.Count} variables; protegido: {Protegido}";
}

internal enum DerechoAcceso
{
    Lectura,
    Modificacion,
    Control
}

internal sealed record ReglaAcceso(string Identidad, DerechoAcceso Derecho);

// Reglas es null cuando la ACL hereda del padre o tiene entradas fuera del modelo (denegaciones, derechos parciales).
internal sealed record AclLeida(string Propietario, IReadOnlyList<ReglaAcceso>? Reglas)
{
    public bool Coincide(IReadOnlyList<ReglaAcceso> esperadas) =>
        Propietario is IdentidadServicio.Administradores or IdentidadServicio.Sistema
        && Reglas is not null && Reglas.ToHashSet().SetEquals(esperadas);
}

// Una ACL protegida (sin herencia del padre), con propietario Administradores y exactamente estas reglas. Restringir crea la carpeta
// si falta, con esa ACL desde su creación; la carpeta que la contiene debe existir.
internal interface IPermisos
{
    void Restringir(string ruta, IReadOnlyList<ReglaAcceso> reglas);
    AclLeida? Leer(string ruta);
}

internal interface IAlmacenConfianza
{
    bool Contiene(string huellaSha256);
    void Agregar(X509Certificate2 certificado);
}

internal interface IProteccionLocal
{
    byte[] Proteger(byte[] datos);
    byte[] Desproteger(byte[] datos);
}

internal sealed record DatosEquipo(bool Windows64, Version VersionWindows, bool Administrador);

internal interface IEquipo
{
    DatosEquipo Describir();
    long BytesLibres(string ruta);
    // Identifica el volumen para comprobar que la clave de recuperación no queda en el mismo disco que el servidor.
    string Unidad(string ruta);
    bool PuertoDisponible(IPAddress direccion, int puerto);
    bool DireccionLocal(IPAddress direccion);
    // Motivo legible si alguna carpeta superior permite a una identidad sin administración sustituir la carpeta indicada.
    string? AncestroInseguro(string ruta);
    string? DirectorioEscritorioComun();
    string? DirectorioMenuInicioComun();
}

internal interface IAccesosDirectos
{
    void Crear(string archivo, string destino, string argumentos, string directorioTrabajo, string descripcion);
    (string Destino, string Argumentos)? Leer(string archivo);
}

internal sealed record SistemaInstalacion(
    IProcesos Procesos,
    IServiciosWindows Servicios,
    IPermisos Permisos,
    IAlmacenConfianza Confianza,
    IFirewall Firewall,
    IProteccionLocal Proteccion,
    IEquipo Equipo,
    IAccesosDirectos Accesos,
    IClienteServidor Cliente,
    IMotorDatos Motor,
    Func<DateTimeOffset> Reloj,
    Func<TimeSpan, CancellationToken, Task> Esperar,
    IRegistroAplicaciones Aplicaciones)
{
    // Ejecutable que se copia como herramienta protegida de retirada; null fuera de la distribución de archivo único.
    public string? Herramienta { get; init; }
}
