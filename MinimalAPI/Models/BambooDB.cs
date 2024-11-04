using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace MinimalAPI.Models
{
    public class BambooDB : DbContext
    {
        public BambooDB(DbContextOptions<BambooDB> options) : base(options) { }
        public DbSet<Person> Persons { get; set; }
        public DbSet<UserLogin> UserLogins { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<PostImages> PostImages { get; set; }
    
    }

    [Table("Person")]
    public class Person
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required]
        public string Uname { get;set; }
        [Required]
        public string Email { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        [DataType(DataType.PhoneNumber)]
        public string? Phone { get; set; }
        public string Password { get; set; }

    }

    [Table("UserLogin")]
    public class UserLogin
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required]
        public string Email { get; set; }
        [Required]
        public string Password { get; set; }
    }

    [Table("Post")]
    public class Post
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        //[JsonIgnore]
        public int Id { get; set; }
        public string title { get; set; }
        public string content { get; set; }
        //private string slug { get; set; }
        //[JsonIgnore]
        public string slug { get; set; }
        //{
        //    get { return title.Replace(' ','-'); }
        //    set { slug = value; }
        //}
        //[JsonIgnore]
        public bool active { get; set; } = true;
        [NotMapped]
        public string status { get; set; }
        //[JsonIgnore]
        public string featuredImage { get; set; }
        //[JsonIgnore]
        public int userId { get; set; }
    }

    [Table("PostImages")]
    public class PostImages
    {
        [Key]
        [JsonIgnore]
        public Guid ImageId { get; set; } = Guid.NewGuid();
        public string fileName { get; set; }
        public byte[] fileData { get; set; }
        public string fileType { get; set; }
    }

    public class UserToken
    {
        public string Token { get; set; }
    }
}
