using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZKAttendanceService.Models
{
    [Table("Holidays")]
    public class Holiday
    {
        [Key]
        public int HolidayId { get; set; }

        [Required]
        [StringLength(100)]
        public string HolidayName { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "date")]
        public DateTime HolidayDate { get; set; }

        public int DurationDays { get; set; } = 1;

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(20)]
        public string? HolidayType { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? ModifiedDate { get; set; }
    }
}
