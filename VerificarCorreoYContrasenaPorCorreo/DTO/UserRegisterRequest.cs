﻿using System.ComponentModel.DataAnnotations;

namespace VerificarCorreoYContrasenaPorCorreo.DTO
{
    public class UserRegisterRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6, ErrorMessage = "Por favor ingrese minimo 6 caracteres")]
        public string Password { get; set; } = string.Empty;

        [Required, Compare("Password", ErrorMessage = "Las contraseñas no coinciden")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
