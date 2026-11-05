using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Clarisa.Instalacion.Sistema.Windows;

// Si el servicio se cierra por un fallo (por ejemplo, al encender el equipo antes de que la red esté lista), Windows lo vuelve a
// iniciar al minuto, hasta tres veces; el contador se reinicia cada día.
internal sealed partial class ServiciosWindows
{
    private const uint NivelAccionesFallo = 2;
    private const uint NivelAccionesFalloSinCaida = 4;
    private const int AccionReiniciar = 1;
    private const uint EsperaReinicioMs = 60_000;
    private const uint PeriodoReinicioSegundos = 24 * 60 * 60;

    private static void FijarReintentos(IntPtr servicio, string nombre)
    {
        var acciones = new AccionServicio[] { new(AccionReiniciar, EsperaReinicioMs), new(AccionReiniciar, EsperaReinicioMs), new(AccionReiniciar, EsperaReinicioMs) };
        var tamano = Marshal.SizeOf<AccionServicio>();
        var memoria = Marshal.AllocHGlobal(tamano * acciones.Length);
        try
        {
            for (var i = 0; i < acciones.Length; i++) Marshal.StructureToPtr(acciones[i], memoria + i * tamano, false);
            var configuracion = new AccionesFallo { PeriodoReinicio = PeriodoReinicioSegundos, Cantidad = (uint)acciones.Length, Acciones = memoria };
            if (!ChangeServiceConfig2Acciones(servicio, NivelAccionesFallo, ref configuracion))
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"No se pudo fijar el reinicio automático de {nombre}.");
            // También cuenta como fallo que el programa termine con un código de error, no solo que se caiga.
            var bandera = 1;
            if (!ChangeServiceConfig2Bandera(servicio, NivelAccionesFalloSinCaida, ref bandera))
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"No se pudo fijar el reinicio automático de {nombre}.");
        }
        finally
        {
            Marshal.FreeHGlobal(memoria);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct AccionServicio(int Tipo, uint Espera);

    [StructLayout(LayoutKind.Sequential)]
    private struct AccionesFallo
    {
        public uint PeriodoReinicio;
        public IntPtr MensajeReinicio;
        public IntPtr Comando;
        public uint Cantidad;
        public IntPtr Acciones;
    }

    [LibraryImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeServiceConfig2Acciones(IntPtr servicio, uint nivel, ref AccionesFallo informacion);

    [LibraryImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeServiceConfig2Bandera(IntPtr servicio, uint nivel, ref int bandera);
}
