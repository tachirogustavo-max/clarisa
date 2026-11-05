using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Pasos.Estacion;
using Clarisa.Instalacion.Registro;

namespace Clarisa.Instalacion.Pasos.Servidor;

// Certificado propio del servidor (no de autoridad): SAN con el nombre de conexión y la IP de escucha, solo autenticación de servidor.
// La clave privada queda en PEM dentro de la carpeta cuya ACL solo permite leer a la cuenta del servicio, Administradores y SYSTEM.
internal sealed class PasoCertificado : IPasoInstalacion
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromDays(3 * 365);
    private static readonly TimeSpan MargenMinimo = TimeSpan.FromDays(30);

    public string Id => "certificado";
    public string Nombre => "Certificado HTTPS del servidor";
    public string Descripcion => "Genera el certificado con el nombre de conexión y exporta su parte pública para las estaciones.";
    public bool Critico => true;
    public bool Privilegiado => false;

    public Task<Comprobacion> ComprobarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        if (!File.Exists(rutas.ArchivoCertificado) || !File.Exists(rutas.ArchivoClaveCertificado) || !File.Exists(rutas.ArchivoCertificadoPublico))
            return Task.FromResult(Comprobacion.NoSatisfecha("Faltan archivos del certificado."));
        X509Certificate2 certificado, publico;
        try
        {
            certificado = Cargar(rutas);
        }
        catch (FalloInstalacionException error)
        {
            // Un corte entre la escritura de la clave y la del certificado deja un par incoherente: se regenera, no se bloquea.
            return Task.FromResult(Comprobacion.NoSatisfecha(error.Message));
        }
        try
        {
            publico = X509CertificateLoader.LoadCertificateFromFile(rutas.ArchivoCertificadoPublico);
        }
        catch (CryptographicException)
        {
            certificado.Dispose();
            return Task.FromResult(Comprobacion.NoSatisfecha("El certificado exportado para las estaciones está dañado."));
        }
        using var _ = certificado;
        using var __ = publico;
        var ahora = contexto.Sistema.Reloj();
        if (publico.Thumbprint != certificado.Thumbprint)
            return Task.FromResult(Comprobacion.NoSatisfecha("El certificado exportado para estaciones no coincide con el del servidor."));
        if (certificado.NotAfter - ahora < MargenMinimo)
            return Task.FromResult(Comprobacion.NoSatisfecha("El certificado vence en menos de 30 días."));
        if (PasoConfianzaCertificado.Problema(publico, PasoConfianzaCertificado.Huella(publico), rutas.DireccionPublica, ahora) is { } problema)
            return Task.FromResult(Comprobacion.NoSatisfecha(problema));
        if (!certificado.MatchesHostname(contexto.Servidor.DireccionEscucha.ToString(), allowWildcards: false))
            return Task.FromResult(Comprobacion.NoSatisfecha("El certificado no incluye la IP de escucha."));
        var huella = PasoConfianzaCertificado.Huella(publico);
        contexto.Hechos["huella-certificado"] = huella;
        return Task.FromResult(Comprobacion.Satisfecha(
            $"SAN {rutas.DireccionPublica.IdnHost}, vence {certificado.NotAfter:yyyy-MM-dd}. Huella SHA-256 para las estaciones: {huella}. " +
            $"Archivo para estaciones: {rutas.ArchivoCertificadoPublico}"));
    }

    public Task AplicarAsync(ContextoEjecucion contexto, CancellationToken ct)
    {
        var rutas = contexto.RutasServidor;
        using var certificado = Generar(contexto.Servidor.NombreConexion, contexto.Servidor.DireccionEscucha, contexto.Sistema.Reloj());
        using var clave = certificado.GetRSAPrivateKey()!;
        EscrituraSegura.ReemplazarTexto(rutas.ArchivoClaveCertificado, clave.ExportPkcs8PrivateKeyPem());
        EscrituraSegura.ReemplazarTexto(rutas.ArchivoCertificado, certificado.ExportCertificatePem());
        var publico = certificado.Export(X509ContentType.Cert);
        EscrituraSegura.Reemplazar(rutas.ArchivoCertificadoPublico, flujo => flujo.Write(publico));
        contexto.Propio(TipoArtefacto.Archivo, rutas.ArchivoCertificado);
        contexto.Propio(TipoArtefacto.Archivo, rutas.ArchivoClaveCertificado);
        contexto.Propio(TipoArtefacto.Archivo, rutas.ArchivoCertificadoPublico);
        return Task.CompletedTask;
    }

    internal static X509Certificate2 Cargar(RutasServidor rutas)
    {
        try
        {
            return X509Certificate2.CreateFromPemFile(rutas.ArchivoCertificado, rutas.ArchivoClaveCertificado);
        }
        catch (CryptographicException error)
        {
            throw new FalloInstalacionException("El certificado o su clave privada están dañados o no corresponden entre sí.", error);
        }
    }

    internal static X509Certificate2 Generar(string nombre, IPAddress escucha, DateTimeOffset ahora)
    {
        using var rsa = RSA.Create(3072);
        var solicitud = new CertificateRequest($"CN={nombre}, O=Clarisa Estudios", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        if (IPAddress.TryParse(nombre, out var ip)) san.AddIpAddress(ip);
        else san.AddDnsName(nombre);
        if (!escucha.Equals(ip)) san.AddIpAddress(escucha);
        solicitud.CertificateExtensions.Add(san.Build(critical: false));
        solicitud.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, critical: true));
        solicitud.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
        solicitud.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false));
        solicitud.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(solicitud.PublicKey, critical: false));
        return solicitud.CreateSelfSigned(ahora.AddMinutes(-10), ahora.Add(Vigencia));
    }
}
