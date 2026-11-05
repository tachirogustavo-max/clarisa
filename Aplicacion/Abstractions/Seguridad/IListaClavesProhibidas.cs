namespace Clarisa.Application.Abstractions.Seguridad;

// Contraseñas comunes o filtradas que se rechazan al crear o cambiar una clave. Se consulta en el servidor, sin enviar nada fuera.
public interface IListaClavesProhibidas
{
    bool Contiene(string claveNormalizada);
}
