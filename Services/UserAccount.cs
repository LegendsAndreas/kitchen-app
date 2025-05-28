using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebKitchen.Services;

[Table("users")]
public class UserAccount
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("username")]
    public string? Username { get; set; }
    
    [Column("password")]
    public string? Password { get; set; }
    
    [Column("role")]
    public string? Role { get; set; }

}