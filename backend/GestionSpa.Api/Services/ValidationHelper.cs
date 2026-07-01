using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using GestionSpa.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace GestionSpa.Api.Services;

public static class ValidationHelper
{
    public const int MaxNombre = 50;
    public const int MaxApellido = 50;
    public const int MaxCedula = 20;
    public const int MaxDocumentoOtro = 50;
    public const int MaxTelefono = 20;
    public const int MaxEmail = 100;
    public const int MaxNotas = 500;
    public const int MaxBuscar = 100;
    public const int MaxLocalidad = 80;
    public const string LocalidadPendiente = "Pendiente agregar localidad";

    private static readonly Regex NombreRegex = new(@"^[\p{L}\s'.-]+$", RegexOptions.Compiled);
    private static readonly Regex CedulaUyRegex = new(@"^\d{1,3}\.\d{3}\.\d{3}-\d$", RegexOptions.Compiled);

    public static List<string> ValidateSocio(
        string nombre, string apellido, string cedula,
        TipoIdentificacionSocio tipoIdentificacion,
        string? telefono, string? email, string? localidad,
        DateTime fechaAlta, DateTime? fechaVencimiento,
        decimal cuotaMensual, bool esAlta)
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
            errors.Add(tipoIdentificacion == TipoIdentificacionSocio.Cedula
                ? "La cédula es obligatoria"
                : "La identificación es obligatoria");
        else if (tipoIdentificacion == TipoIdentificacionSocio.Cedula)
        {
            if (cedula.Length > MaxCedula)
                errors.Add($"La cédula no puede superar {MaxCedula} caracteres");
            else if (!CedulaUyRegex.IsMatch(cedula.Trim()))
                errors.Add("La cédula debe tener el formato uruguayo X.XXX.XXX-X");
        }
        else
        {
            if (cedula.Trim().Length > MaxDocumentoOtro)
                errors.Add($"La identificación no puede superar {MaxDocumentoOtro} caracteres");
        }

        if (esAlta)
        {
            if (string.IsNullOrWhiteSpace(localidad) || IsLocalidadPendiente(localidad))
                errors.Add("La localidad es obligatoria");
            else if (localidad.Trim().Length > MaxLocalidad)
                errors.Add($"La localidad no puede superar {MaxLocalidad} caracteres");
        }
        else if (!string.IsNullOrWhiteSpace(localidad) && localidad.Trim().Length > MaxLocalidad)
        {
            errors.Add($"La localidad no puede superar {MaxLocalidad} caracteres");
        }

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

    public static bool IsLocalidadPendiente(string? localidad) =>
        string.Equals(localidad?.Trim(), LocalidadPendiente, StringComparison.OrdinalIgnoreCase);

    public static List<string> ValidateFamilia(string nombre, decimal cuotaMensual)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(nombre))
            errors.Add("El nombre de la familia es obligatorio");
        else if (nombre.Length > MaxNombre)
            errors.Add($"El nombre no puede superar {MaxNombre} caracteres");
        else if (!NombreRegex.IsMatch(nombre))
            errors.Add("El nombre contiene caracteres no válidos");

        if (cuotaMensual <= 0)
            errors.Add("La cuota mensual debe ser mayor a 0");

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

    public static string NormalizeForSearch(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static bool MatchesSearch(string term, params string?[] fields)
    {
        var normTerm = NormalizeForSearch(term).ToLowerInvariant();
        foreach (var field in fields)
        {
            if (string.IsNullOrEmpty(field)) continue;
            if (field.Contains(term, StringComparison.OrdinalIgnoreCase)) return true;
            if (NormalizeForSearch(field).ToLowerInvariant().Contains(normTerm)) return true;
        }
        return false;
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
