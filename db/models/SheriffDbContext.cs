﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using db.models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SS.Api.Models.DB;
using ss.db.models;
using SS.Db.models.auth;
using SS.Db.models.sheriff;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using SS.Common.authorization;
using SS.Db.models.audit;
using SS.Db.models.audit.notmapped;
using SS.Db.models.lookupcodes;
using SS.Db.models.scheduling;

namespace SS.Db.models
{
    public partial class SheriffDbContext : DbContext, IDataProtectionKeyContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SheriffDbContext()
        {

        }

        public SheriffDbContext(DbContextOptions<SheriffDbContext> options, IHttpContextAccessor httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public virtual DbSet<Location> Location { get; set; }
        public virtual DbSet<LookupCode> LookupCode { get; set; }
        public virtual DbSet<LookupSortOrder> LookupSortOrder { get; set; }
        public virtual DbSet<Region> Region { get; set; }
        public virtual DbSet<Sheriff> Sheriff { get; set; }
        public virtual DbSet<SheriffLeave> SheriffLeave { get; set; }
        public virtual DbSet<SheriffAwayLocation> SheriffAwayLocation { get; set; }
        public virtual DbSet<SheriffTraining> SheriffTraining { get; set; }
        public virtual DbSet<User> User { get; set; }
        public virtual DbSet<UserRole> UserRole { get; set; }
        public virtual DbSet<Permission> Permission { get; set; }
        public virtual DbSet<Role> Role { get; set; }

        #region Scheduling
        public virtual DbSet<Shift> Shift { get; set; }
        public virtual DbSet<Assignment> Assignment { get; set; }
        public virtual DbSet<Duty> Duty { get; set; }
        public virtual DbSet<DutySlot> DutySlot { get; set; }
        #endregion Scheduling

        // This maps to the table that stores keys.
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

        public DbSet<Audit> Audit { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyAllConfigurations();
            modelBuilder.Entity<Audit>().HasIndex(a => a.KeyValues);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql("Name=DatabaseConnectionString");
            }
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var auditEntries = OnBeforeSaveChanges();
            HandleSaveChanges();
            var result = await base.SaveChangesAsync(cancellationToken);
            await OnAfterSaveChanges(auditEntries);
            return result;
        }

        //Only used in tests. 
        public override int SaveChanges()
        {
            var auditEntries = OnBeforeSaveChanges();
            HandleSaveChanges();
            var result = base.SaveChanges();
            OnAfterSaveChanges(auditEntries);
            return result;
        }

        private List<AuditEntry> OnBeforeSaveChanges()
        {
            ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditEntry>();
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is Audit || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditEntry = new AuditEntry(entry);
                auditEntry.TableName = entry.Metadata.GetTableName();
                auditEntries.Add(auditEntry);

                foreach (var property in entry.Properties)
                {
                    if (property.IsTemporary)
                    {
                        // value will be generated by the database, get the value after saving
                        auditEntry.TemporaryProperties.Add(property);
                        continue;
                    }

                    string propertyName = property.Metadata.Name;
                    if (propertyName == "Photo")
                        continue;
                    if (IsKeyValue(property))
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue;
                        continue;
                    }

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                            break;

                        case EntityState.Deleted:
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            break;

                        case EntityState.Modified:
                            if (property.IsModified)
                            {
                                auditEntry.OldValues[propertyName] = property.OriginalValue;
                                auditEntry.NewValues[propertyName] = property.CurrentValue;
                            }
                            break;
                    }
                }
            }

            // Save audit entities that have all the modifications
            foreach (var auditEntry in auditEntries.Where(_ => !_.HasTemporaryProperties))
            {
                Audit.Add(auditEntry.ToAudit());
            }

            // keep a list of entries where the value of some properties are unknown at this step
            return auditEntries.Where(_ => _.HasTemporaryProperties).ToList();
        }

        private bool IsKeyValue(PropertyEntry property)
        {
            if (property.Metadata.Name == "CreatedById" || property.Metadata.Name == "UpdatedById")
                return false;
            return property.Metadata.IsPrimaryKey() || property.Metadata.IsForeignKey();
        }

        private Task OnAfterSaveChanges(List<AuditEntry> auditEntries)
        {
            if (auditEntries == null || auditEntries.Count == 0)
                return Task.CompletedTask;

            foreach (var auditEntry in auditEntries)
            {
                // Get the final value of the temporary properties
                foreach (var prop in auditEntry.TemporaryProperties)
                {
                    if (prop.Metadata.Name == "Photo")
                        continue;
                    if (IsKeyValue(prop))
                    {
                        auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                    else
                    {
                        auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                }

                // Save the Audit entry
                Audit.Add(auditEntry.ToAudit());
            }

            return SaveChangesAsync();
        }

        /// <summary>
        /// Save the entities with who created them or updated them.
        /// </summary>
        private void HandleSaveChanges()
        {
            var modifiedEntries = ChangeTracker.Entries()
                .Where(x => (x.State == EntityState.Added || x.State == EntityState.Modified));

            var userId = GetUserId(_httpContextAccessor?.HttpContext?.User.FindFirst(CustomClaimTypes.UserId)?.Value);
            userId ??= auth.User.SystemUser;
            foreach (var entry in modifiedEntries)
            {
                if (entry.Entity is BaseEntity entity)
                {
                    if (entry.State == EntityState.Added)
                    {
                        entity.CreatedById = userId;
                        entity.CreatedOn = DateTime.UtcNow;
                    }
                    else if (entry.State != EntityState.Deleted)
                    {
                        entity.UpdatedById = userId;
                        entity.UpdatedOn = DateTime.UtcNow;
                    }
                }
            }
        }

        public TEntity DetachedClone<TEntity>(TEntity entity) where TEntity : class
            => Entry(entity).CurrentValues.Clone().ToObject() as TEntity;

        private Guid? GetUserId(string claimValue)
        {
            if (claimValue == null)
                return null;
            return Guid.Parse(claimValue);
        }
    }
}
