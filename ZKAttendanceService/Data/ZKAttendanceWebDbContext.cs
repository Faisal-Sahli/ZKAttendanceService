using Microsoft.EntityFrameworkCore;
using ZKAttendanceService.Models;

namespace ZKAttendanceService.Data
{
    public class ZKAttendanceWebDbContext : DbContext
    {
        public ZKAttendanceWebDbContext(DbContextOptions<ZKAttendanceWebDbContext> options)
            : base(options)
        {
        }

        // DbSets
        public DbSet<Branch> Branches { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<WorkShift> WorkShifts { get; set; }
        public DbSet<Holiday> Holidays { get; set; }
        public DbSet<AttendanceLog> AttendanceLogs { get; set; }
        public DbSet<SyncLog> SyncLogs { get; set; }
        public DbSet<DeviceStatus> DeviceStatuses { get; set; }
        public DbSet<DeviceError> DeviceErrors { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureBranches(modelBuilder);
            ConfigureDevices(modelBuilder);
            ConfigureDepartments(modelBuilder);
            ConfigureWorkShifts(modelBuilder);
            ConfigureEmployees(modelBuilder);
            ConfigureHolidays(modelBuilder);
            ConfigureAttendanceLogs(modelBuilder);
            ConfigureSyncLogs(modelBuilder);
            ConfigureDeviceStatuses(modelBuilder);
            ConfigureDeviceErrors(modelBuilder);
            ConfigureSystemSettings(modelBuilder);
        }

        private void ConfigureBranches(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Branch>(entity =>
            {
                entity.HasKey(e => e.BranchId);
                entity.Property(e => e.BranchId).ValueGeneratedOnAdd();
                entity.Property(e => e.BranchCode).IsRequired().HasMaxLength(20);
                entity.Property(e => e.BranchName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Address).HasMaxLength(500);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");

                entity.HasIndex(e => e.BranchCode).IsUnique().HasDatabaseName("IX_Branches_Code");
                entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_Branches_IsActive");
            });
        }

        private void ConfigureDevices(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Device>(entity =>
            {
                entity.HasKey(e => e.DeviceId);
                entity.Property(e => e.DeviceId).ValueGeneratedOnAdd();
                entity.Property(e => e.DeviceName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.DeviceIP).IsRequired().HasMaxLength(50);
                entity.Property(e => e.SerialNumber).HasMaxLength(100);
                entity.Property(e => e.DeviceModel).HasMaxLength(100);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");

                entity.HasOne(d => d.Branch)
                    .WithMany(b => b.Devices)
                    .HasForeignKey(d => d.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.DeviceIP, e.DevicePort }).IsUnique().HasDatabaseName("IX_Devices_IP_Port");
                entity.HasIndex(e => e.BranchId).HasDatabaseName("IX_Devices_BranchId");
                entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_Devices_IsActive");
            });
        }

        private void ConfigureDepartments(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasKey(e => e.DepartmentId);
                entity.Property(e => e.DepartmentId).ValueGeneratedOnAdd();
                entity.Property(e => e.DepartmentName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.DepartmentCode).HasMaxLength(50);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");

                entity.HasOne(d => d.ParentDepartment)
                    .WithMany(d => d.SubDepartments)
                    .HasForeignKey(d => d.ParentDepartmentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.DepartmentName).IsUnique().HasDatabaseName("IX_Departments_Name");
                entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_Departments_IsActive");
            });
        }

        private void ConfigureWorkShifts(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WorkShift>(entity =>
            {
                entity.HasKey(e => e.ShiftId);
                entity.Property(e => e.ShiftId).ValueGeneratedOnAdd();
                entity.Property(e => e.ShiftName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.StartTime).IsRequired().HasColumnType("time");
                entity.Property(e => e.EndTime).IsRequired().HasColumnType("time");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");

                entity.HasIndex(e => e.ShiftName).HasDatabaseName("IX_WorkShifts_Name");
                entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_WorkShifts_IsActive");
            });
        }

        private void ConfigureEmployees(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Employee>(entity =>
            {
                entity.HasKey(e => e.EmployeeId);
                entity.Property(e => e.EmployeeId).ValueGeneratedOnAdd();
                entity.Property(e => e.BiometricUserId).IsRequired().HasMaxLength(12);
                entity.Property(e => e.EmployeeName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");

                entity.HasOne(e => e.Department)
                    .WithMany(d => d.Employees)
                    .HasForeignKey(e => e.DepartmentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.DefaultShift)
                    .WithMany(s => s.Employees)
                    .HasForeignKey(e => e.DefaultShiftId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.BiometricUserId).IsUnique().HasDatabaseName("IX_Employees_BiometricUserId");
                entity.HasIndex(e => e.EmployeeName).HasDatabaseName("IX_Employees_Name");
                entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_Employees_IsActive");
            });
        }

        private void ConfigureHolidays(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Holiday>(entity =>
            {
                entity.HasKey(e => e.HolidayId);
                entity.Property(e => e.HolidayId).ValueGeneratedOnAdd();
                entity.Property(e => e.HolidayName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.HolidayDate).IsRequired().HasColumnType("date");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");

                entity.HasIndex(e => new { e.HolidayName, e.HolidayDate }).IsUnique().HasDatabaseName("IX_Holidays_NameDate");
                entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_Holidays_IsActive");
            });
        }

        private void ConfigureAttendanceLogs(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AttendanceLog>(entity =>
            {
                entity.HasKey(e => e.LogId);
                entity.Property(e => e.LogId).ValueGeneratedOnAdd();

                entity.Property(e => e.BiometricUserId).IsRequired().HasMaxLength(12);
                entity.Property(e => e.AttendanceTime).IsRequired();
                entity.Property(e => e.AttendanceType).HasMaxLength(50);
                entity.Property(e => e.VerifyMethod).HasMaxLength(50);
                entity.Property(e => e.Notes).HasMaxLength(200);

                entity.Property(e => e.IsSynced).HasDefaultValue(true);
                entity.Property(e => e.IsProcessed).HasDefaultValue(false);
                entity.Property(e => e.IsManual).HasDefaultValue(false);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");

                // ⭐ المهم: UniqueHash لمنع التكرار
                entity.Property(e => e.UniqueHash).IsRequired().HasMaxLength(100);

                entity.HasOne(a => a.Device)
                    .WithMany(d => d.AttendanceLogs)
                    .HasForeignKey(a => a.DeviceId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Branch)
                    .WithMany(b => b.AttendanceLogs)
                    .HasForeignKey(a => a.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Employee)
                    .WithMany(e => e.AttendanceLogs)
                    .HasForeignKey(a => a.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ⭐ Indexes للسرعة
                entity.HasIndex(e => new { e.BiometricUserId, e.AttendanceTime, e.DeviceId })
                    .IsUnique()
                    .HasDatabaseName("IX_AttendanceLogs_Unique");

                entity.HasIndex(e => e.UniqueHash)
                    .IsUnique()
                    .HasDatabaseName("IX_AttendanceLogs_UniqueHash");

                entity.HasIndex(e => new { e.DeviceId, e.AttendanceTime })
                    .HasDatabaseName("IX_AttendanceLogs_Device_Time");

                entity.HasIndex(e => new { e.BiometricUserId, e.AttendanceTime })
                    .HasDatabaseName("IX_AttendanceLogs_User_Time");

                entity.HasIndex(e => e.EmployeeId)
                    .HasDatabaseName("IX_AttendanceLogs_EmployeeId");

                entity.HasIndex(e => e.IsSynced)
                    .HasDatabaseName("IX_AttendanceLogs_IsSynced");

                entity.HasIndex(e => new { e.BranchId, e.AttendanceTime })
                    .HasDatabaseName("IX_AttendanceLogs_Branch_Time");
            });
        }

        private void ConfigureSyncLogs(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SyncLog>(entity =>
            {
                entity.HasKey(e => e.SyncId);
                entity.Property(e => e.SyncId).ValueGeneratedOnAdd();
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ErrorMessage).HasMaxLength(500);
                entity.Property(e => e.Status).HasDefaultValue("Pending");
                entity.Property(e => e.StartTime).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");

                entity.HasOne(s => s.Device)
                    .WithMany()
                    .HasForeignKey(s => s.DeviceId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.Branch)
                    .WithMany()
                    .HasForeignKey(s => s.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.DeviceId, e.StartTime }).HasDatabaseName("IX_SyncLogs_Device_Time");
                entity.HasIndex(e => e.Status).HasDatabaseName("IX_SyncLogs_Status");
            });
        }

        private void ConfigureDeviceStatuses(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DeviceStatus>(entity =>
            {
                entity.HasKey(e => e.StatusId);
                entity.Property(e => e.StatusId).ValueGeneratedOnAdd();
                entity.Property(e => e.IsOnline).HasDefaultValue(false);
                entity.Property(e => e.StatusTime).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.LastUpdateTime).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");

                entity.HasOne(ds => ds.Device)
                    .WithMany(d => d.DeviceStatuses)
                    .HasForeignKey(ds => ds.DeviceId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ds => ds.Branch)
                    .WithMany()
                    .HasForeignKey(ds => ds.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.DeviceId, e.StatusTime }).HasDatabaseName("IX_DeviceStatuses_Device_Time");
                entity.HasIndex(e => e.IsOnline).HasDatabaseName("IX_DeviceStatuses_IsOnline");
            });
        }

        private void ConfigureDeviceErrors(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DeviceError>(entity =>
            {
                entity.HasKey(e => e.ErrorId);
                entity.Property(e => e.ErrorId).ValueGeneratedOnAdd();
                entity.Property(e => e.ErrorMessage).IsRequired().HasMaxLength(500);
                entity.Property(e => e.IsResolved).HasDefaultValue(false);
                entity.Property(e => e.ErrorDateTime).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");

                entity.HasOne(de => de.Device)
                    .WithMany()
                    .HasForeignKey(de => de.DeviceId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(de => de.Branch)
                    .WithMany()
                    .HasForeignKey(de => de.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.DeviceId, e.ErrorDateTime }).HasDatabaseName("IX_DeviceErrors_Device_Time");
                entity.HasIndex(e => e.IsResolved).HasDatabaseName("IX_DeviceErrors_IsResolved");
            });
        }

        private void ConfigureSystemSettings(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SystemSetting>(entity =>
            {
                entity.HasKey(e => e.SettingId);
                entity.Property(e => e.SettingId).ValueGeneratedOnAdd();
                entity.Property(e => e.SettingKey).IsRequired().HasMaxLength(100);
                entity.Property(e => e.SettingValue).IsRequired().HasMaxLength(500);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");

                entity.HasIndex(e => e.SettingKey).IsUnique().HasDatabaseName("IX_SystemSettings_Key");
            });
        }
    }
}
