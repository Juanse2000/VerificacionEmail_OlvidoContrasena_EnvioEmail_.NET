namespace VerificarCorreoYContrasenaPorCorreo.Models
{
    public class Correo
    {
        public int Id { get; set; }
        public string Para { get; set; } = string.Empty;
        public string Asunto { get; set; } = string.Empty;
        public string Cuerpo { get; set; } = string.Empty;
    }
}
