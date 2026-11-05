namespace Clarisa.Instalacion.Sistema;

internal sealed record IdentidadMotor(int Puerto, string DirectorioDatos, string Direccion, string Version);

internal sealed record CuentasMotor(string Base, string PrefijoBases, string UsuarioAplicacion, string ClaveAplicacion,
    string UsuarioMigracion, string UsuarioOperador)
{
    public override string ToString() => $"Cuentas del motor para {Base} (claves protegidas)";
}

internal interface IMotorDatos
{
    // Crea el directorio de datos y fija la clave de administración arrancando el motor sin red, por canalización con nombre aleatorio.
    Task InicializarAsync(string ejecutableInstalar, string ejecutableMotor, string directorioDatos, string claveAdministracion,
        CancellationToken ct);

    Task<IdentidadMotor?> IdentificarAsync(int puerto, string claveAdministracion, CancellationToken ct);

    Task AsegurarCuentasAsync(int puerto, string claveAdministracion, CuentasMotor cuentas, string? claveOperador, CancellationToken ct);

    // null si todo coincide; si no, la diferencia legible sin secretos.
    Task<string?> VerificarCuentasAsync(int puerto, string claveAdministracion, CuentasMotor cuentas, CancellationToken ct);

    Task FijarClaveMigracionAsync(int puerto, string claveAdministracion, string usuario, string clave, CancellationToken ct);
}
