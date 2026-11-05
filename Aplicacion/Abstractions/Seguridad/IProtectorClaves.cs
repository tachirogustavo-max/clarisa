namespace Clarisa.Application.Abstractions.Seguridad;

public enum VerificacionClave
{
    Incorrecta = 1,
    Correcta = 2,
    CorrectaRequiereActualizar = 3
}

// La huella almacenada incluye algoritmo, versión y costo para poder subir el costo sin invalidar las claves existentes.
public interface IProtectorClaves
{
    string Calcular(string clave);
    VerificacionClave Verificar(string clave, string huellaAlmacenada);
}
