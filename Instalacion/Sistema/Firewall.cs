using System.Net;

namespace Clarisa.Instalacion.Sistema;

internal enum PerfilRed
{
    Dominio = 1,
    Privado = 2,
    Publico = 4
}

// Valores tal como los guarda el firewall de Windows, sin textos localizados. Remotas "LocalSubnet" limita la entrada a la red local.
internal sealed record ReglaFirewall(string Nombre, string Programa, int Puerto, IPAddress DireccionLocal)
{
    public const string GrupoPropio = "Clarisa Estudios";
    public const int Tcp = 6;

    public int Protocolo { get; init; } = Tcp;
    public bool Entrada { get; init; } = true;
    public bool Permitir { get; init; } = true;
    public bool Habilitada { get; init; } = true;
    public int Perfiles { get; init; } = (int)(PerfilRed.Dominio | PerfilRed.Privado);
    public string Remotas { get; init; } = "LocalSubnet";
    public string Grupo { get; init; } = GrupoPropio;

    public string? Diferencia(ReglaFirewall esperada)
    {
        if (!string.Equals(Programa, esperada.Programa, StringComparison.OrdinalIgnoreCase)) return "otro programa";
        if (Puerto != esperada.Puerto || Protocolo != esperada.Protocolo) return "otro puerto o protocolo";
        if (!DireccionLocal.Equals(esperada.DireccionLocal)) return "otra dirección local";
        if (!Entrada || !Permitir || !Habilitada) return "no es una regla de entrada habilitada que permita";
        if (Perfiles != esperada.Perfiles) return "otros perfiles de red";
        if (!string.Equals(Remotas, esperada.Remotas, StringComparison.OrdinalIgnoreCase)) return "otros equipos remotos";
        return string.Equals(Grupo, esperada.Grupo, StringComparison.Ordinal) ? null : "otro grupo";
    }
}

internal interface IFirewall
{
    // Todas las reglas con ese nombre: más de una impide decidir cuál es la propia.
    IReadOnlyList<ReglaFirewall> Consultar(string nombre);
    void Crear(ReglaFirewall regla);
    // Sustituye los valores de la única regla con ese nombre; falla si hay cero o varias.
    void Actualizar(ReglaFirewall regla);
    // Retira la única regla con ese nombre; falla si hay cero o varias.
    void Eliminar(string nombre);
    int PerfilesActivos();
}
