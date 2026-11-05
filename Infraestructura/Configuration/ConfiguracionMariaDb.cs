using System.Net;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace Clarisa.Infrastructure.Configuration;

public sealed partial record ConfiguracionMariaDb
{
    public required string Servidor { get; init; }
    public int Puerto { get; init; } = 3306;
    public required string Base { get; init; }
    public required string Usuario { get; init; }
    public required string Clave { get; init; }
    public int SegundosConexion { get; init; } = 5;
    public int SegundosComando { get; init; } = 30;
    public int SegundosEsperaBloqueo { get; init; } = 10;
    public int MaximoConexiones { get; init; } = 20;

    // La base vive en el mismo servidor que la API; las estaciones nunca se conectan a ella.
    public void Validar()
    {
        if (!EsLocal(Servidor))
            throw new ArgumentException("El servidor de base de datos debe ser local (127.0.0.1, ::1 o localhost).", nameof(Servidor));
        if (Puerto is < 1 or > 65535) throw new ArgumentException("El puerto no es válido.", nameof(Puerto));
        if (Base is null || !NombreBase().IsMatch(Base))
            throw new ArgumentException("La base debe usar de 1 a 64 letras minúsculas, números o guiones bajos.", nameof(Base));
        if (string.IsNullOrWhiteSpace(Usuario) || Usuario.Length > 80) throw new ArgumentException("Indica el usuario.", nameof(Usuario));
        if (string.IsNullOrEmpty(Clave)) throw new ArgumentException("Indica la clave del usuario.", nameof(Clave));
        if (SegundosConexion is < 1 or > 60) throw new ArgumentException("Usa de 1 a 60 segundos.", nameof(SegundosConexion));
        if (SegundosComando is < 1 or > 300) throw new ArgumentException("Usa de 1 a 300 segundos.", nameof(SegundosComando));
        if (SegundosEsperaBloqueo < 1 || SegundosEsperaBloqueo >= SegundosComando)
            throw new ArgumentException("La espera de bloqueo debe ser de al menos 1 segundo y menor que el tiempo de comando.",
                nameof(SegundosEsperaBloqueo));
        if (MaximoConexiones is < 1 or > 200) throw new ArgumentException("Usa de 1 a 200 conexiones.", nameof(MaximoConexiones));
    }

    public override string ToString() => $"MariaDB {Servidor}:{Puerto}/{Base} (usuario {Usuario})";

    internal string CadenaConexion()
    {
        Validar();
        return new MySqlConnectionStringBuilder
        {
            Server = Servidor,
            Port = (uint)Puerto,
            Database = Base,
            UserID = Usuario,
            Password = Clave,
            Pooling = true,
            MinimumPoolSize = 0,
            MaximumPoolSize = (uint)MaximoConexiones,
            ConnectionTimeout = (uint)SegundosConexion,
            DefaultCommandTimeout = (uint)SegundosComando,
            ConnectionReset = true,
            IgnoreCommandTransaction = false,
            UseAffectedRows = false,
            AllowUserVariables = false,
            AllowLoadLocalInfile = false,
            AllowPublicKeyRetrieval = false,
            GuidFormat = MySqlGuidFormat.Char36,
            CharacterSet = "utf8mb4",
            ApplicationName = "Clarisa"
        }.ConnectionString;
    }

    private static bool EsLocal(string? servidor) =>
        string.Equals(servidor, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(servidor, out var direccion) && IPAddress.IsLoopback(direccion));

    [GeneratedRegex("^[a-z0-9_]{1,64}$")]
    private static partial Regex NombreBase();
}
