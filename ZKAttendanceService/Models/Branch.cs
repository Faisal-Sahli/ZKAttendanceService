using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZKAttendanceService.Models
{
    [Table("Branches")]  // ✅ أضف هذا
    public class Branch
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BranchId { get; set; }

        [Required]
        [MaxLength(20)]
        public string BranchCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string BranchName { get; set; } = string.Empty;

        public string? City { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        [MaxLength(20)]
        public string? ContactPhone { get; set; }

        public bool IsActive { get; set; } = true;

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? ModifiedDate { get; set; }

        // ✅ أضف هذه
        public DateTime? LastSyncTime { get; set; }

        // ✅ Navigation Properties
        public virtual ICollection<Device> Devices { get; set; } = new List<Device>();

        public virtual ICollection<AttendanceLog> AttendanceLogs { get; set; } = new List<AttendanceLog>();
    }
}
