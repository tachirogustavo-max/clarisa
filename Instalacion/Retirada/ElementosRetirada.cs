using Clarisa.Instalacion.Contratos;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Pasos.Servidor;
using Clarisa.Instalacion.Registro;
using Clarisa.Instalacion.Sistema;

namespace Clarisa.Instalacion.Retirada;

// Lo que la interfaz enumera antes de confirmar. Lo conservado se dice siempre: no se promete que no quede ningún archivo.
internal static class ElementosRetirada
{
    public static IReadOnlyList<ElementoRetirada> Describir(ReferenciaRetirada referencia, EstadoPersistido? estado, string carpeta, string registros)
    {
        var lista = new List<ElementoRetirada>();
        void Retira(string categoria, string descripcion, string? ubicacion) => lista.Add(new(AccionRetirada.SeRetira, categoria, descripcion, ubicacion));
        void Conserva(string categoria, string descripcion, string? ubicacion) => lista.Add(new(AccionRetirada.SeConserva, categoria, descripcion, ubicacion));
        var programa = referencia.Solicitud.Programa;
        if (referencia.Modo == ModoInstalacion.Servidor)
        {
            var rutas = new RutasServidor((SolicitudServidor)referencia.Reconstruir(""));
            Retira("servicio", "Servicio del servidor del liceo (se detiene y se elimina)", RutasServidor.ServicioApi);
            Retira("servicio", "Servicio del motor de datos (se apaga de forma ordenada y se elimina)", RutasServidor.ServicioMotor);
            Retira("firewall", "Regla de entrada propia del servidor, si no fue modificada", PasoRedLocal.NombreRegla);
            Retira("programas", "Programas instalados que no cambiaron", programa);
            Conserva("copia", "Copia cifrada y verificada tomada justo antes de retirar", Path.Combine(rutas.Respaldos, "copias"));
            Conserva("datos", "Datos del motor (la base del liceo)", rutas.DatosMotor);
            Conserva("configuracion", "Configuración protegida del servidor", rutas.ArchivoConfiguracion);
            Conserva("certificado", "Certificado HTTPS y su clave privada", rutas.Certificado);
            Conserva("claves", "Clave de respaldos y credencial de administración del motor", rutas.Clave);
            Conserva("respaldos", "Copias de seguridad existentes", rutas.Respaldos);
            Conserva("datos", "Resto de la carpeta de datos del servidor", rutas.Datos);
        }
        else
        {
            Retira("accesos", "Accesos directos de la aplicación creados por la instalación", null);
            Retira("programas", "Programas instalados que no cambiaron", programa);
            var huellas = estado?.Artefactos.Where(a => a.Tipo == TipoArtefacto.CertificadoConfianza).Select(a => a.Identificador).ToList() ?? [];
            Conserva("certificado", "Certificado del servidor en Raíces de confianza del equipo" +
                                    (huellas.Count == 0 ? " (no se añadió ninguno)" : " (huella " + string.Join(", ", huellas) + ")"), null);
        }
        Retira("registro-windows", "Entrada en Aplicaciones instaladas (al final, si todo lo anterior terminó)", RegistroRetirada.Clave(referencia.Modo));
        Conserva("programas", "Archivos ajenos o modificados dentro de la carpeta de programas, y la carpeta si no queda vacía", programa);
        Conserva("registros", "Registros y estado de la instalación", registros);
        Conserva("herramienta", "Herramienta protegida de retirada, su referencia" +
                                (referencia.Modo == ModoInstalacion.Servidor ? " y el mantenimiento conservado para restaurar la copia" : ""), carpeta);
        return lista;
    }

    public static IReadOnlyList<string> Advertencias(ReferenciaRetirada referencia) => referencia.Modo == ModoInstalacion.Servidor
        ?
        [
            "Las estaciones dejarán de poder conectarse a este servidor.",
            $"No se borra la base ni las copias. Para volver a usar estos datos reinstala la misma versión ({referencia.Version}); otro " +
            "paquete se rechazará hasta que la reinstales o restaures la copia en una instalación nueva.",
            "Cierra las ventanas y consolas que usen archivos del servidor antes de continuar."
        ]
        :
        [
            "Cierra la aplicación de escritorio en este equipo antes de continuar.",
            "Se conserva el certificado de confianza del servidor; un administrador puede retirarlo si nada más lo necesita."
        ];
}
