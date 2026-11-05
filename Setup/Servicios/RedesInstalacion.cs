using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Clarisa.Setup.Servicios;

// Automatica: la dirección la entrega el router (DHCP) y puede cambiar al reiniciar.
public sealed record RedInstalacion(string Nombre, string Direccion, bool Automatica = false)
{
    public string NombreVisible => Nombre + " · " + Direccion;
    public override string ToString() => NombreVisible;
}
public static class RedesInstalacion
{
    public static IReadOnlyList<RedInstalacion> Detectar()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(r => r.OperationalStatus == OperationalStatus.Up
                    && r.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211
                    && !new[] { "vpn", "nord", "tap", "tun", "virtual", "hyper-v" }.Any(p => r.Name.Contains(p, StringComparison.OrdinalIgnoreCase)))
                .SelectMany(r => r.GetIPProperties().UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address)
                        && !a.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                    .Select(a => new RedInstalacion(r.Name, a.Address.ToString(), EsAutomatica(a))))
                .DistinctBy(r => r.Direccion).OrderBy(r => r.Nombre).ToArray();
        }
        catch (NetworkInformationException) { return []; }
    }

    private static bool EsAutomatica(UnicastIPAddressInformation direccion)
    {
        try { return direccion.PrefixOrigin == PrefixOrigin.Dhcp; }
        catch (PlatformNotSupportedException) { return false; }
    }
}
