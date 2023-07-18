using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MimeKit.Text;
using System.Security.Cryptography;
using VerificarCorreoYContrasenaPorCorreo.Data;
using VerificarCorreoYContrasenaPorCorreo.DTO;
using VerificarCorreoYContrasenaPorCorreo.Models;

namespace VerificarCorreoYContrasenaPorCorreo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly DataContext _dataContext;
        private readonly IConfiguration _configuration;
        public UserController(DataContext dataContext, IConfiguration configuration)
        {
            _dataContext = dataContext;
            _configuration = configuration;
        }

        [HttpPost("Register")]
        public async Task<IActionResult> Register(UserRegisterRequest request)
        {
            if (_dataContext.Users.Any(u => u.Email == request.Email))
            {
                return BadRequest("Usuario ya existe.");
            }

            CreatePasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

            var user = new User
            {
                Email = request.Email,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                VerificationToken = CreateRandomToken()
            };

            _dataContext.Users.Add(user);
            await _dataContext.SaveChangesAsync();

            /* Envio email de verificacion */
            var correo = await _dataContext.Correos.Where(x => x.Id == 2).FirstOrDefaultAsync();

            if(correo != null)
            {
                string cuerpoConToken = correo.Cuerpo.Replace("@TOKENVERIFICAR", user.VerificationToken);
                correo.Cuerpo = cuerpoConToken;
            }

            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_configuration.GetSection("EmailUserName").Value));

            //string[] destinatarios = correo.Para.Split(",");

            //foreach (var destinatario in destinatarios)
            //{
            //    email.To.Add(MailboxAddress.Parse(destinatario));
            //}

            email.To.Add(MailboxAddress.Parse(user.Email));
            email.Subject = correo.Asunto;
            email.Body = new TextPart(TextFormat.Html)
            {
                Text = correo.Cuerpo
            };

            using var smpt = new SmtpClient();
            smpt.Connect(_configuration.GetSection("EmailHost").Value, 587, SecureSocketOptions.StartTls);
            smpt.Authenticate(_configuration.GetSection("EmailUserName").Value, _configuration.GetSection("EmailPassword").Value);
            smpt.Send(email);
            smpt.Disconnect(true);

            return Ok("Usuario creado exitosamente!");
        }

        [HttpGet("VerifyEmail")]
        public async Task<IActionResult> VerifyEmail(string token)
        {
            var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.VerificationToken == token);

            if (user == null)
            {
                return BadRequest("Token invalido.");
            }

            user.VerifiedDate = DateTime.UtcNow;
            await _dataContext.SaveChangesAsync();

            return Ok("Usuario verificado exitosamente! \nPor favor ingrese por la pantalla de login");
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login(UserLoginRequest request)
        {
            var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                return BadRequest("Usuario no encontrado.");
            }

            if (!VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
            {
                return BadRequest("Contraseña incorrecta");
            }

            if (user.VerifiedDate == null)
            {
                return BadRequest("Usuario no verificado");
            }

            return Ok($"Bienvenido de vuelta {user.Email}! :)");
        }

        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                return BadRequest("Usuario no econtrado.");
            }

            user.PasswordResetToken = CreateRandomToken();
            user.ResetTokenExpires = DateTime.UtcNow.AddDays(1);
            await _dataContext.SaveChangesAsync();

            return Ok("Ahora puede restablecer su contraseña!");
        }

        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == request.token);

            if (user == null || user.ResetTokenExpires < DateTime.UtcNow)
            {
                return BadRequest("Token invalido.");
            }

            CreatePasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

            user.PasswordHash = passwordHash;
            user.PasswordSalt = passwordSalt;
            user.PasswordResetToken = null;
            user.ResetTokenExpires = null;

            await _dataContext.SaveChangesAsync();

            return Ok("Contraseña restablecida exitosamente!");
        }

        private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            }
        }

        private bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512(passwordSalt))
            {
                var computedHash = hmac.
                    ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));

                return computedHash.SequenceEqual(passwordHash);
            }
        }

        private string CreateRandomToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
        }
    }
}
