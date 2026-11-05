using Clarisa.Api.Security;

namespace Clarisa.Api.Configuration;

public enum ModoServidor
{
    Ejecutar,
    Migrar,
    CrearAdministrador,
    CrearAdministradorEntrada
}

public sealed class ArgumentosServidorException(string mensaje) : Exception(mensaje);

// Solo se admiten el modo y la ruta del archivo protegido: así ningún valor de configuración, y menos una clave, llega por argumentos.
public sealed record OrdenServidor(ModoServidor Modo, string RutaConfiguracion)
{
    public bool DesdeEntorno { get; init; }
    public bool DesdeServicio { get; init; }
    public const string Configuracion = "--configuracion";
    public const string Migrar = "--migrar";

    public const string Uso = "Uso: Clarisa.Api (--configuracion <ruta absoluta> | --entorno | --entorno-servicio) [--migrar | --crear-administrador | --crear-administrador-entrada]";

    public static OrdenServidor Interpretar(IReadOnlyList<string> argumentos)
    {
        string? ruta = null;
        var entorno = false;
        var servicio = false;
        var modo = ModoServidor.Ejecutar;
        for (var indice = 0; indice < argumentos.Count; indice++)
        {
            switch (argumentos[indice])
            {
                case "--entorno":
                    if (entorno || servicio || ruta is not null) throw new ArgumentosServidorException("Indica una sola fuente de configuración.");
                    entorno = true;
                    break;
                case "--entorno-servicio":
                    if (entorno || servicio || ruta is not null) throw new ArgumentosServidorException("Indica una sola fuente de configuración.");
                    servicio = true;
                    break;
                case Configuracion:
                    if (ruta is not null || entorno || servicio) throw new ArgumentosServidorException("Indica una sola fuente de configuración.");
                    if (indice + 1 >= argumentos.Count || argumentos[indice + 1].StartsWith("--", StringComparison.Ordinal))
                        throw new ArgumentosServidorException("Falta la ruta de --configuracion.");
                    ruta = argumentos[++indice];
                    break;
                case Migrar:
                    modo = Elegir(modo, ModoServidor.Migrar);
                    break;
                case PreparacionAdministrador.Argumento:
                    modo = Elegir(modo, ModoServidor.CrearAdministrador);
                    break;
                case PreparacionAdministradorEntrada.Argumento:
                    modo = Elegir(modo, ModoServidor.CrearAdministradorEntrada);
                    break;
                default:
                    throw new ArgumentosServidorException("Argumento no admitido. Los valores de configuración van en el archivo protegido.");
            }
        }
        if (ruta is null && !entorno && !servicio) throw new ArgumentosServidorException("Indica --configuracion, --entorno o --entorno-servicio.");
        return new(modo, ruta ?? "") { DesdeEntorno = entorno, DesdeServicio = servicio };
    }

    private static ModoServidor Elegir(ModoServidor actual, ModoServidor nuevo) => actual == ModoServidor.Ejecutar
        ? nuevo
        : throw new ArgumentosServidorException("Indica un solo modo: ejecuta la migración y la preparación de cuentas por separado.");
}
