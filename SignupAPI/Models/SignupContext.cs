using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace SignupAPI.Models
{
    public partial class SignupContext : DbContext
    {
        public SignupContext()
        {
        }

        public SignupContext(DbContextOptions<SignupContext> options)
            : base(options)
        {
        }

        public virtual DbSet<CustomerOwnedVehicles> CustomerOwnedVehicles { get; set; }
        public virtual DbSet<CustomerVehicle> CustomerVehicle { get; set; }
        public virtual DbSet<FuelType> FuelType { get; set; }
        public virtual DbSet<GarageBusiness> GarageBusiness { get; set; }
        public virtual DbSet<GarageBusinessUser> GarageBusinessUser { get; set; }
        public virtual DbSet<GarageBusinessCustomer> GarageBusinessCustomer { get; set; }
        public virtual DbSet<GarageOwnedVehicles> GarageOwnedVehicles { get; set; }
        public virtual DbSet<GarageVehicleOwner> GarageVehicleOwner { get; set; }
        public virtual DbSet<InvoiceWorkQuote> InvoiceWorkQuote { get; set; }
        public virtual DbSet<Mileage> Mileage { get; set; }
        public virtual DbSet<ServiceHistory> ServiceHistory { get; set; }
        public virtual DbSet<TransmissionType> TransmissionType { get; set; }
        public virtual DbSet<Users> Users { get; set; }
        public virtual DbSet<VehicleInvoice> VehicleInvoice { get; set; }
        public virtual DbSet<VehicleMake> VehicleMake { get; set; }
        public virtual DbSet<VehicleModel> VehicleModel { get; set; }
        public virtual DbSet<VehicleYears> VehicleYears { get; set; }
        public virtual DbSet<WorkItem> WorkItem { get; set; }
        public virtual DbSet<WorkQuote> WorkQuote { get; set; }
        public virtual DbSet<WorkQuoteWorkItem> WorkQuoteWorkItem { get; set; }
        public virtual DbSet<VehiclePart> VehiclePart { get; set; }
        public virtual DbSet<GarageUserInvitation> GarageUserInvitation { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. See http://go.microsoft.com/fwlink/?LinkId=723263 for guidance on storing connection strings.
                optionsBuilder.UseSqlServer("Server=DESKTOP-C821FRH\\SQLEXPRESS;Database=Garagesaas;Trusted_Connection=True;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GarageUserInvitation>(entity =>
            {
                entity.HasIndex(e => e.InvitationTokenHash)
                    .HasName("UX_GarageUserInvitation_TokenHash")
                    .IsUnique();

                entity.Property(e => e.EmailAddress)
                    .IsRequired()
                    .HasMaxLength(256);

                entity.Property(e => e.FirstName)
                    .HasMaxLength(100);

                entity.Property(e => e.LastName)
                    .HasMaxLength(100);

                entity.Property(e => e.Role)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.InvitationTokenHash)
                    .IsRequired()
                    .HasMaxLength(64);

                entity.Property(e => e.CreatedDate)
                    .HasDefaultValueSql("GETUTCDATE()");

                entity.Property(e => e.Revoked).HasColumnName("Revoked");
                entity.Property(e => e.RevokedDate).HasColumnType("datetime");

                entity.HasOne<GarageBusiness>()
                    .WithMany()
                    .HasForeignKey(e => e.GarageBusinessId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName(
                        "FK_GarageUserInvitation_GarageBusiness");

                entity.HasOne<Users>()
                    .WithMany()
                    .HasForeignKey(e => e.InvitedByUserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName(
                        "FK_GarageUserInvitation_InvitedBy");
            });

            modelBuilder.Entity<GarageBusinessUser>(entity =>
            {
                entity.HasIndex(
                    e => new
                    {
                        e.GarageBusinessId,
                        e.UserId
                    })
                    .HasName(
                        "UX_GarageBusinessUser_Garage_User")
                    .IsUnique();

                entity.Property(e => e.Role)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(256);

                entity.Property(e => e.UpdatedBy)
                    .HasMaxLength(256);

                entity.HasOne(d => d.GarageBusiness)
                    .WithMany()
                    .HasForeignKey(d => d.GarageBusinessId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName(
                        "FK_GarageBusinessUser_GarageBusiness");

                entity.HasOne(d => d.User)
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName(
                        "FK_GarageBusinessUser_Users");
            });

            modelBuilder.Entity<CustomerOwnedVehicles>(entity =>
            {
                entity.HasOne(d => d.GarageBusiness)
                    .WithMany(p => p.CustomerOwnedVehicles)
                    .HasForeignKey(d => d.GarageBusinessId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            modelBuilder.Entity<CustomerVehicle>(entity =>
            {
                entity.Property(e => e.GarageOwned)
                    .IsRequired()
                    .HasDefaultValueSql("(CONVERT([bit],(0)))");

                entity.Property(e => e.VehicleNCTDue).HasColumnName("VehicleNCTDue");

                entity.HasOne(d => d.GarageBusiness)
                    .WithMany(p => p.CustomerVehicle)
                    .HasForeignKey(d => d.GarageBusinessId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_CustomerVehicle_GarageBusiness_Id");
            });

            modelBuilder.Entity<GarageBusiness>(entity =>
            {
                entity.Property(e => e.CreatedBy).HasMaxLength(255);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.GarageAddressLine1).HasMaxLength(255);

                entity.Property(e => e.GarageAddressLine2).HasMaxLength(255);

                entity.Property(e => e.GarageAddressLine3).HasMaxLength(255);

                entity.Property(e => e.GarageAddressLine4).HasMaxLength(255);

                entity.Property(e => e.GarageBusinessName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.GarageEmailAddress).HasMaxLength(255);

                entity.Property(e => e.GarageMobileNumber).HasMaxLength(255);

                entity.Property(e => e.GaragePhoneNumber).HasMaxLength(255);

                entity.Property(e => e.Postcode).HasMaxLength(20);

                entity.Property(e => e.UpdatedBy).HasMaxLength(255);

                entity.Property(e => e.UpdatedDate).HasColumnType("datetime");
                entity.Property(e => e.LogoImage).HasMaxLength(255);
                entity.Property(e => e.VatNumber).HasMaxLength(255);
                entity.Property(e => e.BusinessRegistrationNumber).HasMaxLength(255);
            });

            modelBuilder.Entity<GarageBusinessCustomer>(entity =>
            {
                entity.HasOne(d => d.GarageBusiness)
                    .WithMany(p => p.GarageBusinessCustomer)
                    .HasForeignKey(d => d.GarageBusinessId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            modelBuilder.Entity<GarageOwnedVehicles>(entity =>
            {
                entity.HasOne(d => d.GarageBusiness)
                    .WithMany(p => p.GarageOwnedVehicles)
                    .HasForeignKey(d => d.GarageBusinessId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

            modelBuilder.Entity<GarageVehicleOwner>(entity =>
            {
                entity.HasOne(d => d.GarageBusiness)
                    .WithMany(p => p.GarageVehicleOwner)
                    .HasForeignKey(d => d.GarageBusinessId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_GarageVehicleOwner_GarageBusiness_Id");
            });

            modelBuilder.Entity<Users>(entity =>
            {
                entity.Property(e => e.Admin_Owner).HasColumnName("Admin_Owner");

                entity.Property(e => e.CreatedBy).HasMaxLength(255);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.EmailAddress).HasMaxLength(255);

                entity.Property(e => e.FirstName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.LastName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.MobileNumber).HasMaxLength(255);

                entity.Property(e => e.PhoneNumber).HasMaxLength(255);

                entity.Property(e => e.UpdatedBy).HasMaxLength(255);

                entity.Property(e => e.UpdatedDate).HasColumnType("datetime");

                entity.Property(e => e.ExternalUserId).HasMaxLength(100);

                entity.HasIndex(e => e.ExternalUserId).HasName("UX_Users_ExternalUserId")
                    .IsUnique()
                    .HasFilter("[ExternalUserId] IS NOT NULL");

            });

            modelBuilder.Entity<VehicleInvoice>(entity =>
            {
                entity.Property(e => e.CarHire).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.EnvironmentCost).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.Labour).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.Paint).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.StrTotal).HasColumnName("strTotal");

                entity.Property(e => e.SubTotal).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.SundryExpenses).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.Total).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.Vat).HasColumnType("decimal(8, 2)");
            });

            modelBuilder.Entity<WorkQuote>(entity =>
            {
                entity.Property(e => e.CarHire).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.EnvironmentCost).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.Labour).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.Paint).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.SubTotal).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.SundryExpenses).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.Tax).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.Total).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.Vat).HasColumnType("decimal(8, 2)");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
