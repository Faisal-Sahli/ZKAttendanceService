using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZKAttendanceService.Models
{
    [Table("Devices")]  // ✅ أضف
    public class Device
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DeviceId { get; set; }

        [Required]  // ✅ أضف
        public int BranchId { get; set; }

        [Required]
        [MaxLength(200)]
        public string DeviceName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string DeviceIP { get; set; } = string.Empty;

        [Required]
        public int DevicePort { get; set; } = 4370;

        [MaxLength(100)]
        public string? SerialNumber { get; set; }

        [MaxLength(100)]
        public string? DeviceModel { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsOnline { get; set; } = false;  // ✅ أضف

        public DateTime? LastCheckTime { get; set; }  // ✅ أضف

        public DateTime? LastConnectionTime { get; set; }

        [MaxLength(50)]
        public string? ConnectionStatus { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? ModifiedDate { get; set; }

        // Navigation Properties
        [ForeignKey("BranchId")]  // ✅ أضف
        public virtual Branch? Branch { get; set; }

        public virtual ICollection<DeviceStatus> DeviceStatuses { get; set; } = new List<DeviceStatus>();  // ✅ أضف

        public virtual ICollection<AttendanceLog> AttendanceLogs { get; set; } = new List<AttendanceLog>();  // ✅ أضف
    }
}
