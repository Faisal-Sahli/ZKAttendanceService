using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZKAttendanceService.Models
{
    [Table("AttendanceLogs")]
    public class AttendanceLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long LogId { get; set; }

        [Required]
        [StringLength(12)]
        public string BiometricUserId { get; set; } = string.Empty;

        public int? EmployeeId { get; set; }

        [Required]
        public int DeviceId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Required]
        public DateTime AttendanceTime { get; set; }

        [StringLength(50)]
        public string? AttendanceType { get; set; }

        [StringLength(50)]
        public string? VerifyMethod { get; set; }

        public int? WorkCode { get; set; }

        [Required]
        public bool IsSynced { get; set; } = false;

        public DateTime? SyncedDate { get; set; }

        [Required]
        public bool IsProcessed { get; set; } = false;

        public DateTime? ProcessedDate { get; set; }

        [Required]
        public bool IsManual { get; set; } = false;

        [StringLength(200)]
        public string? Notes { get; set; }

        // ⭐ جديد: لمنع التكرار
        [Required]
        [StringLength(100)]
        public string UniqueHash { get; set; } = string.Empty;

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [ForeignKey("DeviceId")]
        public virtual Device? Device { get; set; }

        [ForeignKey("BranchId")]
        public virtual Branch? Branch { get; set; }

        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }
    }
}
