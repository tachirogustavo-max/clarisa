namespace Clarisa.Instalacion.Sistema;

// Entrada de «Aplicaciones instaladas» de Windows. Clave es el nombre de la subclave bajo Uninstall; nunca contiene secretos.
internal sealed record EntradaAplicacion(string Clave, string Nombre, string Version, string Icono, string Ubicacion, string Desinstalar,
    int TamanoKb);

internal interface IRegistroAplicaciones
{
    EntradaAplicacion? Leer(string clave);
    void Escribir(EntradaAplicacion entrada);
    void Eliminar(string clave);
}

internal static class RegistroRetirada
{
    public const string ClaveServidor = "Clarisa.Estudios.Servidor";
    public const string ClaveEstacion = "Clarisa.Estudios.Estacion";

    public static string Clave(Contratos.ModoInstalacion modo) =>
        modo == Contratos.ModoInstalacion.Servidor ? ClaveServidor : ClaveEstacion;

    public static string Comando(string herramienta, string referencia) =>
        $"\"{herramienta}\" {Contratos.ArgumentosRetirada.Opcion} \"{referencia}\"";
}
