using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZKAttendanceService.Models
{
    [Table("WorkShifts")]
    public class WorkShift
    {
        [Key]
        public int ShiftId { get; set; }

        [Required]
        [StringLength(50)]
        public string ShiftName { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "time")]
        public TimeSpan StartTime { get; set; }

        [Required]
        [Column(TypeName = "time")]
        public TimeSpan EndTime { get; set; }

        public int LateMinutes { get; set; } = 15;

        public int EarlyMinutes { get; set; } = 15;

        [Column(TypeName = "time")]
        public TimeSpan? CheckInWindowStart { get; set; }

        [Column(TypeName = "time")]
        public TimeSpan? CheckInWindowEnd { get; set; }

        [Column(TypeName = "time")]
        public TimeSpan? CheckOutWindowStart { get; set; }

        [Column(TypeName = "time")]
        public TimeSpan? CheckOutWindowEnd { get; set; }

        public bool RequireCheckIn { get; set; } = true;

        public bool RequireCheckOut { get; set; } = true;

        public int WorkMinutes { get; set; } = 480;

        [Column(TypeName = "int")]
        public int Color { get; set; } = 16715535;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? ModifiedDate { get; set; }

        // Navigation Properties
        public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}
