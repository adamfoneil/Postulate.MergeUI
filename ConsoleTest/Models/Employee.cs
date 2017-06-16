using Postulate.Orm.Abstract;
using System.ComponentModel.DataAnnotations;

namespace SchemaMergeTests.Models
{
    public class Employee : Record<int>
    {
        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; }

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; }
    }
}
