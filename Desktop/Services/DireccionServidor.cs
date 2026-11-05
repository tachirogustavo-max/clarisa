namespace Clarisa.Desktop.Services;
public static class DireccionServidor
{
    public const string Predeterminada = "https://localhost:5189";
    public static (Uri? Direccion, string? Error) Validar(string texto)
    {
        if (!Uri.TryCreate(texto.Trim(), UriKind.Absolute, out var direccion) || (direccion.Scheme != Uri.UriSchemeHttps && direccion.Scheme != Uri.UriSchemeHttp) || direccion.UserInfo.Length > 0 || direccion.Query.Length > 0 || direccion.Fragment.Length > 0 || direccion.AbsolutePath != "/")
            return (null, "Indica la dirección completa del servidor, sin rutas ni credenciales.");
        if (direccion.Scheme == Uri.UriSchemeHttp && !direccion.IsLoopback)
            return (null, "Las estaciones deben usar una conexión HTTPS al servidor.");
        return (direccion, null);
    }
}
