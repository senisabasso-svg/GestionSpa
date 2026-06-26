using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace GestionSpa.Api.Services;

public static class ValidationHelper
{
    public const int MaxNombre = 50;
    public const int MaxApellido = 50;
    public const int MaxCedula = 20;
    public const int MaxTelefono = 20;
    public const int MaxEmail = 100;
    public const int MaxNotas = 500;
    public const int MaxBuscar = 100;

    private static readonly Regex NombreRegex = new(@"^[\p{L}\s'.-]+$", RegexOptions.Compiled);
    private static readonly Regex CedulaRegex = new(@"^[\d.\-]+$", RegexOptions.Compiled);

    public static List<string> ValidateSocio(
        string nombre, string apellido, string cedula,
        string? telefono, string? email,
        DateTime fechaAlta, DateTime? fechaVencimiento,
        decimal cuotaMensual)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(nombre))
            errors.Add("El nombre es obligatorio");
        else if (nombre.Length > MaxNombre)
            errors.Add($"El nombre no puede superar {MaxNombre} caracteres");
        else if (!NombreRegex.IsMatch(nombre))
            errors.Add("El nombre contiene caracteres no válidos");

        if (string.IsNullOrWhiteSpace(apellido))
            errors.Add("El apellido es obligatorio");
        else if (apellido.Length > MaxApellido)
            errors.Add($"El apellido no puede superar {MaxApellido} caracteres");
        else if (!NombreRegex.IsMatch(apellido))
            errors.Add("El apellido contiene caracteres no válidos");

        if (string.IsNullOrWhiteSpace(cedula))
            errors.Add("La cédula es obligatoria");
        else if (cedula.Length > MaxCedula)
            errors.Add($"La cédula no puede superar {MaxCedula} caracteres");
        else if (!CedulaRegex.IsMatch(cedula))
            errors.Add("La cédula solo puede contener números, puntos y guiones");

        if (!string.IsNullOrWhiteSpace(telefono) && telefono.Length > MaxTelefono)
            errors.Add($"El teléfono no puede superar {MaxTelefono} caracteres");

        if (!string.IsNullOrWhiteSpace(email))
        {
            if (email.Length > MaxEmail)
                errors.Add($"El email no puede superar {MaxEmail} caracteres");
            else if (!IsValidEmail(email))
                errors.Add("El formato del email no es válido");
        }

        if (cuotaMensual <= 0)
            errors.Add("La cuota mensual debe ser mayor a 0");

        var hoy = DateTime.UtcNow.Date;
        if (fechaAlta.Date > hoy)
            errors.Add("La fecha de alta no puede ser futura");

        if (fechaVencimiento.HasValue)
        {
            if (fechaVencimiento.Value.Date < fechaAlta.Date)
                errors.Add("La fecha de vencimiento debe ser posterior a la fecha de alta");
        }

        return errors;
    }

    public static List<string> ValidateCliente(string nombre, string apellido, string? email)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(nombre))
            errors.Add("El nombre es obligatorio");
        else if (nombre.Length > MaxNombre)
            errors.Add($"El nombre no puede superar {MaxNombre} caracteres");

        if (string.IsNullOrWhiteSpace(apellido))
            errors.Add("El apellido es obligatorio");
        else if (apellido.Length > MaxApellido)
            errors.Add($"El apellido no puede superar {MaxApellido} caracteres");

        if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
            errors.Add("El formato del email no es válido");

        return errors;
    }

    public static List<string> ValidateServicio(string nombre, decimal precio, int duracionMinutos)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(nombre))
            errors.Add("El nombre del servicio es obligatorio");
        else if (nombre.Length > MaxNombre)
            errors.Add($"El nombre no puede superar {MaxNombre} caracteres");

        if (precio <= 0)
            errors.Add("El precio debe ser mayor a 0");

        if (duracionMinutos < 0)
            errors.Add("La duración no puede ser negativa");

        return errors;
    }

    public static List<string> ValidateCargo(int servicioId, int? socioId, int? clienteId, int cantidad)
    {
        var errors = new List<string>();
        if (servicioId <= 0)
            errors.Add("Debés seleccionar un servicio");
        if (socioId == null && clienteId == null)
            errors.Add("Debés seleccionar un socio o un cliente");
        if (socioId != null && clienteId != null)
            errors.Add("No podés indicar socio y cliente a la vez");
        if (cantidad < 1)
            errors.Add("La cantidad debe ser al menos 1");
        return errors;
    }

    public static List<string> ValidateMontoPago(decimal monto)
    {
        var errors = new List<string>();
        if (monto <= 0)
            errors.Add("El monto del pago debe ser mayor a 0");
        return errors;
    }

    public static string? SanitizeSearchTerm(string? buscar)
    {
        if (string.IsNullOrWhiteSpace(buscar)) return null;
        var term = buscar.Trim();
        if (term.Length > MaxBuscar) term = term[..MaxBuscar];
        term = term.Replace("%", "").Replace("_", "").Replace("\\", "");
        return string.IsNullOrWhiteSpace(term) ? null : term;
    }

    public static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    public static BadRequestObjectResult ToBadRequest(List<string> errors) =>
        new(new { mensaje = string.Join(" ", errors), errores = errors });
}
