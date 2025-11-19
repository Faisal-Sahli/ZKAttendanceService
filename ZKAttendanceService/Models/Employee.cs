using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZKAttendanceService.Models
{
    [Table("Employees")]
    public class Employee
    {
        [Key]
        public int EmployeeId { get; set; }

        [Required]
        [StringLength(12)]
        [Column(TypeName = "varchar(12)")]
        public string BiometricUserId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string EmployeeName { get; set; } = string.Empty;

        [StringLength(20)]
        public string? SSN { get; set; }

        [StringLength(2)]
        public string? Gender { get; set; }

        [StringLength(50)]
        public string? Title { get; set; }

        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        public DateTime? BirthDate { get; set; }

        public DateTime? HireDate { get; set; }

        public int? DepartmentId { get; set; }

        // Flags للتحكم في الحضور
        public bool CheckAttendance { get; set; } = true;

        public bool CheckLate { get; set; } = true;

        public bool CheckEarly { get; set; } = true;

        public bool CheckOvertime { get; set; } = true;

        public bool CheckHoliday { get; set; } = true;

        public int? DefaultShiftId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? ModifiedDate { get; set; }

        // Navigation Properties
        [ForeignKey("DepartmentId")]
        public virtual Department? Department { get; set; }

        [ForeignKey("DefaultShiftId")]
        public virtual WorkShift? DefaultShift { get; set; }

        public virtual ICollection<AttendanceLog> AttendanceLogs { get; set; } = new List<AttendanceLog>();

    }
}
