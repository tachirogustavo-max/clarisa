using System.Net;

namespace Clarisa.Instalacion.Contratos;

public enum ModoInstalacion
{
    Servidor,
    Estacion
}

public enum AutomatizacionCopias
{
    // Sin política acordada de frecuencia, retención y equipo apagado no se registra ninguna tarea.
    Deshabilitada
}

public abstract record SolicitudInstalacion(string OrigenPaquete, string DirectorioPrograma)
{
    public abstract ModoInstalacion Modo { get; }
}

// NombreConexion es el nombre DNS o la IP que escribirán las estaciones; debe coincidir con el SAN del certificado.
public sealed record SolicitudServidor(
    string OrigenPaquete,
    string DirectorioPrograma,
    string DirectorioDatos,
    string NombreConexion,
    IPAddress DireccionEscucha,
    int PuertoHttps,
    int PuertoMotor,
    string DestinoClaveRecuperacion,
    string UsuarioPrimeraCuenta,
    AutomatizacionCopias Automatizacion = AutomatizacionCopias.Deshabilitada)
    : SolicitudInstalacion(OrigenPaquete, DirectorioPrograma)
{
    public override ModoInstalacion Modo => ModoInstalacion.Servidor;
}

// La huella SHA-256 la muestra el resumen del servidor; la estación solo confía en el certificado que coincide con ella.
public sealed record SolicitudEstacion(
    string OrigenPaquete,
    string DirectorioPrograma,
    string DireccionServidor,
    string ArchivoCertificado,
    string HuellaCertificado,
    bool AccesoEnEscritorio = true)
    : SolicitudInstalacion(OrigenPaquete, DirectorioPrograma)
{
    public override ModoInstalacion Modo => ModoInstalacion.Estacion;
}

public sealed record ProblemaSolicitud(string Campo, string Mensaje);
