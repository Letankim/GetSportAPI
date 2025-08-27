using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace GetSportAPI.Models.Generated;

public partial class GetSportContext : DbContext
{
    public GetSportContext()
    {
    }

    public GetSportContext(DbContextOptions<GetSportContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<Blogpost> Blogposts { get; set; }

    public virtual DbSet<Court> Courts { get; set; }

    public virtual DbSet<Courtbooking> Courtbookings { get; set; }

    public virtual DbSet<Courtslot> Courtslots { get; set; }

    public virtual DbSet<Courtstatushistory> Courtstatushistories { get; set; }

    public virtual DbSet<Feedback> Feedbacks { get; set; }

    public virtual DbSet<Ownerpackage> Ownerpackages { get; set; }

    public virtual DbSet<Package> Packages { get; set; }

    public virtual DbSet<Playmatejoin> Playmatejoins { get; set; }

    public virtual DbSet<Playmatepost> Playmateposts { get; set; }

    public virtual DbSet<Pointtransaction> Pointtransactions { get; set; }

    public virtual DbSet<Userpackage> Userpackages { get; set; }

    public virtual DbSet<Uservoucher> Uservouchers { get; set; }

    public virtual DbSet<Voucher> Vouchers { get; set; }

    public virtual DbSet<Wallet> Wallets { get; set; }

    public virtual DbSet<Wallettransaction> Wallettransactions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=localhost;Database=GetSport;Integrated Security=SSPI;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__account__CB9A1CFFDAB42209");

            entity.ToTable("account");

            entity.Property(e => e.UserId).HasColumnName("userId");
            entity.Property(e => e.Createat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("createat");
            entity.Property(e => e.Dateofbirth).HasColumnName("dateofbirth");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.Fullname)
                .HasMaxLength(100)
                .HasColumnName("fullname");
            entity.Property(e => e.Gender)
                .HasMaxLength(10)
                .HasColumnName("gender");
            entity.Property(e => e.Isactive)
                .HasDefaultValue(true)
                .HasColumnName("isactive");
            entity.Property(e => e.Membershiptype)
                .HasMaxLength(50)
                .HasColumnName("membershiptype");
            entity.Property(e => e.Password)
                .HasMaxLength(100)
                .HasColumnName("password");
            entity.Property(e => e.Phonenumber)
                .HasMaxLength(20)
                .HasColumnName("phonenumber");
            entity.Property(e => e.Qrcode)
                .HasMaxLength(255)
                .HasColumnName("qrcode");
            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .HasColumnName("role");
            entity.Property(e => e.Skilllevel)
                .HasMaxLength(50)
                .HasColumnName("skilllevel");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.Totalpoint).HasColumnName("totalpoint");
        });

        modelBuilder.Entity<Blogpost>(entity =>
        {
            entity.HasKey(e => e.BlogId).HasName("PK__blogpost__FA0AA72DD3222BDD");

            entity.ToTable("blogpost");

            entity.Property(e => e.BlogId).HasColumnName("blogId");
            entity.Property(e => e.AccountId).HasColumnName("accountId");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("createdat");
            entity.Property(e => e.Dateofbirth).HasColumnName("dateofbirth");
            entity.Property(e => e.Imageurl)
                .HasMaxLength(255)
                .HasColumnName("imageurl");
            entity.Property(e => e.Shortdesc)
                .HasMaxLength(255)
                .HasColumnName("shortdesc");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(100)
                .HasColumnName("title");
            entity.Property(e => e.Updatedat)
                .HasColumnType("datetime")
                .HasColumnName("updatedat");

            entity.HasOne(d => d.Account).WithMany(p => p.Blogposts)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_blog_user");
        });

        modelBuilder.Entity<Court>(entity =>
        {
            entity.HasKey(e => e.CourtId).HasName("PK__court__4E6E36E89C46A0C8");

            entity.ToTable("court");

            entity.Property(e => e.CourtId).HasColumnName("courtId");
            entity.Property(e => e.Enddate)
                .HasColumnType("datetime")
                .HasColumnName("enddate");
            entity.Property(e => e.Imageurl)
                .HasMaxLength(255)
                .HasColumnName("imageurl");
            entity.Property(e => e.Isactive)
                .HasDefaultValue(true)
                .HasColumnName("isactive");
            entity.Property(e => e.Location)
                .HasMaxLength(255)
                .HasColumnName("location");
            entity.Property(e => e.OwnerId).HasColumnName("ownerId");
            entity.Property(e => e.Priceperhour)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("priceperhour");
            entity.Property(e => e.Priority).HasColumnName("priority");
            entity.Property(e => e.Startdate)
                .HasColumnType("datetime")
                .HasColumnName("startdate");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");

            entity.HasOne(d => d.Owner).WithMany(p => p.Courts)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_court_owner");
        });

        modelBuilder.Entity<Courtbooking>(entity =>
        {
            entity.HasKey(e => e.BookingId).HasName("PK__courtboo__C6D03BCD11EF75F3");

            entity.ToTable("courtbooking");

            entity.Property(e => e.BookingId).HasColumnName("bookingId");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.Bookingdate)
                .HasColumnType("datetime")
                .HasColumnName("bookingdate");
            entity.Property(e => e.CourtId).HasColumnName("courtId");
            entity.Property(e => e.Createat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("createat");
            entity.Property(e => e.SlotId).HasColumnName("slotId");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("userId");

            entity.HasOne(d => d.Court).WithMany(p => p.Courtbookings)
                .HasForeignKey(d => d.CourtId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_booking_court");

            entity.HasOne(d => d.Slot).WithMany(p => p.Courtbookings)
                .HasForeignKey(d => d.SlotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_booking_slot");

            entity.HasOne(d => d.User).WithMany(p => p.Courtbookings)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_booking_user");
        });

        modelBuilder.Entity<Courtslot>(entity =>
        {
            entity.HasKey(e => e.SlotId).HasName("PK__courtslo__9C4A67135653F376");

            entity.ToTable("courtslot");

            entity.Property(e => e.SlotId).HasColumnName("slotId");
            entity.Property(e => e.CourtId).HasColumnName("courtId");
            entity.Property(e => e.Endtime)
                .HasColumnType("datetime")
                .HasColumnName("endtime");
            entity.Property(e => e.Isavailable)
                .HasDefaultValue(true)
                .HasColumnName("isavailable");
            entity.Property(e => e.Slotnumber).HasColumnName("slotnumber");
            entity.Property(e => e.Starttime)
                .HasColumnType("datetime")
                .HasColumnName("starttime");

            entity.HasOne(d => d.Court).WithMany(p => p.Courtslots)
                .HasForeignKey(d => d.CourtId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_slot_court");
        });

        modelBuilder.Entity<Courtstatushistory>(entity =>
        {
            entity.HasKey(e => e.StatusId).HasName("PK__courtsta__36257A18324CCA9F");

            entity.ToTable("courtstatushistory");

            entity.Property(e => e.StatusId).HasColumnName("statusId");
            entity.Property(e => e.CourtId).HasColumnName("courtId");
            entity.Property(e => e.Statusofcourt)
                .HasMaxLength(50)
                .HasColumnName("statusofcourt");
            entity.Property(e => e.Updateat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("updateat");

            entity.HasOne(d => d.Court).WithMany(p => p.Courtstatushistories)
                .HasForeignKey(d => d.CourtId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_courthist_court");
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("PK__feedback__2613FD24396EFC53");

            entity.ToTable("feedback");

            entity.Property(e => e.FeedbackId).HasColumnName("feedbackId");
            entity.Property(e => e.BookingId).HasColumnName("bookingId");
            entity.Property(e => e.Comment)
                .HasMaxLength(255)
                .HasColumnName("comment");
            entity.Property(e => e.Createat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("createat");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.UserId).HasColumnName("userId");

            entity.HasOne(d => d.Booking).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_fb_booking");

            entity.HasOne(d => d.User).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_fb_user");
        });

        modelBuilder.Entity<Ownerpackage>(entity =>
        {
            entity.HasKey(e => e.OwnerpackageId).HasName("PK__ownerpac__551D866E2DF15DD5");

            entity.ToTable("ownerpackage");

            entity.Property(e => e.OwnerpackageId).HasColumnName("ownerpackageId");
            entity.Property(e => e.Createat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("createat");
            entity.Property(e => e.Duration).HasColumnName("duration");
            entity.Property(e => e.Enddate).HasColumnName("enddate");
            entity.Property(e => e.OwnerId).HasColumnName("ownerId");
            entity.Property(e => e.Packagename)
                .HasMaxLength(50)
                .HasColumnName("packagename");
            entity.Property(e => e.Price)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("price");
            entity.Property(e => e.Priority).HasColumnName("priority");
            entity.Property(e => e.Startdate).HasColumnName("startdate");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");

            entity.HasOne(d => d.Owner).WithMany(p => p.Ownerpackages)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_ownerpkg_owner");
        });

        modelBuilder.Entity<Package>(entity =>
        {
            entity.HasKey(e => e.PackageId).HasName("PK__package__AA8D20C89B55BC97");

            entity.ToTable("package");

            entity.Property(e => e.PackageId).HasColumnName("packageId");
            entity.Property(e => e.Createat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("createat");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.Durationdays).HasColumnName("durationdays");
            entity.Property(e => e.Isactive)
                .HasDefaultValue(true)
                .HasColumnName("isactive");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            entity.Property(e => e.Price)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("price");
            entity.Property(e => e.Updateat)
                .HasColumnType("datetime")
                .HasColumnName("updateat");
        });

        modelBuilder.Entity<Playmatejoin>(entity =>
        {
            entity.HasKey(e => e.JoinId).HasName("PK__playmate__152639308FB6DECF");

            entity.ToTable("playmatejoin");

            entity.HasIndex(e => new { e.PostId, e.UserId }, "uq_pmj").IsUnique();

            entity.Property(e => e.JoinId).HasColumnName("joinId");
            entity.Property(e => e.Joinedat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("joinedat");
            entity.Property(e => e.PostId).HasColumnName("postId");
            entity.Property(e => e.UserId).HasColumnName("userId");

            entity.HasOne(d => d.Post).WithMany(p => p.Playmatejoins)
                .HasForeignKey(d => d.PostId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_pmj_post");

            entity.HasOne(d => d.User).WithMany(p => p.Playmatejoins)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_pmj_user");
        });

        modelBuilder.Entity<Playmatepost>(entity =>
        {
            entity.HasKey(e => e.PostId).HasName("PK__playmate__DD0C739AB5E2D1FA");

            entity.ToTable("playmatepost");

            entity.Property(e => e.PostId).HasColumnName("postId");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CourtbookingId).HasColumnName("courtbookingId");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("createdat");
            entity.Property(e => e.Neededplayers).HasColumnName("neededplayers");
            entity.Property(e => e.Skilllevel)
                .HasMaxLength(50)
                .HasColumnName("skilllevel");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(100)
                .HasColumnName("title");
            entity.Property(e => e.UserId).HasColumnName("userId");

            entity.HasOne(d => d.Courtbooking).WithMany(p => p.Playmateposts)
                .HasForeignKey(d => d.CourtbookingId)
                .HasConstraintName("fk_pmp_booking");

            entity.HasOne(d => d.User).WithMany(p => p.Playmateposts)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_pmp_user");
        });

        modelBuilder.Entity<Pointtransaction>(entity =>
        {
            entity.HasKey(e => e.TransactionId).HasName("PK__pointtra__9B57CF7215B3C373");

            entity.ToTable("pointtransaction");

            entity.Property(e => e.TransactionId).HasColumnName("transactionId");
            entity.Property(e => e.BookingId).HasColumnName("bookingId");
            entity.Property(e => e.Createat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("createat");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.Pointchanged).HasColumnName("pointchanged");
            entity.Property(e => e.Transactiontype)
                .HasMaxLength(50)
                .HasColumnName("transactiontype");
            entity.Property(e => e.UserId).HasColumnName("userId");

            entity.HasOne(d => d.Booking).WithMany(p => p.Pointtransactions)
                .HasForeignKey(d => d.BookingId)
                .HasConstraintName("fk_ptx_booking");

            entity.HasOne(d => d.User).WithMany(p => p.Pointtransactions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_ptx_user");
        });

        modelBuilder.Entity<Userpackage>(entity =>
        {
            entity.HasKey(e => e.UserpackageId).HasName("PK__userpack__1CD235ABC43CE044");

            entity.ToTable("userpackage");

            entity.Property(e => e.UserpackageId).HasColumnName("userpackageId");
            entity.Property(e => e.Createat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("createat");
            entity.Property(e => e.Enddate).HasColumnName("enddate");
            entity.Property(e => e.Isactive)
                .HasDefaultValue(true)
                .HasColumnName("isactive");
            entity.Property(e => e.PackageId).HasColumnName("packageId");
            entity.Property(e => e.Startdate).HasColumnName("startdate");
            entity.Property(e => e.Updateat)
                .HasColumnType("datetime")
                .HasColumnName("updateat");
            entity.Property(e => e.UserId).HasColumnName("userId");

            entity.HasOne(d => d.Package).WithMany(p => p.Userpackages)
                .HasForeignKey(d => d.PackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_userpkg_pkg");

            entity.HasOne(d => d.User).WithMany(p => p.Userpackages)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_userpkg_user");
        });

        modelBuilder.Entity<Uservoucher>(entity =>
        {
            entity.HasKey(e => e.UservoucherId).HasName("PK__uservouc__75983B294F9D968B");

            entity.ToTable("uservoucher");

            entity.HasIndex(e => new { e.UserId, e.VoucherId }, "uq_uservoucher").IsUnique();

            entity.Property(e => e.UservoucherId).HasColumnName("uservoucherId");
            entity.Property(e => e.Assignedat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("assignedat");
            entity.Property(e => e.Usedat)
                .HasColumnType("datetime")
                .HasColumnName("usedat");
            entity.Property(e => e.UserId).HasColumnName("userId");
            entity.Property(e => e.VoucherId).HasColumnName("voucherId");

            entity.HasOne(d => d.User).WithMany(p => p.Uservouchers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_uservoucher_user");

            entity.HasOne(d => d.Voucher).WithMany(p => p.Uservouchers)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_uservoucher_voucher");
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.VoucherId).HasName("PK__voucher__F53389E9300E6672");

            entity.ToTable("voucher");

            entity.HasIndex(e => e.Code, "UQ__voucher__357D4CF926D24CB1").IsUnique();

            entity.Property(e => e.VoucherId).HasColumnName("voucherId");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.Discountpercent)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("discountpercent");
            entity.Property(e => e.Enddate)
                .HasColumnType("datetime")
                .HasColumnName("enddate");
            entity.Property(e => e.Isactive)
                .HasDefaultValue(true)
                .HasColumnName("isactive");
            entity.Property(e => e.Startdate)
                .HasColumnType("datetime")
                .HasColumnName("startdate");
            entity.Property(e => e.Usage).HasColumnName("usage");
            entity.Property(e => e.Usagelimit).HasColumnName("usagelimit");
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(e => e.WalletId).HasName("PK__wallet__3785C870932BDD81");

            entity.ToTable("wallet");

            entity.HasIndex(e => e.UserId, "uq_wallet_user").IsUnique();

            entity.Property(e => e.WalletId).HasColumnName("walletId");
            entity.Property(e => e.Balance)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("balance");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("createdat");
            entity.Property(e => e.Updatedat)
                .HasColumnType("datetime")
                .HasColumnName("updatedat");
            entity.Property(e => e.UserId).HasColumnName("userId");

            entity.HasOne(d => d.User).WithOne(p => p.Wallet)
                .HasForeignKey<Wallet>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_wallet_user");
        });

        modelBuilder.Entity<Wallettransaction>(entity =>
        {
            entity.HasKey(e => e.TransactionId).HasName("PK__wallettr__9B57CF72740882B3");

            entity.ToTable("wallettransaction");

            entity.Property(e => e.TransactionId).HasColumnName("transactionId");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.Bankinfo)
                .HasMaxLength(100)
                .HasColumnName("bankinfo");
            entity.Property(e => e.Comment)
                .HasMaxLength(255)
                .HasColumnName("comment");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("createdat");
            entity.Property(e => e.Direction).HasColumnName("direction");
            entity.Property(e => e.Relatedid).HasColumnName("relatedid");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");
            entity.Property(e => e.WalletId).HasColumnName("walletId");

            entity.HasOne(d => d.Wallet).WithMany(p => p.Wallettransactions)
                .HasForeignKey(d => d.WalletId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_wtx_wallet");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
