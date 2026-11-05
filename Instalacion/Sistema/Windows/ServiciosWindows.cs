using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace Clarisa.Instalacion.Sistema.Windows;

// API tipada del administrador de servicios: crear/actualizar con CreateService/ChangeServiceConfig y leer con QueryServiceConfig.
// Las cuentas virtuales NT SERVICE\<nombre> no llevan contraseña.
internal sealed partial class ServiciosWindows : IServiciosWindows
{
    public EntornoServicio LeerEntorno(string nombre) => EntornoServiciosWindows.Leer(nombre);
    public void GuardarEntorno(string nombre, IReadOnlyDictionary<string, string> valores) => EntornoServiciosWindows.Guardar(nombre, valores);
    private const uint AccesoAdministrador = 0x0002 | 0x0001;
    private const uint AccesoServicio = 0xF01FF;
    private const uint ConsultarConfiguracion = 0x0001;
    private const uint ServicioPropio = 0x10;
    private const uint InicioAutomatico = 0x2;
    private const uint ErrorNormal = 0x1;
    private const int NoExiste = 1060;
    private const int MarcadoParaEliminar = 1072;
    private const uint AccesoEliminar = 0x10000;
    private const int InsuficienteBuffer = 122;
    private const uint NivelDescripcion = 1;
    private const uint NivelSid = 5;
    private const uint SidSinRestriccion = 1;

    public InfoServicio? Consultar(string nombre)
    {
        using var administrador = Abrir(ConsultarConfiguracion);
        var servicio = OpenServiceW(administrador, nombre, ConsultarConfiguracion);
        if (servicio == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            if (error == NoExiste) return null;
            throw new Win32Exception(error, $"No se pudo consultar el servicio {nombre}.");
        }
        try
        {
            QueryServiceConfigW(servicio, IntPtr.Zero, 0, out var necesario);
            if (Marshal.GetLastWin32Error() != InsuficienteBuffer) throw new Win32Exception(Marshal.GetLastWin32Error());
            var memoria = Marshal.AllocHGlobal((int)necesario);
            try
            {
                if (!QueryServiceConfigW(servicio, memoria, necesario, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
                var config = Marshal.PtrToStructure<ConfiguracionServicio>(memoria);
                using var controlador = new ServiceController(nombre);
                return new InfoServicio(nombre, Marshal.PtrToStringUni(config.Ruta) ?? "", Marshal.PtrToStringUni(config.Cuenta) ?? "",
                    config.TipoInicio == InicioAutomatico, Traducir(controlador.Status)) { Visible = Marshal.PtrToStringUni(config.Visible) };
            }
            finally
            {
                Marshal.FreeHGlobal(memoria);
            }
        }
        finally
        {
            CloseServiceHandle(servicio);
        }
    }

    public void CrearOActualizar(DefinicionServicio definicion)
    {
        using var administrador = Abrir(AccesoAdministrador);
        var dependencias = definicion.Dependencias.Count == 0 ? null : string.Join('\0', definicion.Dependencias) + "\0\0";
        var servicio = OpenServiceW(administrador, definicion.Nombre, AccesoServicio);
        if (servicio == IntPtr.Zero)
        {
            servicio = CreateServiceW(administrador, definicion.Nombre, definicion.Visible, AccesoServicio, ServicioPropio, InicioAutomatico,
                ErrorNormal, definicion.LineaComando, null, IntPtr.Zero, dependencias, definicion.Cuenta, null);
            if (servicio == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), $"No se pudo registrar {definicion.Nombre}.");
        }
        else if (!ChangeServiceConfigW(servicio, ServicioPropio, InicioAutomatico, ErrorNormal, definicion.LineaComando, null, IntPtr.Zero,
                     dependencias, definicion.Cuenta, null, definicion.Visible))
        {
            var codigo = Marshal.GetLastWin32Error();
            CloseServiceHandle(servicio);
            throw new Win32Exception(codigo, $"No se pudo actualizar {definicion.Nombre}.");
        }
        try
        {
            var descripcion = new DescripcionServicio { Texto = Marshal.StringToHGlobalUni(definicion.Descripcion) };
            try
            {
                if (!ChangeServiceConfig2W(servicio, NivelDescripcion, ref descripcion))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"No se pudo describir {definicion.Nombre}.");
            }
            finally
            {
                Marshal.FreeHGlobal(descripcion.Texto);
            }
            // La ACL de datos se concede al SID del servicio: debe existir en su token.
            var tipoSid = SidSinRestriccion;
            if (!ChangeServiceConfig2Sid(servicio, NivelSid, ref tipoSid))
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"No se pudo fijar la identidad de {definicion.Nombre}.");
            FijarReintentos(servicio, definicion.Nombre);
        }
        finally
        {
            CloseServiceHandle(servicio);
        }
    }

    public void Eliminar(string nombre)
    {
        using var administrador = Abrir(ConsultarConfiguracion);
        var servicio = OpenServiceW(administrador, nombre, AccesoEliminar);
        if (servicio == IntPtr.Zero)
        {
            if (Marshal.GetLastWin32Error() == NoExiste) return;
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"No se pudo abrir {nombre} para retirarlo.");
        }
        try
        {
            // Ya marcado para eliminar (1072): desaparecerá cuando se cierren los manejadores abiertos.
            if (!DeleteService(servicio) && Marshal.GetLastWin32Error() is var codigo && codigo != MarcadoParaEliminar)
                throw new Win32Exception(codigo, $"No se pudo retirar {nombre}.");
        }
        finally
        {
            CloseServiceHandle(servicio);
        }
    }

    public async Task IniciarAsync(string nombre, TimeSpan limite, CancellationToken ct)
    {
        // Las instalaciones anteriores no tenían reinicio automático: se fija también al iniciar, sin esperar a recrear el servicio.
        using (var administrador = Abrir(AccesoAdministrador))
        {
            var servicio = OpenServiceW(administrador, nombre, AccesoServicio);
            if (servicio != IntPtr.Zero)
            {
                try { FijarReintentos(servicio, nombre); }
                finally { CloseServiceHandle(servicio); }
            }
        }
        using var controlador = new ServiceController(nombre);
        if (controlador.Status != ServiceControllerStatus.Running) controlador.Start();
        await EsperarAsync(controlador, ServiceControllerStatus.Running, limite, ct);
    }

    public async Task DetenerAsync(string nombre, TimeSpan limite, CancellationToken ct)
    {
        using var controlador = new ServiceController(nombre);
        if (controlador.Status != ServiceControllerStatus.Stopped) controlador.Stop();
        await EsperarAsync(controlador, ServiceControllerStatus.Stopped, limite, ct);
    }

    private static async Task EsperarAsync(ServiceController controlador, ServiceControllerStatus esperado, TimeSpan limite, CancellationToken ct)
    {
        var fin = DateTime.UtcNow + limite;
        while (true)
        {
            controlador.Refresh();
            if (controlador.Status == esperado) return;
            if (controlador.Status == ServiceControllerStatus.Stopped && esperado == ServiceControllerStatus.Running)
                throw new Ejecucion.FalloInstalacionException($"El servicio {controlador.ServiceName} se detuvo al iniciar; revisa su registro.");
            if (DateTime.UtcNow > fin)
                throw new Ejecucion.FalloInstalacionException($"El servicio {controlador.ServiceName} no llegó a {esperado} en {limite.TotalSeconds:0} s.");
            await Task.Delay(500, ct);
        }
    }

    private static EstadoServicio Traducir(ServiceControllerStatus estado) => estado switch
    {
        ServiceControllerStatus.Stopped => EstadoServicio.Detenido,
        ServiceControllerStatus.Running => EstadoServicio.EnEjecucion,
        ServiceControllerStatus.StartPending => EstadoServicio.Iniciando,
        ServiceControllerStatus.StopPending => EstadoServicio.Deteniendose,
        _ => EstadoServicio.Otro
    };

    private static ManejadorAdministrador Abrir(uint acceso)
    {
        var manejador = OpenSCManagerW(null, null, acceso);
        if (manejador == IntPtr.Zero)
            throw new UnauthorizedAccessException("No se pudo abrir el administrador de servicios (¿instalador sin permisos de administración?).");
        return new ManejadorAdministrador(manejador);
    }

    private sealed class ManejadorAdministrador(IntPtr valor) : IDisposable
    {
        public IntPtr Valor { get; } = valor;
        public static implicit operator IntPtr(ManejadorAdministrador m) => m.Valor;
        public void Dispose() => CloseServiceHandle(Valor);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ConfiguracionServicio
    {
        public uint Tipo;
        public uint TipoInicio;
        public uint Control;
        public IntPtr Ruta;
        public IntPtr Grupo;
        public uint Etiqueta;
        public IntPtr Dependencias;
        public IntPtr Cuenta;
        public IntPtr Visible;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DescripcionServicio
    {
        public IntPtr Texto;
    }

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr OpenSCManagerW(string? maquina, string? baseDatos, uint acceso);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr OpenServiceW(IntPtr administrador, string nombre, uint acceso);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateServiceW(IntPtr administrador, string nombre, string visible, uint acceso, uint tipo, uint inicio,
        uint control, string ruta, string? grupo, IntPtr etiqueta, string? dependencias, string cuenta, string? clave);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeServiceConfigW(IntPtr servicio, uint tipo, uint inicio, uint control, string ruta, string? grupo,
        IntPtr etiqueta, string? dependencias, string cuenta, string? clave, string visible);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeServiceConfig2W(IntPtr servicio, uint nivel, ref DescripcionServicio informacion);

    [LibraryImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeServiceConfig2Sid(IntPtr servicio, uint nivel, ref uint tipoSid);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryServiceConfigW(IntPtr servicio, IntPtr buffer, uint tamano, out uint necesario);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteService(IntPtr servicio);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseServiceHandle(IntPtr manejador);
}
