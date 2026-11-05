using System.Collections;
using System.Net;
using System.Runtime.InteropServices;

namespace Clarisa.Instalacion.Sistema.Windows;

// API COM del firewall (INetFwPolicy2): lee y escribe valores tipados en lugar de interpretar la salida localizada de netsh.
internal sealed class FirewallWindows : IFirewall
{
    private const int DireccionEntrada = 1;
    private const int AccionPermitir = 1;

    public IReadOnlyList<ReglaFirewall> Consultar(string nombre) =>
        ConPolitica(politica => Reglas(politica, nombre, regla => Leer(nombre, regla)));

    public void Crear(ReglaFirewall regla) => ConPolitica(objeto =>
    {
        dynamic politica = objeto;
        dynamic nueva = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule", throwOnError: true)!)!;
        try
        {
            Escribir(nueva, regla);
            dynamic reglas = politica.Rules;
            try
            {
                reglas.Add(nueva);
            }
            finally
            {
                Liberar(reglas);
            }
        }
        finally
        {
            Liberar(nueva);
        }
        return 0;
    });

    public void Actualizar(ReglaFirewall regla) => ConPolitica(politica =>
    {
        var existentes = Reglas(politica, regla.Nombre, _ => true).Count;
        if (existentes != 1)
            throw new Ejecucion.FalloInstalacionException($"Se esperaba una sola regla «{regla.Nombre}» y hay {existentes}; no se modifica ninguna.");
        return Reglas(politica, regla.Nombre, existente =>
        {
            Escribir(existente, regla);
            return true;
        }).Count;
    });

    public void Eliminar(string nombre) => ConPolitica(politica =>
    {
        var existentes = Reglas(politica, nombre, _ => true).Count;
        if (existentes != 1)
            throw new Ejecucion.FalloInstalacionException($"Se esperaba una sola regla «{nombre}» y hay {existentes}; no se retira ninguna.");
        dynamic reglas = ((dynamic)politica).Rules;
        try
        {
            reglas.Remove(nombre);
        }
        finally
        {
            Liberar(reglas);
        }
        return 0;
    });

    public int PerfilesActivos() => ConPolitica(objeto => (int)((dynamic)objeto).CurrentProfileTypes);

    internal static ReglaFirewall Convertir(string nombre, string? programa, int protocolo, string? puertos, string? locales, string? remotas,
        int direccion, int accion, bool habilitada, int perfiles, string? grupo)
    {
        var puerto = int.TryParse(puertos, out var numero) ? numero : 0;
        var partes = (locales ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var local = partes.Length == 1 && IPAddress.TryParse(partes[0].Split('/')[0], out var ip) ? ip : IPAddress.None;
        return new ReglaFirewall(nombre, programa ?? "", puerto, local)
        {
            Protocolo = protocolo,
            Entrada = direccion == DireccionEntrada,
            Permitir = accion == AccionPermitir,
            Habilitada = habilitada,
            Perfiles = perfiles,
            Remotas = remotas ?? "",
            Grupo = grupo ?? ""
        };
    }

    private static ReglaFirewall Leer(string nombre, object objeto)
    {
        dynamic regla = objeto;
        return Convertir(nombre, (string?)regla.ApplicationName, (int)regla.Protocol, (string?)regla.LocalPorts, (string?)regla.LocalAddresses,
            (string?)regla.RemoteAddresses, (int)regla.Direction, (int)regla.Action, (bool)regla.Enabled, (int)regla.Profiles,
            (string?)regla.Grouping);
    }

    // El protocolo se fija antes que el puerto: el firewall rechaza puertos en reglas sin protocolo TCP/UDP.
    private static void Escribir(object objeto, ReglaFirewall regla)
    {
        dynamic destino = objeto;
        destino.Name = regla.Nombre;
        destino.Description = "Entrada HTTPS del servidor del liceo desde la red local.";
        destino.ApplicationName = regla.Programa;
        destino.Protocol = regla.Protocolo;
        destino.LocalPorts = regla.Puerto.ToString(System.Globalization.CultureInfo.InvariantCulture);
        destino.LocalAddresses = regla.DireccionLocal.ToString();
        destino.RemoteAddresses = regla.Remotas;
        destino.Direction = regla.Entrada ? DireccionEntrada : 2;
        destino.Action = regla.Permitir ? AccionPermitir : 0;
        destino.Profiles = regla.Perfiles;
        destino.Grouping = regla.Grupo;
        destino.Enabled = regla.Habilitada;
    }

    private static List<T> Reglas<T>(object politica, string nombre, Func<object, T> accion)
    {
        var resultado = new List<T>();
        dynamic reglas = ((dynamic)politica).Rules;
        try
        {
            foreach (var elemento in (IEnumerable)reglas)
            {
                try
                {
                    if (string.Equals((string?)((dynamic)elemento).Name, nombre, StringComparison.Ordinal)) resultado.Add(accion(elemento));
                }
                finally
                {
                    Liberar(elemento);
                }
            }
        }
        finally
        {
            Liberar(reglas);
        }
        return resultado;
    }

    private static T ConPolitica<T>(Func<object, T> accion)
    {
        var tipo = Type.GetTypeFromProgID("HNetCfg.FwPolicy2")
                   ?? throw new Ejecucion.FalloInstalacionException("El firewall de Windows no está disponible en este equipo.");
        var politica = Activator.CreateInstance(tipo)!;
        try
        {
            return accion(politica);
        }
        catch (COMException error)
        {
            throw new Ejecucion.FalloInstalacionException($"El firewall de Windows rechazó la operación (0x{error.HResult:X8}).", error);
        }
        finally
        {
            Liberar(politica);
        }
    }

    private static void Liberar(object? com)
    {
        if (com is not null && Marshal.IsComObject(com)) Marshal.FinalReleaseComObject(com);
    }
}
