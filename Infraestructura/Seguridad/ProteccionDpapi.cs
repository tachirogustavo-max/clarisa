using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Clarisa.Infrastructure.Seguridad;

// DPAPI de equipo (CRYPTPROTECT_LOCAL_MACHINE): el servicio y el operador del mismo servidor pueden leerla; fuera de este equipo no
// sirve. Quién puede leer el archivo lo decide su ACL, que fija la instalación. Llamada directa a crypt32 para no añadir paquetes.
internal static class ProteccionDpapi
{
    private const int SinInterfaz = 0x1;
    private const int Equipo = 0x4;

    public static byte[] Proteger(ReadOnlySpan<byte> datos, ReadOnlySpan<byte> entropia) => Ejecutar(datos, entropia, proteger: true);

    public static byte[] Desproteger(ReadOnlySpan<byte> datos, ReadOnlySpan<byte> entropia) => Ejecutar(datos, entropia, proteger: false);

    private static byte[] Ejecutar(ReadOnlySpan<byte> datos, ReadOnlySpan<byte> entropia, bool proteger)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("La protección local de la clave requiere Windows.");
        var entrada = Copiar(datos);
        var adicional = Copiar(entropia);
        var salida = default(Bloque);
        try
        {
            var correcto = proteger
                ? CryptProtectData(ref entrada, null, ref adicional, IntPtr.Zero, IntPtr.Zero, SinInterfaz | Equipo, ref salida)
                : CryptUnprotectData(ref entrada, IntPtr.Zero, ref adicional, IntPtr.Zero, IntPtr.Zero, SinInterfaz, ref salida);
            if (!correcto) throw new CryptographicException("DPAPI rechazó la operación.", new Win32Exception(Marshal.GetLastWin32Error()));
            var resultado = new byte[salida.Longitud];
            Marshal.Copy(salida.Datos, resultado, 0, salida.Longitud);
            return resultado;
        }
        finally
        {
            Liberar(entrada);
            Liberar(adicional);
            if (salida.Datos != IntPtr.Zero)
            {
                Marshal.Copy(new byte[salida.Longitud], 0, salida.Datos, salida.Longitud);
                LocalFree(salida.Datos);
            }
        }
    }

    private static Bloque Copiar(ReadOnlySpan<byte> datos)
    {
        var memoria = Marshal.AllocHGlobal(Math.Max(datos.Length, 1));
        var copia = datos.ToArray();
        try
        {
            Marshal.Copy(copia, 0, memoria, copia.Length);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(copia);
        }
        return new Bloque { Longitud = datos.Length, Datos = memoria };
    }

    private static void Liberar(Bloque bloque)
    {
        if (bloque.Datos == IntPtr.Zero) return;
        Marshal.Copy(new byte[bloque.Longitud], 0, bloque.Datos, bloque.Longitud);
        Marshal.FreeHGlobal(bloque.Datos);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Bloque
    {
        public int Longitud;
        public IntPtr Datos;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref Bloque datos, string? descripcion, ref Bloque entropia, IntPtr reservado,
        IntPtr aviso, int opciones, ref Bloque salida);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref Bloque datos, IntPtr descripcion, ref Bloque entropia, IntPtr reservado,
        IntPtr aviso, int opciones, ref Bloque salida);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memoria);
}
